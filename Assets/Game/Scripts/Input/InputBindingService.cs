using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PiGame.Input
{
    public class InputBindingService
    {
        public const string ActionMapName = "Player";
        public const string KeyboardMouseGroup = "Keyboard&Mouse";
        public const string GamepadGroup = "Gamepad";

        private const string PlayerPrefsKey = "PiGame.InputBindings.v2";
        private const string LegacyPlayerPrefsKey = "PiGame.InputBindings.v1";

        private readonly InputActionAsset _actions;

        public InputBindingService(InputActionAsset actions)
        {
            _actions = actions != null
                ? actions
                : throw new ArgumentNullException(nameof(actions));
        }

        public void Load()
        {
            if (PlayerPrefs.HasKey(LegacyPlayerPrefsKey))
            {
                PlayerPrefs.DeleteKey(LegacyPlayerPrefsKey);
                PlayerPrefs.Save();
            }

            if (!PlayerPrefs.HasKey(PlayerPrefsKey))
            {
                return;
            }

            string overridesJson = PlayerPrefs.GetString(PlayerPrefsKey, string.Empty);
            if (string.IsNullOrWhiteSpace(overridesJson))
            {
                return;
            }

            try
            {
                _actions.LoadBindingOverridesFromJson(overridesJson);
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    $"Não foi possível carregar os controles salvos. "
                    + $"Os padrões serão utilizados. {exception.Message}");
                ResetAll();
            }
        }

        public void Save()
        {
            string overridesJson = _actions.SaveBindingOverridesAsJson();
            PlayerPrefs.SetString(PlayerPrefsKey, overridesJson);
            PlayerPrefs.Save();
        }

        private void ResetAll()
        {
            _actions.RemoveAllBindingOverrides();
            PlayerPrefs.DeleteKey(PlayerPrefsKey);
            PlayerPrefs.Save();
        }

        public void ResetBindingGroup(string bindingGroup)
        {
            if (string.IsNullOrWhiteSpace(bindingGroup))
            {
                throw new ArgumentException(
                    "O grupo de bindings deve ser informado.",
                    nameof(bindingGroup));
            }

            InputBinding mask = InputBinding.MaskByGroup(bindingGroup);
            foreach (InputActionMap actionMap in _actions.actionMaps)
                foreach (InputAction action in actionMap.actions)
                    action.RemoveBindingOverride(mask);

            Save();
        }
    }
}
