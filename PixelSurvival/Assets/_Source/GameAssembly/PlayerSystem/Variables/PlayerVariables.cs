using System;
using System.Collections.Generic;
using System.Linq;
using GameAssembly.PlayerSystem.Data;
using GameAssembly.Utils.VariablesSystem;

namespace GameAssembly.PlayerSystem.Variables
{
    public class PlayerVariables : IVariablesResolver<PlayerVariableBlockerType, Action, Action>
    {
        private readonly Dictionary<PlayerVariableBlockerType, List<IVariableBlocker<PlayerVariableBlockerType>>>
            _blockers = new();

        private readonly Dictionary<PlayerVariableBlockerType, List<Action>> _blockCallbacks = new();
        private readonly Dictionary<PlayerVariableBlockerType, List<Action>> _unblockCallbacks = new();

        public void RegisterCallbacks(Action blockCallback, Action unblockCallback, PlayerVariableBlockerType type)
        {
            _blockCallbacks.TryAdd(type, new List<Action>());
            _unblockCallbacks.TryAdd(type, new List<Action>());

            if (blockCallback != null && !_blockCallbacks[type].Contains(blockCallback))
                _blockCallbacks[type].Add(blockCallback);
            if (unblockCallback != null && !_unblockCallbacks[type].Contains(unblockCallback))
                _unblockCallbacks[type].Add(unblockCallback);
        }

        public void ClearCallbacksForGroup(PlayerVariableBlockerType type)
        {
            if (_unblockCallbacks.TryGetValue(type, out var unblockCallback))
                unblockCallback.Clear();

            if (_blockCallbacks.TryGetValue(type, out var blockCallbacks))
                blockCallbacks.Clear();
        }

        public void RegisterBlocker(IVariableBlocker<PlayerVariableBlockerType> blocker)
        {
            foreach (var type in blocker.BlockTypes)
            {
                if (_blockers.TryAdd(type, new List<IVariableBlocker<PlayerVariableBlockerType>>()))
                    if (_blockCallbacks.TryGetValue(type, out var blockCallbacks))
                        blockCallbacks.ForEach(x => x?.Invoke());

                if (!_blockers[type].Contains(blocker))
                {
                    _blockers[type].Add(blocker);
                    blocker.OnDispose += UnregisterBlocker;
                }
            }
        }

        public void UnregisterBlocker(IVariableBlocker<PlayerVariableBlockerType> blocker)
        {
            foreach (var type in blocker.BlockTypes)
            {
                if (_blockers.TryGetValue(type, out var blockers))
                    blockers.Remove(blocker);
            }

            CheckBlockers();
        }

        private void CheckBlockers()
        {
            foreach (var blocker in _blockers.Where(blocker => blocker.Value.Count == 0).ToList())
            {
                if (_unblockCallbacks.TryGetValue(blocker.Key, out var unblockCallbacks))
                    unblockCallbacks.ForEach(x => x?.Invoke());

                _blockers.Remove(blocker.Key);
            }
        }

        public bool IsVariableBlocked(PlayerVariableBlockerType type) =>
            _blockers.ContainsKey(type) && _blockers[type].Count > 0;
    }
}