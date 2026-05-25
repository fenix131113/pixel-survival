using System;
using GameAssembly.PlayerSystem.Data;
using GameAssembly.PlayerSystem.Variables;
using GameAssembly.Utils.VariablesSystem;
using Mirror;
using UnityEngine;
using VContainer;

namespace GameAssembly.EndGame
{
    public class EndGame : NetworkBehaviour
    {
        private readonly PlayerVariableBlocker _blocker = new(PlayerVariableBlockerType.ATTACK,
            PlayerVariableBlockerType.BUILD, PlayerVariableBlockerType.INTERACT,
            PlayerVariableBlockerType.LOOK, PlayerVariableBlockerType.MOVEMENT);

        [SerializeField] private GameObject endGamePanel;

        [Inject] private IVariablesResolver<PlayerVariableBlockerType, Action, Action> _variablesResolver;

        [SyncVar(hook = nameof(OnEndGameStateChanged))]
        private bool _isGameEnded;

        public bool IsGameEnded() => _isGameEnded;

        [Server]
        public void Server_EndGame() => _isGameEnded = true;

        private void OnEndGameStateChanged(bool _, bool __)
        {
            endGamePanel.SetActive(true);
            _variablesResolver.RegisterBlocker(_blocker);
        }
    }
}