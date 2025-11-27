using System;
using GameAssembly.PlayerSystem.Data;
using GameAssembly.Utils.VariablesSystem;
using Mirror;
using PlayerSystem;
using UnityEngine;
using UnityEngine.InputSystem;
using Utils;
using VContainer;

namespace GameAssembly.PlayerSystem
{
    public class PlayerAim : NetworkBehaviour
    {
        private static readonly int _x = Animator.StringToHash("X");
        private static readonly int _y = Animator.StringToHash("Y");

        [SerializeField] private Transform centerPoint;
        [SerializeField] private Animator anim;

        [Inject] private IVariablesResolver<PlayerVariableBlockerType, Action, Action> _playerVariables;
        [Inject] private InputSystem_Actions _input;

        /// <summary>
        /// Works only on client
        /// </summary>
        public float LookDegrees { get; private set; }

        private void Start()
        {
            if (!isLocalPlayer && !isServer)
            {
                Destroy(this);
                return;
            }

            ObjectInjector.Inject(this);
        }

        private void Update()
        {
            if (!isLocalPlayer)
            {
                anim.SetFloat(_x, 0);
                anim.SetFloat(_y, 0);
                return;
            }

            if (!_input.Player.enabled || _playerVariables.IsVariableBlocked(PlayerVariableBlockerType.LOOK))
                return;

            var rotVector = Camera.main!.ScreenToWorldPoint(Mouse.current.position.ReadValue()) - centerPoint.position;
            LookDegrees = Mathf.Atan2(rotVector.y, rotVector.x) * Mathf.Rad2Deg;

            var selectedSpriteKey = LookDegrees switch
            {
                > -45 and < 45 => 0,
                > 45 and < 135 => 3,
                > 135 and <= 180 or < -135 and > -180 => 2,
                > -135 and < -45 => 1
            };
            
            switch (selectedSpriteKey)
            {
                case 0:
                    anim.SetFloat(_x, 1);
                    anim.SetFloat(_y, 0);
                    break;
                case 1:
                    anim.SetFloat(_x, 0);
                    anim.SetFloat(_y, 1);
                    break;
                case 2:
                    anim.SetFloat(_x, -1);
                    anim.SetFloat(_y, 0);
                    break;
                default:
                    anim.SetFloat(_x, 0);
                    anim.SetFloat(_y, -1);
                    break;
            }
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();

            if (!centerPoint)
                centerPoint = transform;
        }
#endif
    }
}