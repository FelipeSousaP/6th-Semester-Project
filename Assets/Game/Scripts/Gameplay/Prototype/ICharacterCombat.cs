using UnityEngine;

namespace PiGame.Gameplay
{
    public interface ICharacterCombat
    {
        void ShootServer(Vector2 direction);
        void ShootReleaseServer();
        void UpdateShootAimServer(Vector2 direction);

        
        void BeginAbilityServer(Vector2 aimDirection, Vector2 moveDirection);
        void EndAbilityServer(Vector2 aimDirection, Vector2 moveDirection);
    }
}
