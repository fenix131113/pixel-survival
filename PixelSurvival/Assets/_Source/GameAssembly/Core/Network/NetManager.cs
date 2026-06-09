using System;
using System.Collections.Generic;
using System.Linq;
using Epic.OnlineServices;
using Epic.OnlineServices.Lobby;
using GameAssembly.Utils;
using GameAssembly.WorldSystem;
using Mirror;
using UnityEngine;
using UnityEngine.SceneManagement;
using LobbyAttribute = Epic.OnlineServices.Lobby.Attribute;
using Random = UnityEngine.Random;

// ReSharper disable Unity.PerformanceCriticalCodeInvocation

namespace GameAssembly.Core.Network
{
    public class NetManager : NetworkManager
    {
        public const string LobbyCodeAttributeKey = "join_code";
        private const string JoinCodeAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

        public event Action<NetworkConnectionToClient> ServerOnClientConnected;
        public event Action<NetworkConnectionToClient> ServerOnClientDisconnected;
        public event Action<NetworkConnectionToClient> ServerOnServerReadyInGame;
        public event Action<LobbyPlayerChangedMessage> ClientOnChangedLobbyPlayer;
        public event Action ClientOnDisconnected;
        public event Action ClientOnConnected;
        public event Action<string> LobbyCodeReady;
        public event Action<string> LobbyOperationFailed;

        public string CurrentLobbyCode { get; private set; } = string.Empty;

        private EOSLobby _eosLobby;
        private bool _isCreatingLobby;
        private bool _isJoiningLobby;

        #region Menu

        public struct LobbyPlayerChangedMessage : NetworkMessage
        {
            public string PlayersList;

            public static LobbyPlayerChangedMessage CreateMessage()
            {
                var result = NetworkServer.connections.Values.Aggregate(string.Empty,
                    (current, conn) => current + conn.connectionId.ToString() + "\n");

                return new LobbyPlayerChangedMessage { PlayersList = result };
            }
        }

        public void CreateHost()
        {
            if (NetworkServer.active || NetworkClient.active)
            {
                NotifyLobbyFailure("Cannot create lobby while network is already active.");
                return;
            }

            if (_isCreatingLobby)
            {
                return;
            }

            var lobby = EnsureLobby();
            if (lobby == null)
            {
                NotifyLobbyFailure("EOS lobby component is unavailable.");
                return;
            }

            var normalizedCode = CreateRandomJoinCode();

            _isCreatingLobby = true;

            void Cleanup()
            {
                lobby.CreateLobbySucceeded -= OnCreateLobbySucceeded;
                lobby.CreateLobbyFailed -= OnCreateLobbyFailed;
                _isCreatingLobby = false;
            }

            void OnCreateLobbySucceeded(List<LobbyAttribute> _)
            {
                Cleanup();

                CurrentLobbyCode = normalizedCode;
                LobbyCodeReady?.Invoke(CurrentLobbyCode);
                StartHost();
            }

            void OnCreateLobbyFailed(string errorMessage)
            {
                Cleanup();
                NotifyLobbyFailure(errorMessage);
            }

            lobby.CreateLobbySucceeded += OnCreateLobbySucceeded;
            lobby.CreateLobbyFailed += OnCreateLobbyFailed;
            lobby.CreateLobby(
                (uint)maxConnections,
                LobbyPermissionLevel.Publicadvertised,
                false,
                new[] { new AttributeData { Key = LobbyCodeAttributeKey, Value = normalizedCode } });
        }

        public void JoinRoom(string address)
        {
            StartClient(new Uri(address));
        }

        public void JoinRoomByCode(string joinCode)
        {
            if (NetworkServer.active || NetworkClient.active)
            {
                NotifyLobbyFailure("Cannot join lobby while network is already active.");
                return;
            }

            if (_isJoiningLobby)
            {
                return;
            }

            var normalizedCode = NormalizeJoinCode(joinCode);
            if (string.IsNullOrWhiteSpace(normalizedCode))
            {
                NotifyLobbyFailure("Join code is empty.");
                return;
            }

            var lobby = EnsureLobby();
            if (lobby == null)
            {
                NotifyLobbyFailure("EOS lobby component is unavailable.");
                return;
            }

            _isJoiningLobby = true;

            void Cleanup()
            {
                lobby.FindLobbiesSucceeded -= OnFindLobbiesSucceeded;
                lobby.FindLobbiesFailed -= OnFindLobbiesFailed;
                lobby.JoinLobbySucceeded -= OnJoinLobbySucceeded;
                lobby.JoinLobbyFailed -= OnJoinLobbyFailed;
                _isJoiningLobby = false;
            }

            void OnFindLobbiesSucceeded(List<LobbyDetails> foundLobbies)
            {
                if (foundLobbies == null || foundLobbies.Count == 0)
                {
                    Cleanup();
                    NotifyLobbyFailure($"Lobby with code \"{normalizedCode}\" was not found.");
                    return;
                }

                lobby.JoinLobby(foundLobbies[0], new[] { LobbyCodeAttributeKey });
            }

            void OnFindLobbiesFailed(string errorMessage)
            {
                Cleanup();
                NotifyLobbyFailure(errorMessage);
            }

            void OnJoinLobbySucceeded(List<LobbyAttribute> attributes)
            {
                Cleanup();

                var hostAddressAttribute = attributes.Find(x => x.Data.HasValue && x.Data.Value.Key == EOSLobby.hostAddressKey);
                if (!hostAddressAttribute.Data.HasValue)
                {
                    NotifyLobbyFailure("Host address not found in joined lobby attributes.");
                    return;
                }

                CurrentLobbyCode = normalizedCode;
                networkAddress = hostAddressAttribute.Data.Value.Value.AsUtf8;
                StartClient();
            }

            void OnJoinLobbyFailed(string errorMessage)
            {
                Cleanup();
                NotifyLobbyFailure(errorMessage);
            }

            lobby.FindLobbiesSucceeded += OnFindLobbiesSucceeded;
            lobby.FindLobbiesFailed += OnFindLobbiesFailed;
            lobby.JoinLobbySucceeded += OnJoinLobbySucceeded;
            lobby.JoinLobbyFailed += OnJoinLobbyFailed;

            var searchOption = new LobbySearchSetParameterOptions
            {
                ComparisonOp = ComparisonOp.Equal,
                Parameter = new AttributeData
                {
                    Key = LobbyCodeAttributeKey,
                    Value = normalizedCode
                }
            };

            lobby.FindLobbies(1, new[] { searchOption });
        }

        public void LeaveRoom()
        {
            if (_eosLobby && _eosLobby.ConnectedToLobby)
            {
                _eosLobby.LeaveLobby();
            }

            CurrentLobbyCode = string.Empty;

            if (NetworkServer.active && NetworkClient.active)
            {
                StopHost();
            }
            else if (NetworkClient.active)
            {
                StopClient();
            }
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
            if (SceneManager.GetActiveScene().buildIndex != ScenesData.MENU_SCENE_INDEX)
                SceneManager.LoadScene(ScenesData.MENU_SCENE_INDEX);

            CurrentLobbyCode = string.Empty;
            ClientOnDisconnected?.Invoke();
        }

        public override void OnServerConnect(NetworkConnectionToClient conn)
        {
            if (SceneManager.GetActiveScene().buildIndex == ScenesData.MENU_SCENE_INDEX) // If in menu
            {
                NetworkServer.SendToAll(LobbyPlayerChangedMessage.CreateMessage());
            }
            else // If in game
            {
            }

            ServerOnClientConnected?.Invoke(conn);
        }

        public override void OnServerDisconnect(NetworkConnectionToClient conn)
        {
            if (SceneManager.GetActiveScene().buildIndex == ScenesData.GAME_SCENE_INDEX)
            {
                if (conn.identity)
                    GameInstaller.Resolve<ServerBlockDamageSystem>()?.Server_ClearPlayerTarget(conn.identity.netId);

                GameInstaller.Resolve<WorldCreateManager>().Server_StopSendingWorldToConn(conn);
            }

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

        public override void OnServerChangeScene(string newSceneName)
        {
            Server_Expose();
        }

        public override void OnServerSceneChanged(string sceneName)
        {
            // ReSharper disable once ForeachCanBePartlyConvertedToQueryUsingAnotherGetEnumerator
            foreach (var conn in NetworkServer.connections.Values)
            {
                GameInstaller.Resolve<WorldCreateManager>().Server_SendWorldToConn(conn);
            }
        }

        public override void OnServerReady(NetworkConnectionToClient conn)
        {
            base.OnServerReady(conn);

            if (SceneManager.GetActiveScene().buildIndex == ScenesData.GAME_SCENE_INDEX) // If in game
            {
                GameInstaller.Resolve<WorldCreateManager>().Server_SendWorldToConn(conn);
                ServerOnServerReadyInGame?.Invoke(conn);
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

        private EOSLobby EnsureLobby()
        {
            if (_eosLobby == null)
            {
                _eosLobby = GetComponent<EOSLobby>();
            }

            if (_eosLobby == null)
            {
                _eosLobby = gameObject.AddComponent<EOSLobby>();
            }

            return _eosLobby;
        }

        private static string NormalizeJoinCode(string joinCode)
        {
            return string.IsNullOrWhiteSpace(joinCode)
                ? string.Empty
                : joinCode.Trim().ToUpperInvariant();
        }

        private static string CreateRandomJoinCode(int length = 4)
        {
            var codeBuffer = new char[length];

            for (var i = 0; i < codeBuffer.Length; i++)
            {
                codeBuffer[i] = JoinCodeAlphabet[Random.Range(0, JoinCodeAlphabet.Length)];
            }

            return new string(codeBuffer);
        }

        private void NotifyLobbyFailure(string errorMessage)
        {
            var message = string.IsNullOrWhiteSpace(errorMessage) ? "Unknown EOS lobby error." : errorMessage;
            Debug.LogError(message);
            LobbyOperationFailed?.Invoke(message);
        }
    }
}
