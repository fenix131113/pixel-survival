using GameAssembly.BuildSystem;
using GameAssembly.InventorySystem.View;
using GameAssembly.PlayerSystem.Data;
using GameAssembly.PlayerSystem.Variables;
using GameAssembly.Utils;
using GameAssembly.WorldSystem;
using PlayerSystem;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace GameAssembly.Core
{
    public class GameInstaller : LifetimeScope
    {
        [SerializeField] private PlayerDataSO playerData;

        private static GameInstaller _instance;

        private InputSystem_Actions _input;
        private World _world;

        protected void Start()
        {
            ObjectInjector.Initialize(Container);
            _instance = this;
        }

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

            #region World

            _world = new World();
            builder.RegisterInstance(_world);
            builder.RegisterComponentInHierarchy<WorldCreateManager>();

            #endregion

            #region Build

            builder.RegisterComponentInHierarchy<ServerBuild>();

            #endregion
        }

        public static T Resolve<T>()
        {
            return !_instance ? default : _instance.Container.Resolve<T>();
        }
    }
}