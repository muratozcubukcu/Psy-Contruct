using UnityEngine;
using TMPro;

/// <summary>
/// In-scene popup that shows the score breakdown on victory or death.
/// Place on a Canvas panel in the Flight scene and assign the TMP fields.
/// </summary>
public class ScoreBreakdownPopup : MonoBehaviour {

    public static ScoreBreakdownPopup Instance { get; private set; }

    [SerializeField] private GameObject panelRoot;
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI breakdownText;

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
            breakdownText.text =
                $"Time Bonus:         +{Mathf.RoundToInt(b.timeBonus)}  ({b.elapsedSeconds:F1}s)\n" +
                $"Fuel Bonus:         +{Mathf.RoundToInt(b.fuelBonus)}  ({100f - b.fuelUsedPercent:F0}% remaining)\n" +
                $"Health Bonus:       +{Mathf.RoundToInt(b.healthBonus)}  ({100f - b.damageTakenPercent:F0}% remaining)\n" +
                $"Completion Bonus: +{Mathf.RoundToInt(b.completionBonus)}\n" +
                $"\n" +
                $"FINAL SCORE: {Mathf.RoundToInt(b.finalScore)}";
        }

        if (panelRoot != null) panelRoot.SetActive(true);
    }
}
