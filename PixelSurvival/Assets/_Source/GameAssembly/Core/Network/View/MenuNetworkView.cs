using Mirror;
using UnityEngine;
using UnityEngine.UI;

namespace GameAssembly.Core.Network.View
{
    public class MenuNetworkView : MonoBehaviour
    {
        [SerializeField] private Button selectHostButton;
        [SerializeField] private Button startHostButton;
        [SerializeField] private Button joinButton;
        
        private NetManager _netManager;

        private void Start()
        {
            _netManager = NetworkManager.singleton as NetManager;
            Bind();
        }

        private void OnDestroy()
        {
            Expose();
        }

        private void OnStartHostButtonClicked()
        {
            _netManager.CreateHost();
        }

        private void OnSelectHostButtonClicked()
        {
            
        }

        private void OnJoinButtonClicked()
        {
            _netManager.JoinRoom("kcp://localhost:7777");
        }

        private void Bind()
        {
            selectHostButton.onClick.AddListener(OnSelectHostButtonClicked);
            startHostButton.onClick.AddListener(OnStartHostButtonClicked);
            joinButton.onClick.AddListener(OnJoinButtonClicked);
        }
        
        private void Expose()
        {
            selectHostButton.onClick.RemoveAllListeners();
            startHostButton.onClick.RemoveAllListeners();
            joinButton.onClick.RemoveAllListeners();
        }
    }
}