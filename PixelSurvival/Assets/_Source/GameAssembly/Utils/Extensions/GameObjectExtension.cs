using UnityEngine;

namespace GameAssembly.Utils.Extensions
{
    public static class GameObjectExtension
    {
        public static bool GetComponentInAnyParent<T>(this GameObject obj, out T result) where T : Component
        {
            if (!obj)
            {
                result = null;
                return false;
            }

            var lastChecked = obj;
            result = obj.GetComponent<T>();
            
            if (obj.GetComponent<T>())
                return true;

            while (lastChecked.transform.parent)
            {
                lastChecked = lastChecked.transform.parent.gameObject;
                result = lastChecked.transform.GetComponent<T>();
            
                if (result)
                    return true;
            }

            return false;
        }
    }
}