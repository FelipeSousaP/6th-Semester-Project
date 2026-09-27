using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PiGame.UI
{
    public class ConnectionPanelUI : MonoBehaviour, ICancelHandler
    {
        [SerializeField] private Button _hostButton;
        [SerializeField] private Button _clientButton;
        [SerializeField] private Button _secondaryButton;
        [SerializeField] private Button _optionsButton;
        [SerializeField] private Text _clientButtonLabel;
        [SerializeField] private Text _secondaryButtonLabel;
        [SerializeField] private Text _statusText;
        [SerializeField] private InputField _joinCodeInput;

        private Coroutine _selectionRoutine;
        private GameObject _selectionTarget;
        private Navigation _hostNavigation;
        private Navigation _clientNavigation;
        private Navigation _secondaryNavigation;
        private bool _initialized;
        private bool _isConnecting;
        private bool _isEnteringJoinCode;

        public event Action HostRequested;
        public event Action<string> ClientRequested;
        public event Action CancelConnectionRequested;
        public event Action QuitRequested;
        public event Action OptionsRequested;

        private void Awake()
        {
            Initialize();
        }

        private void OnEnable()
        {
            if (!Initialize())
            {
                return;
            }

            _hostButton.onClick.RemoveListener(HandleHostClicked);
            _clientButton.onClick.RemoveListener(HandleClientClicked);
            _secondaryButton.onClick.RemoveListener(HandleSecondaryClicked);
            _optionsButton.onClick.RemoveListener(HandleOptionsClicked);
            _joinCodeInput.onValueChanged.RemoveListener(HandleJoinCodeChanged);
            _hostButton.onClick.AddListener(HandleHostClicked);
            _clientButton.onClick.AddListener(HandleClientClicked);
            _secondaryButton.onClick.AddListener(HandleSecondaryClicked);
            _optionsButton.onClick.AddListener(HandleOptionsClicked);
            _joinCodeInput.onValueChanged.AddListener(HandleJoinCodeChanged);
            FocusDefaultButton();
        }

        private void OnDisable()
        {
            if (!_initialized)
            {
                return;
            }

            _hostButton.onClick.RemoveListener(HandleHostClicked);
            _clientButton.onClick.RemoveListener(HandleClientClicked);
            _secondaryButton.onClick.RemoveListener(HandleSecondaryClicked);
            _optionsButton.onClick.RemoveListener(HandleOptionsClicked);
            _joinCodeInput.onValueChanged.RemoveListener(HandleJoinCodeChanged);
            StopSelectionRoutine();
            RestoreNavigation();
        }

        public void ShowIdle()
        {
            _isConnecting = false;
            _isEnteringJoinCode = false;
            _hostButton.gameObject.SetActive(true);
            _clientButton.gameObject.SetActive(true);
            _optionsButton.gameObject.SetActive(true);
            _joinCodeInput.gameObject.SetActive(false);
            _joinCodeInput.interactable = true;
            _joinCodeInput.SetTextWithoutNotify(string.Empty);
            SetButtonsInteractable(true);
            RestoreNavigation();
            SetClientButtonLabel("CLIENT");
            _secondaryButtonLabel.text = "SAIR";
            SetStatus("ESCOLHA HOST OU CLIENTE");
            FocusDefaultButton();
        }

        public void ShowConnecting(string message)
        {
            _isConnecting = true;
            SetButtonsInteractable(false);
            _joinCodeInput.interactable = false;
            DisableNavigation();
            _secondaryButtonLabel.text = "CANCELAR";
            SetStatus(message);
            SelectButtonNextFrame(_secondaryButton.gameObject);
        }

        public void ShowError(string message)
        {
            _isConnecting = false;
            if (_isEnteringJoinCode)
            {
                _hostButton.gameObject.SetActive(false);
                _clientButton.gameObject.SetActive(true);
                _joinCodeInput.gameObject.SetActive(true);
                _joinCodeInput.interactable = true;
                _clientButton.interactable = true;
                _secondaryButton.interactable = true;
                SetClientButtonLabel("CONECTAR");
                _secondaryButtonLabel.text = "CANCELAR";
                ConfigureJoinCodeNavigation();
                SetStatus(message);
                FocusJoinCodeInput();
                return;
            }

            SetButtonsInteractable(true);
            RestoreNavigation();
            _secondaryButtonLabel.text = "SAIR";
            SetStatus(message);
            FocusDefaultButton();
        }

        public void FocusDefaultButton()
        {
            SelectButtonNextFrame(_hostButton.gameObject);
        }

        public void SetInteractionEnabled(bool enabled)
        {
            if (!enabled)
            {
                _hostButton.interactable = false;
                _clientButton.interactable = false;
                _secondaryButton.interactable = false;
                _optionsButton.interactable = false;
                return;
            }

            _hostButton.interactable = !_isConnecting;
            _clientButton.interactable = !_isConnecting;
            _joinCodeInput.interactable = _isEnteringJoinCode && !_isConnecting;
            _secondaryButton.interactable = true;
            _optionsButton.interactable = true;
        }

        public void OnCancel(BaseEventData eventData)
        {
            HandleSecondaryClicked();
        }

        private void HandleHostClicked()
        {
            HostRequested?.Invoke();
        }

        private void HandleClientClicked()
        {
            if (!_isEnteringJoinCode)
            {
                ShowJoinCodeEntry();
                return;
            }

            if (!TryNormalizeJoinCode(_joinCodeInput.text, out string normalizedJoinCode))
            {
                SetStatus("CÓDIGO INVÁLIDO");
                FocusJoinCodeInput();
                return;
            }

            _joinCodeInput.SetTextWithoutNotify(normalizedJoinCode);
            ClientRequested?.Invoke(normalizedJoinCode);
        }

        private void HandleSecondaryClicked()
        {
            if (_isConnecting)
            {
                CancelConnectionRequested?.Invoke();
                return;
            }

            if (_isEnteringJoinCode)
            {
                ShowIdle();
                return;
            }

            QuitRequested?.Invoke();
        }

        private void HandleOptionsClicked()
        {
            if (!_isConnecting && !_isEnteringJoinCode)
            {
                OptionsRequested?.Invoke();
            }
        }

        private void ShowJoinCodeEntry()
        {
            _isEnteringJoinCode = true;
            _hostButton.gameObject.SetActive(false);
            _optionsButton.gameObject.SetActive(false);
            _clientButton.gameObject.SetActive(true);
            _joinCodeInput.gameObject.SetActive(true);
            _joinCodeInput.interactable = true;
            SetClientButtonLabel("CONECTAR");
            _secondaryButtonLabel.text = "CANCELAR";
            SetStatus("DIGITE O CÓDIGO DA SALA");
            ConfigureJoinCodeNavigation();
            FocusJoinCodeInput();
        }

        private void SetButtonsInteractable(bool interactable)
        {
            _hostButton.interactable = interactable;
            _clientButton.interactable = interactable;
            _optionsButton.interactable = interactable;
        }

        private void DisableNavigation()
        {
            Navigation disabledNavigation = Navigation.defaultNavigation;
            disabledNavigation.mode = Navigation.Mode.None;
            _hostButton.navigation = disabledNavigation;
            _clientButton.navigation = disabledNavigation;
            _secondaryButton.navigation = disabledNavigation;
            _optionsButton.navigation = disabledNavigation;
        }

        private void RestoreNavigation()
        {
            _hostButton.navigation = _hostNavigation;
            _clientButton.navigation = _clientNavigation;
            _secondaryButton.navigation = _secondaryNavigation;
            ConfigureIdleNavigation();
        }

        private void ConfigureJoinCodeNavigation()
        {
            Navigation inputNavigation = Navigation.defaultNavigation;
            inputNavigation.mode = Navigation.Mode.Explicit;
            inputNavigation.selectOnUp = _secondaryButton;
            inputNavigation.selectOnDown = _clientButton;
            _joinCodeInput.navigation = inputNavigation;

            Navigation clientNavigation = Navigation.defaultNavigation;
            clientNavigation.mode = Navigation.Mode.Explicit;
            clientNavigation.selectOnUp = _joinCodeInput;
            clientNavigation.selectOnDown = _secondaryButton;
            _clientButton.navigation = clientNavigation;

            Navigation secondaryNavigation = Navigation.defaultNavigation;
            secondaryNavigation.mode = Navigation.Mode.Explicit;
            secondaryNavigation.selectOnUp = _clientButton;
            secondaryNavigation.selectOnDown = _joinCodeInput;
            _secondaryButton.navigation = secondaryNavigation;
        }

        private void SetStatus(string message)
        {
            _statusText.text = message;
        }

        private void SelectButtonNextFrame(GameObject target)
        {
            StopSelectionRoutine();
            _selectionTarget = target;
            _selectionRoutine = StartCoroutine(SelectButtonRoutine());
        }

        private IEnumerator SelectButtonRoutine()
        {
            yield return null;

            if (_selectionTarget != null && _selectionTarget.activeInHierarchy)
            {
                EventSystem.current?.SetSelectedGameObject(_selectionTarget);
                if (_selectionTarget == _joinCodeInput.gameObject)
                {
                    _joinCodeInput.ActivateInputField();
                }
            }

            _selectionTarget = null;
            _selectionRoutine = null;
        }

        private void StopSelectionRoutine()
        {
            if (_selectionRoutine == null)
            {
                return;
            }

            StopCoroutine(_selectionRoutine);
            _selectionRoutine = null;
            _selectionTarget = null;
        }

        private void FocusJoinCodeInput()
        {
            SelectButtonNextFrame(_joinCodeInput.gameObject);
        }

        private void SetClientButtonLabel(string label)
        {
            _clientButtonLabel.text = label;
        }

        private void HandleJoinCodeChanged(string value)
        {
            char[] filteredCharacters = new char[value.Length];
            int filteredLength = 0;

            foreach (char character in value)
            {
                if (char.IsLetterOrDigit(character))
                {
                    filteredCharacters[filteredLength++] = char.ToUpperInvariant(character);
                }
            }

            string filteredValue = new string(filteredCharacters, 0, filteredLength);
            if (filteredValue != value)
            {
                _joinCodeInput.SetTextWithoutNotify(filteredValue);
            }
        }

        private static bool TryNormalizeJoinCode(
            string joinCode,
            out string normalizedJoinCode)
        {
            normalizedJoinCode = string.Empty;
            if (string.IsNullOrWhiteSpace(joinCode))
            {
                return false;
            }

            string trimmedCode = joinCode.Trim();
            foreach (char character in trimmedCode)
            {
                if (!char.IsLetterOrDigit(character))
                {
                    return false;
                }
            }

            normalizedJoinCode = trimmedCode.ToUpperInvariant();
            return true;
        }

        public bool Initialize()
        {
            if (_initialized)
            {
                return true;
            }

            if (_hostButton == null
                || _clientButton == null
                || _secondaryButton == null
                || _optionsButton == null
                || _clientButtonLabel == null
                || _secondaryButtonLabel == null
                || _statusText == null
                || _joinCodeInput == null)
            {
                Debug.LogError(
                    "ConnectionPanelUI possui referências obrigatórias ausentes no Inspector.",
                    this);
                enabled = false;
                return false;
            }

            _hostNavigation = _hostButton.navigation;
            _clientNavigation = _clientButton.navigation;
            _secondaryNavigation = _secondaryButton.navigation;
            _initialized = true;

            return true;
        }

        private void ConfigureIdleNavigation()
        {
            Navigation hostNavigation = Navigation.defaultNavigation;
            hostNavigation.mode = Navigation.Mode.Explicit;
            hostNavigation.selectOnDown = _clientButton;
            hostNavigation.selectOnUp = _secondaryButton;
            _hostButton.navigation = hostNavigation;

            Navigation clientNavigation = Navigation.defaultNavigation;
            clientNavigation.mode = Navigation.Mode.Explicit;
            clientNavigation.selectOnUp = _hostButton;
            clientNavigation.selectOnDown = _optionsButton;
            _clientButton.navigation = clientNavigation;

            Navigation optionsNavigation = Navigation.defaultNavigation;
            optionsNavigation.mode = Navigation.Mode.Explicit;
            optionsNavigation.selectOnUp = _clientButton;
            optionsNavigation.selectOnDown = _secondaryButton;
            _optionsButton.navigation = optionsNavigation;

            Navigation secondaryNavigation = Navigation.defaultNavigation;
            secondaryNavigation.mode = Navigation.Mode.Explicit;
            secondaryNavigation.selectOnUp = _optionsButton;
            secondaryNavigation.selectOnDown = _hostButton;
            _secondaryButton.navigation = secondaryNavigation;
        }

    }
}
