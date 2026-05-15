using System;
using UnityEngine;

public class Earth : MonoBehaviour {
    public static Earth Instance;
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
        
        spacecraft.TakeDamage((float)Math.Pow(collision.relativeVelocity.magnitude, 1.5));
    }

    private void OnDestroy() {
        if (Instance == this) Instance = null;
    }
}
