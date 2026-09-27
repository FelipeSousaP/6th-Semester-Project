using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PiGame.UI
{
    public class ConfirmationPanelUI : MonoBehaviour, ICancelHandler
    {
        [SerializeField] private TMP_Text _messageText;
        [SerializeField] private Button _confirmButton;
        [SerializeField] private Button _cancelButton;

        private Coroutine _selectionRoutine;

        public event Action Confirmed;
        public event Action Canceled;

        public void Show(string message)
        {
            _messageText.text = message;
            gameObject.SetActive(true);
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }

        public void OnCancel(BaseEventData eventData)
        {
            HandleCanceled();
        }

        private void OnEnable()
        {
            _confirmButton.onClick.AddListener(HandleConfirmed);
            _cancelButton.onClick.AddListener(HandleCanceled);
            _selectionRoutine = StartCoroutine(SelectCancelButtonNextFrame());
        }

        private void OnDisable()
        {
            _confirmButton.onClick.RemoveListener(HandleConfirmed);
            _cancelButton.onClick.RemoveListener(HandleCanceled);

            if (_selectionRoutine == null)
            {
                return;
            }

            StopCoroutine(_selectionRoutine);
            _selectionRoutine = null;
        }

        private void HandleConfirmed()
        {
            Confirmed?.Invoke();
        }

        private void HandleCanceled()
        {
            Canceled?.Invoke();
        }

        private IEnumerator SelectCancelButtonNextFrame()
        {
            yield return null;
            EventSystem.current?.SetSelectedGameObject(_cancelButton.gameObject);
            _selectionRoutine = null;
        }
    }
}
