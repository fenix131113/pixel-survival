using GameAssembly.WorldSystem.Data;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace GameAssembly.WorldSystem.View
{
    public class ChunkRenderer : MonoBehaviour
    {
        private static readonly Color _damagedTintColor = new(0.45f, 0.45f, 0.45f, 1f);

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
                        var upperPos = new Vector3Int(x, y, 1);
                        UpperTilemap.SetTile(upperPos, def.Tile ? def.Tile : null);
                        UpperTilemap.SetTileFlags(upperPos, TileFlags.None);
                        UpperTilemap.SetColor(upperPos, Color.white);
                    }
                    else
                    {
                        var upperPos = new Vector3Int(x, y, 1);
                        UpperTilemap.SetTile(upperPos, null);
                        UpperTilemap.SetTileFlags(upperPos, TileFlags.None);
                        UpperTilemap.SetColor(upperPos, Color.white);
                    }
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

        public void SetBlockDamageTint(Vector2Int localIndexes, float progress01)
        {
            var tilePos = new Vector3Int(localIndexes.x, localIndexes.y, 1);
            if (!UpperTilemap.GetTile(tilePos))
                return;

            UpperTilemap.SetTileFlags(tilePos, TileFlags.None);
            UpperTilemap.SetColor(tilePos, Color.Lerp(Color.white, _damagedTintColor, Mathf.Clamp01(progress01)));
        }

        public void ClearBlockDamageTint(Vector2Int localIndexes)
        {
            var tilePos = new Vector3Int(localIndexes.x, localIndexes.y, 1);
            UpperTilemap.SetTileFlags(tilePos, TileFlags.None);
            UpperTilemap.SetColor(tilePos, Color.white);
        }
    }
}
