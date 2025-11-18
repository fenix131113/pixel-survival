using GameAssembly.Utils;
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

        private void Start()
        {
            _netManager = NetworkManager.singleton as NetManager;
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
            hostPanel.SetActive(true);
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
            hostPanel.SetActive(false);
            ClearPlayersList();
        }

        private void Client_OnConnected()
        {
            hostPanel.SetActive(true);
            ClearPlayersList();
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