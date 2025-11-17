using System;
using GameAssembly.Utils;
using Mirror;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GameAssembly.Core.Network
{
    public class NetManager : NetworkManager
    {
        public event Action<NetworkConnectionToClient> Server_OnClientConnected;
        public event Action<NetworkConnectionToClient> Server_OnClientDisconnected;
        public event Action Client_OnNewLobbyPlayer;
        public event Action Client_OnDisconnected;
        public event Action Client_OnConnected;

        #region Menu

        public struct LobbyPlayerAddedMessage : NetworkMessage
        {
        }

        public void CreateHost()
        {
            StartHost();
        }

        public void JoinRoom(string address)
        {
            networkAddress = address;
            StartClient();
        }

        public void RegisterLobbyMessages()
        {
#if !UNITY_SERVER
            NetworkClient.RegisterHandler<LobbyPlayerAddedMessage>(OnLobbyPlayerConnectedMessage);
#endif
        }

        private void OnLobbyPlayerConnectedMessage(LobbyPlayerAddedMessage msg)
        {
            Client_OnNewLobbyPlayer?.Invoke();
        }

        #endregion

        private void OnConnectedToServer()
        {
            if (SceneManager.GetActiveScene().buildIndex == ScenesData.MENU_SCENE_INDEX) // If in menu
            {
            }
            else // If in game
            {
            }

            Client_OnConnected?.Invoke();
        }

        public override void OnClientDisconnect()
        {
            Debug.Log("OnClientDisconnect");
            Client_OnDisconnected?.Invoke();
        }

        public override void OnServerConnect(NetworkConnectionToClient conn)
        {
            if (SceneManager.GetActiveScene().buildIndex == ScenesData.MENU_SCENE_INDEX) // If in menu
                NetworkServer.SendToAll(new LobbyPlayerAddedMessage());

            Server_OnClientConnected?.Invoke(conn);
        }

        public override void OnServerDisconnect(NetworkConnectionToClient conn)
        {
            Server_OnClientDisconnected?.Invoke(conn);
        }

        public override void OnClientChangeScene(string newSceneName, SceneOperation sceneOperation,
            bool customHandling)
        {
            if (newSceneName != ScenesData.MENU_SCENE_NAME) // If in menu
                NetworkClient.UnregisterHandler<LobbyPlayerAddedMessage>();

            Client_Expose();
        }

        public override void OnServerChangeScene(string newSceneName) => Server_Expose();

        private void Client_Expose()
        {
            Client_OnNewLobbyPlayer = null;
        }

        private void Server_Expose()
        {
            Server_OnClientConnected = null;
            Server_OnClientDisconnected = null;
        }
    }
}