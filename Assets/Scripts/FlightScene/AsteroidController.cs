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

    public float largestAsteroidRadius = 6f; //If largest asteroid size changes, update this number.
    private float timeUntilNextAsteroidSpawn = 5f;
    private float distanceFromCameraBorder;
    private float defaultXSpawnRange;
    private float defaultYSpawnRange;
    private Rigidbody2D spacecraftRB;
    private bool spawningEnabled = true;

    private Dictionary<GameObject, float> offCameraLifetimes = new();
    private void Awake() {
        Instance = this;
        
        distanceFromCameraBorder = 2f + largestAsteroidRadius;
        
        defaultXSpawnRange = (camera.GetComponent<FlightCamera>().GetCameraWidth() / 2) + distanceFromCameraBorder;
        defaultYSpawnRange = (camera.GetComponent<FlightCamera>().GetCameraHeight() / 2) + distanceFromCameraBorder;
    }

    private void Start() {
        FlightCamera.OnAsteroidPassing += FlightCamera_OnAsteroidPassingAction;
        OrbitAssist.OnEnteredOrbit += OnEnteredOrbit;

        spacecraftRB = Spacecraft.GetInstance().GetComponent<Rigidbody2D>();
    }
    
    private void Update() {
        foreach (GameObject asteroid in offCameraLifetimes.Keys.ToList()) {
            offCameraLifetimes[asteroid] += Time.deltaTime;
            if(offCameraLifetimes[asteroid] >= 5f) DestroyAsteroid(asteroid);
        }

        timeUntilNextAsteroidSpawn -= Time.deltaTime;
        if (timeUntilNextAsteroidSpawn <= 0 && spawningEnabled) SpawnAsteroid();
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
        timeUntilNextAsteroidSpawn = UnityEngine.Random.Range(.5f, 1f);
    }

    public void SplitAsteroid(GameObject sourceAsteroid, GameObject contactAsteroid) {
        AsteroidDamage sourceDamage = sourceAsteroid.GetComponent<AsteroidDamage>();
        
        if (sourceDamage.splitAster1 != null) {
            TrySpawnSplitAsteroid(sourceDamage.splitAster1, sourceAsteroid, contactAsteroid);
            sourceDamage.aster1Split = true;
        }
        if (sourceDamage.splitAster2 != null) {
            TrySpawnSplitAsteroid(sourceDamage.splitAster2, sourceAsteroid, contactAsteroid);
            sourceDamage.aster2Split = true;
        }
    }

    private void TrySpawnSplitAsteroid(GameObject splitAsterPrefab, GameObject originalAster, GameObject contactAster) {
        AsteroidDamage OGAsterDamage = originalAster.GetComponent<AsteroidDamage>();
        if (OGAsterDamage.splitAster1 == splitAsterPrefab && OGAsterDamage.aster1Split) return;
        if (OGAsterDamage.splitAster2 == splitAsterPrefab && OGAsterDamage.aster2Split) return;
        
        GameObject splitAster = Instantiate(splitAsterPrefab, splitAsterPrefab.transform.position, Quaternion.identity);
        Destroy(splitAsterPrefab);
        splitAster.SetActive(true);
        splitAster.transform.localScale = new Vector3(2f, 2f, 2f);
        if (CanSpawnSplitAsteroid(splitAster, originalAster, contactAster)) {
            StartCoroutine(splitAster.GetComponent<AsteroidDamage>().HandlePostSplitImmunity());
            splitAster.GetComponent<SpriteRenderer>().enabled = true;
            offCameraLifetimes.Add(splitAster, 0f);
        }
        else Destroy(splitAster);
    }

    private bool CanSpawnSplitAsteroid(GameObject splitAster, GameObject originalAster, GameObject contactAster) {
        
        PolygonCollider2D splitAsterCol = splitAster.GetComponent<PolygonCollider2D>();
        List<Collider2D> collisions = new List<Collider2D>();
        splitAsterCol.Overlap(new ContactFilter2D(), collisions);
        
        foreach (Collider2D col in collisions) {
            if (col.gameObject.layer == LayerMask.NameToLayer("SpaceCraft") || 
                (col.gameObject != originalAster && col.gameObject != contactAster)) 
                return false;
        }
        return true;
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

    public void SwapAsteroidMotion(AsteroidFlight aster1, AsteroidFlight aster2) { 
        (aster2.direction, aster1.direction) = (aster1.direction, aster2.direction);

        aster1.ChangeMotion((aster1.transform.position - aster2.transform.position).normalized);
        aster2.ChangeMotion((aster2.transform.position - aster1.transform.position).normalized);
    }
    
    public void Explode(Vector3 position) => StartCoroutine(DoExplosion(position));

    private IEnumerator DoExplosion(Vector3 position) {
        GameObject explosion = Instantiate(explosionPrefab, position, Quaternion.identity);
        yield return new WaitForSeconds(0.25f);
        Destroy(explosion);
    }

    private void OnEnteredOrbit(object sender, System.EventArgs e) {
        spawningEnabled = false;
    }

    private void OnDestroy() {
        FlightCamera.OnAsteroidPassing -= FlightCamera_OnAsteroidPassingAction;
        OrbitAssist.OnEnteredOrbit -= OnEnteredOrbit;
    }

    public void DestroyAsteroid(GameObject asteroid) {
        offCameraLifetimes.Remove(asteroid);
        Destroy(asteroid);
    }

    private void FlightCamera_OnAsteroidPassingAction(object sender, FlightCamera.AsteroidPassingEventArgs e) {
        if(e.isEntering) offCameraLifetimes.Remove(e.asteroid);
        else offCameraLifetimes.Add(e.asteroid, 0f);
    }
}
