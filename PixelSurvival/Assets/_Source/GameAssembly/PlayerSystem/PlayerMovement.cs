using GameAssembly.PlayerSystem.Data;
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

        private Rigidbody2D _rb;

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
            if (!isLocalPlayer)
                return;

            var movement = _input.Player.enabled ? _input.Player.Move.ReadValue<Vector2>() : Vector2.zero;

            _rb.linearVelocity = movement * _playerData.MoveSpeed;
        }
    }
}