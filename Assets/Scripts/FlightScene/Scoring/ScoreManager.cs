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

        float timeBonus     = ComputeTimeBonus(elapsedTimeAtStop);
        float fuelBonus     = ComputeFuelBonus(fuelUsedPercent);
        float healthBonus   = ComputeHealthBonus(damageTakenPercent);
        float completion    = completed && config != null ? config.completionBonus : 0f;
        float minScore      = config != null ? config.minScore : 0f;

        float total = timeBonus + fuelBonus + healthBonus + completion;

        if (died && config != null && config.zeroScoreOnDeath) total = minScore;

        total = Mathf.Max(minScore, total);

        var breakdown = new ScoreBreakdown {
            timeBonus = timeBonus,
            fuelBonus = fuelBonus,
            healthBonus = healthBonus,
            completionBonus = completion,
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
                      + (completed ? config.completionBonus : 0f);
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

    private void OnDestroy() {
        OrbitAssist.OnEnteredOrbit -= OrbitAssist_OnEnteredOrbit;
        if (subscribedToEngine && engine != null) engine.OnFuelChanged -= Spacecraft_OnFuelChanged;
        if (subscribedToSpacecraft && spacecraft != null) spacecraft.OnHealthChanged -= Spacecraft_OnHealthChanged;
        if (Instance == this) Instance = null;
    }

    public struct ScoreBreakdown {
        public float timeBonus;
        public float fuelBonus;
        public float healthBonus;
        public float completionBonus;
        public float finalScore;
        public float elapsedSeconds;
        public float fuelUsedPercent;
        public float damageTakenPercent;
    }
}
