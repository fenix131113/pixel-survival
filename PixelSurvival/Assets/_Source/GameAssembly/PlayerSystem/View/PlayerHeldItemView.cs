using System.Collections;
using System.Collections.Generic;
using GameAssembly.InventorySystem;
using GameAssembly.ItemsSystem;
using Mirror;
using UnityEngine;

namespace GameAssembly.PlayerSystem.View
{
    public class PlayerHeldItemView : NetworkBehaviour
    {
        private static readonly int _x = Animator.StringToHash("X");
        private static readonly int _y = Animator.StringToHash("Y");

        [SerializeField] private PlayerSelector selector;
        [SerializeField] private PlayerAttack playerAttack;
        [SerializeField] private Animator bodyAnimator;
        [SerializeField] private SpriteRenderer heldItemRenderer;
        [SerializeField] private Animator heldItemAnimator;

        private readonly HashSet<string> _missingAnimationKeys = new();

        private IInventory _inventory;
        private bool _isBound;

        public override void OnStartClient()
        {
            TryResolveReferences();
            Bind();
            RefreshHeldItem();
            StartCoroutine(RefreshNextFrame());
        }

        public override void OnStopClient()
        {
            Unbind();
            ClearHeldItem();
        }

        private void OnDestroy()
        {
            Unbind();
        }

        private void LateUpdate()
        {
            RefreshFlipX();
            RefreshSortingOrder();
        }

        private IEnumerator RefreshNextFrame()
        {
            yield return null;
            RefreshHeldItem();
        }

        private void OnSelectedItemChanged()
        {
            RefreshHeldItem();
        }

        private void OnInventoryItemChanged(int changedIndex)
        {
            if (changedIndex != GetSelectedInventoryIndex())
                return;

            RefreshHeldItem();
        }

        private void OnMeleeAttack(float _, string animationKey)
        {
            if (!heldItemAnimator || !heldItemAnimator.runtimeAnimatorController)
                return;

            if (string.IsNullOrWhiteSpace(animationKey))
                return;

            var stateHash = Animator.StringToHash(animationKey);

            if (!heldItemAnimator.HasState(0, stateHash))
            {
                if (_missingAnimationKeys.Add(animationKey))
                    Debug.LogWarning($"Held item animation state '{animationKey}' was not found on '{name}'.", this);

                return;
            }

            heldItemAnimator.Play(stateHash, 0, 0f);
        }

        private void RefreshHeldItem()
        {
            if (!heldItemRenderer)
                return;

            var selectedItem = GetSelectedItem();
            var icon = selectedItem?.Definition ? selectedItem.Definition.Icon : null;

            heldItemRenderer.sprite = icon;
            heldItemRenderer.enabled = icon;
            RefreshFlipX();
            RefreshSortingOrder();
        }

        private ItemInstance GetSelectedItem()
        {
            if (_inventory == null || selector == null)
                return null;

            var selectedInventoryIndex = GetSelectedInventoryIndex();
            return selectedInventoryIndex > -1 ? _inventory.GetItemByIndex(selectedInventoryIndex) : null;
        }

        private int GetSelectedInventoryIndex()
        {
            if (_inventory == null || selector == null || selector.SelectedIndex < 0)
                return -1;

            var inventoryIndex = _inventory.GetInventorySize() - selector.HotBarSize + selector.SelectedIndex;

            return inventoryIndex >= 0 && inventoryIndex < _inventory.GetInventorySize() ? inventoryIndex : -1;
        }

        private void Bind()
        {
            if (_isBound || selector == null || _inventory == null)
                return;

            selector.OnSelectedItemChanged += OnSelectedItemChanged;
            _inventory.OnItemChanged += OnInventoryItemChanged;

            if (playerAttack)
                playerAttack.OnMeleeAttack += OnMeleeAttack;

            _isBound = true;
        }

        private void Unbind()
        {
            if (!_isBound)
                return;

            if (selector != null)
                selector.OnSelectedItemChanged -= OnSelectedItemChanged;

            if (_inventory != null)
                _inventory.OnItemChanged -= OnInventoryItemChanged;

            if (playerAttack)
                playerAttack.OnMeleeAttack -= OnMeleeAttack;

            _isBound = false;
        }

        private void ClearHeldItem()
        {
            if (!heldItemRenderer)
                return;

            heldItemRenderer.sprite = null;
            heldItemRenderer.enabled = false;
            heldItemRenderer.sortingOrder = 0;
        }

        private void RefreshFlipX()
        {
            if (!heldItemRenderer || !bodyAnimator)
                return;

            var x = bodyAnimator.GetFloat(_x);
            var y = bodyAnimator.GetFloat(_y);

            if (x > 0.5f || y < -0.5f) // right or up
            {
                heldItemRenderer.flipX = false;
                return;
            }

            if (x < -0.5f || y > 0.5f) // left or down
                heldItemRenderer.flipX = true;
        }

        private void RefreshSortingOrder()
        {
            if (!heldItemRenderer || !bodyAnimator)
                return;

            var x = bodyAnimator.GetFloat(_x);
            var y = bodyAnimator.GetFloat(_y);

            heldItemRenderer.sortingOrder = x < -0.5f || y < -0.5f ? -1 : 1;
        }

        private void TryResolveReferences()
        {
            selector ??= GetComponent<PlayerSelector>();
            playerAttack ??= GetComponent<PlayerAttack>();
            _inventory ??= GetComponent<IInventory>();

            if (bodyAnimator == null)
            {
                var animators = GetComponentsInChildren<Animator>(true);

                foreach (var animator in animators)
                {
                    if (!animator || animator == heldItemAnimator)
                        continue;

                    if (animator.runtimeAnimatorController != null)
                    {
                        bodyAnimator = animator;
                        break;
                    }

                    bodyAnimator ??= animator;
                }
            }
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            TryResolveReferences();
        }
#endif
    }
}
