using System;
using System.Collections.Generic;
using GameAssembly.WorldSystem.Data;
using UnityEngine;
using VContainer;

namespace GameAssembly.WorldSystem.View
{
    public class WorldRenderer : MonoBehaviour
    {
        [SerializeField] private BlockDefinition defaultBlock;
        [SerializeField] private ChunkRenderer chunkPrefab;
        private readonly Dictionary<ChunkCoord, ChunkRenderer> _chunksRenderers = new();

        [Inject] private World _world;

        private void Awake()
        {
            _world.Progress.ProgressChanged += OnGenerateProgressChanged;
        }

        private void OnDestroy()
        {
            _world.Progress.ProgressChanged -= OnGenerateProgressChanged;
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
            _chunksRenderers.Add(chunk.Coord, rend);
            rend.Construct(chunk);

            rend.RebuildVisual();
            rend.RebuildCollider();
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
    }
}