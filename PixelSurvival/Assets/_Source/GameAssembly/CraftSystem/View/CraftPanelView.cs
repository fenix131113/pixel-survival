using System;
using PlayerSystem;
using UnityEngine;
using UnityEngine.InputSystem;
using VContainer;

namespace GameAssembly.CraftSystem.View
{
    public class CraftPanelView : MonoBehaviour
    {
        [SerializeField] private GameObject craftPanel;
        
        [Inject] private InputSystem_Actions _input;

        private void Start() => Bind();

        private void OnDestroy() => Expose();

        private void OnCraftMenuClicked(InputAction.CallbackContext callbackContext)
        {
            craftPanel.SetActive(!craftPanel.activeSelf);
        }

        private void Bind() => _input.Player.CraftMenu.performed += OnCraftMenuClicked;

        private void Expose() => _input.Player.CraftMenu.performed -= OnCraftMenuClicked;
    }
}