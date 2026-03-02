using TMPro;
using UnityEngine;
using VContainer;
using Exception = System.Exception;

namespace GameAssembly.WorldSystem.View
{
    public class GenerateProgressView : MonoBehaviour
    {
        [SerializeField] private TMP_Text progressText;

        [Inject] private World _world;

        private void Awake()
        {
            _world.Progress.ProgressChanged += ProgressOnProgressChanged;
            progressText.text = "0%";

            progressText.text = "Done";
        }

        private void OnDestroy()
        {
            _world.Progress.ProgressChanged -= ProgressOnProgressChanged;
        }

        private void ProgressOnProgressChanged(object sender, float e)
        {
            progressText.text = Mathf.RoundToInt(e * 100f) + "%";
        }
    }
}