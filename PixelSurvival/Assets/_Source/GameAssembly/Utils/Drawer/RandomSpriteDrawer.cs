using GameAssembly.Utils.Extensions;
using UnityEngine;

namespace GameAssembly.Utils.Drawer
{
    public class RandomSpriteDrawer : MonoBehaviour, ISingleSpriteDrawer
    {
        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField] private Sprite[] sprites;
        [SerializeField] private bool drawFromStart;

        private void Start()
        {
            if(drawFromStart)
                Draw();
        }

        public void Draw(Sprite sprite = null)
        {
            spriteRenderer.sprite = sprites.GetRandomElement();
        }
    }
}