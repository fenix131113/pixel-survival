using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using GameAssembly.BuildSystem.Data;
using GameAssembly.BuildSystem.WorldObjects;
using GameAssembly.Core;
using GameAssembly.CraftSystem.Data;
using GameAssembly.Utils;
using GameAssembly.WorldSystem.Data;
using Mirror;
using UnityEngine;
using VContainer;

namespace GameAssembly.WorldSystem
{
    public class WorldCreateManager : NetworkBehaviour
    {
        private const float CHUNK_SYNC_INTERVAL = 0.1f;
        private const int MAX_CHUNKS_TO_SEND_PER_SYNC = 16;
        private const int CHUNK_TOTAL_CELLS = Chunk.CHUNK_SIZE * Chunk.CHUNK_SIZE;
        private const int CHUNK_PART_CELLS = 192;

        public static WorldCreateManager Instance { get; private set; }

        [SerializeField, Min(0.1f)] private float blockDamageResetDelay = 4f;
        [SerializeField, Range(0.1f, 0.99f)] private float chunkGenerationProgressWeight = 0.85f;
        [SerializeField, Min(0)] private int borderWallLayers = 4;
        [SerializeField] private BlockDefinitionSO borderWallDefinition;
        [SerializeField] private PlaceableObjectDefinitionSO guaranteedRedBiomeObjectDefinition;

        private readonly Dictionary<int, Coroutine> _syncCoroutines = new();
        private readonly Dictionary<int, HashSet<ChunkCoord>> _sentChunksByConnection = new();
        private readonly HashSet<int> _seedSentConnections = new();
        private readonly HashSet<string> _craftedRecipeIds = new(StringComparer.Ordinal);
        private readonly HashSet<string> _unlockedRecipeIds = new(StringComparer.Ordinal);
        private readonly Dictionary<string, CraftRecipeSO> _craftRecipesById = new(StringComparer.Ordinal);
        private readonly Dictionary<ChunkCoord, ChunkAssemblyState> _chunkAssemblyStates = new();

        private CancellationTokenSource _generationCancellation;
        private bool _craftStateInitialized;
        private bool _clientCraftStateReceived;

        private sealed class ChunkAssemblyState
        {
            public readonly CellData[] Cells;
            public readonly bool[] ReceivedMask;
            public int ReceivedCellsCount;

            public ChunkAssemblyState(int totalCells)
            {
                Cells = new CellData[totalCells];
                ReceivedMask = new bool[totalCells];
                ReceivedCellsCount = 0;
            }
        }

        [Inject] private World _world;
        [Inject] private ServerBlockDamageSystem _blockDamageSystem;
        [Inject] private WorldObjectsGenerator _worldObjectsGenerator;

        public event Action<Vector2Int, float, int, int> ClientOnBlockDamageProgress;
        public event Action<Vector2Int> ClientOnBlockDamageCleared;
        public event Action ClientOnCraftUnlockStateChanged;

        public bool HasCraftStateSnapshot => _clientCraftStateReceived;

        public bool IsRecipeUnlocked(CraftRecipeSO recipe)
        {
            if (!recipe || string.IsNullOrWhiteSpace(recipe.Id))
                return false;

            return _unlockedRecipeIds.Contains(recipe.Id);
        }

        [Server]
        public bool Server_IsRecipeUnlocked(CraftRecipeSO recipe)
        {
            EnsureCraftStateInitialized();
            return IsRecipeUnlocked(recipe);
        }

        [Server]
        public bool Server_RegisterCraftCompleted(CraftRecipeSO recipe)
        {
            EnsureCraftStateInitialized();

            if (!recipe || string.IsNullOrWhiteSpace(recipe.Id))
                return false;

            if (!_craftRecipesById.ContainsKey(recipe.Id))
            {
                Debug.LogWarning($"[{nameof(WorldCreateManager)}] Unknown craft recipe id '{recipe.Id}'.");
                return false;
            }

            if (!_craftedRecipeIds.Add(recipe.Id))
                return false;

            var unlocksChanged = Server_RecalculateUnlockedRecipes();
            if (unlocksChanged)
                ClientOnCraftUnlockStateChanged?.Invoke();

            SetDirty();
            return unlocksChanged;
        }

        public override void OnSerialize(NetworkWriter writer, bool initialState)
        {
            writer.WriteInt(_craftedRecipeIds.Count);
            foreach (var recipeId in _craftedRecipeIds)
                writer.WriteString(recipeId);

            writer.WriteInt(_unlockedRecipeIds.Count);
            foreach (var recipeId in _unlockedRecipeIds)
                writer.WriteString(recipeId);

            base.OnSerialize(writer, initialState);
        }

        public override void OnDeserialize(NetworkReader reader, bool initialState)
        {
            _craftedRecipeIds.Clear();
            var craftedCount = reader.ReadInt();
            for (var i = 0; i < craftedCount; i++)
            {
                var craftedId = reader.ReadString();
                if (!string.IsNullOrWhiteSpace(craftedId))
                    _craftedRecipeIds.Add(craftedId);
            }

            _unlockedRecipeIds.Clear();
            var unlockedCount = reader.ReadInt();
            for (var i = 0; i < unlockedCount; i++)
            {
                var unlockedId = reader.ReadString();
                if (!string.IsNullOrWhiteSpace(unlockedId))
                    _unlockedRecipeIds.Add(unlockedId);
            }

            _clientCraftStateReceived = true;
            ClientOnCraftUnlockStateChanged?.Invoke();

            base.OnDeserialize(reader, initialState);
        }

        private async void Awake()
        {
            if (Instance && Instance != this)
                Debug.LogWarning(
                    $"[{nameof(WorldCreateManager)}] Multiple instances detected. Replacing previous instance.");

            Instance = this;

            try
            {
                _world.ConfigureBorderWalls(borderWallDefinition, borderWallLayers);
                _blockDamageSystem.ResetDelaySeconds = blockDamageResetDelay;
                _worldObjectsGenerator.ConfigureGuaranteedRedBiomeObject(guaranteedRedBiomeObjectDefinition);

                if (!NetworkServer.active)
                    return;

                CancelGeneration();
                _generationCancellation = new CancellationTokenSource();
                var generationToken = _generationCancellation.Token;

                var chunkStageEnd = Mathf.Clamp(chunkGenerationProgressWeight, 0.1f, 0.99f);

                _world.IsLoaded.Value = false;
                await _world.GenerateWorldAsync(0f, chunkStageEnd, markAsLoadedAtEnd: false,
                    cancellationToken: generationToken);
                if (!CanContinueGeneration(generationToken))
                    return;

                await _worldObjectsGenerator.Server_GenerateAsync(objectsProgress =>
                {
                    var globalProgress = Mathf.Lerp(chunkStageEnd, 1f, Mathf.Clamp01(objectsProgress));
                    ((IProgress<float>)_world.Progress)?.Report(globalProgress);
                }, generationToken);
                if (!CanContinueGeneration(generationToken))
                    return;

                ((IProgress<float>)_world.Progress)?.Report(1f);
                _world.IsLoaded.Value = true;
            }
            catch (OperationCanceledException)
            {
                // Expected when host/server stops during async world generation.
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        private bool CanContinueGeneration(CancellationToken generationToken)
        {
            return !generationToken.IsCancellationRequested && this && isActiveAndEnabled && NetworkServer.active;
        }

        private void CancelGeneration()
        {
            if (_generationCancellation == null)
                return;

            if (!_generationCancellation.IsCancellationRequested)
                _generationCancellation.Cancel();

            _generationCancellation.Dispose();
            _generationCancellation = null;
        }

        private void OnDestroy()
        {
            CancelGeneration();

            if (NetworkServer.active)
                UnbindServerBlockDamage();

            StopAllCoroutines();
            if (Instance == this)
                Instance = null;

            _chunkAssemblyStates.Clear();
            ClientOnBlockDamageProgress = null;
            ClientOnBlockDamageCleared = null;
            ClientOnCraftUnlockStateChanged = null;
        }

        private void Update()
        {
            if (!NetworkServer.active)
                return;

            _blockDamageSystem.Server_Tick();
        }

        protected override void OnValidate()
        {
            base.OnValidate();

            if (!guaranteedRedBiomeObjectDefinition)
                return;

            if (!guaranteedRedBiomeObjectDefinition.Prefab)
            {
                Debug.LogError(
                    $"[{nameof(WorldCreateManager)}] Guaranteed red biome object '{guaranteedRedBiomeObjectDefinition.name}' has no prefab assigned.",
                    this);
                return;
            }

            if (!guaranteedRedBiomeObjectDefinition.Prefab.TryGetComponent<PlacedWorldObject>(out _))
            {
                Debug.LogError(
                    $"[{nameof(WorldCreateManager)}] Guaranteed red biome object '{guaranteedRedBiomeObjectDefinition.name}' prefab must contain {nameof(PlacedWorldObject)} component.",
                    this);
            }
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            EnsureCraftStateInitialized();
            BindServerBlockDamage();
        }

        public override void OnStopServer()
        {
            CancelGeneration();
            UnbindServerBlockDamage();
            Server_ResetCraftState();
            _chunkAssemblyStates.Clear();
            base.OnStopServer();
        }

        [TargetRpc]
        public void Target_LoadSeed(NetworkConnectionToClient target, int seed)
        {
            if (NetworkServer.active)
                return;

            _chunkAssemblyStates.Clear();
            _world.SetupSeed(seed);
        }

        [TargetRpc]
        public void Target_LoadChunkPart(NetworkConnectionToClient target, ChunkCoord chunkCoord, int startIndex,
            int totalCells, CellData[] cellsPart)
        {
            if (NetworkServer.active)
                return;

            if (totalCells != CHUNK_TOTAL_CELLS || cellsPart == null || cellsPart.Length == 0)
                return;

            if (_world.GetChunk(chunkCoord) != null)
            {
                _chunkAssemblyStates.Remove(chunkCoord);
                return;
            }

            if (startIndex < 0 || startIndex >= totalCells)
                return;

            if (!_chunkAssemblyStates.TryGetValue(chunkCoord, out var assemblyState) ||
                assemblyState.Cells.Length != totalCells)
            {
                assemblyState = new ChunkAssemblyState(totalCells);
                _chunkAssemblyStates[chunkCoord] = assemblyState;
            }

            var writableCellsCount = Mathf.Min(cellsPart.Length, totalCells - startIndex);
            for (var i = 0; i < writableCellsCount; i++)
            {
                var cellIndex = startIndex + i;
                assemblyState.Cells[cellIndex] = cellsPart[i];

                if (assemblyState.ReceivedMask[cellIndex])
                    continue;

                assemblyState.ReceivedMask[cellIndex] = true;
                assemblyState.ReceivedCellsCount++;
            }

            if (assemblyState.ReceivedCellsCount < totalCells)
                return;

            var cells = new CellData[Chunk.CHUNK_SIZE, Chunk.CHUNK_SIZE];
            for (var linearIndex = 0; linearIndex < totalCells; linearIndex++)
            {
                var x = linearIndex / Chunk.CHUNK_SIZE;
                var y = linearIndex % Chunk.CHUNK_SIZE;
                cells[x, y] = assemblyState.Cells[linearIndex];
            }

            _chunkAssemblyStates.Remove(chunkCoord);
            _world.SetupChunk(new Chunk(chunkCoord, cells));
        }

        [ClientRpc(includeOwner = false)]
        public void Rpc_SyncCell(ChunkCoord chunkCoord, int x, int y, CellData cell)
        {
            if (NetworkServer.active)
                return;

            var world = GameInstaller.Resolve<World>();
            var currentChunk = world.GetChunk(chunkCoord);
            if (currentChunk != null)
            {
                currentChunk.SetCell(x, y, cell);
                return;
            }

            if (!_chunkAssemblyStates.TryGetValue(chunkCoord, out var assemblyState))
                return;

            if (x < 0 || x >= Chunk.CHUNK_SIZE || y < 0 || y >= Chunk.CHUNK_SIZE)
                return;

            var linearIndex = x * Chunk.CHUNK_SIZE + y;
            if (linearIndex < 0 || linearIndex >= assemblyState.Cells.Length)
                return;

            assemblyState.Cells[linearIndex] = cell;
        }

        [ClientRpc]
        private void Rpc_BlockDamageProgress(Vector2Int blockWorldPos, float progress01, int currentDamage,
            int maxHealth)
        {
            ClientOnBlockDamageProgress?.Invoke(blockWorldPos, progress01, currentDamage, maxHealth);
        }

        [ClientRpc]
        private void Rpc_BlockDamageCleared(Vector2Int blockWorldPos)
        {
            ClientOnBlockDamageCleared?.Invoke(blockWorldPos);
        }

        [Server]
        public void Server_SendWorldToConn(NetworkConnectionToClient conn)
        {
            if (conn == null)
                return;

            var connectionId = conn.connectionId;

            if (_syncCoroutines.ContainsKey(connectionId))
                return;

            _syncCoroutines[connectionId] = StartCoroutine(WorldSyncCoroutine(conn));
        }

        [Server]
        public void Server_StopSendingWorldToConn(NetworkConnectionToClient conn)
        {
            if (conn == null)
                return;

            var connectionId = conn.connectionId;

            if (_syncCoroutines.TryGetValue(connectionId, out var coroutine))
                StopCoroutine(coroutine);

            _syncCoroutines.Remove(connectionId);
            _sentChunksByConnection.Remove(connectionId);
            _seedSentConnections.Remove(connectionId);
        }

        [Server]
        private IEnumerator WorldSyncCoroutine(NetworkConnectionToClient conn)
        {
            var wait = new WaitForSeconds(CHUNK_SYNC_INTERVAL);
            var world = GameInstaller.Resolve<World>();

            yield return new WaitUntil(() => world.IsLoaded.Value);

            while (conn is { isAuthenticated: true })
            {
                if (!conn.isReady || !conn.identity)
                {
                    yield return wait;
                    continue;
                }

                var connectionId = conn.connectionId;

                if (!_seedSentConnections.Contains(connectionId))
                {
                    Target_LoadSeed(conn, world.Seed);
                    _seedSentConnections.Add(connectionId);
                }

                SendMissingChunks(conn, world, connectionId);

                yield return wait;
            }

            if (conn != null)
                Server_StopSendingWorldToConn(conn);
        }

        [Server]
        private void SendMissingChunks(NetworkConnectionToClient conn, World world, int connectionId)
        {
            if (!_sentChunksByConnection.TryGetValue(connectionId, out var sentChunks))
            {
                sentChunks = new HashSet<ChunkCoord>();
                _sentChunksByConnection[connectionId] = sentChunks;
            }

            var playerPosition = conn.identity.transform.position;
            var centerChunk = new ChunkCoord(
                Mathf.FloorToInt(playerPosition.x / Chunk.CHUNK_SIZE),
                Mathf.FloorToInt(playerPosition.y / Chunk.CHUNK_SIZE));

            var chunksToSend = new List<Chunk>();
            foreach (var pair in world.Chunks)
            {
                if (sentChunks.Contains(pair.Key))
                    continue;

                chunksToSend.Add(pair.Value);
            }

            if (chunksToSend.Count == 0)
                return;

            chunksToSend.Sort((left, right) =>
            {
                var leftDx = left.Coord.X - centerChunk.X;
                var leftDy = left.Coord.Y - centerChunk.Y;
                var rightDx = right.Coord.X - centerChunk.X;
                var rightDy = right.Coord.Y - centerChunk.Y;

                return (leftDx * leftDx + leftDy * leftDy).CompareTo(rightDx * rightDx + rightDy * rightDy);
            });

            var chunksToSendNow = Mathf.Min(MAX_CHUNKS_TO_SEND_PER_SYNC, chunksToSend.Count);

            for (var i = 0; i < chunksToSendNow; i++)
            {
                var chunk = chunksToSend[i];
                SendChunkInParts(conn, chunk);
                sentChunks.Add(chunk.Coord);
            }
        }

        [Server]
        private void SendChunkInParts(NetworkConnectionToClient conn, Chunk chunk)
        {
            for (var startIndex = 0; startIndex < CHUNK_TOTAL_CELLS; startIndex += CHUNK_PART_CELLS)
            {
                var partCellsCount = Mathf.Min(CHUNK_PART_CELLS, CHUNK_TOTAL_CELLS - startIndex);
                var cellsPart = new CellData[partCellsCount];

                for (var i = 0; i < partCellsCount; i++)
                    cellsPart[i] = GetChunkCellByLinearIndex(chunk, startIndex + i);

                Target_LoadChunkPart(conn, chunk.Coord, startIndex, CHUNK_TOTAL_CELLS, cellsPart);
            }
        }

        private static CellData GetChunkCellByLinearIndex(Chunk chunk, int linearIndex)
        {
            var x = linearIndex / Chunk.CHUNK_SIZE;
            var y = linearIndex % Chunk.CHUNK_SIZE;
            return chunk.Cells[x, y];
        }

        [Server]
        private void EnsureCraftStateInitialized()
        {
            if (_craftStateInitialized)
                return;

            Server_LoadCraftRecipes();
            _craftedRecipeIds.Clear();
            _unlockedRecipeIds.Clear();
            Server_RecalculateUnlockedRecipes();

            _craftStateInitialized = true;
            _clientCraftStateReceived = true;
            ClientOnCraftUnlockStateChanged?.Invoke();
            SetDirty();
        }

        [Server]
        private void Server_ResetCraftState()
        {
            _craftedRecipeIds.Clear();
            _unlockedRecipeIds.Clear();
            _craftRecipesById.Clear();
            _craftStateInitialized = false;
            _clientCraftStateReceived = false;
        }

        [Server]
        private void Server_LoadCraftRecipes()
        {
            _craftRecipesById.Clear();

            var recipes = Resources.LoadAll<CraftRecipeSO>(AssetsPaths.RECIPES_CONFIGS_PATH);
            foreach (var recipe in recipes.OrderBy(x => x != null ? x.Id : string.Empty, StringComparer.Ordinal))
            {
                if (!recipe)
                    continue;

                if (string.IsNullOrWhiteSpace(recipe.Id))
                {
                    Debug.LogError($"[{nameof(WorldCreateManager)}] Craft recipe without id: {recipe.name}", recipe);
                    continue;
                }

                if (_craftRecipesById.ContainsKey(recipe.Id))
                {
                    Debug.LogError(
                        $"[{nameof(WorldCreateManager)}] Duplicate craft recipe id '{recipe.Id}'. Recipe '{recipe.name}' ignored.",
                        recipe);
                    continue;
                }

                _craftRecipesById.Add(recipe.Id, recipe);
            }

            Server_ValidateCraftRecipesConfiguration();
        }

        [Server]
        private bool Server_RecalculateUnlockedRecipes()
        {
            var recalculatedUnlockedIds = new HashSet<string>(StringComparer.Ordinal);

            foreach (var pair in _craftRecipesById)
            {
                if (Server_AreRecipeRequirementsMet(pair.Value))
                    recalculatedUnlockedIds.Add(pair.Key);
            }

            if (_unlockedRecipeIds.SetEquals(recalculatedUnlockedIds))
                return false;

            _unlockedRecipeIds.Clear();
            _unlockedRecipeIds.UnionWith(recalculatedUnlockedIds);
            SetDirty();
            return true;
        }

        [Server]
        private bool Server_AreRecipeRequirementsMet(CraftRecipeSO recipe)
        {
            if (!recipe)
                return false;

            var requiredCrafts = recipe.RequiredCrafts;
            if (requiredCrafts == null || requiredCrafts.Count == 0)
                return true;

            foreach (var requiredCraft in requiredCrafts)
            {
                if (!requiredCraft || string.IsNullOrWhiteSpace(requiredCraft.Id))
                    return false;

                if (!_craftedRecipeIds.Contains(requiredCraft.Id))
                    return false;
            }

            return true;
        }

        [Server]
        private void Server_ValidateCraftRecipesConfiguration()
        {
            foreach (var recipe in _craftRecipesById.Values)
            {
                if (!recipe)
                    continue;

                foreach (var requiredCraft in recipe.RequiredCrafts)
                {
                    if (!requiredCraft)
                    {
                        Debug.LogWarning(
                            $"[{nameof(WorldCreateManager)}] Recipe '{recipe.name}' has null required craft entry.",
                            recipe);
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(requiredCraft.Id))
                    {
                        Debug.LogWarning(
                            $"[{nameof(WorldCreateManager)}] Recipe '{recipe.name}' references recipe '{requiredCraft.name}' without id.",
                            recipe);
                        continue;
                    }

                    if (!_craftRecipesById.ContainsKey(requiredCraft.Id))
                    {
                        Debug.LogWarning(
                            $"[{nameof(WorldCreateManager)}] Recipe '{recipe.name}' requires '{requiredCraft.Id}', but it is not in resources path '{AssetsPaths.RECIPES_CONFIGS_PATH}'.",
                            recipe);
                    }
                }
            }

            var visited = new Dictionary<string, int>(StringComparer.Ordinal);
            var stack = new Stack<string>();
            foreach (var recipeId in _craftRecipesById.Keys)
            {
                if (Server_DetectCycle(recipeId, visited, stack))
                    break;
            }
        }

        [Server]
        private bool Server_DetectCycle(string recipeId, Dictionary<string, int> visited, Stack<string> stack)
        {
            if (!visited.TryGetValue(recipeId, out var state))
                state = 0;

            if (state == 1)
            {
                var cyclePath = stack.Reverse().Concat(new[] { recipeId });
                Debug.LogWarning(
                    $"[{nameof(WorldCreateManager)}] Craft unlock dependency cycle detected: {string.Join(" -> ", cyclePath)}");
                return true;
            }

            if (state == 2 || !_craftRecipesById.TryGetValue(recipeId, out var recipe) || !recipe)
                return false;

            visited[recipeId] = 1;
            stack.Push(recipeId);

            var requiredCrafts = recipe.RequiredCrafts;
            if (requiredCrafts != null)
            {
                foreach (var requiredCraft in requiredCrafts)
                {
                    if (!requiredCraft || string.IsNullOrWhiteSpace(requiredCraft.Id))
                        continue;

                    if (Server_DetectCycle(requiredCraft.Id, visited, stack))
                        return true;
                }
            }

            stack.Pop();
            visited[recipeId] = 2;
            return false;
        }

        [Server]
        private void BindServerBlockDamage()
        {
            _blockDamageSystem.OnBlockDamageChanged += Server_OnBlockDamageChanged;
            _blockDamageSystem.OnBlockDamageCleared += Server_OnBlockDamageCleared;
        }

        [Server]
        private void UnbindServerBlockDamage()
        {
            _blockDamageSystem.OnBlockDamageChanged -= Server_OnBlockDamageChanged;
            _blockDamageSystem.OnBlockDamageCleared -= Server_OnBlockDamageCleared;
        }

        [Server]
        private void Server_OnBlockDamageChanged(BlockDamageSnapshot snapshot)
        {
            Rpc_BlockDamageProgress(snapshot.BlockWorldPos, snapshot.Progress01, snapshot.CurrentDamage,
                snapshot.MaxHealth);
        }

        [Server]
        private void Server_OnBlockDamageCleared(Vector2Int blockWorldPos)
        {
            Rpc_BlockDamageCleared(blockWorldPos);
        }
    }
}
