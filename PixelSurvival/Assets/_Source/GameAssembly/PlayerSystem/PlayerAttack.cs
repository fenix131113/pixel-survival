using System;
using GameAssembly.HealthSystem;
using GameAssembly.HealthSystem.Data;
using GameAssembly.InventorySystem;
using GameAssembly.ItemsSystem;
using GameAssembly.ItemsSystem.Data;
using GameAssembly.ObjectsSystem;
using GameAssembly.PlayerSystem.Data;
using GameAssembly.PlayerSystem.Variables;
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

// ReSharper disable Unity.PerformanceCriticalCodeInvocation

namespace GameAssembly.PlayerSystem
{
    public class PlayerAttack : NetworkBehaviour
    {
        private const string DEFAULT_ATTACK_TRIGGER_NAME = "Attack";

        [SerializeField] private int handDamage = 1;
        [SerializeField] private float baseAttackDistance = 1.5f;
        [SerializeField] private float baseCooldown = 0.5f;
        [SerializeField] private float stopMovementCooldown = 0.3f;
        [SerializeField] private LayerMask meleeTriggerLayers;
        [SerializeField] private string attackTriggerName = DEFAULT_ATTACK_TRIGGER_NAME;

        [Inject] private InputSystem_Actions _input;
        [Inject] private IVariablesResolver<PlayerVariableBlockerType, Action, Action> _variables;
        [Inject] private World _world;
        [Inject] private ServerBlockDamageSystem _blockDamageSystem;

        private float _cooldown;
        private bool _isAttackHeld;
        private IInventory _inventory;
        private PlayerSelector _selector;
        private PlayerAim _aim;
        private Collider2D _playerCollider;
        private Rigidbody2D _playerRigidbody;
        private NetworkAnimator _networkAnimator;
        private PlayerVariableBlocker _attackMovementBlocker;
        private float _attackMovementBlockerTimer;

        private readonly RaycastHit2D[] _hits = new RaycastHit2D[4];

        /// <summary>
        /// Called on the server and current client. On current client first<br/>
        /// <b>Float</b> - current rotation in degrees<br/>
        /// <b>String</b> - held item attack animation key
        /// </summary>
        public event Action<float, string> OnMeleeAttack;

        private void OnDestroy()
        {
            ReleaseAttackMovementBlock();

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

            UpdateAttackMovementBlockTimer();

            if (!isLocalPlayer || !_isAttackHeld || _cooldown > 0)
                return;

            if (!_input.Player.Attack.IsPressed())
            {
                _isAttackHeld = false;
                return;
            }

            TryMeleeAttackFromInput();
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

            ApplyAttackMovementBlock();
            CheckForAttackBehaviour();
            TriggerAttackAnimation();

            var animationKey = _selector.IsSelectedItem
                ? _selector.GetSelectedItem()?.Definition?.InHandAttackAnimationKey
                : null;

            OnMeleeAttack?.Invoke(_aim.LookDegrees, animationKey);
            Cmd_MeleeAttack(_aim.LookDegrees);

            if (isClientOnly)
                _cooldown = baseCooldown;
        }

        [ClientRpc(includeOwner = false)]
        private void Rpc_InvokeOnMeleeAttack(float currentRotationAngle, string animationKey)
        {
            OnMeleeAttack?.Invoke(currentRotationAngle, animationKey);
        }

        #endregion

        #region Server

        [Command]
        private void Cmd_AimPerform()
        {
            CheckForAimBehaviour();
        }

        [Command]
        private void Cmd_MeleeAttack(float lookDegrees)
        {
            if (_cooldown > 0)
                return;

            var selectedItem = _selector.IsSelectedItem ? _selector.GetSelectedItem() : null;
            var animationKey = selectedItem?.Definition?.InHandAttackAnimationKey;

            if (isServerOnly)
            {
                CheckForAttackBehaviour();
                TriggerAttackAnimation();
                OnMeleeAttack?.Invoke(lookDegrees, animationKey);
            }

            Rpc_InvokeOnMeleeAttack(lookDegrees, animationKey);
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
            var attackOrigin = transform.position + new Vector3(_playerCollider.offset.x, _playerCollider.offset.y, 0);
            var damage = overrideDamage > -1 ? overrideDamage : handDamage;
            var attackerNetId = netIdentity ? netIdentity.netId : 0u;
            var selectedItem = _selector.IsSelectedItem ? _selector.GetSelectedItem() : null;
            var selectedToolDefinition = selectedItem?.Definition as ToolItemDefinitionSO;

            for (var index = 0; index < _hits.Length; index++)
                _hits[index] = default;

            Physics2D.RaycastNonAlloc(attackOrigin, dir, _hits, distance, layerMask);

            var hasResolvedTarget = false;

            foreach (var h in _hits)
            {
                if (h == false || !h.collider || h.collider.gameObject == gameObject)
                    continue;

                if (h.collider.TryGetComponent<IHealth>(out var health)) // Attacking objects & mobs
                {
                    hasResolvedTarget = true;
                    health.ChangeHealth(-damage,
                        new DamageContext(gameObject, selectedItem, HealthType.PLAYER));
                    _blockDamageSystem.Server_ClearPlayerTarget(attackerNetId);
                }
                else if (h.collider.gameObject.GetComponentInAnyParent<ChunkRenderer>(out var chunkVisual))
                {
                    var hitPoint = h.point + dir * 0.01f;
                    var blockIndexes = chunkVisual.UpperTilemap.WorldToCell(hitPoint);

                    if (blockIndexes.x < 0 || blockIndexes.x >= Chunk.CHUNK_SIZE ||
                        blockIndexes.y < 0 || blockIndexes.y >= Chunk.CHUNK_SIZE)
                        continue;

                    hasResolvedTarget = true;

                    var cell = chunkVisual.Chunk.GetCell(blockIndexes.x, blockIndexes.y);

                    if (cell.Block.type != BlockType.AIR && cell.Block.IsBreakable)
                    {
                        var blockDamage =
                            ResolveBlockMiningDamage(cell.Block.definition, selectedToolDefinition, damage);
                        if (blockDamage <= 0)
                        {
                            _blockDamageSystem.Server_ClearPlayerTarget(attackerNetId);
                            break;
                        }

                        var blockWorldPos = new Vector2Int(
                            chunkVisual.Chunk.Coord.X * Chunk.CHUNK_SIZE + blockIndexes.x,
                            chunkVisual.Chunk.Coord.Y * Chunk.CHUNK_SIZE + blockIndexes.y);

                        var breakResult =
                            _blockDamageSystem.Server_ApplyPlayerDamage(_world, attackerNetId, blockWorldPos,
                                blockDamage);

                        if (breakResult.IsBlockBroken)
                        {
                            if (cell.Block.definition.DropItem)
                            {
                                ServerItemSpawner.Server_SpawnItem(
                                    chunkVisual.UpperTilemap.GetCellCenterWorld(blockIndexes),
                                    cell.Block.definition.DropItem,
                                    cell.Block.definition.RandomizeDropAmount());
                            }

                            chunkVisual.Chunk.SetBlock(blockIndexes.x, blockIndexes.y, false, BlockData.Air);
                        }
                    }
                    else
                    {
                        TryBreakPlacedFloor(chunkVisual.Chunk, new Vector2Int(blockIndexes.x, blockIndexes.y),
                            selectedToolDefinition, damage);
                        _blockDamageSystem.Server_ClearPlayerTarget(attackerNetId);
                    }
                }
                else
                {
                    hasResolvedTarget = true;
                    _blockDamageSystem.Server_ClearPlayerTarget(attackerNetId);
                }

                break;
            }

            if (hasResolvedTarget)
                return;

            TryBreakPlacedFloorAtRayEnd(attackOrigin, dir, distance, selectedToolDefinition, damage);
            _blockDamageSystem.Server_ClearPlayerTarget(attackerNetId);
        }

        #endregion

        private static int ResolveBlockMiningDamage(BlockDefinitionSO blockDefinition,
            ToolItemDefinitionSO selectedToolDefinition, int baseDamage)
        {
            if (!blockDefinition || !blockDefinition.CanTakeDamageFrom(selectedToolDefinition))
                return 0;

            if (blockDefinition.IsEffectiveTool(selectedToolDefinition))
                return Mathf.Max(1, selectedToolDefinition.MiningDamage);

            return Mathf.Max(1, baseDamage);
        }

        [Server]
        private bool TryBreakPlacedFloorAtRayEnd(Vector3 attackOrigin, Vector2 direction, float attackDistance,
            ToolItemDefinitionSO selectedToolDefinition, int baseDamage)
        {
            if (!CanBreakPlacedFloor(selectedToolDefinition))
                return false;

            var targetPos = attackOrigin + (Vector3)(direction * attackDistance);
            var targetWorldPos = new Vector2Int(Mathf.FloorToInt(targetPos.x), Mathf.FloorToInt(targetPos.y));

            if (!World.IsWorldPositionInsideBounds(targetWorldPos.x, targetWorldPos.y))
                return false;

            var chunk = _world.GetChunkByWorldPosition(targetWorldPos.x, targetWorldPos.y);
            if (chunk == null)
                return false;

            var localIndexes = World.ConvertWorldToChunkSpace(targetWorldPos.x, targetWorldPos.y);
            return TryBreakPlacedFloor(chunk, localIndexes, selectedToolDefinition, baseDamage);
        }

        [Server]
        private bool TryBreakPlacedFloor(Chunk chunk, Vector2Int localIndexes,
            ToolItemDefinitionSO selectedToolDefinition,
            int baseDamage)
        {
            if (chunk == null || !CanBreakPlacedFloor(selectedToolDefinition))
                return false;

            if (localIndexes.x < 0 || localIndexes.x >= Chunk.CHUNK_SIZE ||
                localIndexes.y < 0 || localIndexes.y >= Chunk.CHUNK_SIZE)
                return false;

            var cell = chunk.GetCell(localIndexes.x, localIndexes.y);
            if (cell.Block.type != BlockType.AIR || cell.Floor.Equals(cell.BaseFloor))
                return false;

            var floor = cell.Floor;
            if (floor.type == BlockType.AIR || !floor.IsBreakable || !floor.definition)
                return false;

            var floorDamage = ResolveBlockMiningDamage(floor.definition, selectedToolDefinition, baseDamage);
            if (floorDamage <= 0)
                return false;

            if (floor.definition.DropItem)
            {
                var floorCenter = new Vector3(
                    chunk.Coord.X * Chunk.CHUNK_SIZE + localIndexes.x + 0.5f,
                    chunk.Coord.Y * Chunk.CHUNK_SIZE + localIndexes.y + 0.5f,
                    0f);
                ServerItemSpawner.Server_SpawnItem(floorCenter, floor.definition.DropItem,
                    floor.definition.RandomizeDropAmount());
            }

            chunk.SetBlock(localIndexes.x, localIndexes.y, true, cell.BaseFloor);
            return true;
        }

        private static bool CanBreakPlacedFloor(ToolItemDefinitionSO selectedToolDefinition)
        {
            return selectedToolDefinition && selectedToolDefinition.ToolType == ToolType.SHOVEL;
        }

        private void InitializeClientAndServer()
        {
            _selector = GetComponent<PlayerSelector>();
            _inventory = GetComponent<IInventory>();
            _networkAnimator = GetComponent<NetworkAnimator>();
            _playerRigidbody = GetComponent<Rigidbody2D>();
        }

        private void TriggerAttackAnimation()
        {
            if (!_networkAnimator)
                return;

            var triggerName = string.IsNullOrWhiteSpace(attackTriggerName)
                ? DEFAULT_ATTACK_TRIGGER_NAME
                : attackTriggerName;

            _networkAnimator.SetTrigger(triggerName);
        }

        private void CheckForAttackBehaviour()
        {
            if (_selector.IsSelectedItem &&
                ItemRegistry.Instance.ItemHasBehaviour(_selector.GetSelectedItem().Definition, out var behaviour))
                behaviour.OnAttack(new ItemContext(_selector.GetSelectedItem(), _inventory, netIdentity));
        }

        private void CheckForAimBehaviour()
        {
            if (_selector.IsSelectedItem &&
                ItemRegistry.Instance.ItemHasBehaviour(_selector.GetSelectedItem().Definition, out var behaviour))
                behaviour.OnAim(new ItemContext(_selector.GetSelectedItem(), _inventory, netIdentity));
        }

        private void OnAttackStarted(InputAction.CallbackContext callbackContext)
        {
            _isAttackHeld = true;
        }

        private void OnAttackPerformed(InputAction.CallbackContext callbackContext)
        {
            TryMeleeAttackFromInput();
        }

        private void OnAttackCanceled(InputAction.CallbackContext callbackContext)
        {
            _isAttackHeld = false;
        }

        private void TryMeleeAttackFromInput()
        {
            if (!_input.Player.enabled || _variables.IsVariableBlocked(PlayerVariableBlockerType.ATTACK))
                return;

            MeleeAttack();
        }

        private void OnAimPerformed(InputAction.CallbackContext callbackContext)
        {
            if (!_input.Player.enabled || _variables.IsVariableBlocked(PlayerVariableBlockerType.INTERACT))
                return;

            CheckForAimBehaviour();
            Cmd_AimPerform();
        }

        private void Bind()
        {
            _input.Player.Attack.started += OnAttackStarted;
            _input.Player.Attack.performed += OnAttackPerformed;
            _input.Player.Attack.canceled += OnAttackCanceled;
            _input.Player.Aim.performed += OnAimPerformed;
        }

        private void Expose()
        {
            _input.Player.Attack.started -= OnAttackStarted;
            _input.Player.Attack.performed -= OnAttackPerformed;
            _input.Player.Attack.canceled -= OnAttackCanceled;
            _input.Player.Aim.performed -= OnAimPerformed;
            _isAttackHeld = false;
            ReleaseAttackMovementBlock();
        }

        private void ApplyAttackMovementBlock()
        {
            if (!isLocalPlayer)
                return;

            _attackMovementBlockerTimer = stopMovementCooldown;

            if (_attackMovementBlocker == null)
            {
                _attackMovementBlocker = new PlayerVariableBlocker(PlayerVariableBlockerType.MOVEMENT);
                _variables.RegisterBlocker(_attackMovementBlocker);
            }

            if (_playerRigidbody)
                _playerRigidbody.linearVelocity = Vector2.zero;
        }

        private void UpdateAttackMovementBlockTimer()
        {
            if (!isLocalPlayer || _attackMovementBlocker == null)
                return;

            _attackMovementBlockerTimer -= Time.deltaTime;

            if (_attackMovementBlockerTimer <= 0)
                ReleaseAttackMovementBlock();
        }

        private void ReleaseAttackMovementBlock()
        {
            if (_attackMovementBlocker == null)
                return;

            _attackMovementBlocker.Dispose();
            _attackMovementBlocker = null;
            _attackMovementBlockerTimer = 0;
        }
    }
}