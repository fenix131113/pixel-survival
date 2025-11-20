namespace GameAssembly.ItemsSystem
{
    public static class BehaviourRegister
    {
        public static bool Initialized { get; private set; }

        public static void Initialize()
        {
            if(Initialized)
                return;
            
            //ItemRegistry.Instance.RegisterItemBehaviour(ItemDatabase.TestItem, new TestBehaviour());

            Initialized = true;
        }
    }
}