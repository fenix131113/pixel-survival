using GameAssembly.HealthSystem;
using GameAssembly.HealthSystem.Data;
using GameAssembly.ItemsSystem.Data;
using Mirror;

namespace GameAssembly.ItemsSystem.Behaviours
{
    public class FoodBehaviour : IItemBehaviour
    {
        public void OnAim(ItemContext ctx)
        {
            if (!NetworkServer.active || !ctx.PlayerIdentity ||
                ctx.Instance is not { Definition: FoodItemDefinitionSO def})
                return;
            
            ctx.PlayerIdentity.GetComponent<IHealth>().ChangeHealth(def.HealthRecover,
                new DamageContext(ctx.PlayerIdentity.gameObject, ctx.Instance, HealthType.PLAYER));
            ctx.Instance.TryRemoveCount(1);
        }
    }
}