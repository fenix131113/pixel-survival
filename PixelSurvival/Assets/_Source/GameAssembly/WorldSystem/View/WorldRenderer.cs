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

        [Inject] public World World;

        private void Awake()
        {
            foreach (var pair in World.Chunks)
                SpawnChunk(pair.Value);
        }

        private void Update()
        {
            foreach (var chunk in World.GetDirtyChunks())
            {
                if (chunk.DirtyVisual)
                    RebuildVisual(chunk);
                if (chunk.DirtyCollider)
                    RebuildCollider(chunk);

                chunk.DirtyVisual = false;
                chunk.DirtyCollider = false;
            }
        }

        public void SpawnChunk(Chunk chunk)
        {
            var pos = new Vector3(chunk.Coord.X * Chunk.CHUNK_SIZE, chunk.Coord.Y * Chunk.CHUNK_SIZE, 0);
            var go = Instantiate(chunkPrefab, pos, Quaternion.identity);
            var rend = go.GetComponent<ChunkRenderer>();
            _chunksRenderers.Add(chunk.Coord, rend);
            rend.Construct(chunk);

            for (var i = 0; i < Chunk.CHUNK_SIZE; i++)
            {
                for (var j = 0; j < Chunk.CHUNK_SIZE; j++)
                {
                    rend.Chunk.SetCell(i, j,
                        new CellData { Block = new BlockData(defaultBlock), Floor = new BlockData(defaultBlock) });
                }
            }

            rend.RebuildVisual();
            rend.RebuildCollider();
        }

        private void RebuildVisual(Chunk chunk)
        {
            if(_chunksRenderers.TryGetValue(chunk.Coord, out var rend))
                rend.RebuildVisual();
        }

        private void RebuildCollider(Chunk chunk)
        {
            if(_chunksRenderers.TryGetValue(chunk.Coord, out var rend))
                rend.RebuildCollider();
        }
    }
}