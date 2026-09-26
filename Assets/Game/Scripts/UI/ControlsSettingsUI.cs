using System;
using PiGame.Input;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.Samples.RebindUI;
using UnityEngine.UI;

namespace PiGame.UI
{
    public class ControlsSettingsUI : MonoBehaviour
    {
        [SerializeField] private GameObject _root;
        [SerializeField] private ConfirmationPanelUI _conflictPanel;
        [SerializeField] private Button _resetKeyboardButton;
        [SerializeField] private Button _resetGamepadButton;
        [SerializeField] private Button _backButton;
        [SerializeField] private InputPromptCatalog _promptCatalog;

        private InputActionAsset _actions;
        private InputBindingService _bindingService;
        private RebindActionUI[] _bindingRows;
        private RebindActionUI _activeBinding;
        private InputAction _conflictAction;
        private int _conflictIndex = -1;
        private string _candidatePath;
        private bool _initialized;
        private int _lastRebindFinishedFrame = -1;
        private int _rebindStartedFrame = -1;
        private Gamepad _lastDisplayedGamepad;

        public bool IsVisible => enabled && _root.activeSelf;
        public bool BlocksBackShortcut => _activeBinding?.ongoingRebind != null
            || _lastRebindFinishedFrame == Time.frameCount;

        public event Action Closed;

        public bool Initialize(InputActionAsset actions)
        {
            if (_initialized && _actions == actions)
                return true;

            if (actions == null)
            {
                Debug.LogError("ControlsSettingsUI precisa de um InputActionAsset.", this);
                enabled = false;
                return false;
            }

            if (_root == null || _conflictPanel == null
                || _resetKeyboardButton == null || _resetGamepadButton == null
                || _backButton == null || _promptCatalog == null)
            {
                Debug.LogError("ControlsSettingsUI possui referências obrigatórias ausentes.", this);
                enabled = false;
                return false;
            }

            _actions = actions;
            _bindingRows = _root.GetComponentsInChildren<RebindActionUI>(true);
            if (!ValidateSceneReferences())
            {
                enabled = false;
                return false;
            }

            _bindingService = new InputBindingService(_actions);
            _bindingService.Load();
            BindEvents();
            _lastDisplayedGamepad = Gamepad.current;
            RefreshBindings();
            _conflictPanel.Hide();
            _root.SetActive(false);
            _initialized = true;
            return true;
        }

        public void Show()
        {
            _root.SetActive(true);
            _conflictPanel.Hide();
            RefreshBindings();
            EventSystem.current?.SetSelectedGameObject(_bindingRows[0].gameObject);
        }

        public void Hide(bool notify = true)
        {
            _activeBinding?.ongoingRebind?.Cancel();
            if (_conflictPanel.gameObject.activeSelf)
                CancelConflict();
            else
                _conflictPanel.Hide();
            _root.SetActive(false);
            if (notify)
                Closed?.Invoke();
        }

        public bool HandleBack()
        {
            if (!IsVisible)
                return false;

            if (_conflictPanel.gameObject.activeSelf)
            {
                CancelConflict();
                return true;
            }

            if (_activeBinding?.ongoingRebind != null)
            {
                _activeBinding.ongoingRebind.Cancel();
                return true;
            }

            Hide();
            return true;
        }

        private void Update()
        {
            if (_lastDisplayedGamepad != Gamepad.current)
            {
                _lastDisplayedGamepad = Gamepad.current;
                RefreshBindings();
            }

            if (_activeBinding?.ongoingRebind == null || Time.frameCount == _rebindStartedFrame)
                return;

            bool oppositeDevicePressed = IsGamepadBinding(_activeBinding)
                ? (Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame)
                    || WasAnyButtonPressed(Mouse.current)
                : WasAnyButtonPressed(Gamepad.current);

            if (oppositeDevicePressed)
                _activeBinding.ongoingRebind.Cancel();
        }

        private void OnDestroy()
        {
            if (_initialized)
                UnbindEvents();
        }

        private void BindEvents()
        {
            _resetKeyboardButton.onClick.AddListener(ResetKeyboard);
            _resetGamepadButton.onClick.AddListener(ResetGamepad);
            _backButton.onClick.AddListener(HideFromButton);
            _conflictPanel.Confirmed += ConfirmConflict;
            _conflictPanel.Canceled += CancelConflict;

            foreach (RebindActionUI bindingRow in _bindingRows)
            {
                bindingRow.GetComponent<Button>().onClick.AddListener(bindingRow.StartInteractiveRebind);
                bindingRow.startRebindEvent.AddListener(HandleRebindStarted);
                bindingRow.stopRebindEvent.AddListener(HandleRebindStopped);
                bindingRow.updateBindingUIEvent.AddListener(UpdateBindingDisplay);
            }
        }

        private void UnbindEvents()
        {
            _resetKeyboardButton.onClick.RemoveListener(ResetKeyboard);
            _resetGamepadButton.onClick.RemoveListener(ResetGamepad);
            _backButton.onClick.RemoveListener(HideFromButton);
            _conflictPanel.Confirmed -= ConfirmConflict;
            _conflictPanel.Canceled -= CancelConflict;

            foreach (RebindActionUI bindingRow in _bindingRows)
            {
                bindingRow.GetComponent<Button>().onClick.RemoveListener(bindingRow.StartInteractiveRebind);
                bindingRow.startRebindEvent.RemoveListener(HandleRebindStarted);
                bindingRow.stopRebindEvent.RemoveListener(HandleRebindStopped);
                bindingRow.updateBindingUIEvent.RemoveListener(UpdateBindingDisplay);
            }
        }

        private void HandleRebindStarted(
            RebindActionUI bindingRow,
            InputActionRebindingExtensions.RebindingOperation operation)
        {
            if (!bindingRow.ResolveActionAndBinding(out InputAction action, out int index))
            {
                operation.Cancel();
                return;
            }

            _activeBinding = bindingRow;
            _rebindStartedFrame = Time.frameCount;
            ConfigureOperation(operation, action.bindings[index]);
            operation.OnApplyBinding((_, path) => ApplyOrDeferBinding(bindingRow, action, index, path));
            SetBindingButtonsInteractable(false);
            SetBindingLabel(bindingRow, "PRESSIONE...", false);
        }

        private void ApplyOrDeferBinding(
            RebindActionUI bindingRow, InputAction action, int index, string path)
        {
            if (!bindingRow.TryFindDuplicateBinding(path, out _conflictAction, out _conflictIndex))
            {
                action.ApplyBindingOverride(index, path);
                return;
            }

            _candidatePath = path;
        }

        private void HandleRebindStopped(
            RebindActionUI bindingRow,
            InputActionRebindingExtensions.RebindingOperation operation)
        {
            _lastRebindFinishedFrame = Time.frameCount;
            if (operation.canceled)
            {
                FinishRebind();
                return;
            }

            if (_conflictAction == null)
            {
                _bindingService.Save();
                FinishRebind();
                return;
            }

            _conflictPanel.Show("ESTE BOTÃO JÁ ESTÁ EM USO. DESEJA TROCAR OS CONTROLES?");
        }

        private void ConfirmConflict()
        {
            _activeBinding.ResolveActionAndBinding(out InputAction action, out int index);
            string originalPath = action.bindings[index].effectivePath;
            action.ApplyBindingOverride(index, _candidatePath);
            _conflictAction.ApplyBindingOverride(_conflictIndex, originalPath);
            _bindingService.Save();
            _conflictPanel.Hide();
            FinishRebind();
        }

        private void CancelConflict()
        {
            _conflictPanel.Hide();
            FinishRebind();
        }

        private void FinishRebind()
        {
            RebindActionUI selection = _activeBinding;
            _activeBinding = null;
            _rebindStartedFrame = -1;
            _conflictAction = null;
            _conflictIndex = -1;
            _candidatePath = null;
            SetBindingButtonsInteractable(true);
            RefreshBindings();
            if (selection != null && IsVisible)
                EventSystem.current?.SetSelectedGameObject(selection.gameObject);
        }

        private static void ConfigureOperation(
            InputActionRebindingExtensions.RebindingOperation operation,
            InputBinding binding)
        {
            if (IsGamepadBinding(binding))
            {
                operation.WithControlsHavingToMatchPath("<Gamepad>")
                    .WithCancelingThrough("<Keyboard>/escape");
                return;
            }

            operation.WithControlsExcluding("<Gamepad>")
                .WithControlsExcluding("<Joystick>")
                .WithControlsExcluding("<Touchscreen>")
                .WithControlsExcluding("<Keyboard>/escape")
                .WithCancelingThrough("<Keyboard>/escape");
        }

        private static bool WasAnyButtonPressed(InputDevice device)
        {
            if (device == null)
                return false;

            foreach (InputControl control in device.allControls)
            {
                if (control is ButtonControl button && button.wasPressedThisFrame)
                    return true;
            }

            return false;
        }

        private bool ValidateSceneReferences()
        {
            if (_bindingRows.Length == 0)
            {
                Debug.LogError("ControlsSettingsUI possui referências obrigatórias ausentes.", this);
                return false;
            }

            foreach (RebindActionUI bindingRow in _bindingRows)
            {
                if (bindingRow.GetComponent<Button>() == null
                    || bindingRow.GetComponentInChildren<TMP_Text>(true)?.GetComponentInChildren<Image>(true) == null
                    || !bindingRow.ResolveActionAndBinding(out _, out _))
                {
                    Debug.LogError($"O binding {bindingRow.name} não está configurado.", bindingRow);
                    return false;
                }
            }

            return true;
        }

        private void ResetKeyboard() => ResetGroup(InputBindingService.KeyboardMouseGroup);
        private void ResetGamepad() => ResetGroup(InputBindingService.GamepadGroup);

        private void ResetGroup(string group)
        {
            _bindingService.ResetBindingGroup(group);
            RefreshBindings();
        }

        private void HideFromButton() => Hide();

        private void RefreshBindings()
        {
            foreach (RebindActionUI bindingRow in _bindingRows)
                bindingRow.UpdateBindingDisplay();
        }

        private void UpdateBindingDisplay(
            RebindActionUI bindingRow,
            string displayString,
            string deviceLayoutName,
            string controlPath) => SetBindingLabel(bindingRow, displayString, true);

        private void SetBindingLabel(RebindActionUI bindingRow, string label, bool showPrompt)
        {
            TMP_Text text = bindingRow.GetComponentInChildren<TMP_Text>(true);
            Image icon = text.GetComponentInChildren<Image>(true);
            Sprite sprite = null;
            if (showPrompt && bindingRow.ResolveActionAndBinding(out InputAction action, out int index))
                sprite = _promptCatalog.GetSprite(action.bindings[index].effectivePath);

            icon.sprite = sprite;
            icon.enabled = sprite != null;
            text.enabled = sprite == null;
            text.text = label;
        }

        private void SetBindingButtonsInteractable(bool interactable)
        {
            foreach (RebindActionUI bindingRow in _bindingRows)
                bindingRow.GetComponent<Button>().interactable = interactable;
        }

        private static bool IsGamepadBinding(RebindActionUI bindingRow)
        {
            return bindingRow.ResolveActionAndBinding(out InputAction action, out int index)
                && IsGamepadBinding(action.bindings[index]);
        }

        private static bool IsGamepadBinding(InputBinding binding) =>
            binding.groups?.IndexOf("Gamepad", StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
