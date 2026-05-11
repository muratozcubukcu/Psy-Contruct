using UnityEngine;
using System.Collections;

/// <summary>
/// Tracks time, fuel used, and damage taken during a flight run and computes a score.
/// Place one in the Flight scene and assign a ScoreConfig asset.
/// </summary>
public class ScoreManager : MonoBehaviour {

    public static ScoreManager Instance { get; private set; }

    // Survives scene transition to GameOverScene
    public static ScoreBreakdown? LastBreakdown { get; private set; }

    [Tooltip("Tunable weights / base score. Required.")]
    [SerializeField] private ScoreConfig config;
    public ScoreConfig Config => config;

    [Tooltip("Start tracking automatically when the scene loads.")]
    [SerializeField] private bool autoStart = true;

    [Tooltip("Delay (seconds) before hooking into Spacecraft/Engine, since the Engine is built at runtime from parts.")]
    [SerializeField] private float hookupDelay = 0.5f;


    // Run state
    private bool isTracking;
    private float startTime;
    private float elapsedTimeAtStop;

    // Penalty inputs (all in 0-100 percent units)
    private float fuelUsedPercent;
    private float damageTakenPercent;

    // Last seen values for delta calculation
    private float lastFuelPercent01 = 1f;
    private float lastHealthPercent01 = 1f;

    // Subscriptions
    private Engine engine;
    private Spacecraft spacecraft;
    private bool subscribedToEngine;
    private bool subscribedToSpacecraft;

    private MarsSlingshotPlanner slingshotPlanner;
    private bool slingshotInProgress;
    private bool slingshotCompleted;
    private float slingshotDeviationAccum;
    private int slingshotDeviationSamples;   // green frames
    private int slingshotInRangeFrames;      // every frame in Mars range

    private float difficultyModifier;

    public float ElapsedTime => isTracking ? Time.time - startTime : elapsedTimeAtStop;
    public float FuelUsedPercent => fuelUsedPercent;
    public float DamageTakenPercent => damageTakenPercent;

    private void Awake() {
        if (Instance != null && Instance != this) {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (config == null) {
            Debug.LogError("ScoreManager: No ScoreConfig assigned. Scoring will not work.");
        }
    }

    private void Start() {
        StartCoroutine(HookUpReferences());
        if (autoStart) BeginRun();
        OrbitAssist.OnEnteredOrbit += OrbitAssist_OnEnteredOrbit;

        slingshotPlanner = FindFirstObjectByType<MarsSlingshotPlanner>();
        MarsSlingshotPlanner.OnSlingshotEntered += SlingshotPlanner_OnEntered;
        MarsSlingshotPlanner.OnSlingshotExited += SlingshotPlanner_OnExited;

        Settings settings = Settings.Instance;
        
        if (settings.difficulty == 0) {
            difficultyModifier = 1f;
            config.timeBonusDurationSeconds = 200;
            config.slingshotHeadingTolerance*=1.5f;
        } else if (settings.difficulty == 1) {
            difficultyModifier = 1.5f;
            config.timeBonusDurationSeconds = 175;
        } else if (settings.difficulty == 2) {
            difficultyModifier = 2f;
            config.timeBonusDurationSeconds = 150;
            config.slingshotHeadingTolerance*=0.8f;
        }
    }

    private void Update() {
        if (!slingshotInProgress || !isTracking) return;
        if (slingshotPlanner == null || spacecraft == null) return;

        Vector2 shipPos = spacecraft.transform.position;
        float d = slingshotPlanner.DistanceFromPath(shipPos, out Vector2 pathDir);
        if (d < 0f) return;

        // Heading uses the ship's facing direction, not velocity, so the
        // check responds the instant the player rotates back on course.
        float headingAngle = 180f;
        if (pathDir.sqrMagnitude > 1e-6f) {
            Vector2 facing = spacecraft.transform.up;
            if (facing.sqrMagnitude > 1e-6f) {
                headingAngle = Vector2.Angle(facing.normalized, pathDir);
            }
        }

        float distTol    = config != null ? config.slingshotPathTolerance    : float.PositiveInfinity;
        float headingTol = config != null ? config.slingshotHeadingTolerance : float.PositiveInfinity;
        bool onBoth = d <= distTol && headingAngle <= headingTol;

        // Bonus = correct frames / total in-range frames.
        slingshotInRangeFrames++;

        if (onBoth) {
            slingshotDeviationAccum += d;
            slingshotDeviationSamples++;
        }

        if (MinimapManager.Instance != null) {
            MinimapManager.Instance.highlightBorder = onBoth;
        }
    }

    private void SlingshotPlanner_OnEntered() {
        slingshotInProgress = true;
    }

    private void SlingshotPlanner_OnExited() {
        slingshotInProgress = false;
        if (slingshotDeviationSamples > 0) slingshotCompleted = true;
        if (MinimapManager.Instance != null) MinimapManager.Instance.highlightBorder = false;
    }

    private void OrbitAssist_OnEnteredOrbit(object sender, System.EventArgs e) {
        StopRun();
    }

    private IEnumerator HookUpReferences() {
        // Engine is built at runtime from parts, so wait until both exist.
        yield return new WaitForSeconds(hookupDelay);

        while (spacecraft == null) {
            spacecraft = Spacecraft.GetInstance();
            if (spacecraft == null) yield return new WaitForSeconds(0.1f);
        }

        lastFuelPercent01 = spacecraft.FuelPercentage;
        lastHealthPercent01 = spacecraft.HealthPercentage;

        spacecraft.OnFuelChanged += Spacecraft_OnFuelChanged;
        subscribedToEngine = true;

        spacecraft.OnHealthChanged += Spacecraft_OnHealthChanged;
        subscribedToSpacecraft = true;
    }

    public void BeginRun() {
        startTime = Time.time;
        elapsedTimeAtStop = 0f;
        fuelUsedPercent = 0f;
        damageTakenPercent = 0f;
        slingshotInProgress = false;
        slingshotCompleted = false;
        slingshotDeviationAccum = 0f;
        slingshotDeviationSamples = 0;
        slingshotInRangeFrames = 0;
        isTracking = true;
    }

    public void StopRun() {
        if (!isTracking) return;
        elapsedTimeAtStop = Time.time - startTime;
        isTracking = false;
    }

    private void Spacecraft_OnFuelChanged(object sender, float fuelPercent01) {
        if (!isTracking) { lastFuelPercent01 = fuelPercent01; return; }
        float delta = lastFuelPercent01 - fuelPercent01; // positive = consumed
        if (delta > 0f) fuelUsedPercent += delta * 100f;
        lastFuelPercent01 = fuelPercent01;
    }

    private void Spacecraft_OnHealthChanged(object sender, float healthPercent01) {
        if (!isTracking) { lastHealthPercent01 = healthPercent01; return; }
        float delta = lastHealthPercent01 - healthPercent01; // positive = damage taken
        if (delta > 0f) damageTakenPercent += delta * 100f;
        lastHealthPercent01 = healthPercent01;
    }

    /// <summary>
    /// Computes the current (live) score using current penalty totals.
    /// </summary>
    public float GetCurrentScore() {
        return ComputeScore(ElapsedTime, fuelUsedPercent, damageTakenPercent, completed: false, died: false);
    }

    /// <summary>
    /// Stops tracking and computes the final score for the run.
    /// </summary>
    public ScoreBreakdown FinalizeRun(bool completed, bool died) {
        StopRun();

        float timeBonus       = ComputeTimeBonus(elapsedTimeAtStop);
        float fuelBonus       = ComputeFuelBonus(fuelUsedPercent);
        float healthBonus     = ComputeHealthBonus(damageTakenPercent);
        float slingshotPrec   = ComputeSlingshotPrecisionBonus(died);
        float avgDev          = slingshotDeviationSamples > 0
                                    ? slingshotDeviationAccum / slingshotDeviationSamples
                                    : 0f;
        float minScore        = config != null ? config.minScore : 0f;

        float total = timeBonus + fuelBonus + healthBonus + slingshotPrec;

        total *= difficultyModifier;

        if (died && config != null && config.zeroScoreOnDeath) total = minScore;

        total = Mathf.Max(minScore, total);

        var breakdown = new ScoreBreakdown {
            timeBonus = timeBonus,
            fuelBonus = fuelBonus,
            healthBonus = healthBonus,
            slingshotPrecisionBonus = slingshotPrec,
            slingshotPrecisionBonusMax = config != null ? config.slingshotPrecisionBonusMax : 0f,
            slingshotCompleted = slingshotDeviationSamples > 0,
            averageSlingshotDeviation = avgDev,
            difficultyModifier = difficultyModifier,
            finalScore = total,
            elapsedSeconds = elapsedTimeAtStop,
            fuelUsedPercent = fuelUsedPercent,
            damageTakenPercent = damageTakenPercent,
        };

        LastBreakdown = breakdown;
        return breakdown;
    }

    private float ComputeScore(float seconds, float fuelPct, float dmgPct, bool completed, bool died) {
        if (config == null) return 0f;
        float total = ComputeTimeBonus(seconds)
                      + ComputeFuelBonus(fuelPct)
                      + ComputeHealthBonus(dmgPct)
                      + ComputeSlingshotPrecisionBonus(died);
        if (died && config.zeroScoreOnDeath) total = config.minScore;
        return Mathf.Max(config.minScore, total);
    }

    private float ComputeTimeBonus(float seconds) {
        if (config == null || config.timeBonusDurationSeconds <= 0f) return 0f;
        float remaining = 1f - seconds / config.timeBonusDurationSeconds;
        return Mathf.Max(0f, config.timeBonusMax * remaining);
    }

    private float ComputeFuelBonus(float fuelUsedPct) {
        if (config == null) return 0f;
        float remainingFraction = Mathf.Clamp01((100f - fuelUsedPct) / 100f);
        return config.fuelBonusMax * remainingFraction;
    }

    private float ComputeHealthBonus(float damageTakenPct) {
        if (config == null) return 0f;
        float remainingFraction = Mathf.Clamp01((100f - damageTakenPct) / 100f);
        return config.healthBonusMax * remainingFraction;
    }

    private float ComputeSlingshotPrecisionBonus(bool died) {
        if (config == null) return 0f;
        if (died && config.zeroScoreOnDeath) return 0f;
        if (slingshotInRangeFrames == 0) return 0f;


        float quality = (float)slingshotDeviationSamples / slingshotInRangeFrames;
        return config.slingshotPrecisionBonusMax * quality;
    }

    private void OnDestroy() {
        OrbitAssist.OnEnteredOrbit -= OrbitAssist_OnEnteredOrbit;
        MarsSlingshotPlanner.OnSlingshotEntered -= SlingshotPlanner_OnEntered;
        MarsSlingshotPlanner.OnSlingshotExited -= SlingshotPlanner_OnExited;
        if (subscribedToEngine && engine != null) engine.OnFuelChanged -= Spacecraft_OnFuelChanged;
        if (subscribedToSpacecraft && spacecraft != null) spacecraft.OnHealthChanged -= Spacecraft_OnHealthChanged;
        if (Instance == this) Instance = null;
    }

    public struct ScoreBreakdown {
        public float timeBonus;
        public float fuelBonus;
        public float healthBonus;
        public float slingshotPrecisionBonus;
        public float slingshotPrecisionBonusMax;
        public bool slingshotCompleted;
        public float averageSlingshotDeviation;
        public float difficultyModifier;
        public float finalScore;
        public float elapsedSeconds;
        public float fuelUsedPercent;
        public float damageTakenPercent;
    }
}
