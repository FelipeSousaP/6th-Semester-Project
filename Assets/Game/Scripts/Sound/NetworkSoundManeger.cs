using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace PiGame.Sound
{
    public enum SFXList 
    {
        SFX1,
        SFX2,
        SFX3 
    }
    public enum OSTList
    {
        OST1,
        OST2,
        OST3 
    }
    public enum AmbienceList 
    {
        Ambience1,
        Ambience2, 
        Ambience3 
    }

    public class NetworkSoundManager : NetworkBehaviour
    {
        public static NetworkSoundManager Instance { get; private set; }

        [System.Serializable]
        public struct SoundData
        {
            public SFXList sfxType;
            public AudioClip clip;
        }

        [System.Serializable]
        public struct MusicData
        {
            public OSTList ostType;
            public AudioClip clip;
        }

        [System.Serializable]
        public struct AmbienceData
        {
            public AmbienceList AmbienceType;
            public AudioClip clip;
        }

        [Header("Audio Components")]
        [SerializeField] private AudioSource sfxSource;
        [SerializeField] private AudioSource AmbienceSource;
        [SerializeField] private AudioSource ostSource;

        [Header("Audio Lists")]
        [SerializeField] private List<SoundData> sfxList = new();
        [SerializeField] private List<MusicData> ostList = new();
        [SerializeField] private List<AmbienceData> AmbienceList = new();

        private Dictionary<SFXList, AudioClip> _sfxDict = new();
        private Dictionary<OSTList, AudioClip> _ostDict = new();
        private Dictionary<AmbienceList, AudioClip> _AmbienceDict = new();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            foreach (var item in sfxList)
            {
                if (!_sfxDict.ContainsKey(item.sfxType))
                    _sfxDict.Add(item.sfxType, item.clip);
            }

            foreach (var item in ostList)
            {
                if (!_ostDict.ContainsKey(item.ostType))
                    _ostDict.Add(item.ostType, item.clip);
            }

            foreach (var item in AmbienceList)
            {
                if (!_AmbienceDict.ContainsKey(item.AmbienceType))
                    _AmbienceDict.Add(item.AmbienceType, item.clip);
            }

        }

        public override void OnNetworkSpawn()
        {
            Instance = this;
        }

        public void PlaySFX(SFXList sfx)
        {
            if (Instance == null) return;
            Instance.SendSFXServerRpc(sfx);
        }

        public void PlayOST(OSTList ost)
        {
            if (Instance == null) return;
            Instance.SendOSTServerRpc(ost);
        }

        public void PlayAmbience(AmbienceList ambience)
        {
            if (Instance == null) return;
            Instance.SendAmbienceServerRpc(ambience);
        }

        #region RPC do ambience
        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        private void SendAmbienceServerRpc(AmbienceList ambience)
        {
            SendAmbienceClientRpc(ambience);
        }

        [Rpc(SendTo.ClientsAndHost)]
        private void SendAmbienceClientRpc(AmbienceList ambience)
        {
            PlayAmbienceLocal(ambience);
        }
        #endregion

        #region RPC do SFX
        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        private void SendSFXServerRpc(SFXList sfx)
        {
            SendSFXClientRpc(sfx);
        }

        [Rpc(SendTo.ClientsAndHost)]
        private void SendSFXClientRpc(SFXList sfx)
        {
            PlaySFXLocal(sfx);
        }
        #endregion

        #region Rpc do OST
        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        private void SendOSTServerRpc(OSTList ost)
        {
            SendOSTClientRpc(ost);
        }

        [Rpc(SendTo.ClientsAndHost)]
        private void SendOSTClientRpc(OSTList ost)
        {
            PlayOSTLocal(ost);
        }
        #endregion

        #region Execução Local
        private void PlaySFXLocal(SFXList sfx)
        {
            if (_sfxDict.TryGetValue(sfx, out AudioClip clip))
            {
                sfxSource.PlayOneShot(clip);
            }
            else
            {
                Debug.LogWarning($"O SFX '{sfx}' não foi configurado nas listas do NetworkSoundManager");
            }
        }

        private void PlayOSTLocal(OSTList ost)
        {
            if (_ostDict.TryGetValue(ost, out AudioClip clip))
            {
                ostSource.clip = clip;
                ostSource.loop = true;
                ostSource.Play();
            }
            else
            {
                Debug.LogWarning($"A OST '{ost}' não foi configurada nas listas do NetworkSoundManager");
            }
        }

        private void PlayAmbienceLocal(AmbienceList ambience)
        {
            if (_AmbienceDict.TryGetValue(ambience, out AudioClip clip))
            {
                AmbienceSource.clip = clip;
                AmbienceSource.loop = true;
                AmbienceSource.Play();
            }
            else
            {
                Debug.LogWarning($"A ambientação {ambience} não foi configurada nas listas do NetworkSoundManager");
            }
        }
        // metodo de ambientação que envolva tempo( receber tempo e estado)
        #endregion
    }
}