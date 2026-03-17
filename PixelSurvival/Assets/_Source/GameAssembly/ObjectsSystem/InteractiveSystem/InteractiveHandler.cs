using UnityEngine;

namespace GameAssembly.ObjectsSystem.InteractiveSystem
{
    // Handler for adding to any object and redirect interact call to given interaction object
    public class InteractiveHandler : MonoBehaviour, IInteractiveObject
    {
        [SerializeField] private SpriteRenderer interactiveObject;

        public void Interact()
        {
            interactiveObject.GetComponent<IInteractiveObject>().Interact();
        }

        public Renderer GetRendererTarget() => interactiveObject;

        private void OnValidate()
        {
            if (!interactiveObject || interactiveObject.GetComponent<IInteractiveObject>() != null)
                return;
            
            interactiveObject = null;
            Debug.LogWarning("Selected object is not interactive object!");
        }
    }
}