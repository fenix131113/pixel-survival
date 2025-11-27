using System;
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
        private static readonly int _isMoving = Animator.StringToHash("IsMoving");
        
        [SerializeField] private Animator anim;
        
        [Inject] private IVariablesResolver<PlayerVariableBlockerType, Action, Action> _playerVars;
        [Inject] private InputSystem_Actions _input;
        [Inject] private PlayerDataSO _playerData;

        private Rigidbody2D _rb;
        private PlayerVariableBlocker _blocker;

        private void Start()
        {
            ObjectInjector.Inject(this);

            if (!isLocalPlayer)
                Destroy(_rb);
            else
                _rb = GetComponent<Rigidbody2D>();
        }

        private void Update()
        {
            if (!isLocalPlayer || _playerVars.IsVariableBlocked(PlayerVariableBlockerType.MOVEMENT))
            {
                if(isLocalPlayer)
                    anim.SetBool(_isMoving, false);
                return;
            }

            var movement = _input.Player.enabled ? _input.Player.Move.ReadValue<Vector2>() : Vector2.zero;

            _rb.linearVelocity = movement * _playerData.MoveSpeed;
            anim.SetBool(_isMoving, movement.magnitude != 0);
        }
    }
}