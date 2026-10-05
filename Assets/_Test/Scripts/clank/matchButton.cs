using Unity.Netcode;
using UnityEngine;
using PiGame.Gameplay;

namespace PiGame.Clank.Test 
{
    public class MatchButtonController : MonoBehaviour
    {
        public void ToggleMatchState()
        {
            if (!NetworkManager.Singleton.IsServer) return;

            NetworkPlayerState playerState = FindFirstObjectByType<NetworkPlayerState>();
            if (playerState != null)
            {
                bool newState = !playerState.CanAct;
                playerState.SetMatchActiveServer(newState);
            }
        }
    }
}

