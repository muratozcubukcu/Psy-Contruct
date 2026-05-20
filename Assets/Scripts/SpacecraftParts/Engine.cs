using System;
using TMPro;
using UnityEngine;

//Class defines the behavior of the engine part.
public class Engine : MonoBehaviour {
    [SerializeField] private int speed;
    [SerializeField] private float initialSpeedRampUpLength;
    [SerializeField] private float softSpeedLimit;
    [SerializeField] private float thrustFalloffStrength;
    [SerializeField] private SpriteRenderer engineVisual;
    [SerializeField] private TextMeshProUGUI idUI;

    [Header("Engine Visuals")]
    [Tooltip("Sprite shown when the engine is idle.")]
    [SerializeField] private Sprite engineOffSprite;
    [Tooltip("Sprite shown when the engine is firing.")]
    [SerializeField] private Sprite engineOnSprite;
    [Tooltip("EngineFire flame prefab (Animator-driven) spawned as a child and toggled when firing.")]
    [SerializeField] private Animator engineFirePrefab;
    [Tooltip("Local-space offset from the engine root where the flame is anchored (top-pivot of the flame sprite).")]
    [SerializeField] private Vector2 engineFireOffset;

    [Header("Fuel Settings")]
    [SerializeField] float fuelCostPerSecond;

    [Header("Energy Settings")]
    [SerializeField] private float energyCostPerSecond;

    [SerializeField] private int _engineID;
    public int engineID {
        get => _engineID;
        set {
            _engineID = value;
            idUI.text = value.ToString();
        }
    }
    
    public static int totalEngineCount;
    private GameInput gameInput;
    private Spacecraft spacecraft;
    private Rigidbody2D spacecraftRB;
    private bool active;
    private float fuelAmount;
    private float engineActiveTime;
    private Animator engineFireAnimator;
    private bool firingVisual;

    public void Awake() {
        totalEngineCount++;
        engineID = totalEngineCount;

        gameInput = GameInput.Instance;
        spacecraft = Spacecraft.GetInstance();
        spacecraftRB = spacecraft.gameObject.GetComponent<Rigidbody2D>();

        if (engineFirePrefab != null) {
            engineFireAnimator = Instantiate(engineFirePrefab, transform);
            engineFireAnimator.transform.localPosition = engineFireOffset;
            engineFireAnimator.gameObject.SetActive(false);
        }

        ApplyVisualState(false);
    }

    public void Start() {
        gameInput.OnEnginePerformedAction += GameInput_OnNumericEngineAction;
        gameInput.OnEngineCanceledAction += GameInput_OnNumericEngineAction;
    }
    
    private void FixedUpdate() {
        bool thrusting = active && TryConsumeEnergy() && TryConsumeFuel();
        if (thrusting) ActivateEngine();
        else if (active) engineActiveTime = 0f; // starve out the ramp-up while resources are missing

        if (thrusting != firingVisual) {
            firingVisual = thrusting;
            ApplyVisualState(thrusting);
        }
    }

    private void ActivateEngine() {
        Vector2 initialThrust = speed * InitialSpeedRampUp() * -transform.up;
        Vector2 finalThrust = SoftSpeedLimitMultiplier(initialThrust);
        
        Vector2 distanceToShipCOM = (Vector2)transform.position - spacecraftRB.worldCenterOfMass;
        float torqueApplied = distanceToShipCOM.x * finalThrust.y - distanceToShipCOM.y * finalThrust.x;
        float torqueWithoutSoftLimit = distanceToShipCOM.x * initialThrust.y - distanceToShipCOM.y * initialThrust.x;
        
        spacecraftRB.AddForceAtPosition(finalThrust, transform.position);
        
        //Adds the additional torque that got removed from the soft speed limit multiplier
        spacecraftRB.AddTorque(torqueWithoutSoftLimit - torqueApplied);
    }

    //Makes gaining speed increasingly difficult as speed increases, especially past the soft speed limit. Only applies
    //to the thrust in the direction of motion, minimizing the effect when thrusters are used for either turning or
    //slowing down.
    private Vector2 SoftSpeedLimitMultiplier(Vector2 initialThrust) {
        Vector2 velocityDir = spacecraftRB.linearVelocity.normalized;
        
        //Thrusts parallel and perpendicular to the current motion
        Vector2 parallelThrust = Vector3.Project(initialThrust, velocityDir);
        Vector2 perpendicularThrust = initialThrust - parallelThrust;

        //Range between 0 and 1 where 1 is the engines thrust is completely facing the direction of motion, and 0 is
        //the thrust is facing the opposite
        float forceFacingMotion = (Vector2.Dot(parallelThrust.normalized, velocityDir) + 1f) / 2f;
        
        float speedRatio = spacecraftRB.linearVelocity.magnitude / softSpeedLimit; //(Current speed)/(soft speed limit)
        float initialParallelThrustMultiplier = 1f / (1f + Mathf.Pow(speedRatio, thrustFalloffStrength));

        //Uses Mathf.Lerp to allow for a smooth transition from the soft speed limit effecting the engine thrust when
        //the thrust is facing in the direction of motion, to a non-existent soft speed limit effect when the thrust is
        //facing opposite the direction of motion.
        //Going faster and in dir of motion  ->  smaller thrustMultiplier
        //Smaller thrustMultiplier           ->  engine has less impact
        float thrustMultiplier = Mathf.Lerp(1f, initialParallelThrustMultiplier, forceFacingMotion);
        
        return (parallelThrust * thrustMultiplier) + perpendicularThrust;
    }

    private float InitialSpeedRampUp() {
        engineActiveTime += Time.fixedDeltaTime;
        
        if (engineActiveTime >= initialSpeedRampUpLength) return 1f;

        return (engineActiveTime + initialSpeedRampUpLength) / (initialSpeedRampUpLength * 2);
    }
    
    private void GameInput_OnNumericEngineAction(object sender, GameInput.EngineEventArgs e) {
        if (e.engineNum != engineID) return;

        bool wasActive = active;
        active = e.activated;
        if (active == wasActive) return;
        if (active) engineActiveTime = 0f;
        if (!active && firingVisual) {
            firingVisual = false;
            ApplyVisualState(false);
        }
    }

    private void ApplyVisualState(bool firing) {
        if (engineVisual != null) {
            Sprite target = firing ? engineOnSprite : engineOffSprite;
            if (target == null) return;
            
            engineVisual.sprite = target;
            engineVisual.color = Color.white;
        }
        if (engineFireAnimator != null) engineFireAnimator.gameObject.SetActive(firing);
    }

    private bool TryConsumeFuel() {
        if (spacecraft == null) return false;
        float fuelCost = fuelCostPerSecond * Time.fixedDeltaTime;
        return spacecraft.TryConsumeFuel(fuelCost);
    }
    
    private void AdjustEngineIDsForDeletion(GameObject engineToBeDeleted) {
        if (!engineToBeDeleted.TryGetComponent<Engine>(out Engine deletedEngine)) return;
        int engineID = deletedEngine.engineID;
        int totalEngines = totalEngineCount;

        totalEngineCount = Math.Max(0, totalEngineCount - 1);

        if (engineID == totalEngines) return;
        if (spacecraft == null) return;
        
        foreach (Transform child in spacecraft.transform) {
            if (!child.TryGetComponent(out Engine otherEngine)) continue;

            if (otherEngine.engineID > engineID) otherEngine.engineID--;
        }
    }

    private bool TryConsumeEnergy() {
        if (spacecraft == null) return false;
        float energyCost = energyCostPerSecond * Time.fixedDeltaTime;
        return spacecraft.TryConsumeEnergy(energyCost);
    }

    private void OnDestroy() {
        if (gameInput != null) {
            gameInput.OnEnginePerformedAction -= GameInput_OnNumericEngineAction;
            gameInput.OnEngineCanceledAction -= GameInput_OnNumericEngineAction;
        }
        AdjustEngineIDsForDeletion(gameObject);
    }
}
