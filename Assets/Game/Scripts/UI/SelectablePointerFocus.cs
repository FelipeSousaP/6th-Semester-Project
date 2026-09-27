using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PiGame.UI
{
    public class SelectablePointerFocus : MonoBehaviour, IPointerEnterHandler
    {
        [SerializeField] private Selectable _selectable;

        private void Awake()
        {
            if (_selectable == null)
            {
                _selectable = GetComponent<Selectable>();
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (_selectable != null
                && _selectable.interactable
                && EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(_selectable.gameObject);
            }
        }
    }
}
