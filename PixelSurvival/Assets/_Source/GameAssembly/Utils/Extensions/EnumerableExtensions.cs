using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace GameAssembly.Utils.Extensions
{
    public static class EnumerableExtensions
    {
        public static T GetRandomElement<T>(this IEnumerable<T> source)
        {
            GetRandom(source, out var item, out _, out _);
            return item;
        }

        public static int GetRandomIndex<T>(this IEnumerable<T> source)
        {
            GetRandom(source, out _, out var index, out _);
            return index;
        }

        public static T GetRandomElement<T>(this IEnumerable<T> source, out T[] asArray)
        {
            GetRandom(source, out var item, out _, out var array);
            asArray = array;
            return item;
        }

        public static int GetRandomIndex<T>(this IEnumerable<T> source, out T[] asArray)
        {
            GetRandom(source, out _, out var index, out var array);
            asArray = array;
            return index;
        }

        private static void GetRandom<T>(IEnumerable<T> source, out T item, out int index, out T[] asArray)
        {
            asArray = source.ToArray();
            
            if (asArray.Length == 0)
            {
                item = default;
                index = -1;
            }
            else
            {
                item = asArray[Random.Range(0, asArray.Length)];
                index = Random.Range(0, asArray.Length);
            }
        }
    }
}