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

    [SerializeField] private GameObject panelRoot;
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI breakdownText;
    [SerializeField] private Image victoryBanner;

    [SerializeField] private bool showOnOrbitEntry = true;

    private void Awake() {
        Instance = this;
        if (showOnOrbitEntry) OrbitAssist.OnEnteredOrbit += OrbitAssist_OnEnteredOrbit;
        if (panelRoot != null) panelRoot.SetActive(false);
    }

    private void OrbitAssist_OnEnteredOrbit(object sender, System.EventArgs e) {
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

    private IEnumerator DropBanner()
    {
        float dropTime = 2f;
        float elapsedTime = 0f;
        Vector3 originalPosition = victoryBanner.transform.position;
        Vector3 destination = new Vector3(1000,900,0);
        while (dropTime > elapsedTime) {
            elapsedTime += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsedTime / dropTime);
            victoryBanner.transform.position = Vector3.Lerp(originalPosition, destination, t);
            yield return null;
        }
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
