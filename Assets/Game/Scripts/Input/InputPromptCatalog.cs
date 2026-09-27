using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.DualShock;

namespace PiGame.Input
{
    [CreateAssetMenu(menuName = "PiGame/Input Prompt Catalog")]
    public class InputPromptCatalog : ScriptableObject
    {
        [SerializeField] private Sprite[] _keyboardAndMouse;
        [SerializeField] private Sprite[] _xbox;
        [SerializeField] private Sprite[] _playStation4;
        [SerializeField] private Sprite[] _playStation5;

        public Sprite GetSprite(string controlPath)
        {
            if (string.IsNullOrEmpty(controlPath))
                return null;

            string layout = InputControlPath.TryGetDeviceLayout(controlPath);
            int controlStart = controlPath.IndexOf(">/", StringComparison.Ordinal);
            if (layout == null || controlStart < 0)
                return null;

            string control = controlPath.Substring(controlStart + 2);
            if (IsDeviceLayout(layout, "Keyboard"))
                return FindSprite(_keyboardAndMouse, KeyboardSpriteName(control));

            if (IsDeviceLayout(layout, "Mouse"))
                return FindSprite(_keyboardAndMouse, MouseSpriteName(control));

            if (!IsDeviceLayout(layout, "Gamepad"))
                return null;

            string button = GamepadButtonName(control);
            if (button == null)
                return null;

            Gamepad gamepad = Gamepad.current;
            if (gamepad is DualSenseGamepadHID || Contains(gamepad, "dualsense")
                || Contains(gamepad, "playstation 5"))
                return FindSprite(_playStation5, "PS5_" + PlayStationButtonName(button));

            if (gamepad is DualShockGamepad || Contains(gamepad, "dualshock")
                || Contains(gamepad, "playstation 4"))
                return FindSprite(_playStation4, "PS4_" + PlayStationButtonName(button));

            return FindSprite(_xbox, "XboxSeriesX_" + button);
        }

        private static bool IsDeviceLayout(string layout, string baseLayout)
        {
            return string.Equals(layout, baseLayout, StringComparison.OrdinalIgnoreCase)
                || InputSystem.IsFirstLayoutBasedOnSecond(layout, baseLayout);
        }

        private static bool Contains(Gamepad gamepad, string name)
        {
            return gamepad != null
                && ((gamepad.description.product?.IndexOf(name, StringComparison.OrdinalIgnoreCase) ?? -1) >= 0
                    || (gamepad.displayName?.IndexOf(name, StringComparison.OrdinalIgnoreCase) ?? -1) >= 0);
        }

        private static Sprite FindSprite(Sprite[] sprites, string name)
        {
            if (name == null || sprites == null)
                return null;

            foreach (Sprite sprite in sprites)
            {
                if (sprite != null && string.Equals(sprite.name, name, StringComparison.OrdinalIgnoreCase))
                    return sprite;
            }

            return null;
        }

        private static string KeyboardSpriteName(string key)
        {
            string name = key.ToLowerInvariant() switch
            {
                "escape" => "Esc",
                "space" => "Space",
                "enter" or "numpadenter" => "Enter",
                "tab" => "Tab",
                "leftshift" or "rightshift" => "Shift",
                "leftctrl" or "rightctrl" => "Ctrl",
                "leftalt" or "rightalt" => "Alt",
                "backspace" => "Backspace",
                "delete" => "Del",
                "uparrow" => "Arrow_Up",
                "downarrow" => "Arrow_Down",
                "leftarrow" => "Arrow_Left",
                "rightarrow" => "Arrow_Right",
                _ => key.StartsWith("digit", StringComparison.OrdinalIgnoreCase)
                    ? key.Substring(5)
                    : key.ToUpperInvariant()
            };

            return name + "_Key_Light";
        }

        private static string MouseSpriteName(string button)
        {
            string name = button.ToLowerInvariant() switch
            {
                "leftbutton" => "Mouse_Left",
                "rightbutton" => "Mouse_Right",
                "middlebutton" => "Mouse_Middle",
                _ => null
            };
            return name == null ? null : name + "_Key_Light";
        }

        private static string GamepadButtonName(string button)
        {
            return button.ToLowerInvariant() switch
            {
                "buttonsouth" => "A",
                "buttoneast" => "B",
                "buttonwest" => "X",
                "buttonnorth" => "Y",
                "leftshoulder" => "LB",
                "rightshoulder" => "RB",
                "lefttrigger" => "LT",
                "righttrigger" => "RT",
                "start" => "Menu",
                "select" => "View",
                "leftstick" => "Left_Stick",
                "rightstick" => "Right_Stick",
                "leftstickpress" => "Left_Stick_Click",
                "rightstickpress" => "Right_Stick_Click",
                "dpad" => "Dpad",
                "dpad/up" => "Dpad_Up",
                "dpad/down" => "Dpad_Down",
                "dpad/left" => "Dpad_Left",
                "dpad/right" => "Dpad_Right",
                _ => null
            };
        }

        private static string PlayStationButtonName(string button)
        {
            return button switch
            {
                "A" => "Cross",
                "B" => "Circle",
                "X" => "Square",
                "Y" => "Triangle",
                "LB" => "L1",
                "RB" => "R1",
                "LT" => "L2",
                "RT" => "R2",
                "Menu" => "Options",
                "View" => "Share",
                _ => button
            };
        }
    }
}
