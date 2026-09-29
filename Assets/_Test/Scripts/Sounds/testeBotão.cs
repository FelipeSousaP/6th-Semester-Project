using UnityEngine;
namespace PiGame.Sound.Tests 
{
    public class TesteBotãoNovo : MonoBehaviour
    {
        public void AoClicarNoBotao()
        {
            NetworkSoundManager.Instance.PlaySFX(SFXList.SFX1);
        }
        public void AoClicarNoBotao2()
        {
            NetworkSoundManager.Instance.PlayOST(OSTList.OST1);
        }

        public void AoClicarNoBotao3()
        {
            NetworkSoundManager.Instance.PlayAmbience(AmbienceList.Ambience2);
        }
    }
}

