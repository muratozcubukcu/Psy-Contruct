using UnityEngine;

public class SettingsMenuUI : MonoBehaviour
{
    [SerializeField] private GameObject menuPanel;
    [SerializeField] private GameObject settingsButton;

    private CanvasGroup menuCanvasGroup;
    private Canvas menuCanvas;
    private UnityEngine.UI.Graphic[] menuGraphics;
    private bool menuOpen = false;
    private const int PauseMenuSortingOrder = 500;

    private void Awake() {
        if (menuPanel == null) menuPanel = gameObject;
        if (settingsButton == null) settingsButton = FindSibling("SettingsButton");
        menuCanvas = menuPanel.GetComponent<Canvas>();
        if (menuCanvas == null) menuCanvas = menuPanel.AddComponent<Canvas>();
        menuCanvas.overrideSorting = true;
        menuCanvas.sortingOrder = PauseMenuSortingOrder;
        if (menuPanel.GetComponent<UnityEngine.UI.GraphicRaycaster>() == null) {
            menuPanel.AddComponent<UnityEngine.UI.GraphicRaycaster>();
        }

        menuCanvasGroup = menuPanel.GetComponent<CanvasGroup>();
        if (menuCanvasGroup == null) menuCanvasGroup = menuPanel.AddComponent<CanvasGroup>();
        menuGraphics = menuPanel.GetComponentsInChildren<UnityEngine.UI.Graphic>(true);
        SendPauseBackgroundBehindButtons();
        SetMenuVisible(menuPanel.activeSelf);
    }

    private void OnEnable() {
        if (GameInput.Instance != null) GameInput.Instance.OnAdditiveSettingsClosed += GameInput_OnAdditiveSettingsClosed;
    }

    private void OnDisable() {
        if (GameInput.Instance != null) GameInput.Instance.OnAdditiveSettingsClosed -= GameInput_OnAdditiveSettingsClosed;
    }

    void Update() {
        if (GameInput.Instance != null && GameInput.Instance.IsSettingsSceneOpenAdditively) return;

        if (menuOpen && !menuCanvasGroup.interactable) {
            SetMenuVisible(true);
        }

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            ToggleMenu();
        }
    }

    public void ToggleMenu() {
        if (GameInput.Instance != null && GameInput.Instance.IsSettingsSceneOpenAdditively) return;

        menuOpen = !menuOpen;
        SetMenuVisible(menuOpen);
        SetSettingsButtonVisible(true);

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
        SetMenuVisible(false);
        SetSettingsButtonVisible(true);
        Time.timeScale = 1f;
    }

    public void GoToMainMenu() {
        Time.timeScale = 1f;
        if (ShipBuildingGrid.Instance != null) ShipBuildingGrid.Instance.SaveGridState(false);
        GameInput.Instance.SetMainMenuScene();
    }

    public void GoToBuildScene() {
        Time.timeScale = 1f;
        GameInput.Instance.SetBuildScene();
    }

    public void GoToSettingsScene() {
        menuOpen = true;
        SetMenuVisible(false);
        SetSettingsButtonVisible(true);
        Time.timeScale = 1f;
        GameInput.Instance.SetSettingsSceneAdditive("FlightScene");
    }

    private void GameInput_OnAdditiveSettingsClosed(object sender, System.EventArgs e) {
        menuOpen = false;
        SetMenuVisible(false);
        SetSettingsButtonVisible(true);
        Time.timeScale = 1f;
    }

    private GameObject FindSibling(string siblingName) {
        Transform parent = transform.parent;
        if (parent == null) return null;

        Transform sibling = parent.Find(siblingName);
        return sibling != null ? sibling.gameObject : null;
    }

    private void SetSettingsButtonVisible(bool visible) {
        if (settingsButton == null) settingsButton = FindSibling("SettingsButton");
        if (settingsButton == null) return;

        settingsButton.SetActive(visible);
        settingsButton.transform.SetAsLastSibling();
    }

    private void SetMenuVisible(bool visible) {
        if (menuPanel == null) return;

        if (!menuPanel.activeSelf) menuPanel.SetActive(true);
        if (visible) {
            menuPanel.transform.SetAsLastSibling();
            SendPauseBackgroundBehindButtons();
            SetSettingsButtonVisible(true);
        }

        if (menuCanvasGroup == null) menuCanvasGroup = menuPanel.GetComponent<CanvasGroup>();
        if (menuCanvasGroup == null) menuCanvasGroup = menuPanel.AddComponent<CanvasGroup>();

        menuCanvasGroup.alpha = visible ? 1f : 0f;
        menuCanvasGroup.interactable = visible;
        menuCanvasGroup.blocksRaycasts = visible;

        if (menuGraphics == null || menuGraphics.Length == 0) {
            menuGraphics = menuPanel.GetComponentsInChildren<UnityEngine.UI.Graphic>(true);
        }

        foreach (UnityEngine.UI.Graphic graphic in menuGraphics) {
            if (graphic != null) graphic.enabled = visible;
        }
    }

    private void SendPauseBackgroundBehindButtons() {
        Transform background = menuPanel != null ? menuPanel.transform.Find("PauseBackground") : null;
        if (background != null) background.SetAsFirstSibling();
    }
}
