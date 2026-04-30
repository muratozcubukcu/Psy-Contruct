using UnityEngine;

public class SettingsMenuUI : MonoBehaviour
{
    [SerializeField] private GameObject menuPanel;

    private bool menuOpen = false;

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
        }
        else
        {
            Time.timeScale = 1f; // resume game
        }
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