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
        [SerializeField] private Transform centerPoint;

        [Inject] private IVariablesResolver<PlayerVariableBlockerType, Action, Action> _playerVariables;
        [Inject] private InputSystem_Actions _input;
        
        /// <summary>
        /// Works only on client
        /// </summary>
        public float LookDegrees { get; private set; }

        private void Start()
        {
            if(!isLocalPlayer)
            {
                Destroy(this);
                return;
            }

            ObjectInjector.Inject(this);
        }

        private void Update()
        {
            if(!_input.Player.enabled || _playerVariables.IsVariableBlocked(PlayerVariableBlockerType.LOOK))
                return;
            
            var rotVector = Camera.main!.ScreenToWorldPoint(Mouse.current.position.ReadValue()) - centerPoint.position;
            LookDegrees = Mathf.Atan2(rotVector.y, rotVector.x) * Mathf.Rad2Deg;
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            
            if(!centerPoint)
                centerPoint = transform;
        }
#endif
    }
}