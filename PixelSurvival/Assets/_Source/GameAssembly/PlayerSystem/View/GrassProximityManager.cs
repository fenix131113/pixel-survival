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
        [SerializeField] private float movementThreshold = 0.15f;
        [SerializeField] private float impactScale = 0.03f;
        [SerializeField] private float maxImpactForce = 0.14f;
        [SerializeField] private float returnSpeed = 3.5f;
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
            public SpriteRenderer TargetRenderer;
            public bool IsTouching;
        }

        private void Awake()
        {
            if (!playerBody)
                playerBody = GetComponent<Rigidbody2D>();

            if (!interactionZone)
            {
                var colliders = GetComponents<Collider2D>();
                foreach (var candidate in colliders)
                {
                    if (!candidate || !candidate.isTrigger)
                        continue;

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

        private void OnDisable()
        {
            ResetAllGrass();
        }

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
                if (!other)
                    continue;

                var marker = ResolveMarker(other);
                if (!marker || !marker.TargetRenderer)
                    continue;

                if (!_activeGrass.TryGetValue(marker, out var state))
                {
                    state = new GrassState
                    {
                        TargetRenderer = marker.TargetRenderer
                    };
                    _activeGrass.Add(marker, state);
                }

                var speed = playerBody.linearVelocity.magnitude;
                if (!state.IsTouching && speed >= movementThreshold)
                {
                    var direction = Mathf.Abs(playerBody.linearVelocity.x) > 0.01f
                        ? Mathf.Sign(playerBody.linearVelocity.x)
                        : Mathf.Sign(state.TargetRenderer.transform.position.x - transform.position.x);

                    if (Mathf.Approximately(direction, 0f))
                        direction = 1f;

                    state.CurrentImpact = direction * Mathf.Min(speed * impactScale, maxImpactForce);
                }

                state.IsTouching = true;
            }
        }

        private void UpdateActiveGrass()
        {
            _toRemove.Clear();

            foreach (var pair in _activeGrass)
            {
                var marker = pair.Key;
                var state = pair.Value;
                if (!state.TargetRenderer)
                {
                    _toRemove.Add(marker);
                    continue;
                }

                state.CurrentImpact = Mathf.MoveTowards(state.CurrentImpact, 0f, Time.deltaTime * returnSpeed);
                ApplyImpact(state.TargetRenderer, state.CurrentImpact);

                if (!state.IsTouching && Mathf.Approximately(state.CurrentImpact, 0f))
                    _toRemove.Add(marker);
                else
                    state.IsTouching = false;
            }

            foreach (var marker in _toRemove)
            {
                if (!_activeGrass.TryGetValue(marker, out var state))
                    continue;

                ClearImpact(state.TargetRenderer);
                _activeGrass.Remove(marker);
            }
        }

        private static GrassReactiveMarker ResolveMarker(Collider2D other)
        {
            if (!other)
                return null;

            return other.GetComponent<GrassReactiveMarker>() ? other.GetComponent<GrassReactiveMarker>() : other.GetComponentInParent<GrassReactiveMarker>();
        }

        private void ApplyImpact(SpriteRenderer targetRenderer, float impactForce)
        {
            targetRenderer.GetPropertyBlock(_propertyBlock);
            _propertyBlock.SetFloat(ImpactForceId, impactForce);
            targetRenderer.SetPropertyBlock(_propertyBlock);
        }

        private void ClearImpact(SpriteRenderer targetRenderer)
        {
            if (!targetRenderer)
                return;

            targetRenderer.GetPropertyBlock(_propertyBlock);
            _propertyBlock.SetFloat(ImpactForceId, 0f);
            targetRenderer.SetPropertyBlock(_propertyBlock);
        }

        private void ResetAllGrass()
        {
            if (_propertyBlock == null)
                return;

            foreach (var pair in _activeGrass)
                ClearImpact(pair.Value.TargetRenderer);

            _activeGrass.Clear();
            _toRemove.Clear();
        }
    }
}
