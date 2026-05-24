using System;
using System.Collections.Generic;
using GameAssembly.Core;
using GameAssembly.Core.Network;
using GameAssembly.PlayerSystem.Data;
using GameAssembly.PlayerSystem.Variables;
using GameAssembly.UiSystem.Data;
using GameAssembly.Utils;
using GameAssembly.Utils.VariablesSystem;
using Mirror;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using VContainer;

namespace GameAssembly.UiSystem
{
    public class EscMenuView : MonoBehaviour, IUiMenu
    {
        [SerializeField] private GameObject escMenuPanel;
        [SerializeField] private GameObject settingsPanel;

        [Header("Buttons")]
        [SerializeField] private Button resumeButton;
        [SerializeField] private Button settingsButton;
        [SerializeField] private Button exitToMenuButton;

        [SerializeField] private MenuType menuType = MenuType.ESC_MENU;
        [SerializeField] private MenuType[] allowedMenuTypesOnTop = Array.Empty<MenuType>();

        [Inject] private IVariablesResolver<PlayerVariableBlockerType, Action, Action> _variablesResolver;

        private readonly IVariableBlocker<PlayerVariableBlockerType> _escMenuBlocker =
            new PlayerVariableBlocker(PlayerVariableBlockerType.MOVEMENT, PlayerVariableBlockerType.BUILD,
                PlayerVariableBlockerType.ATTACK, PlayerVariableBlockerType.INTERACT, PlayerVariableBlockerType.LOOK);

        public event Action OnMenuCanceled;

        private void Start() => Bind();

        private void OnDestroy() => Expose();

        public void Open()
        {
            SetSettingsActive(false);

            if (escMenuPanel)
                escMenuPanel.SetActive(true);

            ResolveVariablesResolver()?.RegisterBlocker(_escMenuBlocker);
        }

        public void Close()
        {
            SetSettingsActive(false);

            if (escMenuPanel)
                escMenuPanel.SetActive(false);

            _escMenuBlocker.Dispose();
        }

        public void Cancel()
        {
            Close();
            OnMenuCanceled?.Invoke();
        }

        public bool IsOpen() => escMenuPanel && escMenuPanel.activeSelf;

        public MenuType GetMenuType() => menuType;

        public IReadOnlyCollection<MenuType> GetAllowedMenuTypesOnTop() => allowedMenuTypesOnTop ?? Array.Empty<MenuType>();

        private void OnResumeClicked()
        {
            UiManager.Instance?.CloseRequest(this);
        }

        private void OnSettingsClicked()
        {
            if (!settingsPanel)
                return;

            SetSettingsActive(!settingsPanel.activeSelf);
        }

        private void OnExitToMenuClicked()
        {
            UiManager.Instance?.Client_CloseAllOpenedMenus(includeEscMenu: true);

            if (NetworkManager.singleton is NetManager netManager)
            {
                netManager.LeaveRoom();
                return;
            }

            if (NetworkServer.active && NetworkClient.active)
                NetworkManager.singleton?.StopHost();
            else if (NetworkClient.active)
                NetworkManager.singleton?.StopClient();
            else if (NetworkServer.active)
                NetworkManager.singleton?.StopServer();

            SceneManager.LoadScene(ScenesData.MENU_SCENE_INDEX);
        }

        private void SetSettingsActive(bool isActive)
        {
            if (settingsPanel)
                settingsPanel.SetActive(isActive);
        }

        private IVariablesResolver<PlayerVariableBlockerType, Action, Action> ResolveVariablesResolver()
        {
            if (_variablesResolver != null)
                return _variablesResolver;

            _variablesResolver = GameInstaller.Resolve<IVariablesResolver<PlayerVariableBlockerType, Action, Action>>();
            return _variablesResolver;
        }
        
        private void Bind()
        {
            if (resumeButton)
                resumeButton.onClick.AddListener(OnResumeClicked);

            if (settingsButton)
                settingsButton.onClick.AddListener(OnSettingsClicked);

            if (exitToMenuButton)
                exitToMenuButton.onClick.AddListener(OnExitToMenuClicked);
        }

        private void Expose()
        {
            if (resumeButton)
                resumeButton.onClick.RemoveListener(OnResumeClicked);

            if (settingsButton)
                settingsButton.onClick.RemoveListener(OnSettingsClicked);

            if (exitToMenuButton)
                exitToMenuButton.onClick.RemoveListener(OnExitToMenuClicked);
        }
    }
}
