using UnityEngine;

public class PsycheAsteroid : MonoBehaviour {
    public static PsycheAsteroid Instance;
    private Spacecraft spacecraft;

    private void Awake() {
        if (Instance != null && Instance != this) {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        
        spacecraft = Spacecraft.GetInstance();
        transform.position = Settings.Instance.psychePosition;
        
        Debug.Log(Settings.Instance.psychePosition);
    }
    
    private void OnCollisionEnter2D(Collision2D collision) {
        if (collision.gameObject != spacecraft.gameObject) return;
        
        spacecraft.TakeDamage(collision.relativeVelocity.magnitude * 3);
    }

    private void OnDestroy() {
        if (Instance == this) Instance = null;
    }
}
