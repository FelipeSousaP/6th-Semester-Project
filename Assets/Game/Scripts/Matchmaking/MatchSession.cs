using UnityEngine;

namespace PiGame.Lobby
{
    public class MatchSession : MonoBehaviour
    {
        public static MatchSession Instance {get; private set;}
        public MatchSnapshot Snapshot {get; private set;}
        public LobbyMapId LastMapId { get; private set; } = LobbyMapId.None;
        public int ConsecutiveMapCount { get; private set; }

        private void Awake()
        {
            if(Instance != null)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        public void SetSnapshot(MatchSnapshot matchSnapshot)
        {
            Snapshot = matchSnapshot;
            if (matchSnapshot.MapId == LastMapId)
            {
                ConsecutiveMapCount++;
            }
            else
            {
                LastMapId = matchSnapshot.MapId;
                ConsecutiveMapCount = 1;
            }
        }

        public void Clear()
        {
            Snapshot = null;
        }

        public void ResetMapHistory()
        {
            LastMapId = LobbyMapId.None;
            ConsecutiveMapCount = 0;
        }
    }   
}

