using System;
using System.Collections;
using GameAssembly.PlayerSystem.Data;
using GameAssembly.PlayerSystem.Variables;
using GameAssembly.Utils.VariablesSystem;
using Mirror;
using PlayerSystem;
using UnityEngine;
using Utils;
using VContainer;

namespace GameAssembly.PlayerSystem
{
    public class PlayerMovement : NetworkBehaviour
    {
        [Inject] private PlayerDataSO _playerData;
        [Inject] private InputSystem_Actions _input;
        [Inject] private IVariablesResolver<PlayerVariableBlockerType, Action, Action> _playerVars;

        private Rigidbody2D _rb;
        private PlayerVariableBlocker _blocker;

        private void Start()
        {
            StartCoroutine(TEST());
            ObjectInjector.Inject(this);

            _blocker = new PlayerVariableBlocker(PlayerVariableBlockerType.MOVEMENT,
                PlayerVariableBlockerType.INTERACT);
            _playerVars.RegisterBlocker(_blocker);

            if (!isLocalPlayer)
                Destroy(_rb);
            else
                _rb = GetComponent<Rigidbody2D>();
        }

        private void Update()
        {
            if (!isLocalPlayer || _playerVars.IsVariableBlocked(PlayerVariableBlockerType.MOVEMENT))
                return;

            var movement = _input.Player.enabled ? _input.Player.Move.ReadValue<Vector2>() : Vector2.zero;

            _rb.linearVelocity = movement * _playerData.MoveSpeed;
        }

        private IEnumerator TEST()
        {
            yield return new WaitForSeconds(4);
            
            _blocker.Dispose();
        }
    }
}