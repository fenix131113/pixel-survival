using GameAssembly.Utils.Drawer;
using GameAssembly.Utils.Drawer.Data;
using Mirror;
using UnityEngine;

namespace GameAssembly.PlayerSystem
{
    public class PlayerDrawer : NetworkBehaviour
    {
        [SerializeField] private PlayerAim playerAim;
        [SerializeField] private SingleSpriteDrawer bodyDrawer;
        [SerializeField] private FourSideGroup rotationGroup;

        private int _lastRotSpriteIndex = -1;

        private void Update()
        {
            if (!isLocalPlayer)
                return;

            var selectedSpriteIndex = playerAim.LookDegrees switch
            {
                > -45 and < 45 => 0,
                > 45 and < 135 => 3,
                > 135 and <= 180 or < -135 and > -180 => 2,
                > -135 and < -45 => 1,
                _ => _lastRotSpriteIndex
            };

            if (_lastRotSpriteIndex == selectedSpriteIndex)
                return;

            _lastRotSpriteIndex = selectedSpriteIndex;
            bodyDrawer.Draw(rotationGroup.Rotations[selectedSpriteIndex]);
            Cmd_SetPlayerSprite(_lastRotSpriteIndex);
        }

        [ClientRpc(includeOwner = false)]
        private void Rpc_SetPlayerSprite(int spriteIndex)
        {
            _lastRotSpriteIndex = spriteIndex;
            bodyDrawer.Draw(rotationGroup.Rotations[spriteIndex]);
        }

        [Command]
        private void Cmd_SetPlayerSprite(int index)
        {
            if (index is >= 0 and < 4)
                Rpc_SetPlayerSprite(index);
        }
    }
}