using System.IO;
using UnityEngine;

namespace GameAssembly.Utils
{
    public class NoiseVisualizer : MonoBehaviour
    {
        [Header("Texture size")] public int width = 256;
        public int height = 256;

        [Header("Noise parameters")] public int seed = 52;
        public float scale = 0.05f;

        public string fileName = "BiomeNoise.png";

        [ContextMenu("Generate Noise")]
        private void Start()
        {
            var tex = GenerateNoiseTexture();
            SaveTexture(tex);
            Debug.Log("Noise saved to " + fileName);
        }

        Texture2D GenerateNoiseTexture()
        {
            var tex = new Texture2D(width, height);
            var colors = new Color[width * height];

            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var nx = x * scale + seed * 0.00001f;
                    var ny = y * scale + seed * 0.00001f;

                    var n = Mathf.PerlinNoise(nx, ny);
                    n = Mathf.Clamp01(n);

                    colors[y * width + x] = new Color(n, n, n);
                }
            }

            tex.SetPixels(colors);
            tex.Apply();
            return tex;
        }

        void SaveTexture(Texture2D tex)
        {
            var bytes = tex.EncodeToPNG();
            var path = Path.Combine(Application.dataPath, fileName);
            File.WriteAllBytes(path, bytes);
            Debug.Log("Saved PNG to: " + path);
        }
    }
}