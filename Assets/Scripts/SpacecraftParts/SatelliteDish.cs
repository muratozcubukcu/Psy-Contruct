using UnityEngine;
using UnityEngine.SceneManagement;

public class SatelliteDish : MonoBehaviour {
    
    [SerializeField] private float facingThreshold;
    private RepairQuickTimeUI repairQuickTimeUI;
    
    private GameInput gameInput;
    private bool doQuickTime = false;

    private void Awake() {
        gameInput = GameInput.Instance;
    }
    
    private void Start() {
        gameInput.OnRepairShipPerformedAction += GameInput_OnRepairShipPerformedAction;
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
