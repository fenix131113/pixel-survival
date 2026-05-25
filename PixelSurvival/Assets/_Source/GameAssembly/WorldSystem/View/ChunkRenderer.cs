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
        private World _world;

        private void Awake()
        {
            SetChunkTilemapRenderersEnabled(false);
        }

        private void OnDestroy()
        {
            if (Chunk != null)
            {
                Chunk.OnChunkCellChanged -= OnChunkCellChanged;
            }
        }

        public void Construct(Chunk chunk, World world)
        {
            Chunk = chunk;
            _world = world;
            Chunk.OnChunkCellChanged += OnChunkCellChanged;
        }

        private void OnChunkCellChanged(Chunk chunk, Vector2Int localIndexes)
        {
            UpdateVisualArea(localIndexes);
            RebuildCollider();
        }

        public void RebuildVisual()
        {
            UpperTilemap.ClearAllTiles();
            var upperTiles = new TileBase[Chunk.CHUNK_SIZE * Chunk.CHUNK_SIZE];

            for (var x = 0; x < Chunk.CHUNK_SIZE; x++)
            {
                for (var y = 0; y < Chunk.CHUNK_SIZE; y++)
                {
                    var cell = Chunk.Cells[x, y];
                    upperTiles[GetFlatTileIndex(x, y)] = ResolveUpperTile(x, y, cell.Block);
                }
            }

            UpperTilemap.SetTilesBlock(new BoundsInt(0, 0, 1, Chunk.CHUNK_SIZE, Chunk.CHUNK_SIZE, 1), upperTiles);
            Chunk.DirtyVisual = false;
            UpperTilemap.RefreshAllTiles();
        }

        public void RebuildCollider()
        {
            if (!Chunk.DirtyCollider)
                return;

            tilemapCollider.ProcessTilemapChanges();
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

        private void SetChunkTilemapRenderersEnabled(bool enabled)
        {
            if (UpperTilemap && UpperTilemap.TryGetComponent<TilemapRenderer>(out var upperRenderer))
            {
                upperRenderer.mode = TilemapRenderer.Mode.Individual;
                upperRenderer.enabled = enabled;
            }

            if (FloorTilemap && FloorTilemap.TryGetComponent<TilemapRenderer>(out var floorRenderer))
                floorRenderer.enabled = enabled;
        }

        private void UpdateVisualArea(Vector2Int localIndexes)
        {
            for (var dx = -1; dx <= 1; dx++)
            {
                for (var dy = -1; dy <= 1; dy++)
                {
                    var x = localIndexes.x + dx;
                    var y = localIndexes.y + dy;

                    if (x < 0 || x >= Chunk.CHUNK_SIZE || y < 0 || y >= Chunk.CHUNK_SIZE)
                        continue;

                    var upperPos = new Vector3Int(x, y, 1);
                    var cell = Chunk.Cells[x, y];

                    if (cell.Block.type != BlockType.AIR)
                    {
                        UpperTilemap.SetTile(upperPos, ResolveUpperTile(x, y, cell.Block));
                    }
                    else
                    {
                        UpperTilemap.SetTile(upperPos, null);
                    }
                }
            }

            RefreshArea(localIndexes);
        }

        private void RefreshArea(Vector2Int localIndexes)
        {
            for (var dx = -1; dx <= 1; dx++)
            {
                for (var dy = -1; dy <= 1; dy++)
                {
                    var x = localIndexes.x + dx;
                    var y = localIndexes.y + dy;

                    if (x < 0 || x >= Chunk.CHUNK_SIZE || y < 0 || y >= Chunk.CHUNK_SIZE)
                        continue;

                    UpperTilemap.RefreshTile(new Vector3Int(x, y, 1));
                }
            }
        }

        private static int GetFlatTileIndex(int x, int y)
        {
            return x + y * Chunk.CHUNK_SIZE;
        }

        private TileBase ResolveUpperTile(int localX, int localY, BlockData block)
        {
            if (_world != null && Chunk != null)
            {
                var worldX = Chunk.Coord.X * Chunk.CHUNK_SIZE + localX;
                var worldY = Chunk.Coord.Y * Chunk.CHUNK_SIZE + localY;
                if (_world.TryGetBorderWallTileOverride(worldX, worldY, out var borderTile))
                    return borderTile;
            }

            var blockDef = block.definition;
            return blockDef && blockDef.Tile ? blockDef.Tile : null;
        }
    }
}
