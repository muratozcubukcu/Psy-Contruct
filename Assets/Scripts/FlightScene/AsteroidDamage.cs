using System;
using System.Collections;
using TreeEditor;
using UnityEngine;

/// <summary>
/// Script that damages the spacecraft when it collides with an asteroid.
/// this is attached to asteroids.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class AsteroidDamage : MonoBehaviour {
    [SerializeField] private AsteroidFlight asterFlight;
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
    
    private float lastDamageTime;
    private float damageCooldown;
    private int spacecraftLayer;
    private int asteroidLayer;
    

    //Start func is used for this bc AsteroidController Instance is defined after this Awake() method is called.
    private void Start() {
        asteroidController = AsteroidController.Instance;
        spacecraft = Spacecraft.GetInstance();
        
        damageCooldown = asteroidController.GetDamageCoolDown();
        lastDamageTime = Time.time;
        
        spacecraftLayer = LayerMask.NameToLayer("SpaceCraft");
        asteroidLayer = LayerMask.NameToLayer("Asteroid");
    }
    
    private void OnCollisionEnter2D(Collision2D collision) {
        if (collision.gameObject == spacecraft.gameObject && spacecraft.currentHealth <= 0) return;
        
        if (collision.gameObject.CompareTag("Gravity") ||
            collision.gameObject.GetComponentInChildren<PlanetGravitySource>() != null) {
            
            AsteroidController.Instance.DestroyAsteroid(gameObject);
            return;
        }
        
        HandleCollision(collision.gameObject, collision.contacts[0].point);
        if (collision.gameObject.layer == spacecraftLayer) HandleSpacecraftCollision(collision);
    }
    
    private void HandleCollision(GameObject other, Vector3 collisionPosition) {
        Debug.Log("Init: " + other.gameObject.name);
        if (other == spacecraft.gameObject && damageCooldown > 0f && Time.time < lastDamageTime + damageCooldown) return;
        
        //If asteroids are gonna collide off camera, just make them move away from each other instead.
        //This keeps a more steady amount of asteroids over time by avoiding them splitting.
        if (other.TryGetComponent(out AsteroidFlight otherFlight) &&
            !AsteroidController.Instance.IsVisibleToCamera(transform.position) &&
            !AsteroidController.Instance.IsVisibleToCamera(other.transform.position)) {
            
            (otherFlight.direction, asterFlight.direction) = (asterFlight.direction, otherFlight.direction);

            asterFlight.ChangeMotion((transform.position - other.transform.position).normalized);
            otherFlight.ChangeMotion((other.transform.position - transform.position).normalized);

            return;
        }
            

        AsteroidController.Instance.Explode(collisionPosition);
        
        if (other.layer == asteroidLayer) SplitAsteroid();
    }

    private void HandleSpacecraftCollision(Collision2D collision) {
        if (damageCooldown > 0f && Time.time < lastDamageTime + damageCooldown) return;
        
        Spacecraft spacecraft = Spacecraft.GetInstance();

        float relativeSpeed = collision.relativeVelocity.magnitude;
        float asteroidSpeedPreCollision = asterFlight.speed;
        float spacecraftSpeedPreCollision = asteroidSpeedPreCollision - relativeSpeed;

        Vector3 directionTowardsAster = (spacecraft.transform.position - (Vector3)collision.contacts[0].point).normalized;
        spacecraft.GetComponent<Rigidbody2D>().linearVelocity = -directionTowardsAster * spacecraftSpeedPreCollision;
        
        spacecraft.TakeDamage(damage);
        SplitAsteroid();
        lastDamageTime = Time.time;
    }

    private void SplitAsteroid() {
        asteroidController.SplitAsteroid(gameObject);
        Destroy(gameObject);
    }
    
    public void SetDamage(float newDamage) {
        damage = newDamage;
    }
    
    public void SetDestroyOnCollision(bool shouldDestroy) {
        destroyOnCollision = shouldDestroy;
    }
}