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
        private WorldRenderer _worldRenderer;
        private bool _isVisualBuildInProgress;

        private void Awake()
        {
            _world.Progress.ProgressChanged += ProgressOnProgressChanged;
            _worldRenderer = FindFirstObjectByType<WorldRenderer>();

            if (_worldRenderer)
            {
                _worldRenderer.InitialVisualBuildProgressChanged += OnInitialVisualBuildProgressChanged;
                _worldRenderer.InitialVisualBuildStateChanged += OnInitialVisualBuildStateChanged;
            }

            progressText.text = "0%";
        }

        private void OnDestroy()
        {
            _world.Progress.ProgressChanged -= ProgressOnProgressChanged;

            if (_worldRenderer)
            {
                _worldRenderer.InitialVisualBuildProgressChanged -= OnInitialVisualBuildProgressChanged;
                _worldRenderer.InitialVisualBuildStateChanged -= OnInitialVisualBuildStateChanged;
            }
        }

        private void ProgressOnProgressChanged(object sender, float e)
        {
            if (_isVisualBuildInProgress)
                return;

            progressText.text = Mathf.RoundToInt(e * 100f) + "%";

            if (Mathf.Approximately(e, 1f) && (!_worldRenderer || _worldRenderer.IsInitialVisualBuildCompleted))
                progressText.text = "Done";
        }

        private void OnInitialVisualBuildProgressChanged(float progress01)
        {
            if (!_isVisualBuildInProgress)
                return;

            progressText.text = "Visual " + Mathf.RoundToInt(progress01 * 100f) + "%";
        }

        private void OnInitialVisualBuildStateChanged(bool isInProgress)
        {
            _isVisualBuildInProgress = isInProgress;
            progressText.text = isInProgress ? "Visual 0%" : "Done";
        }
    }
}
