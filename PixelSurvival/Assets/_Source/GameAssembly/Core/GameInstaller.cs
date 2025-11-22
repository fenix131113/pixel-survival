using GameAssembly.InventorySystem.View;
using GameAssembly.PlayerSystem.Data;
using GameAssembly.PlayerSystem.Variables;
using PlayerSystem;
using UnityEngine;
using Utils;
using VContainer;
using VContainer.Unity;

namespace GameAssembly.Core
{
    public class GameInstaller : LifetimeScope
    {
        [SerializeField] private PlayerDataSO playerData;

        private InputSystem_Actions _input;

        protected void Start() => ObjectInjector.Initialize(Container);

        protected override void Configure(IContainerBuilder builder)
        {
            #region Player

            _input = new InputSystem_Actions();
            _input.Player.Enable();
            builder.RegisterInstance(_input);

            builder.RegisterInstance(playerData);

            builder.Register<PlayerVariables>(Lifetime.Scoped)
                .AsImplementedInterfaces()
                .AsSelf();

            #endregion

            #region InventorySystem

            builder.RegisterComponentInHierarchy<MovingItem>();

            #endregion
        }
    }
}