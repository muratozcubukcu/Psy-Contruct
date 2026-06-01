using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections;

/// <summary>
/// In-scene popup that shows the score breakdown on victory or death.
/// Place on a Canvas panel in the Flight scene and assign the TMP fields.
/// </summary>
public class ScoreBreakdownPopup : MonoBehaviour {

    public static ScoreBreakdownPopup Instance { get; private set; }
    private const int VictoryBannerSortingOrder = 400;
    private static readonly Color PopupButtonColor = new(0.18431373f, 0f, 0.5647059f, 1f);

    [SerializeField] private GameObject panelRoot;
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI breakdownText;
    [SerializeField] private Image victoryBanner;

    [SerializeField] private bool showOnOrbitEntry = true;

    [SerializeField] private Button restartButton;
    [SerializeField] private Button mainMenuButton;

    private void Awake() {
        Instance = this;
        if (showOnOrbitEntry) OrbitAssist.OnEnteredOrbit += OrbitAssist_OnEnteredOrbit;
        EnsureActionButtons();
        if (panelRoot != null) panelRoot.SetActive(false);
    }

    private void OrbitAssist_OnEnteredOrbit(object sender, System.EventArgs e) {
        if (GameInput.Instance != null && GameInput.Instance.IsSettingsSceneOpenAdditively) return;
        ShowVictory();
    }

    private void OnDestroy() {
        OrbitAssist.OnEnteredOrbit -= OrbitAssist_OnEnteredOrbit;
        if (Instance == this) Instance = null;
    }

    public void ShowVictory() => Show(victory: true);
    public void ShowDeath()   => Show(victory: false);

    public void Show(bool victory) {
        if (ScoreManager.Instance == null) return;

        var b = ScoreManager.Instance.FinalizeRun(completed: victory, died: !victory);

        if (titleText != null) titleText.text = victory ? "VICTORY!" : "MISSION FAILED";

        if (breakdownText != null) {
            breakdownText.text = BuildBreakdown(b);
        }

        if (panelRoot != null) panelRoot.SetActive(true);

        StartCoroutine(DropBanner());
    }

    private void EnsureActionButtons() {
        if (panelRoot == null) return;

        RectTransform panelRect = panelRoot.GetComponent<RectTransform>();
        if (panelRect == null) return;

        if (restartButton == null) restartButton = FindActionButton(panelRect, "RestartButton");
        if (restartButton == null) restartButton = CreateActionButton(panelRect, "RestartButton", "RESTART", new Vector2(-120f, -245f));
        restartButton.onClick.RemoveListener(RestartRun);
        restartButton.onClick.AddListener(RestartRun);

        if (mainMenuButton == null) mainMenuButton = FindActionButton(panelRect, "MainMenuButton");
        if (mainMenuButton == null) mainMenuButton = CreateActionButton(panelRect, "MainMenuButton", "MAIN MENU", new Vector2(120f, -245f));
        mainMenuButton.onClick.RemoveListener(GoToMainMenu);
        mainMenuButton.onClick.AddListener(GoToMainMenu);
    }

    private static Button FindActionButton(RectTransform parent, string objectName) {
        Transform child = parent.Find(objectName);
        return child != null ? child.GetComponent<Button>() : null;
    }

    private static Button CreateActionButton(RectTransform parent, string objectName, string label, Vector2 anchoredPosition) {
        GameObject buttonGO = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        buttonGO.transform.SetParent(parent, false);
        buttonGO.transform.SetAsLastSibling();

        RectTransform buttonRect = buttonGO.GetComponent<RectTransform>();
        buttonRect.anchorMin = new Vector2(0.5f, 0.5f);
        buttonRect.anchorMax = new Vector2(0.5f, 0.5f);
        buttonRect.pivot = new Vector2(0.5f, 0.5f);
        buttonRect.anchoredPosition = anchoredPosition;
        buttonRect.sizeDelta = new Vector2(210f, 54f);

        Image image = buttonGO.GetComponent<Image>();
        image.color = PopupButtonColor;
        image.raycastTarget = true;

        Button button = buttonGO.GetComponent<Button>();
        button.targetGraphic = image;

        GameObject textGO = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        textGO.transform.SetParent(buttonGO.transform, false);

        RectTransform textRect = textGO.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        TextMeshProUGUI text = textGO.GetComponent<TextMeshProUGUI>();
        text.text = label;
        text.fontSize = 20f;
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.white;
        text.raycastTarget = false;

        return button;
    }

    public void RestartRun() {
        Time.timeScale = 1f;

        Spacecraft existing = Spacecraft.GetInstance();
        if (existing != null) Destroy(existing.gameObject);

        GameInput.Instance.SetBuildScene();
    }

    public void GoToMainMenu() {
        Time.timeScale = 1f;
        GameInput.Instance.SetMainMenuScene();
    }

    private IEnumerator DropBanner()
    {
        if (victoryBanner == null) yield break;

        RectTransform bannerTransform = victoryBanner.rectTransform;
        RectTransform parentTransform = bannerTransform.parent as RectTransform;
        if (parentTransform == null) yield break;

        parentTransform.SetAsLastSibling();
        bannerTransform.SetAsLastSibling();
        Canvas bannerCanvas = victoryBanner.GetComponent<Canvas>();
        if (bannerCanvas == null) bannerCanvas = victoryBanner.gameObject.AddComponent<Canvas>();

        bannerCanvas.overrideSorting = true;
        bannerCanvas.sortingOrder = VictoryBannerSortingOrder;

        float dropTime = 2f;
        float elapsedTime = 0f;
        float bannerHalfHeightPixels = bannerTransform.rect.height * bannerTransform.lossyScale.y * 0.5f;

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            parentTransform,
            new Vector2(Screen.width * 0.5f, Screen.height - bannerHalfHeightPixels),
            null,
            out Vector2 destination);

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            parentTransform,
            new Vector2(Screen.width * 0.5f, Screen.height + bannerHalfHeightPixels),
            null,
            out Vector2 startPosition);

        bannerTransform.anchoredPosition = startPosition;
        while (dropTime > elapsedTime) {
            elapsedTime += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsedTime / dropTime);
            bannerTransform.anchoredPosition = Vector2.Lerp(startPosition, destination, t);
            yield return null;
        }

        bannerTransform.anchoredPosition = destination;
    }

    private string BuildBreakdown(ScoreManager.ScoreBreakdown b) {
        var sb = new System.Text.StringBuilder(512);

        AppendRow(sb, "Time Bonus",        b.timeBonus,               $"({b.elapsedSeconds:F1}s)");
        AppendRow(sb, "Fuel Bonus",        b.fuelBonus,               $"({100f - b.fuelUsedPercent:F0}%)");
        AppendRow(sb, "Health Bonus",      b.healthBonus,             $"({100f - b.damageTakenPercent:F0}%)");
        AppendRow(sb, "Slingshot Bonus",   b.slingshotPrecisionBonus, "");
        sb.Append('\n');
        sb.Append("Difficulty modifier       x" + b.difficultyModifier + "\n");
        sb.Append('\n');
        sb.Append($"FINAL SCORE: {Mathf.RoundToInt(b.finalScore)}");
        return sb.ToString();
    }

    private static void AppendRow(System.Text.StringBuilder sb, string label, float bonus, string note) {
        const int LABEL_WIDTH = 22;
        const int BONUS_WIDTH = 8;
        string labelCell = label.PadRight(LABEL_WIDTH);
        string bonusCell = ("+" + Mathf.RoundToInt(bonus).ToString()).PadLeft(BONUS_WIDTH);
        sb.Append(labelCell).Append(bonusCell);
        if (!string.IsNullOrEmpty(note)) sb.Append("  ").Append(note);
        sb.Append('\n');
    }

}
