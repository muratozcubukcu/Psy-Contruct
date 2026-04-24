using UnityEngine;

/// <summary>
/// Represents the sun in the scene. Solar panels check their angle relative to this object.
/// </summary>
public class Sun : MonoBehaviour {
    public static Sun Instance;

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
