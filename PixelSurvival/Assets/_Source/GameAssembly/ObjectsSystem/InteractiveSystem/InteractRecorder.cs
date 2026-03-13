using System;
using GameAssembly.PlayerSystem.Data;
using GameAssembly.Utils;
using GameAssembly.Utils.VariablesSystem;
using Mirror;
using PlayerSystem;
using UnityEngine;
using UnityEngine.InputSystem;
using VContainer;

namespace GameAssembly.ObjectsSystem.InteractiveSystem
{
    public class InteractRecorder : MonoBehaviour
    {
        [SerializeField] private LayerMask interactiveLayers;

        private const float MAX_INTERACT_DISTANCE = 2.5f;

        [Inject] private InputSystem_Actions _input;
        [Inject] private IVariablesResolver<PlayerVariableBlockerType, Action, Action> _variables;

        private void Start()
        {
            if (NetworkClient.active)
                Bind();
        }

        private void OnDestroy()
        {
            if (NetworkClient.active)
                Expose();
        }

        private void CheckInteract(InputAction.CallbackContext callbackContext)
        {
            if (_variables.IsVariableBlocked(PlayerVariableBlockerType.INTERACT))
                return;

            var worldPos = Camera.main!.ScreenToWorldPoint(Mouse.current.position.ReadValue());
            var result = Physics2D.Raycast(worldPos, Vector2.zero, float.PositiveInfinity, interactiveLayers);

            if (!NetworkClient.localPlayer ||
                Vector2.Distance(worldPos, NetworkClient.localPlayer.transform.position) > MAX_INTERACT_DISTANCE ||
                !result || !result.collider ||
                !LayerService.CheckLayersEquality(result.collider.gameObject.layer, interactiveLayers))
                return;

            if (!result.collider.TryGetComponent(out IInteractiveObject obj))
                return;

            obj.Interact();
        }

        private void Bind() => _input.Player.Interact.performed += CheckInteract;

        private void Expose() => _input.Player.Interact.performed -= CheckInteract;
    }
}