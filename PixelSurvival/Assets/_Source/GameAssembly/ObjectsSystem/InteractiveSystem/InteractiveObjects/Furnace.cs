using System.Collections;
using System.Collections.Generic;
using System.Linq;
using GameAssembly.HealthSystem;
using GameAssembly.InventorySystem;
using GameAssembly.ItemsSystem;
using GameAssembly.ItemsSystem.Data;
using GameAssembly.ObjectsSystem.InteractiveSystem.InteractiveObjects.FurnaceSystem.Data;
using GameAssembly.ObjectsSystem.View.ObjectsView;
using GameAssembly.PlayerSystem.View;
using GameAssembly.UiSystem;
using GameAssembly.Utils;
using Mirror;
using UnityEngine;
using VContainer;

namespace GameAssembly.ObjectsSystem.InteractiveSystem.InteractiveObjects
{
    public class Furnace : BaseInventory, IInteractiveObject
    {
        public const int INPUT_SLOT_INDEX = 0;
        public const int OUTPUT_SLOT_INDEX = 1;

        private const int FURNACE_INVENTORY_SIZE = 2;
        private const float PROGRESS_SYNC_INTERVAL = 0.05f;

        [SerializeField] private AHealthObject healthObject;
        [SerializeField] private SpriteRenderer furnaceRenderer;
        [SerializeField] private FurnaceTier furnaceTier = FurnaceTier.TIER1;

        [Inject] private FurnaceView _view;
        [Inject] private PlayerInventoryView _playerInventoryView;
        [Inject] private ServerInventoryManager _serverInventoryManager;

        private static IReadOnlyList<SmeltRecipeSO> _recipes;
        private Coroutine _serverSmeltingCoroutine;
        private float _smeltProgress01;

        public event System.Action<float> OnSmeltProgressChanged;
        public float SmeltProgress01 => _smeltProgress01;

        protected override void OnValidate() => inventorySize = FURNACE_INVENTORY_SIZE;

        public override void OnSerialize(NetworkWriter writer, bool initialState)
        {
            writer.WriteFloat(_smeltProgress01);
            base.OnSerialize(writer, initialState);
        }

        public override void OnDeserialize(NetworkReader reader, bool initialState)
        {
            _smeltProgress01 = reader.ReadFloat();
            OnSmeltProgressChanged?.Invoke(_smeltProgress01);
            base.OnDeserialize(reader, initialState);
        }

        private void Start()
        {
            if (NetworkClient.active)
                ObjectInjector.Inject(this);

            if (!NetworkServer.active)
                return;

            Server_SetSmeltProgress(0f);
            Server_Bind();
            _serverSmeltingCoroutine = StartCoroutine(Server_SmeltCoroutine());
        }

        private void OnDestroy()
        {
            if (!NetworkServer.active)
                return;

            if (_serverSmeltingCoroutine != null)
                StopCoroutine(_serverSmeltingCoroutine);

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

        public Renderer GetRendererTarget() => furnaceRenderer;

        [Server]
        public override bool TryAddItemFromInstance(ItemInstance instance, bool fullInsert, bool ignoreMeta = false)
        {
            if (instance == null)
                return false;

            return TryAddItemInInputSlot(instance, fullInsert, ignoreMeta);
        }

        [Server]
        public override bool TryAddNewItem(ItemDefinitionSO definition, int count)
        {
            if (!definition || count <= 0)
                return false;

            var inputItem = _items[INPUT_SLOT_INDEX];

            if (inputItem == null)
            {
                if (count > definition.MaxCount)
                    return false;

                _items[INPUT_SLOT_INDEX] = new ItemInstance(definition, count);
                BindNewItem(INPUT_SLOT_INDEX);
                InvokeOnItemChanged(INPUT_SLOT_INDEX);
                SetDirty();
                return true;
            }

            if (inputItem.Definition != definition || inputItem.Count + count > definition.MaxCount)
                return false;

            if (!inputItem.TryAddCount(count))
                return false;

            SetDirty();
            return true;
        }

        [Server]
        public override bool TryAddItemInIndexFromInstance(ItemInstance item, int index, bool fullInsert)
        {
            if (index != INPUT_SLOT_INDEX)
                return false;

            return base.TryAddItemInIndexFromInstance(item, index, fullInsert);
        }

        [Server]
        public override bool CanAddNewItem(ItemDefinitionSO definition, int count = 1)
        {
            if (!definition || count <= 0)
                return false;

            var inputItem = _items[INPUT_SLOT_INDEX];

            if (inputItem == null)
                return count <= definition.MaxCount;

            return inputItem.Definition == definition && inputItem.Count + count <= definition.MaxCount;
        }

        // ReSharper disable once IteratorNeverReturns
        [Server]
        private IEnumerator Server_SmeltCoroutine()
        {
            while (true)
            {
                var recipe = Server_GetCurrentRecipe();

                if (!recipe)
                {
                    Server_SetSmeltProgress(0f);
                    yield return null;
                    continue;
                }

                var duration = Mathf.Max(recipe.SmeltTimeSeconds, PROGRESS_SYNC_INTERVAL);
                var startTime = Time.time;
                var wasInterrupted = false;

                while (Time.time - startTime < duration)
                {
                    if (!Server_CanSmelt(recipe))
                    {
                        wasInterrupted = true;
                        break;
                    }

                    var progress01 = Mathf.Clamp01((Time.time - startTime) / duration);
                    Server_SetSmeltProgress(progress01);
                    yield return new WaitForSeconds(PROGRESS_SYNC_INTERVAL);
                }

                if (wasInterrupted || !Server_CanSmelt(recipe))
                {
                    Server_SetSmeltProgress(0f);
                    continue;
                }

                Server_SetSmeltProgress(1f);
                Server_SmeltOne(recipe);
                Server_SetSmeltProgress(0f);
            }
        }

        [Server]
        private SmeltRecipeSO Server_GetCurrentRecipe()
        {
            if (_items == null || _items.Length < FURNACE_INVENTORY_SIZE)
                return null;

            var inputItem = _items[INPUT_SLOT_INDEX];

            if (inputItem == null)
                return null;

            EnsureRecipesLoaded();

            return _recipes.Where(recipe => IsRecipeValidForCurrentState(recipe, inputItem))
                .OrderByDescending(recipe => recipe.RequiredFurnaceTier)
                .FirstOrDefault();
        }

        [Server]
        private bool Server_CanSmelt(SmeltRecipeSO recipe)
        {
            if (!recipe || _items == null || _items.Length < FURNACE_INVENTORY_SIZE)
                return false;

            var inputItem = _items[INPUT_SLOT_INDEX];
            return inputItem != null && IsRecipeValidForCurrentState(recipe, inputItem);
        }

        [Server]
        private void Server_SmeltOne(SmeltRecipeSO recipe)
        {
            var inputItem = _items[INPUT_SLOT_INDEX];

            if (inputItem == null || !inputItem.TryRemoveCount(recipe.InputCount))
                return;

            var outputItem = _items[OUTPUT_SLOT_INDEX];

            if (outputItem == null)
            {
                _items[OUTPUT_SLOT_INDEX] = new ItemInstance(recipe.ResultItem, recipe.ResultCount);
                BindNewItem(OUTPUT_SLOT_INDEX);
                InvokeOnItemChanged(OUTPUT_SLOT_INDEX);
                return;
            }

            outputItem.TryAddCount(recipe.ResultCount);
        }

        [Server]
        private bool IsRecipeValidForCurrentState(SmeltRecipeSO recipe, ItemInstance inputItem)
        {
            if (!recipe || !recipe.InputItem || !recipe.ResultItem || recipe.InputCount <= 0 || recipe.ResultCount <= 0 ||
                recipe.SmeltTimeSeconds <= 0f)
                return false;

            if (furnaceTier < recipe.RequiredFurnaceTier)
                return false;

            if (inputItem.Definition != recipe.InputItem || inputItem.Count < recipe.InputCount)
                return false;

            var outputItem = _items[OUTPUT_SLOT_INDEX];

            if (outputItem == null)
                return recipe.ResultCount <= recipe.ResultItem.MaxCount;

            return outputItem.Definition == recipe.ResultItem &&
                   outputItem.Count + recipe.ResultCount <= outputItem.Definition.MaxCount;
        }

        [Server]
        private bool TryAddItemInInputSlot(ItemInstance sourceItem, bool fullInsert, bool ignoreMeta)
        {
            var inputItem = _items[INPUT_SLOT_INDEX];

            if (inputItem == null)
            {
                var newCount = Mathf.Min(sourceItem.Count, sourceItem.Definition.MaxCount);

                if (newCount <= 0)
                    return false;

                if (fullInsert && newCount < sourceItem.Count)
                    return false;

                var itemMeta = sourceItem.Meta.ToDictionary(x => x.Key, y => y.Value);
                _items[INPUT_SLOT_INDEX] = new ItemInstance(sourceItem.Definition, itemMeta, newCount);
                BindNewItem(INPUT_SLOT_INDEX);
                InvokeOnItemChanged(INPUT_SLOT_INDEX);
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

        private static void EnsureRecipesLoaded()
        {
            if (_recipes != null)
                return;

            _recipes = Resources.LoadAll<SmeltRecipeSO>(AssetsPaths.SMELTING_RECIPES_CONFIGS_PATH);
        }

        [Server]
        private void Server_SetSmeltProgress(float progress01)
        {
            var normalized = Mathf.Clamp01(progress01);
            if (Mathf.Approximately(_smeltProgress01, normalized))
                return;

            _smeltProgress01 = normalized;
            OnSmeltProgressChanged?.Invoke(_smeltProgress01);
            SetDirty();
        }

        [Server]
        private void DropEverythingFromFurnace()
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
            healthObject.OnZeroHealth += DropEverythingFromFurnace;
        }

        [Server]
        private void Server_Expose()
        {
            healthObject.OnZeroHealth -= DropEverythingFromFurnace;
        }
    }
}
