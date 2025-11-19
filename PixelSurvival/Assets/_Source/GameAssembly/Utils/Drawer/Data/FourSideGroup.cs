using System;
using UnityEngine;

namespace GameAssembly.Utils.Drawer.Data
{
    [Serializable]
    public class FourSideGroup
    {
        /// Right(0) -> Down(1) -> Left(2) -> Up(3)
        [field: SerializeField] public Sprite[] Rotations { get; private set; }
    }
}