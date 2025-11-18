using System;
using System.Collections.Generic;
using System.Linq;
using GameAssembly.PlayerSystem.Data;
using GameAssembly.Utils.VariablesSystem;

namespace GameAssembly.PlayerSystem.Variables
{
    public class PlayerVariableBlocker : IVariableBlocker<PlayerVariableBlockerType>
    {
        private readonly List<PlayerVariableBlockerType> _blockTypes;

        public event Action<IVariableBlocker<PlayerVariableBlockerType>> OnDispose;
        public IReadOnlyCollection<PlayerVariableBlockerType> BlockTypes => _blockTypes;

        public PlayerVariableBlocker(params PlayerVariableBlockerType[] blockTypes) =>
            _blockTypes = blockTypes.ToList();

        public void Dispose()
        {
            OnDispose?.Invoke(this);
            OnDispose = null;
        }
    }
}