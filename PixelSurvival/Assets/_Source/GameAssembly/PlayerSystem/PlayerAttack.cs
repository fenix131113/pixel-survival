using System;
using GameAssembly.HealthSystem;
using GameAssembly.HealthSystem.Data;
using GameAssembly.InventorySystem;
using GameAssembly.ItemsSystem;
using GameAssembly.PlayerSystem.Data;
using GameAssembly.Utils.VariablesSystem;
using GameAssembly.WorldSystem;
using GameAssembly.WorldSystem.View;
using Mirror;
using PlayerSystem;
using UnityEngine;
using UnityEngine.InputSystem;
using Utils;
using VContainer;

namespace GameAssembly.PlayerSystem
{
    public class PlayerAttack : NetworkBehaviour
    {
        [SerializeField] private int handDamage = 1;
        [SerializeField] private float baseAttackDistance = 1.5f;
        [SerializeField] private float baseCooldown = 0.5f;
        [SerializeField] private LayerMask meleeTriggerLayers;

        [Inject] private InputSystem_Actions _input;
        [Inject] private IVariablesResolver<PlayerVariableBlockerType, Action, Action> _variables;

        private float _cooldown;
        private IInventory _inventory;
        private PlayerSelector _selector;
        private PlayerAim _aim;
        
        private readonly RaycastHit2D[] _hits = new RaycastHit2D[4];

        /// <summary>
        /// Called on the server and current client. On current client first<br/>
        /// <b>Float</b> - current rotation in degrees
        /// </summary>
        public event Action<float> OnMeleeAttack;

        private void OnDestroy()
        {
            if(isLocalPlayer)
                Expose(); // Client expose
        }

        private void Start()
        {
            if(isClientOnly)
                return;
            
            ObjectInjector.Inject(this);

            _aim = GetComponent<PlayerAim>();
            InitializeClientAndServer();
        }

        private void Update()
        {
            if (_cooldown > 0)
                _cooldown -= Time.deltaTime;
        }

        #region Client

        public override void OnStartClient()
        {
            ObjectInjector.Inject(this);

            _aim = GetComponent<PlayerAim>();
            InitializeClientAndServer();
            
            if(isLocalPlayer)
                Bind();
        }

        public void MeleeAttack()
        {
            if(_cooldown > 0)
                return;
            
            CheckForBehaviour();

            OnMeleeAttack?.Invoke(_aim.LookDegrees);
            Cmd_MeleeAttack(_aim.LookDegrees);
            
            if(isClientOnly)
                _cooldown = baseCooldown;
        }

        // [ClientRpc(includeOwner = false)]
        // private void Rpc_InvokeOnMeleeAttack(float currentRotationAngle)
        // {
        //     OnMeleeAttack?.Invoke(currentRotationAngle);
        // }

        #endregion

        #region Server

        [Command]
        private void Cmd_MeleeAttack(float lookDegrees)
        {
            if(_cooldown > 0)
                return;
            
            if (isServerOnly)
            {
                CheckForBehaviour();
                OnMeleeAttack?.Invoke(lookDegrees);
            }
            
            Server_CheckForMeleeAttack(lookDegrees, baseAttackDistance, meleeTriggerLayers);
            _cooldown = baseCooldown;
        }

        [Server]
        public void Server_CheckForMeleeAttack(float currentRotationAngle, float distance, int layerMask,
            int overrideDamage = -1)
        {
            var dir = new Vector2(
                Mathf.Cos(currentRotationAngle * Mathf.Deg2Rad),
                Mathf.Sin(currentRotationAngle * Mathf.Deg2Rad)
            );

            for (var index = 0; index < _hits.Length; index++)
                _hits[index] = default;

            Physics2D.RaycastNonAlloc(transform.position, dir, _hits, distance, layerMask);

            foreach (var h in _hits)
            {
                if (h == default || !h.collider || h.collider.gameObject == gameObject)
                    continue;

                if (h.collider.TryGetComponent<IHealth>(out var health)) // Attacking objects & mobs
                {
                    var damage = handDamage;

                    if (overrideDamage > -1)
                        damage = overrideDamage;

                    health.ChangeHealth(-damage,
                        new DamageContext(gameObject, _selector.GetSelectedItem(), HealthType.PLAYER));
                }
                else if(h.collider.TryGetComponent<ChunkRenderer>(out var chunkVisual)) // Breaking world(chunk) blocks
                {
                    
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