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

        private async void Awake()
        {
            try
            {
                _world.Progress.ProgressChanged += ProgressOnProgressChanged;
                progressText.text = "0%";

                await _world.GenerateWorldAsync();

                progressText.text = "Done";
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
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