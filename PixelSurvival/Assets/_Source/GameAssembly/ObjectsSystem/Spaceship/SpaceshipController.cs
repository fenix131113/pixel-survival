using System;
using System.Collections;
using System.Collections.Generic;
using GameAssembly.BuildSystem.Data;
using GameAssembly.BuildSystem.WorldObjects;
using GameAssembly.Core;
using Mirror;
using UnityEngine;

namespace GameAssembly.ObjectsSystem.Spaceship
{
    public class SpaceshipController : NetworkBehaviour
    {
        [SerializeField] private RequiredObjectSlot[] requiredObjects = Array.Empty<RequiredObjectSlot>();
        [SerializeField] private bool logWhenAssemblyBecomesInvalid = true;

        [field: SerializeField] public bool IsAssemblyCompleted { get; private set; }

        private readonly List<RequirementRuntimeState> _requirements = new();
        private readonly Dictionary<Vector2Int, List<int>> _requirementIndexesByCell = new();

        private Coroutine _initCoroutine;
        private WorldObjectRegistry _registry;
        private int _satisfiedRequirementsCount;

        public override void OnStartServer()
        {
            base.OnStartServer();
            _initCoroutine = StartCoroutine(Server_InitializeWhenRegistryReady());
        }

        public override void OnStopServer()
        {
            if (_initCoroutine != null)
            {
                StopCoroutine(_initCoroutine);
                _initCoroutine = null;
            }

            if (_registry != null)
            {
                _registry.CellObjectChanged -= OnRegistryCellObjectChanged;
                _registry = null;
            }

            base.OnStopServer();
        }

        [Server]
        private IEnumerator Server_InitializeWhenRegistryReady()
        {
            while (NetworkServer.active)
            {
                _registry = GameInstaller.Resolve<WorldObjectRegistry>();
                if (_registry != null)
                {
                    Server_RebuildRequirements();
                    yield break;
                }

                yield return null;
            }
        }

        [Server]
        private void Server_RebuildRequirements()
        {
            _requirements.Clear();
            _requirementIndexesByCell.Clear();
            _satisfiedRequirementsCount = 0;

            if (_registry == null)
                return;

            var controllerCell = GetControllerCell();

            for (var i = 0; i < requiredObjects.Length; i++)
            {
                var requiredObject = requiredObjects[i];
                if (!requiredObject.RequiredDefinition)
                {
                    Debug.LogWarning(
                        $"[{nameof(SpaceshipController)}] Requirement #{i} has no {nameof(PlaceableObjectDefinitionSO)} configured.",
                        this);
                    continue;
                }

                var worldCell = controllerCell + requiredObject.LocalCellOffset;
                var requirementIndex = _requirements.Count;

                _requirements.Add(new RequirementRuntimeState(worldCell, requiredObject.RequiredDefinition));

                if (!_requirementIndexesByCell.TryGetValue(worldCell, out var indices))
                {
                    indices = new List<int>();
                    _requirementIndexesByCell[worldCell] = indices;
                }

                indices.Add(requirementIndex);
            }

            if (_requirements.Count == 0)
            {
                Debug.LogWarning($"[{nameof(SpaceshipController)}] No valid requirements configured.", this);
                IsAssemblyCompleted = false;
                return;
            }

            WarnAboutConflictingCellRequirements();

            _registry.CellObjectChanged -= OnRegistryCellObjectChanged;
            _registry.CellObjectChanged += OnRegistryCellObjectChanged;

            Server_ReevaluateAllRequirements();
        }

        [Server]
        private void Server_ReevaluateAllRequirements()
        {
            _satisfiedRequirementsCount = 0;

            foreach (var requirement in _requirements)
            {
                requirement.IsSatisfied = Server_IsRequirementSatisfied(requirement);
                if (requirement.IsSatisfied)
                    _satisfiedRequirementsCount++;
            }

            Server_UpdateAssemblyState();
        }

        [Server]
        private bool Server_IsRequirementSatisfied(RequirementRuntimeState requirement)
        {
            if (_registry == null || !_registry.TryGetObjectAtCell(requirement.WorldCell, out var placedObjectAtCell))
                return false;

            return placedObjectAtCell && placedObjectAtCell.Definition == requirement.RequiredDefinition;
        }

        [Server]
        private void OnRegistryCellObjectChanged(Vector2Int cell, PlacedWorldObject placedObjectAtCell)
        {
            if (!_requirementIndexesByCell.TryGetValue(cell, out var requirementIndices))
                return;

            for (var i = 0; i < requirementIndices.Count; i++)
            {
                var requirement = _requirements[requirementIndices[i]];
                var isSatisfiedNow =
                    placedObjectAtCell && placedObjectAtCell.Definition == requirement.RequiredDefinition;

                if (requirement.IsSatisfied == isSatisfiedNow)
                    continue;

                requirement.IsSatisfied = isSatisfiedNow;
                _satisfiedRequirementsCount += isSatisfiedNow ? 1 : -1;
            }

            Server_UpdateAssemblyState();
        }

        [Server]
        private void Server_UpdateAssemblyState()
        {
            var isCompletedNow = _requirements.Count > 0 && _satisfiedRequirementsCount == _requirements.Count;
            if (IsAssemblyCompleted == isCompletedNow)
                return;

            IsAssemblyCompleted = isCompletedNow;

            if (isCompletedNow)
            {
                Debug.Log($"[{nameof(SpaceshipController)}] Spaceship build is completed.", this);
                return;
            }

            if (logWhenAssemblyBecomesInvalid)
                Debug.Log($"[{nameof(SpaceshipController)}] Spaceship build is no longer completed.", this);
        }

        private Vector2Int GetControllerCell()
        {
            var position = transform.position;
            return new Vector2Int(Mathf.FloorToInt(position.x), Mathf.FloorToInt(position.y));
        }

        [Server]
        private void WarnAboutConflictingCellRequirements()
        {
            foreach (var (key, indices) in _requirementIndexesByCell)
            {
                if (indices.Count < 2)
                    continue;

                var firstDefinition = _requirements[indices[0]].RequiredDefinition;

                for (var i = 1; i < indices.Count; i++)
                {
                    if (_requirements[indices[i]].RequiredDefinition == firstDefinition)
                        continue;

                    Debug.LogWarning(
                        $"[{nameof(SpaceshipController)}] Conflicting requirements configured for cell {key}. " +
                        "One cell can contain only one placed object.",
                        this);

                    break;
                }
            }
        }

        [Serializable]
        private struct RequiredObjectSlot
        {
            [SerializeField] private Vector2Int localCellOffset;
            [SerializeField] private PlaceableObjectDefinitionSO requiredDefinition;

            public Vector2Int LocalCellOffset => localCellOffset;
            public PlaceableObjectDefinitionSO RequiredDefinition => requiredDefinition;
        }

        private sealed class RequirementRuntimeState
        {
            public readonly Vector2Int WorldCell;
            public readonly PlaceableObjectDefinitionSO RequiredDefinition;
            public bool IsSatisfied;

            public RequirementRuntimeState(Vector2Int worldCell, PlaceableObjectDefinitionSO requiredDefinition)
            {
                WorldCell = worldCell;
                RequiredDefinition = requiredDefinition;
            }
        }
    }
}