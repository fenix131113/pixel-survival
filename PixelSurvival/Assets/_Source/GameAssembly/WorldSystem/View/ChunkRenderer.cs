using GameAssembly.WorldSystem.Data;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace GameAssembly.WorldSystem.View
{
    public class ChunkRenderer : MonoBehaviour
    {
        [field: SerializeField] public Tilemap UpperTilemap { get; private set; }
        [field: SerializeField] public Tilemap FloorTilemap { get; private set; }
        [SerializeField] private TilemapCollider2D tilemapCollider;
        public Chunk Chunk { get; private set; }


        public void Construct(Chunk chunk)
        {
            Chunk = chunk;
            Chunk.OnChunkChanged += OnChunkChanged;
        }

        private void OnChunkChanged(Chunk chunk)
        {
            RebuildVisual();
            RebuildCollider();
        }

        public void RebuildVisual()
        {
            UpperTilemap.ClearAllTiles();

            for (var x = 0; x < Chunk.CHUNK_SIZE; x++)
            {
                for (var y = 0; y < Chunk.CHUNK_SIZE; y++)
                {
                    var cell = Chunk.Cells[x, y];

                    if (cell.Floor.type != BlockType.AIR)
                    {
                        var def = cell.Floor.definition;
                        FloorTilemap.SetTile(new Vector3Int(x, y, 0), def.Tile ? def.Tile : null);
                    }
                    else
                    {
                        FloorTilemap.SetTile(new Vector3Int(x, y, 0), null);
                    }

                    if (cell.Block.type != BlockType.AIR)
                    {
                        var def = cell.Block.definition;
                        UpperTilemap.SetTile(new Vector3Int(x, y, 1), def.Tile ? def.Tile : null);
                    }
                    else
                        UpperTilemap.SetTile(new Vector3Int(x, y, 1), null);
                }
            }

            Chunk.DirtyVisual = false;
        }

        public void RebuildCollider()
        {
            if (!Chunk.DirtyCollider)
                return;

            tilemapCollider.enabled = false;
            tilemapCollider.enabled = true;
            Chunk.DirtyCollider = false;
        }
    }
}