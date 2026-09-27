using UnityEngine;

namespace PiGame.UI
{
    [CreateAssetMenu(
        fileName = "data_pause_menu",
        menuName = "Pi Game/UI/Pause Menu Definition")]
    public class PauseMenuDefinition : ScriptableObject
    {
        [Header("Textos")]
        [SerializeField] private string _title = "PAUSE";
        [SerializeField] private string _resumeLabel = "VOLTAR";
        [SerializeField] private bool _showControlsButton;
        [SerializeField] private string _controlsLabel = "CONTROLES";
        [SerializeField] private string _exitLabel = "SAIR";
        [SerializeField] private string _exitConfirmationMessage = "DESEJA SAIR?";

        public string Title => _title;
        public string ResumeLabel => _resumeLabel;
        public bool ShowControlsButton => _showControlsButton;
        public string ControlsLabel => _controlsLabel;
        public string ExitLabel => _exitLabel;
        public string ExitConfirmationMessage => _exitConfirmationMessage;
    }
}
