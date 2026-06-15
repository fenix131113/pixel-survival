using GameAssembly.BuildSystem;
using GameAssembly.BuildSystem.WorldObjects;
using GameAssembly.InventorySystem;
using GameAssembly.InventorySystem.View;
using GameAssembly.ObjectsSystem.View.ObjectsView;
using GameAssembly.PlayerSystem.Data;
using GameAssembly.PlayerSystem.Variables;
using GameAssembly.PlayerSystem.View;
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

            builder.RegisterComponentInHierarchy<ServerInventoryManager>();
            builder.RegisterComponentInHierarchy<PlayerInventoryView>();
            builder.RegisterComponentInHierarchy<MovingItem>();
            builder.RegisterComponentInHierarchy<ChestView>();
            builder.RegisterComponentInHierarchy<WorkbenchView>();
            builder.RegisterComponentInHierarchy<EndGameChestView>();
            builder.RegisterComponentInHierarchy<FurnaceView>();
            builder.RegisterComponentInHierarchy<CampfireView>();
            builder.RegisterComponentInHierarchy<FloatingLabel>();

            #endregion

            #region World

            _world = new World();
            builder.RegisterInstance(_world);
            builder.Register<ServerBlockDamageSystem>(Lifetime.Scoped)
                .AsSelf();
            builder.RegisterComponentInHierarchy<WorldCreateManager>();

            #endregion

            #region Build

            builder.Register<WorldObjectRegistry>(Lifetime.Scoped)
                .AsSelf();
            builder.Register<WorldObjectsGenerator>(Lifetime.Scoped)
                .AsSelf();
            builder.RegisterComponentInHierarchy<ServerBuild>();

            #endregion
        }

        public static T Resolve<T>()
        {
            return !_instance ? default : _instance.Container.Resolve<T>();
        }
    }
}
