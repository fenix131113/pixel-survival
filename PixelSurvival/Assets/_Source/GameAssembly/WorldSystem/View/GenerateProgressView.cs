using TMPro;
using UnityEngine;
using VContainer;

namespace GameAssembly.WorldSystem.View
{
    public class GenerateProgressView : MonoBehaviour
    {
        [SerializeField] private TMP_Text progressText;
        [SerializeField] private TMP_Text stageText;
        [SerializeField] private GameObject loadingScreenObject;
        [SerializeField] private string worldStageLabel = "World";
        [SerializeField] private string visualStageLabel = "Visual";
        [SerializeField] private string doneLabel = "Done";

        [Inject] private World _world;
        private WorldRenderer _worldRenderer;
        private bool _isVisualBuildInProgress;
        private float _worldProgress01;

        private void Awake()
        {
            if (_world == null)
            {
                Debug.LogError($"[{nameof(GenerateProgressView)}] {nameof(World)} is not injected.", this);
                enabled = false;
                return;
            }

            _world.Progress.ProgressChanged += ProgressOnProgressChanged;
            _worldRenderer = FindFirstObjectByType<WorldRenderer>();

            if (_worldRenderer)
            {
                _worldRenderer.InitialVisualBuildProgressChanged += OnInitialVisualBuildProgressChanged;
                _worldRenderer.InitialVisualBuildStateChanged += OnInitialVisualBuildStateChanged;
            }

            _worldProgress01 = _world.IsLoaded.Value ? 1f : 0f;
            SetStageText(IsLoadingCompleted() ? doneLabel : worldStageLabel);
            SetProgressText(_worldProgress01 >= 1f ? doneLabel : "0%");
            RefreshLoadingScreenState();
        }

        private void OnDestroy()
        {
            if (_world != null)
                _world.Progress.ProgressChanged -= ProgressOnProgressChanged;

            if (_worldRenderer)
            {
                _worldRenderer.InitialVisualBuildProgressChanged -= OnInitialVisualBuildProgressChanged;
                _worldRenderer.InitialVisualBuildStateChanged -= OnInitialVisualBuildStateChanged;
            }
        }

        private void ProgressOnProgressChanged(object sender, float progress01)
        {
            _worldProgress01 = Mathf.Clamp01(progress01);

            if (_isVisualBuildInProgress)
            {
                RefreshLoadingScreenState();
                return;
            }

            SetStageText(IsLoadingCompleted() ? doneLabel : worldStageLabel);
            SetProgressText(IsLoadingCompleted() ? doneLabel : Mathf.RoundToInt(_worldProgress01 * 100f) + "%");
            RefreshLoadingScreenState();
        }

        private void OnInitialVisualBuildProgressChanged(float progress01)
        {
            if (!_isVisualBuildInProgress)
                return;

            SetStageText(visualStageLabel);
            SetProgressText(Mathf.RoundToInt(Mathf.Clamp01(progress01) * 100f) + "%");
            RefreshLoadingScreenState();
        }

        private void OnInitialVisualBuildStateChanged(bool isInProgress)
        {
            _isVisualBuildInProgress = isInProgress;

            if (isInProgress)
            {
                SetStageText(visualStageLabel);
                SetProgressText("0%");
            }
            else if (IsLoadingCompleted())
            {
                SetStageText(doneLabel);
                SetProgressText(doneLabel);
            }

            RefreshLoadingScreenState();
        }

        private bool IsLoadingCompleted()
        {
            var isWorldCompleted = Mathf.Approximately(_worldProgress01, 1f);
            var isVisualCompleted = !_worldRenderer || _worldRenderer.IsInitialVisualBuildCompleted;
            return isWorldCompleted && isVisualCompleted && !_isVisualBuildInProgress;
        }

        private void RefreshLoadingScreenState()
        {
            if (!loadingScreenObject)
                return;

            loadingScreenObject.SetActive(!IsLoadingCompleted());
        }

        private void SetProgressText(string text)
        {
            if (progressText)
                progressText.text = text;
        }

        private void SetStageText(string text)
        {
            if (stageText)
                stageText.text = text;
        }
    }
}
