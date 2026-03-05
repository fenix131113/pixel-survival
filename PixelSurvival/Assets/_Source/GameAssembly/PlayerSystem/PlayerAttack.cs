using System;
using GameAssembly.HealthSystem;
using GameAssembly.HealthSystem.Data;
using GameAssembly.InventorySystem;
using GameAssembly.ItemsSystem;
using GameAssembly.ObjectsSystem;
using GameAssembly.PlayerSystem.Data;
using GameAssembly.Utils;
using GameAssembly.Utils.Extensions;
using GameAssembly.Utils.VariablesSystem;
using GameAssembly.WorldSystem;
using GameAssembly.WorldSystem.Data;
using GameAssembly.WorldSystem.View;
using Mirror;
using PlayerSystem;
using UnityEngine;
using UnityEngine.InputSystem;
using VContainer;

namespace GameAssembly.PlayerSystem
{
    public class PlayerAttack : NetworkBehaviour //TODO: make attack block when inventory or other panels opened
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
        private Collider2D _playerCollider;

        private readonly RaycastHit2D[] _hits = new RaycastHit2D[4];

        /// <summary>
        /// Called on the server and current client. On current client first<br/>
        /// <b>Float</b> - current rotation in degrees
        /// </summary>
        public event Action<float> OnMeleeAttack;

        private void OnDestroy()
        {
            if (isLocalPlayer)
                Expose(); // Client expose
        }

        private void Start()
        {
            _playerCollider = GetComponent<Collider2D>();

            if (isClientOnly)
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

            if (isLocalPlayer)
                Bind();
        }

        public void MeleeAttack()
        {
            if (_cooldown > 0)
                return;

            CheckForBehaviour();

            OnMeleeAttack?.Invoke(_aim.LookDegrees);
            Cmd_MeleeAttack(_aim.LookDegrees);

            if (isClientOnly)
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
            if (_cooldown > 0)
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

            Physics2D.RaycastNonAlloc(
                transform.position + new Vector3(_playerCollider.offset.x, _playerCollider.offset.y, 0), dir, _hits,
                distance, layerMask);

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
                else if (h.collider.gameObject
                         .GetComponentInAnyParent<ChunkRenderer>(out var chunkVisual)) // Breaking world(chunk) blocks. TODO: maybe move break logic to another script
                {// TODO: Add placed floor breaking and separate it's logic
                    var hitPoint = h.point + dir * 0.01f;
                    var blockIndexes = chunkVisual.Tilemap.WorldToCell(hitPoint);

                    if (blockIndexes.x < 0 || blockIndexes.x >= Chunk.CHUNK_SIZE ||
                        blockIndexes.y < 0 || blockIndexes.y >= Chunk.CHUNK_SIZE)
                        continue;

                    var cell = chunkVisual.Chunk.GetCell(blockIndexes.x, blockIndexes.y);

                    if (cell.Block.type != BlockType.AIR && cell.Block.IsBreakable)
                    {
                        ServerItemSpawner.Server_SpawnItem(chunkVisual.Tilemap.GetCellCenterWorld(blockIndexes), cell.Block.definition.DropItem, cell.Block.definition.RandomizeDropAmount());
                        chunkVisual.Chunk.SetBlock(blockIndexes.x, blockIndexes.y, false, BlockData.Air);
                    }
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
            if (!_variables.IsVariableBlocked(PlayerVariableBlockerType.ATTACK))
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