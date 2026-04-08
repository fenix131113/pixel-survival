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
        [SerializeField] private Button joinButton;
        [Header("Host")] [SerializeField] private Button selectHostButton;
        [SerializeField] private Button startHostButton;
        [SerializeField] private Button leaveHostButton;
        [SerializeField] private GameObject hostPanel;
        [SerializeField] private TMP_Text lobbyPlayersListLabel;

        private NetManager _netManager;
        private MenuPanelAnimator _hostPanelAnimator;

        private void Start()
        {
            _netManager = NetworkManager.singleton as NetManager;
            SetupPanels();
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
            _netManager.CreateHost();
            ShowHostPanel();
            startHostButton.gameObject.SetActive(true);
        }

        private void OnJoinButtonClicked()
        {
            _netManager.RegisterLobbyMessages();
            _netManager.JoinRoom("kcp://localhost:7777");
        }

        private void OnLeaveButtonClicked()
        {
            if (NetworkServer.active && NetworkClient.active)
                _netManager.StopHost();
            else if (NetworkClient.active)
                NetworkClient.Disconnect();
        }

        private void Client_OnDisconnected()
        {
            startHostButton.gameObject.SetActive(false);
            HideHostPanel();
            ClearPlayersList();
        }

        private void Client_OnConnected()
        {
            ShowHostPanel();
            ClearPlayersList();
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

            lobbyPlayersListLabel.text = msg.PlayersList;
        }

        private void ClearPlayersList()
        {
            lobbyPlayersListLabel.text = string.Empty;
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
        }

        private void Expose()
        {
            selectHostButton.onClick.RemoveAllListeners();
            startHostButton.onClick.RemoveAllListeners();
            joinButton.onClick.RemoveAllListeners();
            leaveHostButton.onClick.RemoveAllListeners();
        }
    }
}
