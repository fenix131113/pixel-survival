using Mirror;
using Unity.Cinemachine;
using UnityEngine;

namespace GameAssembly.PlayerSystem
{
    public class PlayerCamera : NetworkBehaviour
    {
        private CinemachineCamera _playerCamera;

        private void Start()
        {
            if (!isLocalPlayer)
                return;

            _playerCamera = FindFirstObjectByType<CinemachineCamera>();
            SetTarget(transform);
        }

        public void SetTarget(Transform target) => _playerCamera.Target = new CameraTarget { TrackingTarget = target };
    }
}