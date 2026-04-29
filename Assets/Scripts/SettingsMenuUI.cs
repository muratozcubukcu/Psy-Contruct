using TMPro;
using UnityEngine;

public class SettingsMenuUI : MonoBehaviour
{
    [SerializeField] private GameObject menuPanel;

    [Header("Controls Toggle")]
    [SerializeField] private TextMeshProUGUI controlsButtonLabel;
    [SerializeField] private string controlsLabelFormat = "Controls: {0}";

    private bool menuOpen = false;

    void Start() {
        RefreshControlsLabel();
    }

    void Update() {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            ToggleMenu();
        }
    }

    public void ToggleMenu() {
        menuOpen = !menuOpen;
        menuPanel.SetActive(menuOpen);

        if (menuOpen)
        {
            Time.timeScale = 0f; // pause game
            RefreshControlsLabel();
        }
        else
        {
            Time.timeScale = 1f; // resume game
        }
    }

    public void ToggleControlScheme() {
        if (Settings.Instance == null) return;
        Settings.Instance.toggleControlScheme();
        RefreshControlsLabel();
    }

    private void RefreshControlsLabel() {
        if (controlsButtonLabel == null || Settings.Instance == null) return;
        controlsButtonLabel.text = string.Format(controlsLabelFormat, Settings.Instance.ControlSchemeLabel);
    }

    public void ResumeGame() {
        menuOpen = false;
        menuPanel.SetActive(false);
        Time.timeScale = 1f;
    }

    public void GoToMainMenu() {
        Time.timeScale = 1f;
        GameInput.Instance.SetMainMenuScene();
        
        ShipBuildingGrid.Instance.SaveGridState(false);
    }

    public void GoToBuildScene() {
        Time.timeScale = 1f;
        GameInput.Instance.SetBuildScene();
    }
}