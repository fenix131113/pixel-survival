using System.Linq;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace GameAssembly.Utils.Tiles
{
    [CreateAssetMenu(menuName = "Tiles/Advanced Rule Tile")]
    public class AdvancedRuleTile : RuleTile<AdvancedRuleTile.Neighbor>
    {
        [Header("Custom Groups")] public TileBase[] groupA;
        public TileBase[] groupB;

        public class Neighbor : RuleTile.TilingRuleOutput.Neighbor
        {
            public const int GROUP_A = 3;
            public const int NOT_GROUP_A = 4;

            public const int GROUP_B = 5;
            public const int NOT_GROUP_B = 6;
        }

        public override bool RuleMatch(int neighbor, TileBase other)
        {
            return neighbor switch
            {
                TilingRuleOutput.Neighbor.This => other == this,
                TilingRuleOutput.Neighbor.NotThis => other != this,
                Neighbor.GROUP_A => Contains(groupA, other),
                Neighbor.NOT_GROUP_A => !Contains(groupA, other),
                Neighbor.GROUP_B => Contains(groupB, other),
                Neighbor.NOT_GROUP_B => !Contains(groupB, other),
                _ => base.RuleMatch(neighbor, other)
            };
        }

        private bool Contains(TileBase[] array, TileBase tile) => array != null && array.Any(t => t == tile);
    }
}