using Mirror;
using UnityEngine;

namespace GameAssembly.Utils.Drawer
{
    public class SingleSpriteDrawer : NetworkBehaviour, ISingleSpriteDrawer
    {
        [SerializeField] private SpriteRenderer spriteRenderer;
        
        public void Draw(Sprite sprite) => spriteRenderer.sprite = sprite;
    }
}