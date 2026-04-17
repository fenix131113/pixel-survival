using System.Collections;
using System.Collections.Generic;
using GameAssembly.InventorySystem;
using GameAssembly.ItemsSystem;
using GameAssembly.ItemsSystem.Data;
using GameAssembly.Utils;
using Mirror;
using TMPro;
using UnityEngine;

namespace GameAssembly.ObjectsSystem
{
    public class PickableObject : NetworkBehaviour
    {
        [Header("Item")]
        [SerializeField] private ItemDefinitionSO itemDefinition;
        [SerializeField] private int itemCount;
        [SerializeField] private float despawnTime = 180f;
        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField] private TMP_Text counter;
        [SerializeField] private Collider2D triggerCollider;
        [SerializeField] private LayerMask triggerLayers;

        [Header("Visual: Spawn")]
        [SerializeField, Min(0.01f)] private float spawnAnimationDuration = 0.35f;
        [SerializeField] private float spawnStartHeight = 0.85f;
        [SerializeField] private float spawnHorizontalSpread = 0.2f;
        [SerializeField] private float spawnStartScale = 0.65f;
        [SerializeField] private float spawnSettleAmplitude = 0.08f;
        [SerializeField] private float spawnSettleBounces = 2.5f;

        [Header("Visual: Idle")]
        [SerializeField] private float idleBobAmplitude = 0.06f;
        [SerializeField] private float idleBobFrequency = 1.7f;
        [SerializeField] private float idleScaleAmplitude = 0.03f;
        [SerializeField] private float idleScaleFrequency = 2.2f;
        [SerializeField] private float idleSwayAngle = 3.5f;
        [SerializeField] private float idleSwayFrequency = 1.25f;

        [Header("Visual: Pickup")]
        [SerializeField, Min(0.05f)] private float pickupFlyDuration = 0.32f;
        [SerializeField] private float pickupArcHeight = 0.5f;
        [SerializeField] private float pickupEndScale = 0.35f;
        [SerializeField] private float pickupSpinAngle = 24f;
        [SerializeField] private float pickupDestroyDelayPadding = 0.06f;

        private Dictionary<NetworkIdentity, float> _takeProtections;
        private double _serverSpawnTime;
        private bool _isPendingServerDestroy;

        private bool _visualCached;
        private Vector3 _spriteBaseLocalPosition;
        private Vector3 _spriteBaseLocalScale;
        private Quaternion _spriteBaseLocalRotation;
        private Color _spriteBaseColor;
        private Vector3 _counterBaseLocalPosition;
        private Vector3 _counterBaseLocalScale;
        private Quaternion _counterBaseLocalRotation;
        private Color _counterBaseColor;
        private float _visualPhase;
        private float _spawnElapsed;
        private bool _pickupVisualActive;
        private float _pickupVisualStartTime;
        private uint _pickupTargetNetId;
        private Vector3 _pickupStartWorldPosition;
        private float _pickupArcPhase;
        private bool _variantInitialized;
        private float _variantIdleBobAmplitude = 1f;
        private float _variantIdleBobFrequency = 1f;
        private float _variantIdleScaleAmplitude = 1f;
        private float _variantIdleScaleFrequency = 1f;
        private float _variantIdleSwayAmplitude = 1f;
        private float _variantIdleSwayFrequency = 1f;
        private float _variantSpawnDuration = 1f;
        private float _variantSpawnHeight = 1f;
        private float _variantSpawnHorizontal = 1f;
        private float _variantSpawnSettle = 1f;
        private float _variantBaseScale = 1f;
        private float _variantPickupArc = 1f;
        private float _variantPickupSpin = 1f;
        private float _variantDirection = 1f;

        public ItemInstance Item { get; private set; }

        public override void OnSerialize(NetworkWriter writer, bool initialState)
        {
            var hasItem = Item != null;
            writer.WriteBool(hasItem);
            if (hasItem)
                writer.Write(Item);
            writer.WriteDouble(_serverSpawnTime);
        }

        public override void OnDeserialize(NetworkReader reader, bool initialState)
        {
            Item = reader.ReadBool() ? reader.Read<ItemInstance>() : null;
            _serverSpawnTime = reader.ReadDouble();
            Draw();

            if (!initialState || !isClient)
                return;

            var spawnAge = Mathf.Max(0f, (float)(NetworkTime.time - _serverSpawnTime));
            _spawnElapsed = Mathf.Clamp(spawnAge, 0f, GetSpawnDuration());
        }

        public void Initialize(ItemInstance item)
        {
            Item = item;

            if (NetworkServer.active)
                _serverSpawnTime = NetworkTime.time;

            SetDirty();
            Draw();
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            CacheVisualDefaults();

            if (!spriteRenderer)
                return;

            _variantInitialized = false;
            var spawnAge = Mathf.Max(0f, (float)(NetworkTime.time - _serverSpawnTime));
            _visualPhase = CalculateVisualPhase();
            InitializeVisualVariant();
            _spawnElapsed = Mathf.Clamp(spawnAge, 0f, GetSpawnDuration());
            ResetVisualsImmediately();
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            StartCoroutine(DespawnCoroutine());

            if (!itemDefinition)
                return;

            Initialize(new ItemInstance(itemDefinition, itemCount));
        }

        private void Update()
        {
            UpdateTakeProtections();

            if (!isClient)
                return;

            UpdateVisuals();
        }

        [Server]
        public void SetTakeProtectionForPlayer(NetworkIdentity playerIdentity, float protectionTime)
        {
            if (!playerIdentity)
                return;

            _takeProtections ??= new Dictionary<NetworkIdentity, float>();
            _takeProtections[playerIdentity] = Time.time + protectionTime;
        }

        private void Draw()
        {
            if (!spriteRenderer || Item == null || !Item.Definition)
                return;

            spriteRenderer.sprite = Item.Definition.InventoryIcon ? Item.Definition.InventoryIcon : Item.Definition.Icon;
            if (counter)
                counter.text = Item.Count.ToString();
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!isServer || _isPendingServerDestroy)
                return;

            if (!LayerService.CheckLayersEquality(other.gameObject.layer, triggerLayers))
                return;

            if (!other.TryGetComponent<IInventory>(out var inventory))
                return;

            var pickerIdentity = other.GetComponent<NetworkIdentity>();

            if (pickerIdentity && _takeProtections != null && _takeProtections.ContainsKey(pickerIdentity))
                return;

            var previousCount = Item?.Count ?? 0;
            if (previousCount <= 0)
                return;

            inventory.TryAddItemFromInstance(Item, false);
            var currentCount = Item?.Count ?? 0;

            if (currentCount >= previousCount)
                return;

            if (currentCount == 0)
            {
                StartPickupDestroySequence(pickerIdentity);
                return;
            }

            SetDirty();
            Draw();
            Rpc_PlayPartialPickupPulse();
        }

        [Server]
        private void StartPickupDestroySequence(NetworkIdentity pickerIdentity)
        {
            if (_isPendingServerDestroy)
                return;

            _isPendingServerDestroy = true;

            if (triggerCollider)
                triggerCollider.enabled = false;
            else if (TryGetComponent<Collider2D>(out var fallbackCollider))
                fallbackCollider.enabled = false;

            Rpc_PlayPickupVisual(pickerIdentity ? pickerIdentity.netId : 0u);
            StartCoroutine(DestroyAfterPickupVisualCoroutine());
        }

        [Server]
        private IEnumerator DestroyAfterPickupVisualCoroutine()
        {
            var waitDuration = Mathf.Max(0.01f, pickupFlyDuration + pickupDestroyDelayPadding);
            yield return new WaitForSeconds(waitDuration);

            if (this != null && gameObject != null)
                NetworkServer.Destroy(gameObject);
        }

        [ClientRpc]
        private void Rpc_PlayPickupVisual(uint targetNetId)
        {
            if (!isClient)
                return;

            CacheVisualDefaults();
            _pickupTargetNetId = targetNetId;
            _pickupVisualStartTime = Time.time;
            _pickupStartWorldPosition = spriteRenderer ? spriteRenderer.transform.position : transform.position;
            _pickupArcPhase = _visualPhase;
            _pickupVisualActive = true;
        }

        [ClientRpc]
        private void Rpc_PlayPartialPickupPulse()
        {
            if (!isClient || _pickupVisualActive)
                return;

            _spawnElapsed = Mathf.Min(_spawnElapsed, GetSpawnDuration() * 0.9f);
        }

        [Server]
        private IEnumerator DespawnCoroutine()
        {
            yield return new WaitForSeconds(despawnTime);

            if (_isPendingServerDestroy)
                yield break;

            NetworkServer.Destroy(gameObject);
        }

        private void UpdateTakeProtections()
        {
            if (!isServer || _takeProtections == null || _takeProtections.Count == 0)
                return;

            NetworkIdentity expiredKey = null;

            foreach (var pair in _takeProtections)
            {
                if (Time.time < pair.Value)
                    continue;

                expiredKey = pair.Key;
                break;
            }

            if (expiredKey)
                _takeProtections.Remove(expiredKey);
        }

        private void UpdateVisuals()
        {
            if (!spriteRenderer)
                return;

            CacheVisualDefaults();
            InitializeVisualVariant();

            if (_pickupVisualActive)
            {
                UpdatePickupVisual();
                return;
            }

            UpdateSpawnAndIdleVisual();
        }

        private void UpdateSpawnAndIdleVisual()
        {
            var spawnDuration = GetSpawnDuration();

            if (spawnDuration > 0f && _spawnElapsed < spawnDuration)
                _spawnElapsed = Mathf.Min(spawnDuration, _spawnElapsed + Time.deltaTime);

            var spawnT = spawnDuration <= 0f
                ? 1f
                : Mathf.Clamp01(_spawnElapsed / spawnDuration);
            var spawnEase = EaseOutCubic(spawnT);

            var spawnOffset = new Vector3(
                Mathf.Lerp(GetSpawnHorizontalOffset(), 0f, spawnEase),
                Mathf.Lerp(spawnStartHeight * _variantSpawnHeight, 0f, spawnEase),
                0f);

            if (spawnSettleAmplitude > 0f && spawnSettleBounces > 0f)
            {
                var settle = Mathf.Sin(spawnT * Mathf.PI * spawnSettleBounces) * (1f - spawnT) *
                             spawnSettleAmplitude * _variantSpawnSettle;
                spawnOffset.y += settle;
            }

            var idleWeight = Mathf.SmoothStep(0f, 1f, spawnT);
            var time = Time.time + _visualPhase;

            var bob = Mathf.Sin(time * idleBobFrequency * _variantIdleBobFrequency * Mathf.PI * 2f) *
                      idleBobAmplitude * _variantIdleBobAmplitude * idleWeight;
            var scalePulse = 1f + Mathf.Sin((time * idleScaleFrequency * _variantIdleScaleFrequency + _visualPhase) *
                                            Mathf.PI * 2f) * idleScaleAmplitude * _variantIdleScaleAmplitude *
                idleWeight;
            var sway = Mathf.Sin((time * idleSwayFrequency * _variantIdleSwayFrequency + _visualPhase * 1.7f) *
                                 Mathf.PI * 2f) * idleSwayAngle * _variantIdleSwayAmplitude * _variantDirection *
                idleWeight;

            var spawnScale = Mathf.Lerp(spawnStartScale, 1f, EaseOutBack(spawnT));
            var finalScale = spawnScale * scalePulse * _variantBaseScale;
            var finalAlpha = Mathf.Lerp(0.35f, 1f, spawnT);

            var spritePosition = _spriteBaseLocalPosition + spawnOffset + Vector3.up * bob;
            var counterPosition = _counterBaseLocalPosition + spawnOffset + Vector3.up * bob;

            spriteRenderer.transform.localPosition = spritePosition;
            spriteRenderer.transform.localRotation = _spriteBaseLocalRotation * Quaternion.Euler(0f, 0f, sway);
            spriteRenderer.transform.localScale = _spriteBaseLocalScale * finalScale;
            SetSpriteAlpha(finalAlpha);

            if (!counter)
                return;

            counter.transform.localPosition = counterPosition;
            counter.transform.localRotation = _counterBaseLocalRotation;
            counter.transform.localScale = _counterBaseLocalScale * finalScale;
            SetCounterAlpha(finalAlpha);
        }

        private void UpdatePickupVisual()
        {
            var duration = Mathf.Max(0.01f, pickupFlyDuration);
            var t = Mathf.Clamp01((Time.time - _pickupVisualStartTime) / duration);
            var eased = EaseInCubic(t);
            var targetWorldPosition = ResolvePickupTargetPosition();

            var worldPosition = Vector3.LerpUnclamped(_pickupStartWorldPosition, targetWorldPosition, eased);
            worldPosition.y += Mathf.Sin(t * Mathf.PI) * pickupArcHeight * _variantPickupArc;

            var spin = Mathf.Sin((t + _pickupArcPhase) * Mathf.PI) * pickupSpinAngle * _variantPickupSpin *
                       _variantDirection * (1f - t);
            var scale = Mathf.Lerp(1f, pickupEndScale, eased);
            var alpha = Mathf.Lerp(1f, 0f, EaseOutCubic(t));

            if (spriteRenderer)
            {
                spriteRenderer.transform.position = worldPosition;
                spriteRenderer.transform.rotation = Quaternion.Euler(0f, 0f, spin);
                spriteRenderer.transform.localScale = _spriteBaseLocalScale * scale;
                SetSpriteAlpha(alpha);
            }

            if (counter)
            {
                counter.transform.position = worldPosition;
                counter.transform.rotation = Quaternion.identity;
                counter.transform.localScale = _counterBaseLocalScale * scale;
                SetCounterAlpha(alpha);
            }

            if (t < 1f)
                return;

            _pickupVisualActive = false;
            HideVisuals();
        }

        private Vector3 ResolvePickupTargetPosition()
        {
            if (_pickupTargetNetId != 0u && NetworkClient.spawned != null &&
                NetworkClient.spawned.TryGetValue(_pickupTargetNetId, out var identity) && identity != null)
            {
                return identity.transform.position + Vector3.up * 0.6f;
            }

            return _pickupStartWorldPosition + Vector3.up * 0.75f;
        }

        private void HideVisuals()
        {
            if (spriteRenderer)
                spriteRenderer.enabled = false;

            if (counter)
                counter.enabled = false;
        }

        private void ResetVisualsImmediately()
        {
            if (!spriteRenderer)
                return;

            spriteRenderer.enabled = true;
            spriteRenderer.transform.localPosition = _spriteBaseLocalPosition;
            spriteRenderer.transform.localRotation = _spriteBaseLocalRotation;
            spriteRenderer.transform.localScale = _spriteBaseLocalScale;
            SetSpriteAlpha(1f);

            if (!counter)
                return;

            counter.enabled = true;
            counter.transform.localPosition = _counterBaseLocalPosition;
            counter.transform.localRotation = _counterBaseLocalRotation;
            counter.transform.localScale = _counterBaseLocalScale;
            SetCounterAlpha(1f);
        }

        private void CacheVisualDefaults()
        {
            if (_visualCached)
                return;

            if (spriteRenderer)
            {
                _spriteBaseLocalPosition = spriteRenderer.transform.localPosition;
                _spriteBaseLocalScale = spriteRenderer.transform.localScale;
                _spriteBaseLocalRotation = spriteRenderer.transform.localRotation;
                _spriteBaseColor = spriteRenderer.color;
            }

            if (counter)
            {
                _counterBaseLocalPosition = counter.transform.localPosition;
                _counterBaseLocalScale = counter.transform.localScale;
                _counterBaseLocalRotation = counter.transform.localRotation;
                _counterBaseColor = counter.color;
            }

            _visualCached = true;
        }

        private void SetSpriteAlpha(float alpha)
        {
            if (!spriteRenderer)
                return;

            var color = _spriteBaseColor;
            color.a *= Mathf.Clamp01(alpha);
            spriteRenderer.color = color;
        }

        private void SetCounterAlpha(float alpha)
        {
            if (!counter)
                return;

            var color = _counterBaseColor;
            color.a *= Mathf.Clamp01(alpha);
            counter.color = color;
        }

        private float GetSpawnHorizontalOffset() =>
            Mathf.Sin(_visualPhase * 5.13f) * spawnHorizontalSpread * _variantSpawnHorizontal * _variantDirection;

        private float GetSpawnDuration() => Mathf.Max(0.01f, spawnAnimationDuration * _variantSpawnDuration);

        private float CalculateVisualPhase()
        {
            var seed = netId != 0 ? netId : (uint)GetInstanceID();
            return (seed % 997u) / 997f;
        }

        private void InitializeVisualVariant()
        {
            if (_variantInitialized)
                return;

            var seed = GetVariantSeed();

            _variantIdleBobAmplitude = RandomRange(ref seed, 0.86f, 1.2f);
            _variantIdleBobFrequency = RandomRange(ref seed, 0.9f, 1.14f);
            _variantIdleScaleAmplitude = RandomRange(ref seed, 0.8f, 1.25f);
            _variantIdleScaleFrequency = RandomRange(ref seed, 0.9f, 1.18f);
            _variantIdleSwayAmplitude = RandomRange(ref seed, 0.82f, 1.24f);
            _variantIdleSwayFrequency = RandomRange(ref seed, 0.88f, 1.16f);

            _variantSpawnDuration = RandomRange(ref seed, 0.88f, 1.15f);
            _variantSpawnHeight = RandomRange(ref seed, 0.8f, 1.25f);
            _variantSpawnHorizontal = RandomRange(ref seed, 0.75f, 1.35f);
            _variantSpawnSettle = RandomRange(ref seed, 0.85f, 1.3f);
            _variantBaseScale = RandomRange(ref seed, 0.92f, 1.08f);

            _variantPickupArc = RandomRange(ref seed, 0.82f, 1.25f);
            _variantPickupSpin = RandomRange(ref seed, 0.8f, 1.3f);
            _variantDirection = NextRandom01(ref seed) < 0.5f ? -1f : 1f;
            _variantInitialized = true;
        }

        private uint GetVariantSeed()
        {
            if (netId != 0u)
                return netId * 747796405u + 2891336453u;

            var instanceId = GetInstanceID();
            var absId = (uint)System.Math.Abs((long)instanceId);
            return absId + 1u;
        }

        private static float RandomRange(ref uint state, float min, float max)
        {
            return Mathf.Lerp(min, max, NextRandom01(ref state));
        }

        private static float NextRandom01(ref uint state)
        {
            state = state * 1664525u + 1013904223u;
            return (state & 0x00FFFFFFu) / 16777215f;
        }

        private static float EaseOutCubic(float t) => 1f - Mathf.Pow(1f - t, 3f);

        private static float EaseInCubic(float t) => t * t * t;

        private static float EaseOutBack(float t)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            var p = t - 1f;
            return 1f + c3 * p * p * p + c1 * p * p;
        }

        protected override void OnValidate()
        {
            if (!spriteRenderer)
                TryGetComponent(out spriteRenderer);

            if (!triggerCollider)
                TryGetComponent(out triggerCollider);

            if (!counter)
                counter = GetComponentInChildren<TMP_Text>();
        }
    }
}
