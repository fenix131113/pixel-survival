using GameAssembly.WorldSystem.Data;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace GameAssembly.WorldSystem.View
{
    public class ChunkRenderer : MonoBehaviour
    {
        [field: SerializeField] public Tilemap Tilemap { get; private set; }
        [SerializeField] private TilemapCollider2D tilemapCollider;
        public Chunk Chunk { get; private set; }
        

        public void Construct(Chunk chunk)
        {
            Chunk = chunk;
        }

        public void RebuildVisual()
        {
            Tilemap.ClearAllTiles();

            for (var x = 0; x < Chunk.CHUNK_SIZE; x++)
            {
                for (var y = 0; y < Chunk.CHUNK_SIZE; y++)
                {
                    var cell = Chunk.Cells[x, y];

                    if (cell.Floor.type != BlockType.AIR)
                    {
                        var def = cell.Floor.definition;
                        if (def.tile)
                            Tilemap.SetTile(new Vector3Int(x, y, 0), def.tile);
                    }
                    else
                        Tilemap.SetTile(new Vector3Int(x, y, 0), null);

                    if (cell.Block.type != BlockType.AIR)
                    {
                        var def = cell.Block.definition;
                        if (def.tile)
                            Tilemap.SetTile(new Vector3Int(x, y, 1), def.tile);
                    }
                    else
                        Tilemap.SetTile(new Vector3Int(x, y, 1), null);
                }
            }
        }

        public void RebuildCollider() // TODO: Make collider building
        {
            if (!Chunk.DirtyCollider)
                return;
            
            tilemapCollider.enabled = false;
            tilemapCollider.enabled = true;
            Chunk.DirtyCollider = false;
        }
    }
}