using UnityEngine;

/// <summary>
/// Represents the sun in the scene. Solar panels check their angle relative to this object.
/// </summary>
public class Sun : MonoBehaviour {
    public static Sun Instance;
    private Spacecraft spacecraft;

    private void Awake() {
        if (Instance != null && Instance != this) {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        
        spacecraft = Spacecraft.GetInstance();
    }
    
    private void OnCollisionEnter2D(Collision2D collision) {
        if (collision.gameObject != spacecraft.gameObject) return;
        
        spacecraft.TakeDamage(collision.relativeVelocity.magnitude * 3);
    }

    private void OnDestroy() {
        if (Instance == this) Instance = null;
    }
}
