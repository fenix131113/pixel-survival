using System;
using GameAssembly.PlayerSystem.Data;
using GameAssembly.PlayerSystem.Variables;
using GameAssembly.Utils;
using GameAssembly.Utils.VariablesSystem;
using GameAssembly.WorldSystem.View;
using Mirror;
using PlayerSystem;
using UnityEngine;
using VContainer;

namespace GameAssembly.PlayerSystem
{
    public class PlayerMovement : NetworkBehaviour
    {
        private static readonly int _isMoving = Animator.StringToHash("IsMoving");
        
        [SerializeField] private Animator anim;
        
        [Inject] private IVariablesResolver<PlayerVariableBlockerType, Action, Action> _playerVars;
        [Inject] private InputSystem_Actions _input;
        [Inject] private PlayerDataSO _playerData;

        private Rigidbody2D _rb;
        private PlayerVariableBlocker _visualBuildBlocker;
        private WorldRenderer _worldRenderer;
        private Renderer[] _cachedRenderers;
        private bool[] _rendererStates;

        private void Start()
        {
            ObjectInjector.Inject(this);
            _cachedRenderers = GetComponentsInChildren<Renderer>(true);
            _rendererStates = new bool[_cachedRenderers.Length];

            _rb = GetComponent<Rigidbody2D>();

            // Keep physics body on server objects (including remote players in host mode),
            // otherwise server-side triggers like item pickup will never fire for them.
            if (!isLocalPlayer && !isServer && _rb)
            {
                Destroy(_rb);
                _rb = null;
            }

            SetupVisualBuildGate();
        }

        private void OnDestroy()
        {
            if (_worldRenderer)
                _worldRenderer.InitialVisualBuildStateChanged -= OnInitialVisualBuildStateChanged;

            _visualBuildBlocker?.Dispose();
        }

        private void Update()
        {
            if (!isLocalPlayer || _playerVars.IsVariableBlocked(PlayerVariableBlockerType.MOVEMENT))
            {
                if(isLocalPlayer && _playerVars.IsVariableBlocked(PlayerVariableBlockerType.MOVEMENT))
                    _rb.linearVelocity = Vector2.zero;
                
                if(isLocalPlayer)
                    anim.SetBool(_isMoving, false);
                return;
            }

            var movement = _input.Player.enabled ? _input.Player.Move.ReadValue<Vector2>() : Vector2.zero;

            _rb.linearVelocity = movement * _playerData.MoveSpeed;
            anim.SetBool(_isMoving, movement.magnitude != 0);
        }

        private void SetupVisualBuildGate()
        {
            if (!NetworkClient.active)
                return;

            _worldRenderer = FindFirstObjectByType<WorldRenderer>();
            if (!_worldRenderer || _worldRenderer.IsInitialVisualBuildCompleted)
                return;

            CacheRendererStates();
            SetRenderersEnabled(false);

            if (isLocalPlayer)
            {
                _visualBuildBlocker = new PlayerVariableBlocker(
                    PlayerVariableBlockerType.MOVEMENT,
                    PlayerVariableBlockerType.LOOK,
                    PlayerVariableBlockerType.ATTACK,
                    PlayerVariableBlockerType.INTERACT,
                    PlayerVariableBlockerType.BUILD);
                _playerVars.RegisterBlocker(_visualBuildBlocker);
            }

            _worldRenderer.InitialVisualBuildStateChanged += OnInitialVisualBuildStateChanged;
        }

        private void OnInitialVisualBuildStateChanged(bool isInProgress)
        {
            if (isInProgress)
                return;

            if (_worldRenderer)
                _worldRenderer.InitialVisualBuildStateChanged -= OnInitialVisualBuildStateChanged;

            RestoreRendererStates();
            _visualBuildBlocker?.Dispose();
            _visualBuildBlocker = null;
            _worldRenderer = null;
        }

        private void CacheRendererStates()
        {
            for (var i = 0; i < _cachedRenderers.Length; i++)
                _rendererStates[i] = _cachedRenderers[i] && _cachedRenderers[i].enabled;
        }

        private void SetRenderersEnabled(bool on)
        {
            foreach (var cachedRenderer in _cachedRenderers)
            {
                if (cachedRenderer)
                    cachedRenderer.enabled = on;
            }
        }

        private void RestoreRendererStates()
        {
            for (var i = 0; i < _cachedRenderers.Length; i++)
            {
                if (_cachedRenderers[i])
                    _cachedRenderers[i].enabled = _rendererStates[i];
            }
        }
    }
}
