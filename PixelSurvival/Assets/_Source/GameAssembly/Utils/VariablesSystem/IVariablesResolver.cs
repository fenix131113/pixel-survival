namespace GameAssembly.Utils.VariablesSystem
{
    /// <typeparam name="T">Class Type of blocker type (like enum)</typeparam>
    /// <typeparam name="TV">Unblock callback Type</typeparam>
    /// <typeparam name="TVT">Block callback Type</typeparam>
    public interface IVariablesResolver<in T, in TV, in TVT>
    {
        public void RegisterCallbacks(TVT blockCallback, TV unblockCallback, T type);
        public void ClearCallbacksForGroup(T type);
        public void RegisterBlocker(IVariableBlocker<T> blocker);
        public void UnregisterBlocker(IVariableBlocker<T> blocker);
        public bool IsVariableBlocked(T type);
    }
}