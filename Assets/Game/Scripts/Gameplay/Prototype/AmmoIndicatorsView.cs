using UnityEngine;

namespace PiGame.Gameplay
{
    public class AmmoIndicatorsView : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer[] _indicators;

        private Color[] _colors;

        private void Awake()
        {
            _colors = new Color[_indicators.Length];
            for (int index = 0; index < _indicators.Length; index++)
            {
                _colors[index] = _indicators[index].color;
                _indicators[index].enabled = false;
            }
        }

        public void SetCount(int count, bool visible)
        {
            for (int index = 0; index < _indicators.Length; index++)
            {
                _indicators[index].color = _colors[index];
                _indicators[index].enabled = visible && index < count;
            }
        }
    }
}
