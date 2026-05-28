using UnityEngine;
using UnityEngine.SceneManagement;

public class SatelliteDish : MonoBehaviour {

    [SerializeField] private float facingThreshold;
    private RepairQuickTimeUI repairQuickTimeUI;

    private GameInput gameInput;
    private GameObject waveChild;
    private bool wavePreviousState;

    private void Awake() {
        gameInput = GameInput.Instance;

        // SatelliteWave child renders the animated radio-wave overlay used during the repair minigame.
        // Kept disabled outside the minigame so the dish appears static.
        Transform waveTransform = transform.Find("SatelliteWave");
        if (waveTransform != null) {
            waveChild = waveTransform.gameObject;
            waveChild.SetActive(false);
        }
    }

    private void Start() {
        gameInput.OnRepairShipPerformedAction += GameInput_OnRepairShipPerformedAction;
    }

    private void Update() {
        // Mirror the minigame UI's active state onto the wave overlay so it only animates during repair.
        if (waveChild == null || RepairQuickTimeUI.Instance == null) return;
        bool minigameActive = RepairQuickTimeUI.Instance.gameObject.activeInHierarchy;
        if (minigameActive != wavePreviousState) {
            waveChild.SetActive(minigameActive);
            wavePreviousState = minigameActive;
        }
    }

    private bool IsFacingEarth() {
        Vector2 directionToEarth = (Earth.Instance.transform.position - transform.position).normalized;
        float dot = Vector2.Dot(transform.up, directionToEarth);

        return dot > facingThreshold;
    }

    private void GameInput_OnRepairShipPerformedAction(object sender, System.EventArgs e) {
        Debug.Log($"Satellite facing earth: {IsFacingEarth()}");
        RepairQuickTimeUI.Instance.gameObject.SetActive(IsFacingEarth());
    }

    private void OnDestroy() {
        gameInput.OnRepairShipPerformedAction -= GameInput_OnRepairShipPerformedAction;
    }
}
