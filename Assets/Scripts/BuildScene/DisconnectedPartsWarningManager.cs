using System;
using UnityEngine;
using UnityEngine.PlayerLoop;

public class DisconnectedPartsWarningManager : MonoBehaviour {

    public static DisconnectedPartsWarningManager Instance;

    [SerializeField] private Canvas canvas;

    private void Start() => Instance = this;

    public void IgnoreButtonClicked() {
        ShipBuildingGrid.Instance.RemoveDisconnectedParts();
        GameInput.Instance.SetFlightFactsScene();
    }
    
    public void KeepBuildingButtonClicked() {
        canvas.enabled = false;
        StartCoroutine(ShipBuildingGrid.Instance.FadeClearDisconnectedHighlights());
    }

    public void DisplayWarning() {
        canvas.enabled = true;
    }
}
