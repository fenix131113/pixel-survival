using System;
using System.Linq;
using GameAssembly.Utils;
using Mirror;
using UnityEngine.SceneManagement;

namespace GameAssembly.Core.Network
{
    public class NetManager : NetworkManager
    {
        public event Action<NetworkConnectionToClient> ServerOnClientConnected;
        public event Action<NetworkConnectionToClient> ServerOnClientDisconnected;
        public event Action<LobbyPlayerChangedMessage> ClientOnChangedLobbyPlayer;
        public event Action ClientOnDisconnected;
        public event Action ClientOnConnected;

        #region Menu

        public struct LobbyPlayerChangedMessage : NetworkMessage
        {
            public string PlayersList;

            public static LobbyPlayerChangedMessage CreateMessage()
            {
                var result = NetworkServer.connections.Values.Aggregate(string.Empty,
                    (current, conn) => current + (conn.connectionId.ToString() + "\n"));

                return new LobbyPlayerChangedMessage() { PlayersList = result };
            }
        }

        public void CreateHost()
        {
            StartHost();
        }

        public void JoinRoom(string address)
        {
            StartClient(new Uri(address));
        }

        public void RegisterLobbyMessages()
        {
#if !UNITY_SERVER
            NetworkClient.RegisterHandler<LobbyPlayerChangedMessage>(OnLobbyPlayerChangedMessage);
#endif
        }

        private void OnLobbyPlayerChangedMessage(LobbyPlayerChangedMessage msg)
        {
            ClientOnChangedLobbyPlayer?.Invoke(msg);
        }

        #endregion
        public override void OnClientConnect()
        {
            base.OnClientConnect();

            if (SceneManager.GetActiveScene().buildIndex == ScenesData.MENU_SCENE_INDEX) // If in menu
            {
            }
            else // If in game
            {
            }

            ClientOnConnected?.Invoke();
        }

        public override void OnClientDisconnect()
        {
            if(SceneManager.GetActiveScene().buildIndex != ScenesData.MENU_SCENE_INDEX)
                SceneManager.LoadScene(ScenesData.MENU_SCENE_INDEX);
            
            ClientOnDisconnected?.Invoke();
        }

        public override void OnServerConnect(NetworkConnectionToClient conn)
        {
            if (SceneManager.GetActiveScene().buildIndex == ScenesData.MENU_SCENE_INDEX) // If in menu
                NetworkServer.SendToAll(LobbyPlayerChangedMessage.CreateMessage());

            ServerOnClientConnected?.Invoke(conn);
        }

        public override void OnServerDisconnect(NetworkConnectionToClient conn)
        {
            base.OnServerDisconnect(conn);
            
            if (SceneManager.GetActiveScene().buildIndex == ScenesData.MENU_SCENE_INDEX) // If in menu
                NetworkServer.SendToAll(LobbyPlayerChangedMessage.CreateMessage());

            ServerOnClientDisconnected?.Invoke(conn);
        }

        public override void OnClientChangeScene(string newSceneName, SceneOperation sceneOperation,
            bool customHandling)
        {
            if (newSceneName != ScenesData.MENU_SCENE_NAME) // If in menu
                NetworkClient.UnregisterHandler<LobbyPlayerChangedMessage>();
            
            Client_Expose();
        }

        public override void OnServerChangeScene(string newSceneName) => Server_Expose();

        public override void OnServerReady(NetworkConnectionToClient conn)
        {
            base.OnServerReady(conn);
            
            if (SceneManager.GetActiveScene().buildIndex == ScenesData.GAME_SCENE_INDEX) // If in game
            {
                var spawnedPlayer = Instantiate(playerPrefab);

                NetworkServer.AddPlayerForConnection(conn, spawnedPlayer);
            }
        }

        private void Client_Expose()
        {
            ClientOnChangedLobbyPlayer = null;
            ClientOnConnected = null;
            ClientOnDisconnected = null;
        }

        private void Server_Expose()
        {
            ServerOnClientConnected = null;
            ServerOnClientDisconnected = null;
        }
    }
}