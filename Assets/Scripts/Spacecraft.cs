using UnityEngine;
using UnityEngine.SceneManagement;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TMPro;

/// <summary>
/// The manager of the spacecraft as a whole. responsible for managing what mode each piece is in as well as activating engines
/// </summary>
public class Spacecraft : MonoBehaviour {
    
    private static Spacecraft Instance;
    public static Spacecraft GetInstance() => Instance;
    
    public static bool IsBuildMode { get; private set; }
    public static bool IsFlightMode { get; private set; }
    
    [SerializeField] private Rigidbody2D rb;
    [SerializeField] private OrbitAssist orbitAssist;
    
    // Health system
    [Header("Health Settings")]
    [SerializeField] private float maxHealth = 100f;
    [SerializeField] public float currentHealth;
    [SerializeField] public float damageCooldown;

    // Events for health changes
    public event EventHandler<float> OnHealthChanged; // Passes current health percentage (0-1)
    
    public float HealthPercentage => maxHealth > 0 ? currentHealth / maxHealth : 0f;

    // Energy system
    [Header("Energy Settings")]
    [SerializeField] private float maxEnergy = 10f;
    [SerializeField] private float currentEnergy;

    public event EventHandler<float> OnEnergyChanged; // Passes current energy percentage (0-1)
    
    [Header("Fuel Settings")]
    [SerializeField] private float fuelPerTank = 50f;
    private float maxFuel = 0f;
    [SerializeField] private float currentFuel;

    public event EventHandler<float> OnFuelChanged; // Passes current fuel percentage (0-1)
    public float EnergyPercentage => maxEnergy > 0 ? currentEnergy / maxEnergy : 0f;
    public float FuelPercentage => maxFuel > 0 ? currentFuel / maxFuel : 0f;
    public Vector3 centerOfMass;

    [Space(20)] 
    [SerializeField] private GameObject[] sparks;
    private Transform[] sparkSpots;
    
    
    private void Awake() {
        if (Instance != null && Instance != this) {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        
        rb = GetComponentInChildren<Rigidbody2D>();
        Settings settings = Settings.Instance;

        if (settings.difficulty == 0) // easy settings
        {
            maxHealth = 90f;
            maxEnergy = 16f;
        } else if (settings.difficulty == 1) // normal settings
        {
            maxHealth = 60f;
            maxEnergy = 12f;
        } else if (settings.difficulty == 2) // hard settings
        {
            maxHealth = 30f;
            maxEnergy = 8f;
        }
        currentHealth = maxHealth;
        currentEnergy = maxEnergy;
        SceneManager.sceneLoaded += OnSceneLoaded;
        SceneManager.sceneUnloaded += OnSceneUnloaded;
    }
    private void Start() {
        UpdatePhysicsMode();
    }

    private void Update() {
       if(IsFlightMode) SpacecraftMotionUI.Instance.UpdateMotion(rb.linearVelocity.magnitude, rb.linearVelocity.normalized);
    }

    private void FixedUpdate() {
        //Done in fixed update so different computers dont do varying amounts of sparks
        if (IsFlightMode && currentHealth < maxHealth) DoSparks();
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode) {
        if(scene.name == "FlightScene") orbitAssist.GetPsycheAsteroid();
        
        // Delay physics update to next frame to ensure all children are initialized
        StartCoroutine(UpdatePhysicsModeDelayed());
    }

    private void OnSceneUnloaded(Scene scene) {
        if (scene.name == "BuildScene") {
            IsBuildMode = false;
            PrepareForFlight();
            return;
        }
        
        if (scene.name == "FlightScene") IsFlightMode = false;
    }

    private IEnumerator UpdatePhysicsModeDelayed() {
        // Wait one frame to ensure all child objects are initialized
        yield return null;
        UpdatePhysicsMode();
    }
    
    private void UpdatePhysicsMode() {
        string currentScene = SceneManager.GetActiveScene().name;
        
        if (currentScene == "BuildScene") {
            SetBuildingMode();
        } else if (currentScene == "FlightScene") {
            SetFlightMode();
        }
    }
    
    private void SetBuildingMode() {
        IsBuildMode = true;
        IsFlightMode = false;
        
        Engine[] engineScripts = GetComponentsInChildren<Engine>();
        
        // Enable PartDrag components in build mode so parts can be dragged
        PartDrag[] partDrags = GetComponentsInChildren<PartDrag>();
        foreach (PartDrag partDrag in partDrags) {
            partDrag.enabled = true;
        }

        // Set main spacecraft to kinematic but keep simulation enabled
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.simulated = true;  // Keep simulated = true so mouse events work
        
        // Disable engines
        foreach (Engine e in engineScripts) {
            e.enabled = false;
        }

        // Disable solar panels
        SolarPanel[] solarPanels = GetComponentsInChildren<SolarPanel>();
        foreach (SolarPanel panel in solarPanels) {
            panel.enabled = false;
        }
    }
    
    private void SetFlightMode() {
        IsBuildMode = false;
        IsFlightMode = true;

        Engine[] engineScripts = GetComponentsInChildren<Engine>();

        // DISABLE PartDrag components in flight mode so parts can't be dragged
        PartDrag[] partDrags = GetComponentsInChildren<PartDrag>();
        foreach (PartDrag partDrag in partDrags) {
            partDrag.enabled = false;
        }

        rb.position = Vector2.zero;
        rb.rotation = 0f;
        transform.position = Vector3.zero;
        transform.rotation = Quaternion.identity;
        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0f;

        rb.simulated = true;
        rb.bodyType = RigidbodyType2D.Dynamic;
        rb.gravityScale = 0f;
        rb.excludeLayers = 0;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        rb.WakeUp();

        // Enable engines
        foreach (Engine e in engineScripts) {
            e.enabled = true;
        }

        // Enable solar panels
        SolarPanel[] solarPanels = GetComponentsInChildren<SolarPanel>();
        foreach (SolarPanel panel in solarPanels) {
            panel.enabled = true;
        }

        // Set max fuel based on tanks
        FuelTank[] fuelTanks = GetComponentsInChildren<FuelTank>();
        maxFuel = fuelPerTank * fuelTanks.Length;

        
        sparkSpots = GetComponentsInChildren<Transform>().Where(obj => obj.CompareTag("SparkSpot")).ToArray();

        // Reset health and energy when entering flight mode
        ResetHealth();
        ResetEnergy();
        ResetFuel();
    }

    public void TakeDamage(float damage) {
        if (damage <= 0) return;
        
        currentHealth = Mathf.Max(0, currentHealth - damage);
        
        // Notify listeners of health change
        OnHealthChanged?.Invoke(this, HealthPercentage);
        
        if (currentHealth <= 0) {
            StartCoroutine(HandleDeath());
            return;
        }

        StartCoroutine(VisualBlinking());
    }
    
    private IEnumerator VisualBlinking() {
        SpriteRenderer[] spacecraftSRs = GetComponentsInChildren<SpriteRenderer>(true);
        TextMeshProUGUI[] spacecraftTMPs = GetComponentsInChildren<TextMeshProUGUI>();
        float currTime = Time.time;
        
        while (currTime + damageCooldown > Time.time) {
            foreach (SpriteRenderer sr in spacecraftSRs) {
                if(sr != null) sr.enabled = !sr.enabled; //Needs null check bc sparks may get destroyed
            }
            foreach (TextMeshProUGUI tmp in spacecraftTMPs) {
                tmp.enabled = !tmp.enabled;
            }
            
            yield return new WaitForSeconds(0.2f);
        }
        
        foreach (SpriteRenderer sr in spacecraftSRs) {
            if(sr != null) sr.enabled = true; //Needs null check bc sparks may get destroyed
        }
        foreach (TextMeshProUGUI tmp in spacecraftTMPs) {
            tmp.enabled = true;
        }
    }
    
    public void Heal(float healAmount) {
        if (healAmount <= 0) return;
        
        Debug.Log($"Healing {healAmount}");
        
        currentHealth = Mathf.Min(maxHealth, currentHealth + healAmount);
        OnHealthChanged?.Invoke(this, HealthPercentage);
    }
    
    public void ResetHealth() {
        currentHealth = maxHealth;
        OnHealthChanged?.Invoke(this, HealthPercentage);
    }

    public void AddEnergy(float amount) {
        if (amount <= 0f) return;
        currentEnergy = Mathf.Min(currentEnergy + amount, maxEnergy);
        OnEnergyChanged?.Invoke(this, EnergyPercentage);
    }

    public bool TryConsumeEnergy(float amount) {
        if (amount <= 0f || currentEnergy < amount) return false;
        currentEnergy -= amount;
        OnEnergyChanged?.Invoke(this, EnergyPercentage);
        return true;
    }

    public void ResetEnergy() {
        currentEnergy = maxEnergy;
        OnEnergyChanged?.Invoke(this, EnergyPercentage);
    }

    public bool TryConsumeFuel(float amount) {
        if (amount <= 0f || currentFuel < amount) return false;
        currentFuel -= amount;
        OnFuelChanged?.Invoke(this, FuelPercentage);
        return true;
    }

    public void ResetFuel() {
        currentFuel = maxFuel;
        OnFuelChanged?.Invoke(this, FuelPercentage);
    }

    public void PrepareForFlight() {
        HandleSpacecraftMass();
        SetPartRigidBodies(false);
    }

    private void HandleSpacecraftMass() {
        SpacecraftPartDatabase partDb = SpacecraftPartDatabase.Instance;
        
        //To find center of mass, use equation (summation of mp) / totalMass.
        //Where m is individual part mass and p is the individual part local position relative to the other parts. 
        float totalMass = 0f;
        Vector2 numerator = new Vector2(0, 0);
        foreach (Transform part in transform) {
            float partMass = partDb.GetMass(partDb.GetPartGameObject(part.name));
            totalMass += partMass;

            numerator += partMass * (Vector2)part.position;
        }

        rb.mass = totalMass;
        centerOfMass = numerator / totalMass;

        foreach (Transform part in transform) {
            part.position -= centerOfMass;
        }

        rb.centerOfMass = Vector2.zero;
    }

    public void SetPartRigidBodies(bool enabled, RigidbodyType2D type = RigidbodyType2D.Dynamic,
        Vector2 linearVelocity = default, bool messyMotion = false) {
        
        if (enabled) {
            if(linearVelocity == default) linearVelocity = Vector2.zero;
            
            foreach (Transform child in transform) {
                Rigidbody2D childRb;
                if(!child.gameObject.TryGetComponent<Rigidbody2D>(out childRb)) {
                    childRb = child.gameObject.AddComponent<Rigidbody2D>();
                    childRb.freezeRotation = true;
                }
                
                childRb.bodyType = type;
                if (messyMotion) {
                    linearVelocity += new Vector2(UnityEngine.Random.Range(-5f, 5f), UnityEngine.Random.Range(-5f, 5f));
                    childRb.freezeRotation = false;
                }
                childRb.linearVelocity = linearVelocity;
            }

            return;
        }
        
        List<Transform> children = new();
        foreach (Transform child in transform) children.Add(child);

        foreach (Transform child in children) {
            Rigidbody2D childRb = child.gameObject.GetComponent<Rigidbody2D>();

            Vector3 worldPos = child.position;
            Quaternion worldRot = child.rotation;

            child.SetParent(null, true);
            if (childRb != null) DestroyImmediate(childRb);
            child.SetParent(transform, true);

            child.position = worldPos;
            child.rotation = worldRot;
        }

        Physics2D.SyncTransforms();
    }

    private void DoSparks() {
        int activeSparkIndex = UnityEngine.Random.Range(0, sparkSpots.Length + (int)(HealthPercentage * 4000));
        
        if (activeSparkIndex >= sparkSpots.Length) return;
        
        GameObject spark = sparks[UnityEngine.Random.Range(0, sparks.Length)];
        Transform sparkSpot = sparkSpots[activeSparkIndex];
        Instantiate(spark, sparkSpot.position, Quaternion.identity, sparkSpot.parent);
    }

    private IEnumerator HandleDeath() {
        SetPartRigidBodies(true, RigidbodyType2D.Dynamic, rb.linearVelocity, true);
        
        rb.simulated = false;
        yield return new WaitForSeconds(3f);
        GameInput.Instance.SetGameOverScene(false);
    }

    private void OnDestroy() {
        if (Instance != this) return;
        
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneUnloaded -= OnSceneUnloaded;
        Instance = null;
        Engine.totalEngineCount = 0;
    }
}