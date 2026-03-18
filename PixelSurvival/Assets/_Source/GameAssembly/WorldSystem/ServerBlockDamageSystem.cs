using System;
using System.Collections.Generic;
using GameAssembly.WorldSystem.Data;
using Mirror;
using UnityEngine;

namespace GameAssembly.WorldSystem
{
    public readonly struct BlockDamageSnapshot
    {
        public readonly Vector2Int BlockWorldPos;
        public readonly int CurrentDamage;
        public readonly int MaxHealth;
        public readonly float Progress01;

        public BlockDamageSnapshot(Vector2Int blockWorldPos, int currentDamage, int maxHealth)
        {
            BlockWorldPos = blockWorldPos;
            CurrentDamage = currentDamage;
            MaxHealth = maxHealth;
            Progress01 = maxHealth <= 0 ? 0f : Mathf.Clamp01(currentDamage / (float)maxHealth);
        }
    }

    public readonly struct BlockDamageApplyResult
    {
        public static readonly BlockDamageApplyResult _none = new(false, false, null, 0, 0);

        public readonly bool IsDamageApplied;
        public readonly bool IsBlockBroken;
        public readonly BlockDefinitionSO BlockDefinition;
        public readonly int CurrentDamage;
        public readonly int MaxHealth;

        public float Progress01 => MaxHealth <= 0 ? 0f : Mathf.Clamp01(CurrentDamage / (float)MaxHealth);

        private BlockDamageApplyResult(bool isDamageApplied, bool isBlockBroken, BlockDefinitionSO blockDefinition,
            int currentDamage, int maxHealth)
        {
            IsDamageApplied = isDamageApplied;
            IsBlockBroken = isBlockBroken;
            BlockDefinition = blockDefinition;
            CurrentDamage = currentDamage;
            MaxHealth = maxHealth;
        }

        public static BlockDamageApplyResult Applied(BlockDefinitionSO definition, int currentDamage, int maxHealth)
        {
            return new BlockDamageApplyResult(true, false, definition, currentDamage, maxHealth);
        }

        public static BlockDamageApplyResult Broken(BlockDefinitionSO definition, int maxHealth)
        {
            return new BlockDamageApplyResult(true, true, definition, maxHealth, maxHealth);
        }
    }

    public class ServerBlockDamageSystem
    {
        private sealed class BlockDamageState
        {
            public readonly HashSet<uint> Contributors = new();

            public BlockDefinitionSO Definition;
            public int CurrentDamage;
            public float LastDamageTime;

            public BlockDamageState(BlockDefinitionSO definition, float lastDamageTime)
            {
                Definition = definition;
                LastDamageTime = lastDamageTime;
            }
        }

        private readonly Dictionary<Vector2Int, BlockDamageState> _damageStates = new();
        private readonly Dictionary<uint, Vector2Int> _activeTargetsByPlayer = new();

        public float ResetDelaySeconds { get; set; } = 4f;

        public event Action<BlockDamageSnapshot> OnBlockDamageChanged;
        public event Action<Vector2Int> OnBlockDamageCleared;

        [Server]
        public BlockDamageApplyResult Server_ApplyPlayerDamage(World world, uint playerNetId, Vector2Int blockWorldPos,
            int damage)
        {
            if (playerNetId == 0 || damage <= 0)
                return BlockDamageApplyResult._none;

            var now = Time.time;
            HandlePlayerTargetChange(playerNetId, blockWorldPos, now);

            _activeTargetsByPlayer[playerNetId] = blockWorldPos;

            return Server_ApplyDamageInternal(world, blockWorldPos, damage, now, playerNetId);
        }

        [Server]
        public BlockDamageApplyResult Server_ApplyWorldDamage(World world, Vector2Int blockWorldPos, int damage)
        {
            if (damage <= 0)
                return BlockDamageApplyResult._none;

            return Server_ApplyDamageInternal(world, blockWorldPos, damage, Time.time, 0);
        }

        public void Server_ClearPlayerTarget(uint playerNetId)
        {
            if (!_activeTargetsByPlayer.Remove(playerNetId, out var previousTarget))
                return;

            if (_damageStates.TryGetValue(previousTarget, out var previousState))
                previousState.Contributors.Remove(playerNetId);
        }

        public void Server_Tick()
        {
            if (_damageStates.Count == 0)
                return;

            var now = Time.time;
            List<Vector2Int> expired = null;

            foreach (var pair in _damageStates)
            {
                if (!IsExpired(pair.Value, now))
                    continue;

                expired ??= new List<Vector2Int>();
                expired.Add(pair.Key);
            }

            if (expired == null)
                return;

            foreach (var blockWorldPos in expired)
                ClearState(blockWorldPos, true);
        }

        [Server]
        private BlockDamageApplyResult Server_ApplyDamageInternal(World world, Vector2Int blockWorldPos, int damage,
            float now, uint contributorPlayerNetId)
        {
            if (world == null || damage <= 0)
                return BlockDamageApplyResult._none;

            if (_damageStates.TryGetValue(blockWorldPos, out var oldState) && IsExpired(oldState, now))
                ClearState(blockWorldPos, true);

            var chunk = world.GetChunkByWorldPosition(blockWorldPos.x, blockWorldPos.y);
            if (chunk == null)
                return BlockDamageApplyResult._none;

            var localIndexes = World.ConvertWorldToChunkSpace(blockWorldPos.x, blockWorldPos.y);
            var cell = chunk.GetCell(localIndexes.x, localIndexes.y);
            var block = cell.Block;

            if (block.type == BlockType.AIR || !block.IsBreakable || !block.definition)
                return BlockDamageApplyResult._none;

            var maxHealth = Mathf.Max(1, block.definition.Health);

            if (!_damageStates.TryGetValue(blockWorldPos, out var state))
            {
                state = new BlockDamageState(block.definition, now);
                _damageStates[blockWorldPos] = state;
            }
            else if (state.Definition != block.definition)
            {
                state.Definition = block.definition;
                state.CurrentDamage = 0;
                state.Contributors.Clear();
                state.LastDamageTime = now;
            }

            if (contributorPlayerNetId != 0)
                state.Contributors.Add(contributorPlayerNetId);

            state.CurrentDamage += damage;
            state.LastDamageTime = now;

            if (state.CurrentDamage >= maxHealth)
            {
                ClearState(blockWorldPos, true);
                return BlockDamageApplyResult.Broken(block.definition, maxHealth);
            }

            var snapshot = new BlockDamageSnapshot(blockWorldPos, state.CurrentDamage, maxHealth);
            OnBlockDamageChanged?.Invoke(snapshot);

            return BlockDamageApplyResult.Applied(block.definition, state.CurrentDamage, maxHealth);
        }

        private void HandlePlayerTargetChange(uint playerNetId, Vector2Int blockWorldPos, float now)
        {
            if (!_activeTargetsByPlayer.TryGetValue(playerNetId, out var previousTarget) || previousTarget == blockWorldPos)
                return;

            if (!_damageStates.TryGetValue(previousTarget, out var previousState))
                return;

            if (IsExpired(previousState, now))
            {
                ClearState(previousTarget, true);
                return;
            }

            previousState.Contributors.Remove(playerNetId);

            // Keep one active mining target per player. If nobody else mines previous target,
            // its progress is reset immediately.
            if (previousState.Contributors.Count == 0)
                ClearState(previousTarget, true);
        }

        private bool IsExpired(BlockDamageState state, float now)
        {
            return now - state.LastDamageTime >= ResetDelaySeconds;
        }

        private void ClearState(Vector2Int blockWorldPos, bool notifyClients)
        {
            if (!_damageStates.Remove(blockWorldPos))
                return;

            RemovePlayersTargeting(blockWorldPos);

            if (notifyClients)
                OnBlockDamageCleared?.Invoke(blockWorldPos);
        }

        private void RemovePlayersTargeting(Vector2Int blockWorldPos)
        {
            if (_activeTargetsByPlayer.Count == 0)
                return;

            List<uint> toRemove = null;

            foreach (var pair in _activeTargetsByPlayer)
            {
                if (pair.Value != blockWorldPos)
                    continue;

                toRemove ??= new List<uint>();
                toRemove.Add(pair.Key);
            }

            if (toRemove == null)
                return;

            foreach (var playerNetId in toRemove)
                _activeTargetsByPlayer.Remove(playerNetId);
        }
    }
}
