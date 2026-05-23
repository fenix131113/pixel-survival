using GameAssembly.ItemsSystem.Data;
using Mirror;
using UnityEngine;

namespace GameAssembly.HealthSystem
{
    public class ToolHealthObject : BaseHealthObject
    {
        private enum ToolLevelRequirementMode : byte
        {
            Any = 0,
            MinimumTool = 1
        }

        [SerializeField] private ToolType needType;
        [SerializeField] private ToolLevelRequirementMode levelRequirementMode = ToolLevelRequirementMode.Any;
        [SerializeField] private ToolItemDefinitionSO minimumRequiredTool;

        [Server]
        public override void ChangeHealth(int value, DamageContext ctx)
        {
            if (value < 0)
            {
                if (ctx.DamageItem is not { Definition: ToolItemDefinitionSO toolDefinition } ||
                    !CanDealDamageWithTool(toolDefinition))
                    return;

                value = -toolDefinition.MiningDamage;
            }

            base.ChangeHealth(value, ctx);
        }

        private bool CanDealDamageWithTool(ToolItemDefinitionSO toolDefinition)
        {
            if (!toolDefinition || toolDefinition.ToolType != needType)
                return false;

            if (!SupportsLevelRequirement(needType) || levelRequirementMode == ToolLevelRequirementMode.Any)
                return true;

            if (!minimumRequiredTool || minimumRequiredTool.ToolType != needType)
                return false;

            return toolDefinition.MiningDamage >= minimumRequiredTool.MiningDamage;
        }

        private static bool SupportsLevelRequirement(ToolType toolType)
        {
            return toolType is ToolType.PICKAXE or ToolType.AXE;
        }
    }
}
