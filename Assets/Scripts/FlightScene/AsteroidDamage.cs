using System;
using System.Collections;
using Codice.Client.Common.EventTracking;
using TreeEditor;
using UnityEngine;

/// <summary>
/// Script that damages the spacecraft when it collides with an asteroid.
/// this is attached to asteroids.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class AsteroidDamage : MonoBehaviour {
    [SerializeField] private AsteroidFlight asterFlight;
    [SerializeField] public GameObject splitAster1 = null;
    [SerializeField] public GameObject splitAster2 = null;
    
    [Header("Damage Settings")]
    [Tooltip("Amount of damage to deal to the spacecraft on collision")]
    [SerializeField] private float damage = 10f;
    
    [Header("Behavior Settings")]
    [Tooltip("Destroy this asteroid on collision with spacecraft")]
    [SerializeField] private bool destroyOnCollision;
    
    [Tooltip("Disable this component after first collision")]
    [SerializeField] private bool disableAfterCollision;

    private AsteroidController asteroidController;
    private Spacecraft spacecraft;
    
    private int spacecraftLayer;
    private int asteroidLayer;
    private bool justSplitOff;
    public bool aster1Split;
    public bool aster2Split;
    
    

    //Start func is used for this bc AsteroidController Instance is defined after this Awake() method is called.
    private void Start() {
        asteroidController = AsteroidController.Instance;
        spacecraft = Spacecraft.GetInstance();
        
        spacecraftLayer = LayerMask.NameToLayer("SpaceCraft");
        asteroidLayer = LayerMask.NameToLayer("Asteroid");
    }
    
    private void OnCollisionEnter2D(Collision2D collision) {
        if (collision.gameObject.layer == spacecraftLayer && spacecraft.currentHealth <= 0) return;
        
        if (collision.gameObject.CompareTag("Gravity") ||
            collision.gameObject.GetComponentInChildren<PlanetGravitySource>() != null) {
            
            AsteroidController.Instance.DestroyAsteroid(gameObject);
            return;
        }
        
        HandleCollision(collision.gameObject, collision.contacts[0].point);
        if (collision.gameObject.layer == spacecraftLayer) HandleSpacecraftCollision(collision);
    }
    
    private void HandleCollision(GameObject other, Vector3 collisionPosition) {
        if(!other.TryGetComponent(out AsteroidFlight otherFlight)) {
            AsteroidController.Instance.Explode(collisionPosition);
            return;
        }
        
        //If asteroids are gonna collide off camera, just make them move away from each other instead.
        //This keeps a more steady amount of asteroids over time by avoiding them splitting.
        if (!AsteroidController.Instance.IsVisibleToCamera(transform.position) && 
            !AsteroidController.Instance.IsVisibleToCamera(other.transform.position)) {
            
            AsteroidController.Instance.SwapAsteroidMotion(asterFlight, otherFlight);
            return;
        }

        //If an asteroid just split, just make them move away from each other instead.
        //This helps prevent asteroid splitting chain reactions.
        if (justSplitOff || (other.TryGetComponent(out AsteroidDamage otherDamage) && otherDamage.justSplitOff)) {
            AsteroidController.Instance.SwapAsteroidMotion(asterFlight, otherFlight);
            return;
        }
        
        AsteroidController.Instance.Explode(collisionPosition);
        
        if (other.layer == asteroidLayer) SplitAsteroid(other);
    }

    private void HandleSpacecraftCollision(Collision2D collision) {
        float spacecraftSpeedPreCollision = asterFlight.speed - collision.relativeVelocity.magnitude;

        Vector3 dirTowardsAster = (spacecraft.transform.position - (Vector3)collision.contacts[0].point).normalized;
        spacecraft.GetComponent<Rigidbody2D>().linearVelocity = -dirTowardsAster * spacecraftSpeedPreCollision;
        SplitAsteroid(collision.gameObject);
        
        spacecraft.TakeDamage(damage);
    }

    private void SplitAsteroid(GameObject contactAsteroid) {
        asteroidController.SplitAsteroid(gameObject, contactAsteroid);
        Destroy(gameObject);
    }

    //Minimizes asteroid splittng chain reactions by briefly giving them immunity after splitting
    public IEnumerator HandlePostSplitImmunity() {
        justSplitOff = true;
        yield return new WaitForSeconds(0.15f);
        justSplitOff = false;
    }
    
    public void SetDamage(float newDamage) {
        damage = newDamage;
    }
    
    public void SetDestroyOnCollision(bool shouldDestroy) {
        destroyOnCollision = shouldDestroy;
    }
}