using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace PiGame.UI
{
    public class PauseMenuUI : MonoBehaviour
    {
        [Header("Configuração")]
        [SerializeField] private InputActionAsset _inputActions;

        [Header("Hierarquia")]
        [SerializeField] private GameObject _root;
        [SerializeField] private TMP_Text _titleText;
        [SerializeField] private TMP_Text _confirmationText;
        [SerializeField] private GameObject _actionsRoot;
        [SerializeField] private GameObject _confirmationRoot;
        [SerializeField] private Button _resumeButton;
        [SerializeField] private Button _controlsButton;
        [SerializeField] private Button _exitButton;
        [SerializeField] private Button _confirmExitButton;
        [SerializeField] private Button _cancelExitButton;
        [SerializeField] private ControlsSettingsUI _controlsView;

        private PauseMenuDefinition _definition;
        private Coroutine _selectionRoutine;
        private bool _buttonsBound;

        public event Action ResumeRequested;
        public event Action ExitRequested;
        public event Action ExitConfirmed;
        public event Action ExitCanceled;
        public event Action ControlsClosed;

        public bool IsVisible => enabled && _root.activeSelf;
        public bool IsConfirmationVisible => enabled && _confirmationRoot.activeSelf;
        public bool IsControlsVisible => _controlsView != null && _controlsView.IsVisible;
        public bool BlocksControlsBackShortcut => IsControlsVisible && _controlsView.BlocksBackShortcut;

        private void Awake()
        {
            if (!ValidateReferences())
            {
                enabled = false;
                return;
            }

            ConfigureConfirmationNavigation();
            BindButtons();
        }

        private void OnDestroy()
        {
            UnbindButtons();
        }

        private void OnDisable()
        {
            StopSelectionRoutine();
        }

        public bool Initialize(PauseMenuDefinition definition)
        {
            _definition = definition;
            if (_definition == null || !ValidateReferences())
            {
                Debug.LogError("PauseMenuUI precisa de uma definição e das referências da cena.", this);
                enabled = false;
                return false;
            }

            RefreshStaticTexts();
            ConfigureControlsView(_inputActions);
            return true;
        }

        public bool Initialize(
            InputActionAsset actions,
            string title,
            string resumeLabel,
            string exitLabel,
            string confirmationMessage)
        {
            _inputActions = actions;
            if (!ValidateReferences())
            {
                enabled = false;
                return false;
            }

            _titleText.text = title;
            _confirmationText.text = confirmationMessage;
            SetButtonLabel(_resumeButton, resumeLabel);
            SetButtonLabel(_controlsButton, "CONTROLES");
            SetButtonLabel(_exitButton, exitLabel);
            ConfigureActions(true);
            ConfigureControlsView(actions);
            return true;
        }

        public void SetVisible(bool isVisible)
        {
            _root.SetActive(isVisible);
            if (!isVisible)
            {
                if (_controlsView != null && _controlsView.IsVisible)
                {
                    _controlsView.Hide(false);
                }
                StopSelectionRoutine();
                return;
            }

            HideConfirmation();
            QueueSelection(_resumeButton);
        }

        public void ShowControls()
        {
            _actionsRoot.SetActive(false);
            _controlsView.Show();
            _root.SetActive(false);
        }

        public bool HandleControlsBack()
        {
            return _controlsView != null
                && _controlsView.IsVisible
                && _controlsView.HandleBack();
        }

        public void ShowConfirmation()
        {
            _actionsRoot.SetActive(false);
            _confirmationRoot.SetActive(true);
            QueueSelection(_cancelExitButton);
        }

        public void HideConfirmation()
        {
            _confirmationRoot.SetActive(false);
            _actionsRoot.SetActive(true);
            if (IsVisible)
            {
                QueueSelection(_resumeButton);
            }
        }

        public void SetBusy(bool isBusy)
        {
            SetButtonInteractable(_resumeButton, !isBusy);
            SetButtonInteractable(_controlsButton, !isBusy);
            SetButtonInteractable(_exitButton, !isBusy);
            SetButtonInteractable(_confirmExitButton, !isBusy);
            SetButtonInteractable(_cancelExitButton, !isBusy);
        }

        private void BindButtons()
        {
            if (_buttonsBound)
            {
                return;
            }

            _resumeButton.onClick.AddListener(HandleResumeClicked);
            _controlsButton.onClick.AddListener(ShowControls);
            _exitButton.onClick.AddListener(HandleExitClicked);
            _confirmExitButton.onClick.AddListener(HandleExitConfirmedClicked);
            _cancelExitButton.onClick.AddListener(HandleExitCanceledClicked);
            _buttonsBound = true;
        }

        private void UnbindButtons()
        {
            if (!_buttonsBound)
            {
                return;
            }

            _resumeButton.onClick.RemoveListener(HandleResumeClicked);
            _controlsButton.onClick.RemoveListener(ShowControls);
            _exitButton.onClick.RemoveListener(HandleExitClicked);
            _confirmExitButton.onClick.RemoveListener(HandleExitConfirmedClicked);
            _cancelExitButton.onClick.RemoveListener(HandleExitCanceledClicked);
            if (_controlsView != null)
            {
                _controlsView.Closed -= HandleControlsClosed;
            }

            _buttonsBound = false;
        }

        private void HandleResumeClicked() => ResumeRequested?.Invoke();
        private void HandleExitClicked() => ExitRequested?.Invoke();
        private void HandleExitConfirmedClicked() => ExitConfirmed?.Invoke();
        private void HandleExitCanceledClicked() => ExitCanceled?.Invoke();

        private void RefreshStaticTexts()
        {
            _titleText.text = _definition.Title;
            _confirmationText.text = _definition.ExitConfirmationMessage;
            SetButtonLabel(_resumeButton, _definition.ResumeLabel);
            SetButtonLabel(_controlsButton, _definition.ControlsLabel);
            SetButtonLabel(_exitButton, _definition.ExitLabel);
            ConfigureActions(_definition.ShowControlsButton);
        }

        private void ConfigureActions(bool showControls)
        {
            _controlsButton.gameObject.SetActive(showControls);
        }

        private void ConfigureConfirmationNavigation()
        {
            _confirmExitButton.navigation = new Navigation
            {
                mode = Navigation.Mode.Explicit,
                selectOnRight = _cancelExitButton
            };
            _cancelExitButton.navigation = new Navigation
            {
                mode = Navigation.Mode.Explicit,
                selectOnLeft = _confirmExitButton
            };
        }

        private void ConfigureControlsView(InputActionAsset actions)
        {
            if (!_controlsButton.gameObject.activeSelf)
            {
                return;
            }

            if (_controlsView == null || actions == null)
            {
                Debug.LogError("O botão CONTROLES precisa de ControlsSettingsUI e InputActionAsset.", this);
                _controlsButton.interactable = false;
                return;
            }

            if (!_controlsView.Initialize(actions))
            {
                _controlsButton.interactable = false;
                return;
            }

            _controlsView.Closed -= HandleControlsClosed;
            _controlsView.Closed += HandleControlsClosed;
        }

        private void HandleControlsClosed()
        {
            _root.SetActive(true);
            _actionsRoot.SetActive(true);
            QueueSelection(_controlsButton);
            ControlsClosed?.Invoke();
        }

        private bool ValidateReferences()
        {
            bool valid = _root != null
                && _titleText != null
                && _confirmationText != null
                && _actionsRoot != null
                && _confirmationRoot != null
                && _resumeButton != null
                && _controlsButton != null
                && _exitButton != null
                && _confirmExitButton != null
                && _cancelExitButton != null;
            if (!valid)
            {
                Debug.LogError(
                    "PauseMenuUI possui referências obrigatórias ausentes. Configure a hierarquia pela cena ou prefab.",
                    this);
            }

            return valid;
        }

        private void QueueSelection(Button button)
        {
            StopSelectionRoutine();
            if (gameObject.activeInHierarchy)
            {
                _selectionRoutine = StartCoroutine(SelectNextFrame(button));
            }
        }

        private IEnumerator SelectNextFrame(Button button)
        {
            yield return null;
            _selectionRoutine = null;
            if (UnityEngine.EventSystems.EventSystem.current != null
                && button.interactable)
            {
                UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(button.gameObject);
            }
        }

        private void StopSelectionRoutine()
        {
            if (_selectionRoutine == null)
            {
                return;
            }

            StopCoroutine(_selectionRoutine);
            _selectionRoutine = null;
        }

        private static void SetButtonLabel(Button button, string label)
        {
            TMP_Text text = button.GetComponentInChildren<TMP_Text>();
            if (text == null)
            {
                Debug.LogError($"O botão {button.name} precisa de um TMP_Text filho.", button);
                return;
            }

            text.text = label;
        }

        private static void SetButtonInteractable(Button button, bool interactable)
        {
            button.interactable = interactable;
        }
    }
}
