using GameAssembly.HealthSystem;
using GameAssembly.HealthSystem.Data;
using GameAssembly.InventorySystem;
using GameAssembly.Core;
using GameAssembly.EnemySystem;
using GameAssembly.UiSystem;
using GameAssembly.Utils;
using GameAssembly.WorldSystem;
using GameAssembly.WorldSystem.Data;
using Mirror;
using UnityEngine;
using VContainer;

namespace GameAssembly.PlayerSystem
{
    [DisallowMultipleComponent]
    public class PlayerDeathAndRespawn : NetworkBehaviour
    {
        [SerializeField] private AHealthObject healthObject;
        [SerializeField] private Rigidbody2D playerBody;
        [SerializeField, Min(0f)] private float dropScatterRadius = 0.65f;
        [SerializeField, Min(0f)] private float respawnAggroCooldown = 3f;

        [Inject] private World _world;
        [Inject] private ServerInventoryManager _serverInventoryManager;

        private IInventory _inventory;
        private bool _isRespawning;

        private void Awake()
        {
            if (!healthObject)
                TryGetComponent(out healthObject);

            if (!playerBody)
                TryGetComponent(out playerBody);

            _inventory = GetComponent<IInventory>();
        }

        private void Start()
        {
            if (!isServer)
                return;

            ObjectInjector.Inject(this);
            _world ??= GameInstaller.Resolve<World>();
            _serverInventoryManager ??= GameInstaller.Resolve<ServerInventoryManager>();
            Server_Bind();
        }

        private void OnDestroy()
        {
            if (!isServer)
                return;

            Server_Expose();
        }

        [Server]
        private void Server_Bind()
        {
            if (healthObject != null)
                healthObject.OnZeroHealth += Server_OnPlayerZeroHealth;
        }

        [Server]
        private void Server_Expose()
        {
            if (healthObject != null)
                healthObject.OnZeroHealth -= Server_OnPlayerZeroHealth;
        }

        [Server]
        private void Server_OnPlayerZeroHealth()
        {
            if (_isRespawning || !healthObject)
                return;

            _isRespawning = true;

            try
            {
                var deathPosition = transform.position;
                Server_DropWholeInventory(deathPosition);
                Server_ResetEnemiesAggro();

                var respawnPosition = Server_ResolveRespawnPosition();
                Server_MovePlayerTo(respawnPosition);
                Server_RestoreHealthToFull();

                if (connectionToClient != null)
                    Target_RespawnPlayerClient(connectionToClient, respawnPosition);
            }
            finally
            {
                _isRespawning = false;
            }
        }

        [Server]
        private Vector3 Server_ResolveRespawnPosition()
        {
            if (_world == null || !_world.IsLoaded.Value)
                return transform.position;

            var spawnCell = _world.FindRandomNearestBlockByType(_world.WorldCenterXY, _world.WorldCenterXY,
                BlockType.AIR, false);
            return new Vector3(spawnCell.x + 0.5f, spawnCell.y + 0.5f, 0f);
        }

        [Server]
        private void Server_DropWholeInventory(Vector3 deathPosition)
        {
            if (_inventory == null || _serverInventoryManager == null)
                return;

            var inventorySize = _inventory.GetInventorySize();
            for (var index = 0; index < inventorySize; index++)
            {
                var item = _inventory.GetItemByIndex(index);
                if (item == null)
                    continue;

                var randomOffset = Random.insideUnitCircle * dropScatterRadius;
                var dropPosition = (Vector2)deathPosition + randomOffset;

                _serverInventoryManager.Server_DropItemFromInventory(netIdentity, index, dropPosition,
                    connectionToClient);
            }
        }

        [Server]
        private void Server_ResetEnemiesAggro()
        {
            if (!healthObject)
                return;

            var enemies = FindObjectsByType<EnemyMeleeAgent>(FindObjectsSortMode.None);
            foreach (var enemy in enemies)
            {
                if (enemy)
                    enemy.Server_ForgetTarget(healthObject, respawnAggroCooldown);
            }
        }

        [Server]
        private void Server_MovePlayerTo(Vector3 worldPosition)
        {
            transform.position = worldPosition;

            if (playerBody)
            {
                playerBody.linearVelocity = Vector2.zero;
                playerBody.angularVelocity = 0f;
            }
        }

        [Server]
        private void Server_RestoreHealthToFull()
        {
            var restoreAmount = healthObject.GetMaxHealth();
            if (restoreAmount <= 0)
                return;

            healthObject.ChangeHealth(restoreAmount, new DamageContext(gameObject, null, HealthType.UNKNOWN));
        }

        [TargetRpc]
        private void Target_RespawnPlayerClient(NetworkConnectionToClient target, Vector3 worldPosition)
        {
            UiManager.Instance?.Client_CloseAllOpenedMenus();

            transform.position = worldPosition;

            if (playerBody)
            {
                playerBody.linearVelocity = Vector2.zero;
                playerBody.angularVelocity = 0f;
            }
        }
    }
}
