using Mirror;
using Unity.Cinemachine;

namespace GameAssembly.PlayerSystem
{
    public class PlayerCamera : NetworkBehaviour
    {
        private CinemachineCamera _playerCamera;

        private void Start()
        {
            if(!isLocalPlayer)
                return;
            
            _playerCamera = FindFirstObjectByType<CinemachineCamera>();
            _playerCamera.Target = new CameraTarget { TrackingTarget = transform };
        }
    }
}