using UnityEngine;

namespace GameAssembly.ObjectsSystem.View
{
    [DisallowMultipleComponent]
    public class GrassReactiveMarker : MonoBehaviour
    {
        [field: SerializeField] public SpriteRenderer TargetRenderer { get; private set; }
        [field: SerializeField] public Collider2D InteractionCollider { get; private set; }

        private void Reset()
        {
            if (!TargetRenderer)
                TargetRenderer = GetComponent<SpriteRenderer>();

            if (!InteractionCollider)
                InteractionCollider = GetComponent<Collider2D>();
        }
    }
}
