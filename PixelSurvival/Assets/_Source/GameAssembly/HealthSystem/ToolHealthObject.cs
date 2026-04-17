using GameAssembly.ItemsSystem.Data;
using Mirror;
using UnityEngine;

namespace GameAssembly.HealthSystem
{
    public class ToolHealthObject : BaseHealthObject //TODO: Make that some objects that need to be broken with pickaxe require MINIMUM XXX level of pickaxe
    {
        [SerializeField] private ToolType needType;

        [Server]
        public override void ChangeHealth(int value, DamageContext ctx)
        {
            if (value < 0)
            {
                if (ctx.DamageItem is not { Definition: ToolItemDefinitionSO so } || so.ToolType != needType)
                    return;

                value = -so.MiningDamage;
            }

            base.ChangeHealth(value, ctx);
        }
    }
}