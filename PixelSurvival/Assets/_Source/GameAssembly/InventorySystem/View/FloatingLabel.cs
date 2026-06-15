using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GameAssembly.InventorySystem.View
{
    public class FloatingLabel : MonoBehaviour
    {
        [SerializeField] private TMP_Text label;

        public GameObject Target { get; private set; }

        private void Update()
        {
            if(!Target)
                return;
            
            transform.position = Mouse.current.position.ReadValue();
            label.gameObject.SetActive(true);
        }

        public void Show(GameObject target, string text)
        {
            label.text = text;
            Target = target;
            gameObject.SetActive(true);
        }

        public void Hide()
        {
            label.gameObject.SetActive(false);
            gameObject.SetActive(false);
            Target = null;
        }
    }
}