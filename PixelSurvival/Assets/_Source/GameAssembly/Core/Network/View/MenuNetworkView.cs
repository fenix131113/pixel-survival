using GameAssembly.Utils;
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

        private NetManager _netManager;
        private MenuPanelAnimator _hostPanelAnimator;
        private string _lobbyCode;

        private void Start()
        {
            _netManager = NetworkManager.singleton as NetManager;
            SetupPanels();

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
            _netManager.RegisterLobbyMessages();
            _netManager.CreateHost(ReadJoinCodeInput());
            ShowHostPanel();
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

            if (joinCodeInput)
            {
                joinCodeInput.text = lobbyCode;
            }

            ClearPlayersList();
        }

        private void OnLobbyOperationFailed(string errorMessage)
        {
            lobbyPlayersListLabel.text = errorMessage;
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

        private void BindClient()
        {
            selectHostButton.onClick.AddListener(OnSelectHostButtonClicked);
            startHostButton.onClick.AddListener(OnStartGameButtonClicked);
            joinButton.onClick.AddListener(OnJoinButtonClicked);
            leaveHostButton.onClick.AddListener(OnLeaveButtonClicked);

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
