using GameAssembly.Generated;
using GameAssembly.ItemsSystem.Items;

namespace GameAssembly.ItemsSystem
{
    public static class BehaviourRegister
    {
        public static bool Initialized { get; private set; }

        public static void Initialize()
        {
            if(Initialized)
                return;
            
            ItemRegistry.Instance.RegisterItemBehaviour(ItemDatabase.Apple, new DebugBehaviour());

            Initialized = true;
        }
    }
}