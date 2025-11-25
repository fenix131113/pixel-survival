using GameAssembly.HealthSystem.Data;
using GameAssembly.ItemsSystem;
using UnityEngine;

namespace GameAssembly.HealthSystem
{
    public class DamageContext
    {
        public readonly HealthType DamageFromHealthType;
        public readonly GameObject DamageSourceObject;
        public readonly ItemInstance DamageItem;

        public DamageContext(GameObject damageSourceObject, ItemInstance damageItem, HealthType damageFromHealthType = HealthType.UNKNOWN)
        {
            DamageFromHealthType = damageFromHealthType;
            DamageSourceObject = damageSourceObject;
            DamageItem = damageItem;
        }
    }
}