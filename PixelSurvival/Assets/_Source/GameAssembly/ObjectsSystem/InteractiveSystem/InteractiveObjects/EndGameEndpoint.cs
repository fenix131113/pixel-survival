using System;

namespace GameAssembly.ObjectsSystem.InteractiveSystem.InteractiveObjects
{
    public static class EndGameEndpoint
    {
        public static event Action<EndGameChest> OnEndGameTriggered;

        internal static void Raise(EndGameChest sourceChest)
        {
            OnEndGameTriggered?.Invoke(sourceChest);
        }
    }
}
