using System;
using GameAssembly.ItemsSystem.Data;
using GameAssembly.PlayerSystem;
using GameAssembly.PlayerSystem.Data;
using GameAssembly.Utils;
using GameAssembly.Utils.VariablesSystem;
using Mirror;
using PlayerSystem;
using UnityEngine;
using UnityEngine.InputSystem;
using VContainer;

namespace GameAssembly.BuildSystem
{
    public class PlayerBuild : NetworkBehaviour
    {
        [Inject] private IVariablesResolver<PlayerVariableBlockerType, Action, Action> _playerVariables;
        [Inject] private InputSystem_Actions _input;
        [Inject] private ServerBuild _serverBuild;

        private PlayerSelector _selector;
        private bool _isInBuildMode;

        private void Start()
        {
            ObjectInjector.Inject(this);
            
            if (!NetworkClient.active)
                return;

            if (!isLocalPlayer)
            {
                enabled = false;
                return;
            }

            _selector = NetworkClient.localPlayer.GetComponent<PlayerSelector>();
            Bind();
        }

        private void OnDestroy()
        {
            if (!NetworkClient.active || !isLocalPlayer)
                return;

            Expose();
        }

        private void CheckCurrentItem()
        {
            if (!_selector.IsSelectedItem || (_selector.GetSelectedItem().Definition is not BlockItemDefinitionSO &&
                _selector.GetSelectedItem().Definition is not PlaceableObjectItemDefinitionSO))
            {
                DeactivateBuildMode();
                return;
            }

            ActivateBuildMode();
        }

        private void ActivateBuildMode()
        {
            _isInBuildMode = true;
        }

        private void DeactivateBuildMode()
        {
            _isInBuildMode = false;
        }

        private void OnPlaceBuildClicked(InputAction.CallbackContext obj)
        {
            if (!_isInBuildMode || _playerVariables.IsVariableBlocked(PlayerVariableBlockerType.BUILD))
                return;

            var worldPos = Camera.main!.ScreenToWorldPoint(Mouse.current.position.ReadValue());
            var coords = new Vector2Int(Mathf.FloorToInt(worldPos.x), Mathf.FloorToInt(worldPos.y));

            Cmd_PlaceBlock(coords);
        }

        [Command]
        private void Cmd_PlaceBlock(Vector2Int blockWorldPos, NetworkConnectionToClient sender = null)
        {
            _serverBuild.Server_PlaceBlock(blockWorldPos, sender);
        }

        private void Bind()
        {
            _selector.OnSelectedItemChanged += CheckCurrentItem;
            _input.Player.Aim.performed += OnPlaceBuildClicked;
        }

        private void Expose()
        {
            _selector.OnSelectedItemChanged -= CheckCurrentItem;
            _input.Player.Aim.performed -= OnPlaceBuildClicked;
        }
    }
}