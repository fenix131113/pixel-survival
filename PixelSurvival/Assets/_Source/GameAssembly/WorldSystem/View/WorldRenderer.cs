using System;
using System.Collections.Generic;
using UnityEngine;
using VContainer;

namespace GameAssembly.WorldSystem.View
{
    public class WorldRenderer : MonoBehaviour
    {
        [SerializeField] private ChunkRenderer chunkPrefab;

        private readonly Dictionary<ChunkCoord, ChunkRenderer> _chunksRenderers = new();
        private readonly Dictionary<Vector2Int, float> _activeBlockDamageProgress = new();

        [Inject] private World _world;
        [Inject] private WorldCreateManager _worldCreateManager;

        private void Awake()
        {
            _world.Progress.ProgressChanged += OnGenerateProgressChanged;

            if (!_worldCreateManager)
                return;

            _worldCreateManager.ClientOnBlockDamageProgress += OnBlockDamageProgress;
            _worldCreateManager.ClientOnBlockDamageCleared += OnBlockDamageCleared;
        }

        private void OnDestroy()
        {
            _world.Progress.ProgressChanged -= OnGenerateProgressChanged;

            if (!_worldCreateManager)
                return;

            _worldCreateManager.ClientOnBlockDamageProgress -= OnBlockDamageProgress;
            _worldCreateManager.ClientOnBlockDamageCleared -= OnBlockDamageCleared;
        }

        private void LateUpdate()
        {
            if (_activeBlockDamageProgress.Count == 0)
                return;

            foreach (var pair in _activeBlockDamageProgress)
                TryApplyBlockDamageTint(pair.Key, pair.Value);
        }

        private void OnGenerateProgressChanged(object sender, float e)
        {
            foreach (var pair in _world.Chunks)
                if (!_chunksRenderers.ContainsKey(pair.Key))
                    SpawnChunk(pair.Value);
        }

        public void SpawnChunk(Chunk chunk)
        {
            var pos = new Vector3(chunk.Coord.X * Chunk.CHUNK_SIZE, chunk.Coord.Y * Chunk.CHUNK_SIZE, 0);
            var go = Instantiate(chunkPrefab, pos, Quaternion.identity);
            var rend = go.GetComponent<ChunkRenderer>();
            rend.transform.parent = transform;
            _chunksRenderers.Add(chunk.Coord, rend);
            rend.Construct(chunk);

            rend.RebuildVisual();
            rend.RebuildCollider();
            ApplyDamageTintForChunk(chunk.Coord, rend);
        }

        private void RebuildChunkVisual(Chunk chunk)
        {
            if (_chunksRenderers.TryGetValue(chunk.Coord, out var rend))
                rend.RebuildVisual();
        }

        private void RebuildChunkCollider(Chunk chunk)
        {
            if (_chunksRenderers.TryGetValue(chunk.Coord, out var rend))
                rend.RebuildCollider();
        }

        private void OnBlockDamageProgress(Vector2Int blockWorldPos, float progress01, int currentDamage, int maxHealth)
        {
            _activeBlockDamageProgress[blockWorldPos] = progress01;
            TryApplyBlockDamageTint(blockWorldPos, progress01);
        }

        private void OnBlockDamageCleared(Vector2Int blockWorldPos)
        {
            _activeBlockDamageProgress.Remove(blockWorldPos);
            TryClearBlockDamageTint(blockWorldPos);
        }

        private void ApplyDamageTintForChunk(ChunkCoord chunkCoord, ChunkRenderer chunkRend)
        {
            foreach (var pair in _activeBlockDamageProgress)
            {
                if (!GetChunkCoordByWorldPos(pair.Key).Equals(chunkCoord))
                    continue;

                var localIndexes = World.ConvertWorldToChunkSpace(pair.Key.x, pair.Key.y);
                chunkRend.SetBlockDamageTint(localIndexes, pair.Value);
            }
        }

        private void TryApplyBlockDamageTint(Vector2Int blockWorldPos, float progress01)
        {
            if (!TryGetChunkRendererByWorldPos(blockWorldPos, out var chunkRend, out var localIndexes))
                return;

            chunkRend.SetBlockDamageTint(localIndexes, progress01);
        }

        private void TryClearBlockDamageTint(Vector2Int blockWorldPos)
        {
            if (!TryGetChunkRendererByWorldPos(blockWorldPos, out var chunkRend, out var localIndexes))
                return;

            chunkRend.ClearBlockDamageTint(localIndexes);
        }

        private bool TryGetChunkRendererByWorldPos(Vector2Int blockWorldPos, out ChunkRenderer chunkRenderer,
            out Vector2Int localIndexes)
        {
            var chunkCoord = GetChunkCoordByWorldPos(blockWorldPos);
            localIndexes = World.ConvertWorldToChunkSpace(blockWorldPos.x, blockWorldPos.y);

            return _chunksRenderers.TryGetValue(chunkCoord, out chunkRenderer);
        }

        private static ChunkCoord GetChunkCoordByWorldPos(Vector2Int blockWorldPos)
        {
            return new ChunkCoord(
                Mathf.FloorToInt(blockWorldPos.x / (float)Chunk.CHUNK_SIZE),
                Mathf.FloorToInt(blockWorldPos.y / (float)Chunk.CHUNK_SIZE));
        }
    }
}
