using UnityEngine;
using UnityEngine.InputSystem;

namespace PiGame.UI
{
    public static class DisplayModeController
    {
        private const int DefaultWindowWidth = 960;
        private const int DefaultWindowHeight = 540;

        private static Vector2Int _windowedResolution =
            new Vector2Int(DefaultWindowWidth, DefaultWindowHeight);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            if (Screen.fullScreenMode == FullScreenMode.Windowed)
            {
                _windowedResolution = new Vector2Int(Screen.width, Screen.height);
            }

            InputSystem.onAfterUpdate -= HandleInputAfterUpdate;
            InputSystem.onAfterUpdate += HandleInputAfterUpdate;
        }

        private static void HandleInputAfterUpdate()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }

            bool pressedF11 = keyboard.f11Key.wasPressedThisFrame;
            bool pressedAltEnter = keyboard.enterKey.wasPressedThisFrame
                && (keyboard.leftAltKey.isPressed || keyboard.rightAltKey.isPressed);

            if (pressedF11 || pressedAltEnter)
            {
                ToggleFullscreen();
            }
        }

        public static void ToggleFullscreen()
        {
            if (Screen.fullScreenMode == FullScreenMode.Windowed)
            {
                _windowedResolution = new Vector2Int(Screen.width, Screen.height);
                Screen.SetResolution(
                    Display.main.systemWidth,
                    Display.main.systemHeight,
                    FullScreenMode.FullScreenWindow);
                return;
            }

            Screen.SetResolution(
                _windowedResolution.x,
                _windowedResolution.y,
                FullScreenMode.Windowed);
        }
    }
}
