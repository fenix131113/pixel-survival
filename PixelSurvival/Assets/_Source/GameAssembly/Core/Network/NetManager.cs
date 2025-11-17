using Mirror;

namespace GameAssembly.Core.Network
{
    public class NetManager : NetworkManager
    {
        #region Menu

        public void CreateHost()
        {
            StartHost();
        }

        public void JoinRoom(string address)
        {
            networkAddress = address;
            StartClient();
        }

        #endregion
    }
}