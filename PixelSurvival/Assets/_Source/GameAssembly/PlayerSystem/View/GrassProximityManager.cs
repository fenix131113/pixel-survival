using System.Collections.Generic;
using GameAssembly.ObjectsSystem.View;
using Mirror;
using UnityEngine;

namespace GameAssembly.PlayerSystem.View
{
    [DisallowMultipleComponent]
    public class GrassProximityManager : MonoBehaviour
    {
        private static readonly int ImpactForceId = Shader.PropertyToID("_ImpactForce");

        [SerializeField] private Rigidbody2D playerBody;
        [SerializeField] private Collider2D interactionZone;
        [SerializeField] private LayerMask grassLayers;
        
        [Header("Spring Setting")]
        [Tooltip("Min Player Speed")]
        [SerializeField] private float movementThreshold = 0.15f;
        [Tooltip("Sensitivity")]
        [SerializeField] private float impactScale = 0.5f; 
        [Tooltip("Max Slant")]
        [SerializeField] private float maxImpactForce = 0.2f;
        [Tooltip("The power of return")]
        [SerializeField] private float stiffness = 100f;
        [Tooltip("Fade out")]
        [SerializeField] private float damping = 10f;

        [SerializeField] private int overlapBufferSize = 24;

        private readonly Dictionary<GrassReactiveMarker, GrassState> _activeGrass = new();
        private NetworkIdentity _networkIdentity;
        private MaterialPropertyBlock _propertyBlock;
        private readonly List<GrassReactiveMarker> _toRemove = new();
        private Collider2D[] _overlapResults;
        private ContactFilter2D _grassFilter;

        private sealed class GrassState
        {
            public float CurrentImpact;
            public float Velocity; // Добавили скорость для физики пружины
            public SpriteRenderer TargetRenderer;
            public bool IsTouching;
        }

        private void Awake()
        {
            if (!playerBody) playerBody = GetComponent<Rigidbody2D>();

            // (Твой код поиска interactionZone остается прежним...)
            if (!interactionZone)
            {
                var colliders = GetComponents<Collider2D>();
                foreach (var candidate in colliders)
                {
                    if (!candidate || !candidate.isTrigger) continue;
                    interactionZone = candidate;
                    break;
                }
            }

            _networkIdentity = GetComponent<NetworkIdentity>();
            _propertyBlock = new MaterialPropertyBlock();
            _overlapResults = new Collider2D[Mathf.Max(8, overlapBufferSize)];
            _grassFilter = new ContactFilter2D
            {
                useLayerMask = true,
                layerMask = grassLayers,
                useTriggers = true
            };
        }

        private void OnDisable() => ResetAllGrass();

        private void Update()
        {
            if ((_networkIdentity && !_networkIdentity.isLocalPlayer) || !playerBody || !interactionZone)
                return;

            RefreshTouchedGrass();
            UpdateActiveGrass();
        }

        private void RefreshTouchedGrass()
        {
            var overlapCount = interactionZone.Overlap(_grassFilter, _overlapResults);
            for (var i = 0; i < overlapCount; i++)
            {
                var other = _overlapResults[i];
                if (!other) continue;

                var marker = ResolveMarker(other);
                if (!marker || !marker.TargetRenderer) continue;

                if (!_activeGrass.TryGetValue(marker, out var state))
                {
                    state = new GrassState { TargetRenderer = marker.TargetRenderer };
                    _activeGrass.Add(marker, state);
                }

                var speed = playerBody.linearVelocity.magnitude;
                
                // Вместо мгновенного приравнивания значения, добавляем ВЕЛОСИТИ (импульс)
                if (!state.IsTouching && speed >= movementThreshold)
                {
                    var direction = Mathf.Abs(playerBody.linearVelocity.x) > 0.01f
                        ? Mathf.Sign(playerBody.linearVelocity.x)
                        : Mathf.Sign(state.TargetRenderer.transform.position.x - transform.position.x);

                    // Даем толчок. Мы не меняем ImpactForce напрямую, а "толкаем" скорость.
                    state.Velocity += direction * speed * impactScale;
                }

                state.IsTouching = true;
            }
        }

        private void UpdateActiveGrass()
        {
            _toRemove.Clear();
            float dt = Time.deltaTime;

            foreach (var pair in _activeGrass)
            {
                var marker = pair.Key;
                var state = pair.Value;

                if (!state.TargetRenderer)
                {
                    _toRemove.Add(marker);
                    continue;
                }

                // --- ФИЗИКА ПРУЖИНЫ ---
                // Сила возврата (зависит от текущего отклонения)
                float force = -stiffness * state.CurrentImpact;
                // Затухание (сопротивление воздуха/внутреннее трение)
                force -= damping * state.Velocity;

                // Обновляем скорость и позицию (наклон)
                state.Velocity += force * dt;
                state.CurrentImpact += state.Velocity * dt;

                // Ограничиваем наклон, чтобы трава не ложилась на землю слишком сильно
                state.CurrentImpact = Mathf.Clamp(state.CurrentImpact, -maxImpactForce, maxImpactForce);

                ApplyImpact(state.TargetRenderer, state.CurrentImpact);

                // Если пружина почти замерла и игрок не касается, удаляем из активных
                if (!state.IsTouching && Mathf.Abs(state.CurrentImpact) < 0.001f && Mathf.Abs(state.Velocity) < 0.001f)
                    _toRemove.Add(marker);
                else
                    state.IsTouching = false;
            }

            foreach (var marker in _toRemove)
            {
                if (_activeGrass.TryGetValue(marker, out var state))
                {
                    ClearImpact(state.TargetRenderer);
                    _activeGrass.Remove(marker);
                }
            }
        }

        // Остальные методы (ApplyImpact, ClearImpact, ResolveMarker) без изменений...
        private void ApplyImpact(SpriteRenderer targetRenderer, float impactForce)
        {
            targetRenderer.GetPropertyBlock(_propertyBlock);
            _propertyBlock.SetFloat(ImpactForceId, impactForce);
            targetRenderer.SetPropertyBlock(_propertyBlock);
        }

        private void ClearImpact(SpriteRenderer targetRenderer)
        {
            if (!targetRenderer) return;
            targetRenderer.GetPropertyBlock(_propertyBlock);
            _propertyBlock.SetFloat(ImpactForceId, 0f);
            targetRenderer.SetPropertyBlock(_propertyBlock);
        }

        private static GrassReactiveMarker ResolveMarker(Collider2D other) =>
            other.GetComponent<GrassReactiveMarker>() ?? other.GetComponentInParent<GrassReactiveMarker>();

        private void ResetAllGrass()
        {
            if (_propertyBlock == null) return;
            foreach (var pair in _activeGrass) ClearImpact(pair.Value.TargetRenderer);
            _activeGrass.Clear();
            _toRemove.Clear();
        }
    }
}