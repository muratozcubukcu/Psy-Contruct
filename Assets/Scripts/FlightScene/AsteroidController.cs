using System.Net;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using System.Numerics;
using Vector3 = UnityEngine.Vector3;
using Quaternion = UnityEngine.Quaternion;

//Static class used to keep track of asteroids, spawning them in and destroying them and stuff
public class AsteroidController : MonoBehaviour {
    public static AsteroidController Instance { get; private set; }
    
    [SerializeField] private int maxAsteroids;
    [SerializeField] private int minAsteroidSpeed;
    [SerializeField] private int maxAsteroidSpeed;
    [SerializeField] private GameObject hugeAsteroid;
    [SerializeField] private GameObject bigAsteroid;
    [SerializeField] private GameObject medAsteroid;
    [SerializeField] private GameObject smallAsteroid;
    [SerializeField] private Camera camera;
    [SerializeField] private float damageCooldown = 1f;
    
    private int currentAsteroidCount;
    private float timeUntilNextAsteroidSpawn = 5f;
    private float maxHorizontalSpawnRange;
    private float maxVerticalSpawnRange;
    private float distanceFromCameraBorder = 2f;

    private Dictionary<GameObject, float> outOfCameraTimes = new();
    private void Awake() {
        Instance = this;
        maxHorizontalSpawnRange = (camera.GetComponent<FlightCamera>().GetCameraWidth() / 2) + distanceFromCameraBorder;
        maxVerticalSpawnRange = (camera.GetComponent<FlightCamera>().GetCameraHeight() / 2) + distanceFromCameraBorder;
    }

    private void Start() {
        FlightCamera.OnAsteroidPassing += FlightCamera_OnAsteroidPassingAction;
    }
    
    private void Update() {
        foreach (GameObject asteroid in outOfCameraTimes.Keys.ToList()) {
            outOfCameraTimes[asteroid] += Time.deltaTime;
            if(outOfCameraTimes[asteroid] >= 3f) DestroyAsteroid(asteroid);
        }
        
        if (currentAsteroidCount == maxAsteroids) return;

        timeUntilNextAsteroidSpawn -= Time.deltaTime;
        if (timeUntilNextAsteroidSpawn <= 0) SpawnAsteroid();
    }

    private void SpawnAsteroid() {
        GameObject[] spawnPool = { hugeAsteroid, bigAsteroid, medAsteroid };
        GameObject nextAsteroid = spawnPool[UnityEngine.Random.Range(0, spawnPool.Length)];
        int spawnSide;
        Vector3 spawnPosition = GetSpawnPosition(out spawnSide);
        Collider2D[] spawnPositionOverlaps = Physics2D.OverlapCircleAll(spawnPosition, (float)(distanceFromCameraBorder - .1));

        foreach (Collider2D c in spawnPositionOverlaps) {
            if (!c.gameObject.CompareTag("Gravity")) return;
        }
        if (outOfCameraTimes.ContainsKey(nextAsteroid)) return;

        GameObject asteroid = Instantiate(nextAsteroid, spawnPosition, UnityEngine.Quaternion.identity);
        asteroid.GetComponent<AsteroidFlight>().spawnSide = spawnSide;

        outOfCameraTimes.Add(asteroid, 0f);
        timeUntilNextAsteroidSpawn = UnityEngine.Random.Range(1, 2); //Adjust asteroid spawn frequency here
        currentAsteroidCount++;
    }

    public void SplitAsteroid(GameObject sourceAsteroid)
    {
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
        outOfCameraTimes.Add(asteroidLeft, 0f);
        GameObject asteroidRight = Instantiate(nextAsteroid, sourceTransform.position, Quaternion.identity);
        asteroidRight.GetComponent<AsteroidFlight>().direction = Quaternion.Euler(0,0,120) * sourceFlight.direction;
        outOfCameraTimes.Add(asteroidRight, 0f);
    }

    private Vector3 GetSpawnPosition(out int spawnSide) {
        spawnSide = UnityEngine.Random.Range(0, 4);
        Vector3 offset = new Vector3();

        switch (spawnSide) {
            case 0: //Spawn above camera
                offset = new Vector3(UnityEngine.Random.Range(-maxHorizontalSpawnRange, maxHorizontalSpawnRange), maxVerticalSpawnRange, -camera.transform.position.z);
                break;
            case 1: //below
                offset = new Vector3(UnityEngine.Random.Range(-maxHorizontalSpawnRange, maxHorizontalSpawnRange), -maxVerticalSpawnRange, -camera.transform.position.z);
                break;
            case 2: //left
                offset = new Vector3(-maxHorizontalSpawnRange, UnityEngine.Random.Range(-maxVerticalSpawnRange, maxVerticalSpawnRange), -camera.transform.position.z);
                break;
            case 3: //right
                offset = new Vector3(maxHorizontalSpawnRange, UnityEngine.Random.Range(-maxVerticalSpawnRange, maxVerticalSpawnRange), -camera.transform.position.z);
                break;
        }

        return camera.transform.position + offset;
    }

    public void DestroyAsteroid(GameObject asteroid) {
        outOfCameraTimes.Remove(asteroid);
        Destroy(asteroid);
        currentAsteroidCount--;
    }

    private void FlightCamera_OnAsteroidPassingAction(object sender, FlightCamera.AsteroidPassingEventArgs e) {
        if(e.isEntering) outOfCameraTimes.Remove(e.asteroid);
        else outOfCameraTimes.Add(e.asteroid, 0f);
    }

    public float GetDamageCoolDown() => damageCooldown;
}
