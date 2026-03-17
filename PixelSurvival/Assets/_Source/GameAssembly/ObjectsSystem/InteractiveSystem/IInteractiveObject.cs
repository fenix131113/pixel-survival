using UnityEngine;

namespace GameAssembly.ObjectsSystem.InteractiveSystem
{
    public interface IInteractiveObject
    {
        void Interact();
        Renderer GetRendererTarget();
    }
}