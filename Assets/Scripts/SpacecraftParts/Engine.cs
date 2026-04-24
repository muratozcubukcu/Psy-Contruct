using System;
using TMPro;
using UnityEngine;

//Class defines the behavior of the engine part. 
public class Engine : MonoBehaviour {
    [SerializeField] private int speed;
    [SerializeField] private float initialSpeedRampUpLength;
    [SerializeField] private float softSpeedLimit;
    [SerializeField] private float thrustFalloffStrength;
    [SerializeField] private float stabilizationStrength;
    [SerializeField] private SpriteRenderer engineVisual;
    [SerializeField] private TextMeshProUGUI idUI;
    
    [Header("Fuel Settings")]
    [SerializeField] float fuelCostPerSecond = 1f;

    [Header("Energy Settings")]
    [SerializeField] private float energyCostPerSecond = 1f;

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

    public event System.EventHandler<float> OnFuelChanged;


    public void Awake() {
        totalEngineCount++;
        engineID = totalEngineCount;

        gameInput = GameInput.Instance;
        spacecraft = Spacecraft.GetInstance();
        spacecraftRB = spacecraft.gameObject.GetComponent<Rigidbody2D>();
    }

    public void Start() {
        gameInput.OnEnginePerformedAction += GameInput_OnEngineAction; //Adds GameInput_OnEngineAction() as a listener to the OnEngineAction event. 
        gameInput.OnEngineCanceledAction += GameInput_OnEngineAction;
    }
    
    private void FixedUpdate() {
        if (active && TryConsumeEnergy() && TryConsumeFuel()) ActivateEngine();
    }

    private void ActivateEngine() {
        Vector2 initialThrust = speed * InitialSpeedRampUp() * transform.up;
        Vector2 finalThrust = SoftSpeedLimitMultiplier(initialThrust);
        
        spacecraftRB.AddForceAtPosition(finalThrust, transform.position);
        spacecraftRB.AddTorque(-spacecraftRB.angularVelocity * stabilizationStrength);
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
        float finalParallelThrustMultiplier = Mathf.Lerp(1f, initialParallelThrustMultiplier, forceFacingMotion);
        
        return (parallelThrust * finalParallelThrustMultiplier) + perpendicularThrust;
    }

    private float InitialSpeedRampUp() {
        engineActiveTime += Time.fixedDeltaTime;
        
        if (engineActiveTime >= initialSpeedRampUpLength) return 1f;

        return (engineActiveTime + initialSpeedRampUpLength) / (initialSpeedRampUpLength * 2);
    }
    
    private void GameInput_OnEngineAction(object sender, GameInput.EngineEventArgs e) { 
        if(engineID == e.engineNum) active = e.activated;
            
        if (active) {
            engineVisual.color = Color.red;
            engineActiveTime = 0f;
        }
        else engineVisual.color = Color.yellow;
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
        gameInput.OnEnginePerformedAction -= GameInput_OnEngineAction;
        gameInput.OnEngineCanceledAction -= GameInput_OnEngineAction;
        AdjustEngineIDsForDeletion(gameObject);
    }
}
