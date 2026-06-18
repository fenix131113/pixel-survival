using GameAssembly.Generated;
using FoodBehaviour = GameAssembly.ItemsSystem.Behaviours.FoodBehaviour;

namespace GameAssembly.ItemsSystem
{
    public static class BehaviourRegister
    {
        public static bool Initialized { get; private set; }

        public static void Initialize()
        {
            if(Initialized)
                return;
            
            var foodBehaviour = new FoodBehaviour();
            
            //ItemRegistry.Instance.RegisterItemBehaviour(ItemDatabase.Apple, new DebugBehaviour());
            ItemRegistry.Instance.RegisterItemBehaviour(ItemDatabase.ProteinPorridge, foodBehaviour);
            ItemRegistry.Instance.RegisterItemBehaviour(ItemDatabase.SweetWorm, foodBehaviour);
            ItemRegistry.Instance.RegisterItemBehaviour(ItemDatabase.CrispyWorm, foodBehaviour);
            ItemRegistry.Instance.RegisterItemBehaviour(ItemDatabase.VerySweetStone, foodBehaviour);
            ItemRegistry.Instance.RegisterItemBehaviour(ItemDatabase.SweetPorridge, foodBehaviour);
            ItemRegistry.Instance.RegisterItemBehaviour(ItemDatabase.RockyPorridge, foodBehaviour);
            ItemRegistry.Instance.RegisterItemBehaviour(ItemDatabase.Worm, foodBehaviour);
            ItemRegistry.Instance.RegisterItemBehaviour(ItemDatabase.SweetRoot, foodBehaviour);
            ItemRegistry.Instance.RegisterItemBehaviour(ItemDatabase.Resin, foodBehaviour);
            ItemRegistry.Instance.RegisterItemBehaviour(ItemDatabase.SweetRock, foodBehaviour);

            Initialized = true;
        }
    }
}