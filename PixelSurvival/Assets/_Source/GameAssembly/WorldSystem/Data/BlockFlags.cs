namespace GameAssembly.WorldSystem.Data
{
    [System.Flags]
    public enum BlockFlags : byte
    {
        NONE = 0,
        SOLID = 1 << 0,
        BREAKABLE = 1 << 1,
    }
}