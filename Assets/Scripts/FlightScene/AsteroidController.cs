using System.Net;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using System.Numerics;
using Vector3 = UnityEngine.Vector3;
using Quaternion = UnityEngine.Quaternion;

//Static class used to keep track of asteroids, spawning them in and destroying them and stuff
public class AsteroidController : MonoBehaviour {
    public static AsteroidController Instance { get; private set; }

    [SerializeField] private GameObject explosionPrefab;
    [SerializeField] private int minAsteroidSpeed;
    [SerializeField] private int maxAsteroidSpeed;
    [SerializeField] private GameObject hugeAsteroid;
    [SerializeField] private GameObject bigAsteroid;
    [SerializeField] private GameObject medAsteroid;
    [SerializeField] private GameObject smallAsteroid;
    [SerializeField] private Camera camera;
    [SerializeField] private float damageCooldown = 1f;

    public float largestAsteroidRadius = 6f; //If largest asteroid size changes, update this number.
    private float timeUntilNextAsteroidSpawn = 5f;
    private float distanceFromCameraBorder;
    private float defaultXSpawnRange;
    private float defaultYSpawnRange;
    private Rigidbody2D spacecraftRB;
    
    private Dictionary<GameObject, float> offCameraLifetimes = new();
    private void Awake() {
        Instance = this;
        
        distanceFromCameraBorder = 2f + largestAsteroidRadius;
        
        defaultXSpawnRange = (camera.GetComponent<FlightCamera>().GetCameraWidth() / 2) + distanceFromCameraBorder;
        defaultYSpawnRange = (camera.GetComponent<FlightCamera>().GetCameraHeight() / 2) + distanceFromCameraBorder;
    }

    private void Start() {
        FlightCamera.OnAsteroidPassing += FlightCamera_OnAsteroidPassingAction;
        
        spacecraftRB = Spacecraft.GetInstance().GetComponent<Rigidbody2D>();
    }
    
    private void Update() {
        foreach (GameObject asteroid in offCameraLifetimes.Keys.ToList()) {
            offCameraLifetimes[asteroid] += Time.deltaTime;
            if(offCameraLifetimes[asteroid] >= 3f) DestroyAsteroid(asteroid);
        }

        timeUntilNextAsteroidSpawn -= Time.deltaTime;
        if (timeUntilNextAsteroidSpawn <= 0) SpawnAsteroid();
    }

    private void SpawnAsteroid() {
        GameObject[] spawnPool = { hugeAsteroid, bigAsteroid, medAsteroid };
        GameObject nextAsteroid = spawnPool[UnityEngine.Random.Range(0, spawnPool.Length)];

        Vector3 spawnPosition = GetSpawnPosition();
        Collider2D[] spawnPositionOverlaps = Physics2D.OverlapCircleAll(spawnPosition, largestAsteroidRadius);
        foreach (Collider2D c in spawnPositionOverlaps) {
            if (!c.gameObject.CompareTag("Gravity")) return;
        }
        if (offCameraLifetimes.ContainsKey(nextAsteroid)) return;
        
        GameObject asteroid = Instantiate(nextAsteroid, spawnPosition, Quaternion.identity);

        offCameraLifetimes.Add(asteroid, 0f);
        float shipSpeedDivider = (spacecraftRB.linearVelocity.magnitude / 1.5f) + 1;
        shipSpeedDivider = 1;
        timeUntilNextAsteroidSpawn = UnityEngine.Random.Range(.5f / shipSpeedDivider, 1f / shipSpeedDivider);
    }

    public void SplitAsteroid(GameObject sourceAsteroid) {
        AsteroidFlight sourceFlight = sourceAsteroid.GetComponent<AsteroidFlight>();
        Transform sourceTransform = sourceAsteroid.GetComponent<Transform>();
        
        int sourceAsteroidSize = sourceFlight.asteroidSize;
        GameObject nextAsteroid;
        switch (sourceAsteroidSize) {
            case 3:
                nextAsteroid = bigAsteroid;
                break;
            case 2:
                nextAsteroid = medAsteroid;
                break;
            case 1:
                nextAsteroid = smallAsteroid;
                break;
            default:
                return;
        }

        GameObject asteroidLeft = Instantiate(nextAsteroid, sourceTransform.position, Quaternion.identity);
        asteroidLeft.GetComponent<AsteroidFlight>().direction = Quaternion.Euler(0,0,-120) * sourceFlight.direction;
        offCameraLifetimes.Add(asteroidLeft, 0f);
        GameObject asteroidRight = Instantiate(nextAsteroid, sourceTransform.position, Quaternion.identity);
        asteroidRight.GetComponent<AsteroidFlight>().direction = Quaternion.Euler(0,0,120) * sourceFlight.direction;
        offCameraLifetimes.Add(asteroidRight, 0f);
    }

    private Vector3 GetSpawnPosition(int tries = 0) {
        float upperXSpawnRange = defaultXSpawnRange;
        float upperYSpawnRange = defaultYSpawnRange;
        float lowerXSpawnRange = -defaultXSpawnRange;
        float lowerYSpawnRange = -defaultYSpawnRange;

        if (tries == 400) return new Vector3(upperXSpawnRange, upperYSpawnRange);

        Vector3 velocity = spacecraftRB.linearVelocity;

        if (velocity.x > 0) upperXSpawnRange += distanceFromCameraBorder;
        else lowerXSpawnRange -= distanceFromCameraBorder;
        
        if(velocity.y > 0) upperYSpawnRange += distanceFromCameraBorder;
        else lowerYSpawnRange -= distanceFromCameraBorder;
        
        float x = UnityEngine.Random.Range(lowerXSpawnRange, upperXSpawnRange);
        float y = UnityEngine.Random.Range(lowerYSpawnRange, upperYSpawnRange);

        Vector3 spawnPosition = new Vector3(x, y, -camera.transform.position.z) + camera.transform.position;

        if (IsVisibleToCamera(spawnPosition)) return GetSpawnPosition(tries + 1);
        
        return spawnPosition;
    }
    
    public bool IsVisibleToCamera(Vector3 worldPos) {
        Collider2D[] spawnPositionOverlaps = Physics2D.OverlapCircleAll(worldPos, largestAsteroidRadius);
        foreach (Collider2D c in spawnPositionOverlaps) {
            if (c.gameObject.CompareTag("MainCamera")) return true;
        }

        return false;
    }
    
    public void Explode(Vector3 position) => StartCoroutine(DoExplosion(position));

    private IEnumerator DoExplosion(Vector3 position) {
        GameObject explosion = Instantiate(explosionPrefab, position, Quaternion.identity);
        yield return new WaitForSeconds(0.25f);
        Destroy(explosion);
    }


    public void DestroyAsteroid(GameObject asteroid) {
        offCameraLifetimes.Remove(asteroid);
        Destroy(asteroid);
    }

    private void FlightCamera_OnAsteroidPassingAction(object sender, FlightCamera.AsteroidPassingEventArgs e) {
        if(e.isEntering) offCameraLifetimes.Remove(e.asteroid);
        else offCameraLifetimes.Add(e.asteroid, 0f);
    }

    public float GetDamageCoolDown() => damageCooldown;
}
