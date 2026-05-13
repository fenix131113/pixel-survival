using System;
using GameAssembly.PlayerSystem.Data;
using GameAssembly.Utils;
using GameAssembly.Utils.VariablesSystem;
using Mirror;
using PlayerSystem;
using UnityEngine;
using UnityEngine.InputSystem;
using VContainer;

// ReSharper disable Unity.PerformanceCriticalCodeInvocation

namespace GameAssembly.ObjectsSystem.InteractiveSystem
{
    public class InteractRecorder : MonoBehaviour
    {
        [SerializeField] private LayerMask interactiveLayers;
        [SerializeField] private Material outlineMaterial;
        [SerializeField] private GameObject clickHelp;
        [SerializeField] private Vector2 offset;

        private const float MAX_INTERACT_DISTANCE = 2.5f;

        [Inject] private InputSystem_Actions _input;
        [Inject] private IVariablesResolver<PlayerVariableBlockerType, Action, Action> _variables;

        private Camera _camera;
        private Material _lastMaterial;
        private Renderer _lastRenderer;

        private void Start()
        {
            if (!NetworkClient.active)
                return;

            _camera = Camera.main;
            Bind();
        }

        private void Update()
        {
            if (!_camera || Mouse.current == null || !NetworkClient.active)
                return;

            if (_camera)
                CheckForOutline();
        }

        private void OnDestroy()
        {
            if (NetworkClient.active)
                Expose();
        }

        private void CheckForOutline()
        {
            if (!NetworkClient.localPlayer)
            {
                clickHelp.SetActive(false);
                return;
            }

            var worldPos = _camera!.ScreenToWorldPoint(Mouse.current.position.ReadValue());

            if (Vector2.Distance(worldPos, NetworkClient.localPlayer.transform.position) > MAX_INTERACT_DISTANCE)
            {
                clickHelp.SetActive(false);
                DisposeLastRenderer();
                return;
            }

            var result = Physics2D.Raycast(worldPos, Vector2.zero, float.PositiveInfinity, interactiveLayers);

            if (!result || !result.collider.TryGetComponent(out IInteractiveObject interactive))
            {
                clickHelp.SetActive(false);
                DisposeLastRenderer();
                return;
            }

            DisposeLastRenderer();
            _lastRenderer = interactive.GetRendererTarget();
            _lastMaterial = interactive.GetRendererTarget().material;

            if (outlineMaterial)
                interactive.GetRendererTarget().material = outlineMaterial;

            clickHelp.SetActive(true);
            clickHelp.transform.position = (Vector2)worldPos + offset;
        }

        private void DisposeLastRenderer()
        {
            if (!_lastRenderer)
                return;

            _lastRenderer.material = _lastMaterial;
            _lastMaterial = null;
            _lastRenderer = null;
        }

        private void CheckInteract(InputAction.CallbackContext callbackContext)
        {
            if (_variables.IsVariableBlocked(PlayerVariableBlockerType.INTERACT))
                return;

            var worldPos = _camera.ScreenToWorldPoint(Mouse.current.position.ReadValue());
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