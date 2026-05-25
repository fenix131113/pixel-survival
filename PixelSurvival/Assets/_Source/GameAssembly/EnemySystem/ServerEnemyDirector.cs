using System.Collections.Generic;
using GameAssembly.Core;
using GameAssembly.HealthSystem;
using GameAssembly.HealthSystem.Data;
using GameAssembly.WorldSystem;
using GameAssembly.WorldSystem.Data;
using Mirror;
using Pathfinding;
using UnityEngine;
using VContainer;

namespace GameAssembly.EnemySystem
{
    [DisallowMultipleComponent]
    public class ServerEnemyDirector : NetworkBehaviour
    {
        [SerializeField] private EnemyMeleeAgent enemyPrefab;

        [Header("Global Limits")]
        [SerializeField, Min(1)] private int globalMaxEnemies = 60;

        [Header("Spawn Timing (Per Player)")]
        [SerializeField, Min(0.25f)] private float spawnIntervalMin = 7f;
        [SerializeField, Min(0.25f)] private float spawnIntervalMax = 14f;
        [SerializeField, Min(1)] private int spawnCountMin = 1;
        [SerializeField, Min(1)] private int spawnCountMax = 2;

        [Header("Spawn Position")]
        [SerializeField, Min(1f)] private float minSpawnDistanceFromPlayers = 10f;
        [SerializeField, Min(1f)] private float maxSpawnDistanceFromPlayer = 20f;
        [SerializeField, Min(1)] private int spawnAttemptsPerEnemy = 12;
        [SerializeField, Min(0.05f)] private float spawnCheckRadius = 0.35f;
        [SerializeField] private LayerMask spawnBlockingLayers;

        [Header("Despawn")]
        [SerializeField, Min(5f)] private float despawnDistanceFromPlayers = 48f;

        [Header("Tick")]
        [SerializeField, Min(0.1f)] private float serverTickInterval = 0.5f;

        [Inject] private World _world;

        private readonly Dictionary<uint, float> _nextSpawnTimeByPlayerNetId = new();
        private readonly List<NetworkIdentity> _players = new();
        private readonly List<EnemyMeleeAgent> _enemies = new();
        private readonly List<uint> _scheduleCleanupBuffer = new();

        private float _nextTickTime;

        public override void OnStartServer()
        {
            base.OnStartServer();
            _world ??= GameInstaller.Resolve<World>();

            var tickOffset = Random.Range(0f, serverTickInterval);
            _nextTickTime = Time.time + tickOffset;
        }

        public override void OnStopServer()
        {
            base.OnStopServer();
            _nextSpawnTimeByPlayerNetId.Clear();
            _players.Clear();
            _enemies.Clear();
            _scheduleCleanupBuffer.Clear();
        }

        private void Update()
        {
            if (!isServer || Time.time < _nextTickTime)
                return;

            _nextTickTime = Time.time + Mathf.Max(0.1f, serverTickInterval);

            if (_world == null || !_world.IsLoaded.Value || AstarPath.active == null || AstarPath.active.isScanning)
                return;

            if (!enemyPrefab)
                return;

            Server_CollectAlivePlayers(_players);
            Server_PruneSchedules(_players);
            Server_CollectEnemies(_enemies);

            var currentEnemies = _enemies.Count;
            currentEnemies = Server_DespawnFarEnemies(_enemies, _players, currentEnemies);
            Server_TrySpawnForPlayers(_players, ref currentEnemies);
        }

        [Server]
        private void Server_CollectAlivePlayers(List<NetworkIdentity> result)
        {
            result.Clear();

            foreach (var connection in NetworkServer.connections.Values)
            {
                if (connection == null || !connection.isReady || !connection.identity)
                    continue;

                if (!connection.identity.TryGetComponent<AHealthObject>(out var healthObject))
                    continue;

                if (healthObject.GetHealthType() != HealthType.PLAYER || healthObject.GetHealth() <= 0)
                    continue;

                result.Add(connection.identity);
            }
        }

        [Server]
        private void Server_CollectEnemies(List<EnemyMeleeAgent> result)
        {
            result.Clear();

            var found = FindObjectsByType<EnemyMeleeAgent>(FindObjectsSortMode.None);
            foreach (var enemy in found)
            {
                if (!enemy || !enemy.netIdentity || enemy.netIdentity.netId == 0)
                    continue;

                result.Add(enemy);
            }
        }

        [Server]
        private void Server_PruneSchedules(List<NetworkIdentity> activePlayers)
        {
            _scheduleCleanupBuffer.Clear();

            foreach (var pair in _nextSpawnTimeByPlayerNetId)
            {
                if (Server_ContainsPlayer(activePlayers, pair.Key))
                    continue;

                _scheduleCleanupBuffer.Add(pair.Key);
            }

            foreach (var removedPlayerNetId in _scheduleCleanupBuffer)
                _nextSpawnTimeByPlayerNetId.Remove(removedPlayerNetId);
        }

        [Server]
        private int Server_DespawnFarEnemies(List<EnemyMeleeAgent> enemies, List<NetworkIdentity> players,
            int currentEnemies)
        {
            if (enemies.Count == 0)
                return currentEnemies;

            foreach (var enemy in enemies)
            {
                if (!enemy)
                    continue;

                if (players.Count > 0 && Server_IsNearAnyPlayer(enemy.transform.position, players, despawnDistanceFromPlayers))
                    continue;

                NetworkServer.Destroy(enemy.gameObject);
                currentEnemies = Mathf.Max(0, currentEnemies - 1);
            }

            return currentEnemies;
        }

        [Server]
        private void Server_TrySpawnForPlayers(List<NetworkIdentity> players, ref int currentEnemies)
        {
            if (players.Count == 0 || currentEnemies >= globalMaxEnemies)
                return;

            for (var i = 0; i < players.Count; i++)
            {
                if (currentEnemies >= globalMaxEnemies)
                    return;

                var playerIdentity = players[i];
                if (!playerIdentity)
                    continue;

                var playerNetId = playerIdentity.netId;
                var now = Time.time;

                if (!_nextSpawnTimeByPlayerNetId.TryGetValue(playerNetId, out var nextSpawnTime))
                {
                    _nextSpawnTimeByPlayerNetId[playerNetId] = now + Server_GetRandomSpawnInterval();
                    continue;
                }

                if (now < nextSpawnTime)
                    continue;

                _nextSpawnTimeByPlayerNetId[playerNetId] = now + Server_GetRandomSpawnInterval();
                var spawnCount = Random.Range(Mathf.Min(spawnCountMin, spawnCountMax),
                    Mathf.Max(spawnCountMin, spawnCountMax) + 1);

                for (var spawned = 0; spawned < spawnCount; spawned++)
                {
                    if (currentEnemies >= globalMaxEnemies)
                        return;

                    if (!Server_TryFindSpawnPosition(playerIdentity.transform.position, players, out var spawnPos))
                        continue;

                    var createdEnemy = Instantiate(enemyPrefab, spawnPos, Quaternion.identity);
                    NetworkServer.Spawn(createdEnemy.gameObject);
                    currentEnemies++;
                }
            }
        }

        [Server]
        private bool Server_TryFindSpawnPosition(Vector3 anchorPosition, List<NetworkIdentity> players,
            out Vector3 spawnPosition)
        {
            var minDistance = Mathf.Max(0.1f, Mathf.Min(minSpawnDistanceFromPlayers, maxSpawnDistanceFromPlayer));
            var maxDistance = Mathf.Max(minDistance + 0.01f, Mathf.Max(minSpawnDistanceFromPlayers, maxSpawnDistanceFromPlayer));
            var attempts = Mathf.Max(1, spawnAttemptsPerEnemy);

            for (var attempt = 0; attempt < attempts; attempt++)
            {
                var angle = Random.Range(0f, Mathf.PI * 2f);
                var distance = Random.Range(minDistance, maxDistance);
                var offset = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distance;
                var rawPos = (Vector2)anchorPosition + offset;

                var cellPos = new Vector2Int(Mathf.FloorToInt(rawPos.x), Mathf.FloorToInt(rawPos.y));
                if (!World.IsWorldPositionInsideBounds(cellPos.x, cellPos.y))
                    continue;

                var chunk = _world.GetChunkByWorldPosition(cellPos.x, cellPos.y);
                if (chunk == null)
                    continue;

                var localIndexes = World.ConvertWorldToChunkSpace(cellPos.x, cellPos.y);
                var cell = chunk.GetCell(localIndexes.x, localIndexes.y);

                if (cell.Block.type != BlockType.AIR || cell.Floor.type == BlockType.AIR)
                    continue;

                var worldCenterPos = new Vector3(cellPos.x + 0.5f, cellPos.y + 0.5f, 0f);

                if (!Server_IsFarEnoughFromPlayers(worldCenterPos, players, minDistance))
                    continue;

                if (Physics2D.OverlapCircle(worldCenterPos, spawnCheckRadius, spawnBlockingLayers))
                    continue;

                spawnPosition = worldCenterPos;
                return true;
            }

            spawnPosition = default;
            return false;
        }

        [Server]
        private bool Server_IsFarEnoughFromPlayers(Vector2 position, List<NetworkIdentity> players, float minDistance)
        {
            var minSqrDistance = minDistance * minDistance;

            for (var i = 0; i < players.Count; i++)
            {
                var player = players[i];
                if (!player)
                    continue;

                if (((Vector2)player.transform.position - position).sqrMagnitude < minSqrDistance)
                    return false;
            }

            return true;
        }

        [Server]
        private bool Server_IsNearAnyPlayer(Vector2 position, List<NetworkIdentity> players, float maxDistance)
        {
            var maxSqrDistance = maxDistance * maxDistance;

            for (var i = 0; i < players.Count; i++)
            {
                var player = players[i];
                if (!player)
                    continue;

                if (((Vector2)player.transform.position - position).sqrMagnitude <= maxSqrDistance)
                    return true;
            }

            return false;
        }

        [Server]
        private float Server_GetRandomSpawnInterval()
        {
            var min = Mathf.Min(spawnIntervalMin, spawnIntervalMax);
            var max = Mathf.Max(spawnIntervalMin, spawnIntervalMax);
            return Random.Range(min, max);
        }

        [Server]
        private static bool Server_ContainsPlayer(List<NetworkIdentity> players, uint playerNetId)
        {
            for (var i = 0; i < players.Count; i++)
            {
                var player = players[i];
                if (player && player.netId == playerNetId)
                    return true;
            }

            return false;
        }
    }
}
