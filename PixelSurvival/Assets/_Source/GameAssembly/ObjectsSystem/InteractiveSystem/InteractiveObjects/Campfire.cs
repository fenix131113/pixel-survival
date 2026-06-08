using System;
using System.Collections;
using System.Linq;
using GameAssembly.HealthSystem;
using GameAssembly.InventorySystem;
using GameAssembly.ItemsSystem;
using GameAssembly.ItemsSystem.Data;
using GameAssembly.ObjectsSystem.View.ObjectsView;
using GameAssembly.PlayerSystem.View;
using GameAssembly.UiSystem;
using GameAssembly.Utils;
using Mirror;
using UnityEngine;
using VContainer;

namespace GameAssembly.ObjectsSystem.InteractiveSystem.InteractiveObjects
{
    public class Campfire : BaseInventory, IInteractiveObject
    {
        public const int FIRST_INPUT_SLOT_INDEX = 0;
        public const int SECOND_INPUT_SLOT_INDEX = 1;
        public const int OUTPUT_SLOT_INDEX = 2;

        private const int CAMPFIRE_INVENTORY_SIZE = 3;
        private const float PROGRESS_SYNC_INTERVAL = 0.05f;
        private const float MIN_COOK_TIME_SECONDS = 0.01f;

        [SerializeField] private AHealthObject healthObject;
        [SerializeField] private SpriteRenderer campfireRenderer;
        [SerializeField] private ItemDefinitionSO[] allowedInputItems = Array.Empty<ItemDefinitionSO>();
        [SerializeField] private CampfireRecipe[] recipes = Array.Empty<CampfireRecipe>();
        [SerializeField] private ItemDefinitionSO fallbackResultItem;
        [SerializeField, Min(1)] private int fallbackResultCount = 1;
        [SerializeField, Min(MIN_COOK_TIME_SECONDS)] private float fallbackCookTimeSeconds = 1f;

        [Inject] private CampfireView _view;
        [Inject] private PlayerInventoryView _playerInventoryView;
        [Inject] private ServerInventoryManager _serverInventoryManager;

        private Coroutine _serverCookingCoroutine;
        private float _cookProgress01;

        public event System.Action<float> OnCookProgressChanged;
        public float CookProgress01 => _cookProgress01;

        protected override void OnValidate()
        {
            inventorySize = CAMPFIRE_INVENTORY_SIZE;
            fallbackResultCount = Mathf.Max(1, fallbackResultCount);
            fallbackCookTimeSeconds = Mathf.Max(MIN_COOK_TIME_SECONDS, fallbackCookTimeSeconds);

            if (recipes == null)
                return;

            foreach (var recipe in recipes)
                recipe?.Validate();
        }

        public override void OnSerialize(NetworkWriter writer, bool initialState)
        {
            writer.WriteFloat(_cookProgress01);
            base.OnSerialize(writer, initialState);
        }

        public override void OnDeserialize(NetworkReader reader, bool initialState)
        {
            _cookProgress01 = reader.ReadFloat();
            OnCookProgressChanged?.Invoke(_cookProgress01);
            base.OnDeserialize(reader, initialState);
        }

        private void Start()
        {
            if (NetworkClient.active)
                ObjectInjector.Inject(this);

            if (!NetworkServer.active)
                return;

            Server_SetCookProgress(0f);
            Server_Bind();
            _serverCookingCoroutine = StartCoroutine(Server_CookCoroutine());
        }

        private void OnDestroy()
        {
            if (!NetworkServer.active)
                return;

            if (_serverCookingCoroutine != null)
                StopCoroutine(_serverCookingCoroutine);

            Server_Expose();
        }

        public void Interact()
        {
            if (_view == null || _playerInventoryView == null || UiManager.Instance == null)
                return;

            _view.Initialize(this);

            UiManager.Instance.OpenRequest(_playerInventoryView);
            UiManager.Instance.OpenRequest(_view);
        }

        public Renderer GetRendererTarget() => campfireRenderer;

        [Server]
        public override bool TryAddItemFromInstance(ItemInstance instance, bool fullInsert, bool ignoreMeta = false)
        {
            if (instance is not { Count: > 0 } || !IsAllowedInputItem(instance.Definition))
                return false;

            return TryAddItemToInputSlots(instance, fullInsert, ignoreMeta);
        }

        [Server]
        public override bool TryAddNewItem(ItemDefinitionSO definition, int count)
        {
            if (!definition || count <= 0 || !IsAllowedInputItem(definition))
                return false;

            if (!CanAddNewItem(definition, count))
                return false;

            var item = new ItemInstance(definition, count);
            return TryAddItemFromInstance(item, true) && item.Count == 0;
        }

        [Server]
        public override bool TryAddItemInIndexFromInstance(ItemInstance item, int index, bool fullInsert)
        {
            if (!IsInputSlot(index))
                return false;

            return TryAddItemInInputSlot(item, index, fullInsert, false);
        }

        [Server]
        public override bool CanAddNewItem(ItemDefinitionSO definition, int count = 1)
        {
            if (!definition || count <= 0 || !IsAllowedInputItem(definition))
                return false;

            return GetAvailableInputSpace(definition) >= count;
        }

        // ReSharper disable once IteratorNeverReturns
        [Server]
        private IEnumerator Server_CookCoroutine()
        {
            while (true)
            {
                var cookEntry = Server_GetCurrentCookEntry();

                if (!cookEntry.IsValid)
                {
                    Server_SetCookProgress(0f);
                    yield return null;
                    continue;
                }

                var duration = Mathf.Max(cookEntry.CookTimeSeconds, PROGRESS_SYNC_INTERVAL);
                var startTime = Time.time;
                var wasInterrupted = false;

                while (Time.time - startTime < duration)
                {
                    if (!Server_CanCook(cookEntry))
                    {
                        wasInterrupted = true;
                        break;
                    }

                    var progress01 = Mathf.Clamp01((Time.time - startTime) / duration);
                    Server_SetCookProgress(progress01);
                    yield return new WaitForSeconds(PROGRESS_SYNC_INTERVAL);
                }

                if (wasInterrupted || !Server_CanCook(cookEntry))
                {
                    Server_SetCookProgress(0f);
                    continue;
                }

                Server_SetCookProgress(1f);
                Server_CookOne(cookEntry);
                Server_SetCookProgress(0f);
            }
        }

        [Server]
        private CampfireCookEntry Server_GetCurrentCookEntry()
        {
            if (_items == null || _items.Length < CAMPFIRE_INVENTORY_SIZE)
                return default;

            var firstInputItem = _items[FIRST_INPUT_SLOT_INDEX];
            var secondInputItem = _items[SECOND_INPUT_SLOT_INDEX];

            if (firstInputItem == null || secondInputItem == null)
                return default;

            if (!IsAllowedInputItem(firstInputItem.Definition) || !IsAllowedInputItem(secondInputItem.Definition))
                return default;

            var recipe = GetRecipe(firstInputItem.Definition, secondInputItem.Definition);
            var cookEntry = recipe != null
                ? new CampfireCookEntry(firstInputItem.Definition, secondInputItem.Definition, recipe.ResultItem,
                    recipe.ResultCount, recipe.CookTimeSeconds)
                : new CampfireCookEntry(firstInputItem.Definition, secondInputItem.Definition, fallbackResultItem,
                    fallbackResultCount, fallbackCookTimeSeconds);

            return Server_CanCook(cookEntry) ? cookEntry : default;
        }

        [Server]
        private bool Server_CanCook(CampfireCookEntry cookEntry)
        {
            if (!cookEntry.IsValid || _items == null || _items.Length < CAMPFIRE_INVENTORY_SIZE)
                return false;

            var firstInputItem = _items[FIRST_INPUT_SLOT_INDEX];
            var secondInputItem = _items[SECOND_INPUT_SLOT_INDEX];

            if (firstInputItem == null || secondInputItem == null)
                return false;

            if (firstInputItem.Definition != cookEntry.FirstInputItem ||
                secondInputItem.Definition != cookEntry.SecondInputItem)
                return false;

            if (firstInputItem.Count <= 0 || secondInputItem.Count <= 0)
                return false;

            if (!IsAllowedInputItem(firstInputItem.Definition) || !IsAllowedInputItem(secondInputItem.Definition))
                return false;

            return CanAddResult(cookEntry.ResultItem, cookEntry.ResultCount);
        }

        [Server]
        private void Server_CookOne(CampfireCookEntry cookEntry)
        {
            if (!Server_CanCook(cookEntry))
                return;

            if (!TryAddResult(cookEntry.ResultItem, cookEntry.ResultCount))
                return;

            _items[FIRST_INPUT_SLOT_INDEX]?.TryRemoveCount(1);
            _items[SECOND_INPUT_SLOT_INDEX]?.TryRemoveCount(1);
        }

        [Server]
        private bool TryAddItemToInputSlots(ItemInstance sourceItem, bool fullInsert, bool ignoreMeta)
        {
            var availableSpace = GetAvailableInputSpace(sourceItem, ignoreMeta);
            if (availableSpace <= 0)
                return false;

            if (fullInsert && sourceItem.Count > availableSpace)
                return false;

            var sourceCount = sourceItem.Count;
            TryAddItemToExistingInputSlots(sourceItem, ignoreMeta);
            TryAddItemToEmptyInputSlots(sourceItem, ignoreMeta);

            return sourceItem.Count < sourceCount;
        }

        [Server]
        private void TryAddItemToExistingInputSlots(ItemInstance sourceItem, bool ignoreMeta)
        {
            if (_items[FIRST_INPUT_SLOT_INDEX] != null)
                TryAddItemInInputSlot(sourceItem, FIRST_INPUT_SLOT_INDEX, false, ignoreMeta);
            if (_items[SECOND_INPUT_SLOT_INDEX] != null)
                TryAddItemInInputSlot(sourceItem, SECOND_INPUT_SLOT_INDEX, false, ignoreMeta);
        }

        [Server]
        private void TryAddItemToEmptyInputSlots(ItemInstance sourceItem, bool ignoreMeta)
        {
            if (_items[FIRST_INPUT_SLOT_INDEX] == null)
                TryAddItemInInputSlot(sourceItem, FIRST_INPUT_SLOT_INDEX, false, ignoreMeta);
            if (_items[SECOND_INPUT_SLOT_INDEX] == null)
                TryAddItemInInputSlot(sourceItem, SECOND_INPUT_SLOT_INDEX, false, ignoreMeta);
        }

        [Server]
        private bool TryAddItemInInputSlot(ItemInstance sourceItem, int index, bool fullInsert, bool ignoreMeta)
        {
            if (sourceItem is not { Count: > 0 } || !IsInputSlot(index) || !IsAllowedInputItem(sourceItem.Definition))
                return false;

            var inputItem = _items[index];

            if (inputItem == null)
            {
                var newCount = Mathf.Min(sourceItem.Count, sourceItem.Definition.MaxCount);

                if (newCount <= 0)
                    return false;

                if (fullInsert && newCount < sourceItem.Count)
                    return false;

                var itemMeta = sourceItem.Meta.ToDictionary(x => x.Key, y => y.Value);
                _items[index] = new ItemInstance(sourceItem.Definition, itemMeta, newCount);
                BindNewItem(index);
                InvokeOnItemChanged(index);
                sourceItem.TryRemoveCount(newCount);
                SetDirty();
                return true;
            }

            if (inputItem.Definition != sourceItem.Definition || (!ignoreMeta && !sourceItem.MatchAllMeta(inputItem.Meta)))
                return false;

            var freeSpace = inputItem.Definition.MaxCount - inputItem.Count;
            if (freeSpace <= 0)
                return false;

            if (fullInsert && sourceItem.Count > freeSpace)
                return false;

            var countToAdd = fullInsert ? sourceItem.Count : Mathf.Min(sourceItem.Count, freeSpace);

            if (!inputItem.TryAddCount(countToAdd))
                return false;

            sourceItem.TryRemoveCount(countToAdd);
            SetDirty();
            return true;
        }

        [Server]
        private bool TryAddResult(ItemDefinitionSO resultItem, int resultCount)
        {
            if (!CanAddResult(resultItem, resultCount))
                return false;

            var outputItem = _items[OUTPUT_SLOT_INDEX];

            if (outputItem == null)
            {
                _items[OUTPUT_SLOT_INDEX] = new ItemInstance(resultItem, resultCount);
                BindNewItem(OUTPUT_SLOT_INDEX);
                InvokeOnItemChanged(OUTPUT_SLOT_INDEX);
                return true;
            }

            return outputItem.TryAddCount(resultCount);
        }

        private bool CanAddResult(ItemDefinitionSO resultItem, int resultCount)
        {
            if (!resultItem || resultCount <= 0 || resultCount > resultItem.MaxCount)
                return false;

            var outputItem = _items[OUTPUT_SLOT_INDEX];

            if (outputItem == null)
                return true;

            return outputItem.Definition == resultItem && outputItem.Count + resultCount <= outputItem.Definition.MaxCount;
        }

        private CampfireRecipe GetRecipe(ItemDefinitionSO firstInputItem, ItemDefinitionSO secondInputItem)
        {
            if (recipes == null)
                return null;

            return recipes.FirstOrDefault(recipe => recipe != null && recipe.IsValid &&
                                                    recipe.Matches(firstInputItem, secondInputItem));
        }

        private int GetAvailableInputSpace(ItemDefinitionSO definition)
        {
            if (!definition || !IsAllowedInputItem(definition) || _items == null)
                return 0;

            var availableSpace = 0;
            availableSpace += GetAvailableInputSpaceInSlot(definition, FIRST_INPUT_SLOT_INDEX);
            availableSpace += GetAvailableInputSpaceInSlot(definition, SECOND_INPUT_SLOT_INDEX);
            return availableSpace;
        }

        private int GetAvailableInputSpace(ItemInstance sourceItem, bool ignoreMeta)
        {
            if (sourceItem is not { Count: > 0 } || !IsAllowedInputItem(sourceItem.Definition) || _items == null)
                return 0;

            var availableSpace = 0;
            availableSpace += GetAvailableInputSpaceInSlot(sourceItem, FIRST_INPUT_SLOT_INDEX, ignoreMeta);
            availableSpace += GetAvailableInputSpaceInSlot(sourceItem, SECOND_INPUT_SLOT_INDEX, ignoreMeta);
            return availableSpace;
        }

        private int GetAvailableInputSpaceInSlot(ItemDefinitionSO definition, int index)
        {
            if (!IsInputSlot(index))
                return 0;

            var slotItem = _items[index];

            if (slotItem == null)
                return definition.MaxCount;

            if (slotItem.Definition != definition)
                return 0;

            return slotItem.Definition.MaxCount - slotItem.Count;
        }

        private int GetAvailableInputSpaceInSlot(ItemInstance sourceItem, int index, bool ignoreMeta)
        {
            if (!IsInputSlot(index))
                return 0;

            var slotItem = _items[index];

            if (slotItem == null)
                return sourceItem.Definition.MaxCount;

            if (slotItem.Definition != sourceItem.Definition || (!ignoreMeta && !sourceItem.MatchAllMeta(slotItem.Meta)))
                return 0;

            return slotItem.Definition.MaxCount - slotItem.Count;
        }

        private bool IsAllowedInputItem(ItemDefinitionSO definition)
        {
            if (!definition || allowedInputItems == null)
                return false;

            return allowedInputItems.Any(item => item == definition);
        }

        private static bool IsInputSlot(int index) =>
            index == FIRST_INPUT_SLOT_INDEX || index == SECOND_INPUT_SLOT_INDEX;

        [Server]
        private void Server_SetCookProgress(float progress01)
        {
            var normalized = Mathf.Clamp01(progress01);
            if (Mathf.Approximately(_cookProgress01, normalized))
                return;

            _cookProgress01 = normalized;
            OnCookProgressChanged?.Invoke(_cookProgress01);
            SetDirty();
        }

        [Server]
        private void DropEverythingFromCampfire()
        {
            for (var index = 0; index < _items.Length; index++)
            {
                var itemInstance = _items[index];

                if (itemInstance != null)
                    _serverInventoryManager.Server_DropItemFromInventory(netIdentity, index, transform.position);
            }
        }

        [Server]
        private void Server_Bind()
        {
            healthObject.OnZeroHealth += DropEverythingFromCampfire;
        }

        [Server]
        private void Server_Expose()
        {
            healthObject.OnZeroHealth -= DropEverythingFromCampfire;
        }

        [Serializable]
        private class CampfireRecipe
        {
            [field: SerializeField] public ItemDefinitionSO FirstInputItem { get; private set; }
            [field: SerializeField] public ItemDefinitionSO SecondInputItem { get; private set; }
            [field: SerializeField] public ItemDefinitionSO ResultItem { get; private set; }
            [field: SerializeField] [field: Min(1)] public int ResultCount { get; private set; } = 1;
            [field: SerializeField] [field: Min(MIN_COOK_TIME_SECONDS)] public float CookTimeSeconds { get; private set; } = 1f;

            public bool IsValid => FirstInputItem && SecondInputItem && ResultItem && ResultCount > 0 &&
                                   CookTimeSeconds > 0f;

            public bool Matches(ItemDefinitionSO firstInputItem, ItemDefinitionSO secondInputItem) =>
                FirstInputItem == firstInputItem && SecondInputItem == secondInputItem ||
                FirstInputItem == secondInputItem && SecondInputItem == firstInputItem;

            public void Validate()
            {
                ResultCount = Mathf.Max(1, ResultCount);
                CookTimeSeconds = Mathf.Max(MIN_COOK_TIME_SECONDS, CookTimeSeconds);
            }
        }

        private readonly struct CampfireCookEntry
        {
            public readonly ItemDefinitionSO FirstInputItem;
            public readonly ItemDefinitionSO SecondInputItem;
            public readonly ItemDefinitionSO ResultItem;
            public readonly int ResultCount;
            public readonly float CookTimeSeconds;
            public readonly bool IsValid;

            public CampfireCookEntry(ItemDefinitionSO firstInputItem, ItemDefinitionSO secondInputItem,
                ItemDefinitionSO resultItem, int resultCount, float cookTimeSeconds)
            {
                FirstInputItem = firstInputItem;
                SecondInputItem = secondInputItem;
                ResultItem = resultItem;
                ResultCount = resultCount;
                CookTimeSeconds = cookTimeSeconds;
                IsValid = FirstInputItem && SecondInputItem && ResultItem && ResultCount > 0 &&
                          CookTimeSeconds > 0f;
            }
        }
    }
}
