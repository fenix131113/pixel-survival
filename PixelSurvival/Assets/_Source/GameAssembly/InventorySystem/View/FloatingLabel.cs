using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GameAssembly.InventorySystem.View
{
    public class FloatingLabel : MonoBehaviour
    {
        [SerializeField] private TMP_Text label;

        private bool _isShowing;

        private void Update()
        {
            if(!_isShowing)
                return;
            
            transform.position = Mouse.current.position.ReadValue();
        }

        public void Show(string text)
        {
            label.text = text;
            gameObject.SetActive(true);
            _isShowing = true;
        }

        public void Hide()
        {
            gameObject.SetActive(false);
            _isShowing = false;
        }
    }
}