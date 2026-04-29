using System;
using UnityEngine;

public class Mars : MonoBehaviour {
    public static Mars Instance;

    public MinimapTrigger minimapTrigger;

    private void Awake() {
        if (Instance != null && Instance != this) {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void OnDestroy() {
        if (Instance == this) Instance = null;
    }
}
