using GameAssembly.Utils;
using System.Collections;
using EpicTransport;
using GameAssembly.MainMenuSystem;
using Mirror;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GameAssembly.Core.Network.View
{
    public class MenuNetworkView : MonoBehaviour
    {
        [Header("Join")]
        [SerializeField] private Button joinButton;
        [SerializeField] private TMP_InputField joinCodeInput;
        [Header("Host")] [SerializeField] private Button selectHostButton;
        [SerializeField] private Button startHostButton;
        [SerializeField] private Button leaveHostButton;
        [SerializeField] private GameObject hostPanel;
        [SerializeField] private TMP_Text lobbyPlayersListLabel;
        [Header("Other")] [SerializeField] private GameObject blocker;
        [SerializeField] private Button exitButton;

        private NetManager _netManager;
        private MenuPanelAnimator _hostPanelAnimator;
        private string _lobbyCode;
        private Coroutine _waitForEosInitializationRoutine;

        private void Start()
        {
            _netManager = NetworkManager.singleton as NetManager;
            SetupPanels();
            SetupBlocker();

            if (joinButton)
            {
                joinButton.interactable = true;
            }
#if !UNITY_SERVER
            BindClient();
#endif
        }

        private void OnDestroy()
        {
            if (_waitForEosInitializationRoutine != null)
                StopCoroutine(_waitForEosInitializationRoutine);

#if !UNITY_SERVER
            Expose();
#endif
        }

        private void OnStartGameButtonClicked()
        {
            if (NetworkServer.active && !NetworkServer.isLoadingScene)
                _netManager.ServerChangeScene(ScenesData.GAME_SCENE_NAME);
        }

        private void OnSelectHostButtonClicked()
        {
            blocker.gameObject.SetActive(true);
            _netManager.RegisterLobbyMessages();
            _netManager.CreateHost();
            startHostButton.gameObject.SetActive(true);
        }

        private void OnJoinButtonClicked()
        {
            var joinCode = ReadJoinCodeInput();
            if (string.IsNullOrWhiteSpace(joinCode))
            {
                OnLobbyOperationFailed("Enter join code before connecting.");
                return;
            }

            _netManager.RegisterLobbyMessages();
            _netManager.JoinRoomByCode(joinCode);
        }

        private void OnLeaveButtonClicked()
        {
            _netManager.LeaveRoom();
        }

        private void Client_OnDisconnected()
        {
            _lobbyCode = string.Empty;
            startHostButton.gameObject.SetActive(false);
            HideHostPanel();
            ClearPlayersList();
        }

        private void Client_OnConnected()
        {
            _lobbyCode = _netManager.CurrentLobbyCode;
            ShowHostPanel();
            ClearPlayersList();
        }

        private void OnLobbyCodeReady(string lobbyCode)
        {
            _lobbyCode = lobbyCode;

            blocker.gameObject.SetActive(false);
            ShowHostPanel();
            ClearPlayersList();
        }

        private void OnLobbyOperationFailed(string errorMessage)
        {
            Debug.LogError($"Error while lobby creation: {errorMessage}");
            blocker.gameObject.SetActive(false);
        }

        private void SetupPanels()
        {
            if (!hostPanel)
                return;

            _hostPanelAnimator = hostPanel.GetComponent<MenuPanelAnimator>();
            if (!_hostPanelAnimator)
                _hostPanelAnimator = hostPanel.AddComponent<MenuPanelAnimator>();

            _hostPanelAnimator.SetVisibleImmediate(hostPanel.activeSelf);
        }

        private void ShowHostPanel()
        {
            if (_hostPanelAnimator)
            {
                _hostPanelAnimator.Show();
                return;
            }

            if (hostPanel)
                hostPanel.SetActive(true);
        }

        private void HideHostPanel()
        {
            if (_hostPanelAnimator)
            {
                _hostPanelAnimator.Hide();
                return;
            }

            if (hostPanel)
                hostPanel.SetActive(false);
        }

        private void UpdatePlayersList(NetManager.LobbyPlayerChangedMessage msg)
        {
            ClearPlayersList();

            if (!NetworkClient.active)
                return;

            var codeText = string.IsNullOrWhiteSpace(_lobbyCode) ? string.Empty : $"Code: {_lobbyCode}\n";
            lobbyPlayersListLabel.text = codeText + msg.PlayersList;
        }

        private void ClearPlayersList()
        {
            lobbyPlayersListLabel.text = string.IsNullOrWhiteSpace(_lobbyCode) ? string.Empty : $"Code: {_lobbyCode}";
        }

        private string ReadJoinCodeInput()
        {
            return joinCodeInput ? joinCodeInput.text : string.Empty;
        }

        private void SetupBlocker()
        {
            if (!blocker)
                return;

            var eosSdkComponent = FindFirstObjectByType<EOSSDKComponent>();
            if (eosSdkComponent != null && EOSSDKComponent.Initialized)
            {
                blocker.SetActive(false);
                return;
            }

            blocker.SetActive(true);
            _waitForEosInitializationRoutine = StartCoroutine(WaitForEosInitialization());
        }

        private void OnExitButtonClicked() => Application.Quit();

        private IEnumerator WaitForEosInitialization()
        {
            while (true)
            {
                var eosSdkComponent = FindFirstObjectByType<EOSSDKComponent>();
                if (eosSdkComponent && EOSSDKComponent.Initialized)
                    break;

                yield return null;
            }

            if (blocker)
                blocker.SetActive(false);

            _waitForEosInitializationRoutine = null;
        }

        private void BindClient()
        {
            selectHostButton.onClick.AddListener(OnSelectHostButtonClicked);
            startHostButton.onClick.AddListener(OnStartGameButtonClicked);
            joinButton.onClick.AddListener(OnJoinButtonClicked);
            leaveHostButton.onClick.AddListener(OnLeaveButtonClicked);
            exitButton.onClick.AddListener(OnExitButtonClicked);

            // Auto-expose
            _netManager.ClientOnChangedLobbyPlayer += UpdatePlayersList;
            _netManager.ClientOnDisconnected += Client_OnDisconnected;
            _netManager.ClientOnConnected += Client_OnConnected;
            _netManager.LobbyCodeReady += OnLobbyCodeReady;
            _netManager.LobbyOperationFailed += OnLobbyOperationFailed;
        }

        private void Expose()
        {
            selectHostButton.onClick.RemoveAllListeners();
            startHostButton.onClick.RemoveAllListeners();
            joinButton.onClick.RemoveAllListeners();
            leaveHostButton.onClick.RemoveAllListeners();
            exitButton.onClick.RemoveAllListeners();

            if (_netManager == null)
                return;

            _netManager.ClientOnChangedLobbyPlayer -= UpdatePlayersList;
            _netManager.ClientOnDisconnected -= Client_OnDisconnected;
            _netManager.ClientOnConnected -= Client_OnConnected;
            _netManager.LobbyCodeReady -= OnLobbyCodeReady;
            _netManager.LobbyOperationFailed -= OnLobbyOperationFailed;
        }
    }
}
