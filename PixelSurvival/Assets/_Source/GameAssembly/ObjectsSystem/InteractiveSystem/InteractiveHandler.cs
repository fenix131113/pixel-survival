using UnityEngine;

namespace GameAssembly.ObjectsSystem.InteractiveSystem
{
    public class InteractiveHandler : MonoBehaviour, IInteractiveObject
    {
        [SerializeField] private GameObject interactiveObject;

        public void Interact()
        {
            interactiveObject.GetComponent<IInteractiveObject>().Interact();
        }
        
        private void OnValidate()
        {
            if (!interactiveObject || interactiveObject.GetComponent<IInteractiveObject>() != null)
                return;
            
            interactiveObject = null;
            Debug.LogWarning("Selected object is not interactive object!");
        }
    }
}