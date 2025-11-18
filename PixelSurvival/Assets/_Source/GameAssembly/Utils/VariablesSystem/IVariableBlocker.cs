using System;
using System.Collections.Generic;

namespace GameAssembly.Utils.VariablesSystem
{
    public interface IVariableBlocker<out T> : IDisposable
    {
        event Action<IVariableBlocker<T>> OnDispose;
        public IReadOnlyCollection<T> BlockTypes { get; }
    }
}