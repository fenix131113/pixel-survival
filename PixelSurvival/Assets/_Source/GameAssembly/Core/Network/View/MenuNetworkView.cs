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
            //TODO: LOGIC TO START GAME
        }

        private void OnSelectHostButtonClicked()
        {
            _netManager.RegisterLobbyMessages();
            _netManager.CreateHost();
            hostPanel.SetActive(true);
        }

        private void OnJoinButtonClicked()
        {
            _netManager.RegisterLobbyMessages();
            _netManager.JoinRoom("kcp://localhost:7777");
        }

        private void OnLeaveButtonClicked()
        {
            if(NetworkServer.active && NetworkClient.active)
                _netManager.StopHost();
            else if (NetworkClient.active) 
                NetworkClient.Disconnect();
        }

        private void Client_OnDisconnected()
        {
            hostPanel.SetActive(false);
            UpdatePlayersList();
        }

        private void UpdatePlayersList()
        {
            if (!NetworkClient.active)
            {
                lobbyPlayersListLabel.text = string.Empty;
                return;
            }

            foreach (var conn in NetworkServer.connections.Values)
                lobbyPlayersListLabel.text += conn.connectionId.ToString() + "\n";
        }

        private void BindClient()
        {
            selectHostButton.onClick.AddListener(OnSelectHostButtonClicked);
            startHostButton.onClick.AddListener(OnStartGameButtonClicked);
            joinButton.onClick.AddListener(OnJoinButtonClicked);
            leaveHostButton.onClick.AddListener(OnLeaveButtonClicked);

            // Auto-expose
            _netManager.Client_OnNewLobbyPlayer += UpdatePlayersList;
            _netManager.Client_OnDisconnected += Client_OnDisconnected;
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