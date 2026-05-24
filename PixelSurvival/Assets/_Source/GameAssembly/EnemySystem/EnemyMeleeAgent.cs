using System.Collections.Generic;
using GameAssembly.HealthSystem;
using GameAssembly.HealthSystem.Data;
using Mirror;
using Pathfinding;
using UnityEngine;

namespace GameAssembly.EnemySystem
{
    [DisallowMultipleComponent]
    public class EnemyMeleeAgent : NetworkBehaviour
    {
        [SerializeField] private AHealthObject healthObject;
        [SerializeField] private Rigidbody2D body;

        [Header("Targeting")]
        [SerializeField] private LayerMask playerLayerMask = 1 << 3;
        [SerializeField, Min(0.1f)] private float viewDistance = 10f;
        [SerializeField, Min(0.05f)] private float targetSearchInterval = 0.2f;
        [SerializeField, Min(0f)] private float loseTargetDelay = 2f;

        [Header("Movement")]
        [SerializeField, Min(0.1f)] private float moveSpeed = 2.2f;
        [SerializeField, Min(0.05f)] private float pathRefreshInterval = 0.3f;
        [SerializeField, Min(0.01f)] private float nextWaypointDistance = 0.15f;

        [Header("Attack")]
        [SerializeField, Min(0.1f)] private float attackDistance = 1.2f;
        [SerializeField, Min(1)] private int attackDamage = 6;
        [SerializeField, Min(0.05f)] private float attackCooldown = 1.1f;

        private readonly Collider2D[] _targetBuffer = new Collider2D[16];
        private readonly List<Vector3> _pathPoints = new();

        private Seeker _seeker;
        private ContactFilter2D _playerFilter;
        private AHealthObject _target;
        private int _currentPathIndex;
        private bool _pathPending;
        private float _nextTargetSearchTime;
        private float _nextPathRefreshTime;
        private float _nextAttackTime;
        private float _targetLostDeadline;
        private uint _ignoredTargetNetId;
        private float _ignoredTargetUntilTime;

        private void Awake()
        {
            if (!healthObject)
                TryGetComponent(out healthObject);

            if (!body)
                TryGetComponent(out body);

            _seeker = GetComponent<Seeker>();
            if (!_seeker)
                _seeker = gameObject.AddComponent<Seeker>();

            _playerFilter = new ContactFilter2D
            {
                useLayerMask = true,
                layerMask = playerLayerMask,
                useTriggers = true
            };
        }

        private void Start()
        {
            if (!isServer)
            {
                enabled = false;
                return;
            }

            _nextTargetSearchTime = Time.time + Random.Range(0f, targetSearchInterval);
            _nextPathRefreshTime = Time.time + Random.Range(0f, pathRefreshInterval);
            _nextAttackTime = Time.time + Random.Range(0f, attackCooldown);
        }

        private void Update()
        {
            Server_UpdateTargeting();
            Server_UpdatePathing();
            Server_UpdateAttacks();
        }

        private void FixedUpdate()
        {
            Server_MoveAlongPath();
        }

        private void OnDestroy()
        {
            if (body)
                body.linearVelocity = Vector2.zero;
        }

        [Server]
        public void Server_ForgetTarget(AHealthObject target, float ignoreSeconds)
        {
            if (!target)
                return;

            if (target.TryGetComponent<NetworkIdentity>(out var targetIdentity) && targetIdentity.netId != 0)
            {
                _ignoredTargetNetId = targetIdentity.netId;
                _ignoredTargetUntilTime = Mathf.Max(_ignoredTargetUntilTime, Time.time + Mathf.Max(0f, ignoreSeconds));
            }

            if (_target != target)
                return;

            _target = null;
            _targetLostDeadline = 0f;
            Server_ClearPath();
        }

        [Server]
        private void Server_UpdateTargeting()
        {
            if (Time.time < _nextTargetSearchTime)
                return;

            _nextTargetSearchTime = Time.time + Mathf.Max(0.01f, targetSearchInterval);

            if (Server_IsTargetAliveAndValid(_target))
            {
                var distance = Vector2.Distance(transform.position, _target.transform.position);
                if (distance <= viewDistance)
                {
                    _targetLostDeadline = 0f;
                    return;
                }

                if (_targetLostDeadline <= 0f)
                    _targetLostDeadline = Time.time + loseTargetDelay;

                if (Time.time < _targetLostDeadline)
                    return;
            }

            _target = Server_FindNearestPlayerTarget();
            _targetLostDeadline = 0f;

            if (!_target)
                Server_ClearPath();
        }

        [Server]
        private void Server_UpdatePathing()
        {
            if (!_target || !Server_IsTargetAliveAndValid(_target))
            {
                Server_ClearPath();
                return;
            }

            var targetPosition = _target.transform.position;
            var distanceToTarget = Vector2.Distance(transform.position, targetPosition);

            if (distanceToTarget <= attackDistance * 0.9f)
            {
                Server_ClearPath();
                return;
            }

            if (_pathPending || Time.time < _nextPathRefreshTime || !_seeker || !_seeker.IsDone())
                return;

            _nextPathRefreshTime = Time.time + Mathf.Max(0.01f, pathRefreshInterval);
            _pathPending = true;

            _seeker.StartPath(transform.position, targetPosition, Server_OnPathCompleted);
        }

        [Server]
        private void Server_UpdateAttacks()
        {
            if (!_target || !Server_IsTargetAliveAndValid(_target))
                return;

            if (Time.time < _nextAttackTime)
                return;

            if (Vector2.Distance(transform.position, _target.transform.position) > attackDistance)
                return;

            _target.ChangeHealth(-attackDamage, new DamageContext(gameObject, null, HealthType.ENEMY));
            _nextAttackTime = Time.time + Mathf.Max(0.01f, attackCooldown);
        }

        [Server]
        private void Server_MoveAlongPath()
        {
            if (!body)
                return;

            if (_pathPoints.Count == 0 || _currentPathIndex >= _pathPoints.Count)
            {
                body.linearVelocity = Vector2.zero;
                return;
            }

            var currentPosition = body.position;

            while (_currentPathIndex < _pathPoints.Count)
            {
                var waypoint = (Vector2)_pathPoints[_currentPathIndex];
                if (Vector2.Distance(currentPosition, waypoint) > nextWaypointDistance)
                    break;

                _currentPathIndex++;
            }

            if (_currentPathIndex >= _pathPoints.Count)
            {
                body.linearVelocity = Vector2.zero;
                return;
            }

            var direction = ((Vector2)_pathPoints[_currentPathIndex] - currentPosition).normalized;
            body.linearVelocity = direction * moveSpeed;
        }

        [Server]
        private AHealthObject Server_FindNearestPlayerTarget()
        {
            var foundCount = Physics2D.OverlapCircle((Vector2)transform.position, viewDistance, _playerFilter,
                _targetBuffer);

            AHealthObject nearestTarget = null;
            var nearestSqrDistance = float.MaxValue;

            for (var i = 0; i < foundCount; i++)
            {
                var hitCollider = _targetBuffer[i];
                if (!hitCollider)
                    continue;

                var candidate = hitCollider.GetComponentInParent<AHealthObject>();
                if (!Server_IsTargetAliveAndValid(candidate))
                    continue;

                var sqrDistance = ((Vector2)candidate.transform.position - (Vector2)transform.position).sqrMagnitude;
                if (sqrDistance >= nearestSqrDistance)
                    continue;

                nearestSqrDistance = sqrDistance;
                nearestTarget = candidate;
            }

            return nearestTarget;
        }

        [Server]
        private bool Server_IsTargetAliveAndValid(AHealthObject target)
        {
            if (!target || target.GetHealthType() != HealthType.PLAYER || target.GetHealth() <= 0)
                return false;

            if (!target.TryGetComponent<NetworkIdentity>(out var targetIdentity) || targetIdentity.netId == 0)
                return true;

            if (_ignoredTargetNetId != targetIdentity.netId)
                return true;

            if (Time.time >= _ignoredTargetUntilTime)
            {
                _ignoredTargetNetId = 0;
                _ignoredTargetUntilTime = 0f;
                return true;
            }

            return false;
        }

        [Server]
        private void Server_ClearPath()
        {
            _currentPathIndex = 0;
            _pathPoints.Clear();

            if (body)
                body.linearVelocity = Vector2.zero;
        }

        [Server]
        private void Server_OnPathCompleted(Path path)
        {
            _pathPending = false;

            if (!this || !isServer)
                return;

            if (path == null || path.error || path.vectorPath == null || path.vectorPath.Count == 0)
            {
                Server_ClearPath();
                return;
            }

            _pathPoints.Clear();
            _pathPoints.AddRange(path.vectorPath);
            _currentPathIndex = 0;
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, viewDistance);

            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, attackDistance);
        }
#endif
    }
}
