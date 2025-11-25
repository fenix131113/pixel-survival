using System;
using GameAssembly.HealthSystem;
using GameAssembly.HealthSystem.Data;
using GameAssembly.InventorySystem;
using GameAssembly.ItemsSystem;
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
    public class PlayerAttack : NetworkBehaviour // TODO: Make cooldown
    {
        [SerializeField] private int handDamage = 1;
        [SerializeField] private float baseAttackDistance = 1.5f;
        [SerializeField] private LayerMask meleeTriggerLayers;

        [Inject] private InputSystem_Actions _input;
        [Inject] private IVariablesResolver<PlayerVariableBlockerType, Action, Action> _variables;

        private IInventory _inventory;
        private PlayerSelector _selector;
        private PlayerAim _aim;

        /// <summary>
        /// Called on the server and current client. On current client first<br/>
        /// <b>Float</b> - current rotation in degrees
        /// </summary>
        public event Action<float> OnMeleeAttack;

        private void OnDestroy()
        {
            Expose(); // Server and client expose
        }

        #region Client

        public override void OnStartClient()
        {
            ObjectInjector.Inject(this);

            InitializeClientAndServer();
            _aim = GetComponent<PlayerAim>();

            Bind();
        }

        public void MeleeAttack()
        {
            CheckForBehaviour();

            OnMeleeAttack?.Invoke(_aim.LookDegrees);
            Cmd_MeleeAttack(_aim.LookDegrees);
        }

        // [ClientRpc(includeOwner = false)]
        // private void Rpc_InvokeOnMeleeAttack(float currentRotationAngle)
        // {
        //     OnMeleeAttack?.Invoke(currentRotationAngle);
        // }

        #endregion

        #region Server

        public override void OnStartServer()
        {
            ObjectInjector.Inject(this);

            InitializeClientAndServer();
        }

        [Command]
        private void Cmd_MeleeAttack(float currentRotationAngle)
        {
            if (isServerOnly)
            {
                CheckForBehaviour();
                OnMeleeAttack?.Invoke(currentRotationAngle);
            }
            
            Server_CheckForMeleeAttack(currentRotationAngle, baseAttackDistance, meleeTriggerLayers);
        }

        [Server]
        public void Server_CheckForMeleeAttack(float currentRotationAngle, float distance, int layerMask,
            int overrideDamage = -1)
        {
            var dir = new Vector2(
                Mathf.Cos(currentRotationAngle * Mathf.Deg2Rad),
                Mathf.Sin(currentRotationAngle * Mathf.Deg2Rad)
            );

            var results = new RaycastHit2D[4];
            Physics2D.RaycastNonAlloc(transform.position, dir, results, distance, layerMask);

            foreach (var h in results)
            {
                if (!h.collider || h.collider.gameObject == gameObject)
                    continue;

                if (h.collider.TryGetComponent<IHealth>(out var health))
                {
                    var damage = handDamage;

                    if (overrideDamage > -1)
                        damage = overrideDamage;

                    health.ChangeHealth(-damage,
                        new DamageContext(gameObject, _selector.GetSelectedItem(), HealthType.PLAYER));
                }

                break;
            }
        }

        #endregion

        private void InitializeClientAndServer()
        {
            _selector = GetComponent<PlayerSelector>();
            _inventory = GetComponent<IInventory>();
        }

        private void CheckForBehaviour()
        {
            if (_selector.IsSelectedItem &&
                ItemRegistry.Instance.ItemHasBehaviour(_selector.GetSelectedItem().Definition, out var behaviour))
                behaviour.OnAttack(new ItemContext(_selector.GetSelectedItem(), _inventory, netIdentity));
        }

        private void OnAttackInput(InputAction.CallbackContext callbackContext)
        {
            if(!_variables.IsVariableBlocked(PlayerVariableBlockerType.ATTACK))
                MeleeAttack();
        }

        private void Bind()
        {
            _input.Player.Attack.performed += OnAttackInput;
        }

        private void Expose()
        {
            _input.Player.Attack.performed -= OnAttackInput;
        }
    }
}