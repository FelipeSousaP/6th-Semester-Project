using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.Samples.RebindUI;

namespace PiGame.Input
{
    public class ControlsRebindSession
    {
        private RebindActionUI _activeRow;
        private InputAction _conflictingAction;
        private int _conflictingBindingIndex;
        private string _candidatePath;
        private int _startedFrame;

        public RebindActionUI ActiveRow => _activeRow;
        public bool IsCapturing => _activeRow?.ongoingRebind != null;
        public bool HasConflict => _conflictingAction != null;

        public void Start(RebindActionUI row, InputActionRebindingExtensions.RebindingOperation operation)
        {
            row.ResolveActionAndBinding(out InputAction action, out int bindingIndex);
            _activeRow = row;
            _startedFrame = Time.frameCount;
            ConfigureOperation(operation, action.bindings[bindingIndex]);
            operation.OnApplyBinding((_, path) => ApplyOrDeferBinding(action, bindingIndex, path));
        }

        public void CancelCapture() => _activeRow?.ongoingRebind?.Cancel();

        public void CancelIfOtherDevicePressed()
        {
            if (!IsCapturing || Time.frameCount == _startedFrame)
                return;

            _activeRow.ResolveActionAndBinding(out InputAction action, out int bindingIndex);
            bool oppositeDevicePressed = IsGamepadBinding(action.bindings[bindingIndex])
                ? (Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame)
                    || WasAnyButtonPressed(Mouse.current)
                : WasAnyButtonPressed(Gamepad.current);

            if (oppositeDevicePressed)
                CancelCapture();
        }

        public void AcceptConflict()
        {
            _activeRow.ResolveActionAndBinding(out InputAction action, out int bindingIndex);
            string previousPath = action.bindings[bindingIndex].effectivePath;
            action.ApplyBindingOverride(bindingIndex, _candidatePath);
            _conflictingAction.ApplyBindingOverride(_conflictingBindingIndex, previousPath);
        }

        public RebindActionUI Finish()
        {
            RebindActionUI previousRow = _activeRow;
            _activeRow = null;
            _conflictingAction = null;
            _candidatePath = null;
            return previousRow;
        }

        private void ApplyOrDeferBinding(InputAction action, int bindingIndex, string path)
        {
            if (TryFindConflict(action, bindingIndex, path))
                _candidatePath = path;
            else
                action.ApplyBindingOverride(bindingIndex, path);
        }

        private bool TryFindConflict(InputAction targetAction, int targetIndex, string path)
        {
            foreach (InputAction action in targetAction.actionMap.actions)
            {
                for (int index = 0; index < action.bindings.Count; index++)
                {
                    InputBinding binding = action.bindings[index];
                    if ((action == targetAction && index == targetIndex) || binding.isComposite)
                        continue;

                    if (!string.Equals(binding.effectivePath, path, StringComparison.OrdinalIgnoreCase))
                        continue;

                    _conflictingAction = action;
                    _conflictingBindingIndex = index;
                    return true;
                }
            }

            return false;
        }

        private static void ConfigureOperation(
            InputActionRebindingExtensions.RebindingOperation operation, InputBinding binding)
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

        private static bool IsGamepadBinding(InputBinding binding) =>
            binding.groups?.IndexOf("Gamepad", StringComparison.OrdinalIgnoreCase) >= 0;

        private static bool WasAnyButtonPressed(InputDevice device)
        {
            if (device == null)
                return false;

            foreach (InputControl control in device.allControls)
                if (control is ButtonControl button && button.wasPressedThisFrame)
                    return true;

            return false;
        }
    }
}
