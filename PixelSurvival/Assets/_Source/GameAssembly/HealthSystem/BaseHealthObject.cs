using Mirror;
using UnityEngine;

namespace GameAssembly.HealthSystem
{
    public class BaseHealthObject : AHealthObject
    {
        [SerializeField] protected bool immediatelyDestroy;
        
        protected override void Server_OnHealthZero()
        {
            base.Server_OnHealthZero();
            
            if(immediatelyDestroy)
                NetworkServer.Destroy(gameObject);
        }
    }
}