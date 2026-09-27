using UnityEngine;

namespace PiGame.Gameplay
{
    public interface ICharacterCombat
    {
        void ShootServer(Vector2 direction);
        void BeginAbilityServer(Vector2 direction);
        void EndAbilityServer(Vector2 direction);
    }
}
