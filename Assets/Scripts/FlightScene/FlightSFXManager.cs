using System.Collections;
using UnityEngine;

/// <summary>
/// audio manager for FlightScene SFX. Lives on a GameObject in FlightScene
/// </summary>
public class FlightSFXManager : MonoBehaviour {
    public static FlightSFXManager Instance { get; private set; }

    [Header("Thrusters")]
    [Tooltip("AudioSource that plays the looping thruster sound. Should have loop=false in inspector; the script sets loop=true on Awake.")]
    [SerializeField] private AudioSource thrusterLoopSource;
    [Tooltip("AudioSource that plays the one-shot thruster fade tail.")]
    [SerializeField] private AudioSource thrusterFadeSource;
    [SerializeField] private AudioClip thrusterLoopClip;
    [SerializeField] private AudioClip thrusterFadeClip;
    [Tooltip("Volume multiplier for the looping thruster sound (stacked on top of Settings.sfxVolume).")]
    [Range(0f, 1f)]
    [SerializeField] private float thrusterVolumeScale = 1f;
    [Tooltip("Volume multiplier for the fade-out tail. Drop this if the front of the fade clip is louder than the loop.")]
    [Range(0f, 1f)]
    [SerializeField] private float thrusterFadeVolumeScale = 0.5f;
    [Tooltip("How long (seconds) the loop keeps playing into the fade tail after engines stop, ramping its volume down. Smooths the volume jump when the fade clip starts.")]
    [Range(0f, 1f)]
    [SerializeField] private float thrusterFadeOverlap = 0.2f;
    [Tooltip("Seconds the fade clip ramps up from silent to full volume at its start. Mutes the loud beginning of the fade clip so it doesn't punch over the loop.")]
    [Range(0f, 1f)]
    [SerializeField] private float thrusterFadeInDuration = 0.15f;

    [Header("Impacts")]
    [Tooltip("AudioSource ship-hit impact clips play through (PlayOneShot). Put one on this GameObject.")]
    [SerializeField] private AudioSource impactSource;
    [Tooltip("AudioSource for asteroid-vs-asteroid impacts. MUST live on its own GameObject, because the AudioLowPassFilter this script attaches would otherwise muffle every other AudioSource on the same GameObject. If left null, falls back to impactSource (no muffling).")]
    [SerializeField] private AudioSource asteroidImpactSource;
    [Tooltip("Lower = more muffled. ~22000 = no filtering, ~1500 = in another room, ~800 = inside cockpit, ~500 = underwater.")]
    [Range(80f, 22000f)]
    [SerializeField] private float asteroidImpactLowPassCutoff = 800f;
    [Tooltip("Resonance bump at the cutoff frequency. ~1 = none, ~2 = slight boost (warmer), ~5 = obvious peak.")]
    [Range(1f, 10f)]
    [SerializeField] private float asteroidImpactLowPassResonance = 1.5f;
    [Tooltip("One is picked at random for each impact. Add as many as you want.")]
    [SerializeField] private AudioClip[] impactClips;
    [Tooltip("Volume multiplier for ship-hit impacts (stacked on top of Settings.sfxVolume).")]
    [Range(0f, 1f)]
    [SerializeField] private float impactVolumeScale = 1f;
    [Tooltip("Volume multiplier for asteroid-vs-asteroid impacts. Kept lower than ship hits so background collisions don't drown out the player's own hits.")]
    [Range(0f, 1f)]
    [SerializeField] private float asteroidImpactVolumeScale = 0.35f;

    [Header("Resource Depletion Warnings")]
    [Tooltip("AudioSource fuel/energy depleted warnings play through (PlayOneShot).")]
    [SerializeField] private AudioSource lowResourceSource;
    [SerializeField] private AudioClip fuelLowClip;
    [SerializeField] private AudioClip energyLowClip;
    [Tooltip("Volume multiplier for depletion warnings.")]
    [Range(0f, 1f)]
    [SerializeField] private float lowResourceVolumeScale = 1f;
    [Tooltip("Fuel percentage at/below which the fuel-depleted warning plays.")]
    [Range(0f, 1f)]
    [SerializeField] private float fuelLowThreshold = 0.005f;
    [Tooltip("Energy percentage at/below which the energy-depleted warning plays.")]
    [Range(0f, 1f)]
    [SerializeField] private float energyLowThreshold = 0.005f;

    [Header("Solar Charging")]
    [Tooltip("Looping AudioSource that plays while any solar panel is charging. Script sets loop=true on Awake.")]
    [SerializeField] private AudioSource solarChargingSource;
    [SerializeField] private AudioClip solarChargingClip;
    [Tooltip("Volume multiplier for the solar charging loop. Kept lower than thrusters/impacts since it can play continuously while the ship faces the sun.")]
    [Range(0f, 1f)]
    [SerializeField] private float solarChargingVolumeScale = 0.75f;
    [Tooltip("Seconds for the solar loop to ramp up when the first panel starts charging.")]
    [Range(0f, 2f)]
    [SerializeField] private float solarFadeInDuration = 0.3f;
    [Tooltip("Seconds for the solar loop to ramp down when the last panel stops charging.")]
    [Range(0f, 2f)]
    [SerializeField] private float solarFadeOutDuration = 0.5f;

    private int firingEngineCount;
    private int chargingPanelCount;
    private bool fuelLowFired;
    private bool energyLowFired;
    private Spacecraft subscribedSpacecraft;
    private Coroutine fadeOutCoroutine;
    private Coroutine fadeInCoroutine;
    private Coroutine solarFadeCoroutine;

    private void Awake() {
        if (Instance != null && Instance != this) {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (thrusterLoopSource != null) {
            thrusterLoopSource.clip = thrusterLoopClip;
            thrusterLoopSource.loop = true;
            thrusterLoopSource.playOnAwake = false;
        }
        if (thrusterFadeSource != null) {
            thrusterFadeSource.loop = false;
            thrusterFadeSource.playOnAwake = false;
        }
        if (solarChargingSource != null) {
            solarChargingSource.clip = solarChargingClip;
            solarChargingSource.loop = true;
            solarChargingSource.playOnAwake = false;
            solarChargingSource.volume = 0f;
        }

        ConfigureAsteroidImpactFilter();
    }

    private void Start() {
        SubscribeToSpacecraft();
    }

    private void SubscribeToSpacecraft() {
        Spacecraft sc = Spacecraft.GetInstance();
        if (sc == null || sc == subscribedSpacecraft) return;
        subscribedSpacecraft = sc;
        sc.OnFuelChanged += HandleFuelChanged;
        sc.OnEnergyChanged += HandleEnergyChanged;
    }

    private void HandleFuelChanged(object sender, float pct) {
        if (pct <= fuelLowThreshold) {
            if (!fuelLowFired) {
                fuelLowFired = true;
                PlayLowResource(fuelLowClip);
            }
        } else {
            fuelLowFired = false;
        }
    }

    private void HandleEnergyChanged(object sender, float pct) {
        if (pct <= energyLowThreshold) {
            if (!energyLowFired) {
                energyLowFired = true;
                PlayLowResource(energyLowClip);
            }
        } else {
            energyLowFired = false;
        }
    }

    private void PlayLowResource(AudioClip clip) {
        if (lowResourceSource == null || clip == null) return;
        float v = lowResourceVolumeScale;
        if (Settings.Instance != null) v *= Settings.Instance.sfxVolume;
        if (v > 0f) lowResourceSource.PlayOneShot(clip, v);
    }

    private void ConfigureAsteroidImpactFilter() {
        if (asteroidImpactSource == null) return;

        if (asteroidImpactSource.gameObject == gameObject) {
            Debug.LogWarning("FlightSFXManager: asteroidImpactSource is on the same GameObject as the other audio sources. The low-pass filter will muffle ALL of them. Move asteroidImpactSource onto a child GameObject.", this);
        }

        if (!asteroidImpactSource.TryGetComponent(out AudioLowPassFilter filter)) {
            filter = asteroidImpactSource.gameObject.AddComponent<AudioLowPassFilter>();
        }
        filter.cutoffFrequency = asteroidImpactLowPassCutoff;
        filter.lowpassResonanceQ = asteroidImpactLowPassResonance;
    }

    private void Update() {
        if (fadeOutCoroutine == null && thrusterLoopSource != null && thrusterLoopSource.isPlaying) {
            thrusterLoopSource.volume = CurrentThrusterVolume();
        }
        if (solarFadeCoroutine == null && solarChargingSource != null && solarChargingSource.isPlaying) {
            solarChargingSource.volume = CurrentSolarVolume();
        }
    }

    public void NotifyEngineFiring(bool firing) {
        int prev = firingEngineCount;
        firingEngineCount += firing ? 1 : -1;
        if (firingEngineCount < 0) firingEngineCount = 0;

        if (prev == 0 && firingEngineCount > 0) StartThrusterLoop();
        else if (prev > 0 && firingEngineCount == 0) StopThrusterLoop();
    }

    private void StartThrusterLoop() {
        if (thrusterLoopSource == null || thrusterLoopClip == null) return;
        if (fadeOutCoroutine != null) {
            StopCoroutine(fadeOutCoroutine);
            fadeOutCoroutine = null;
        }
        if (fadeInCoroutine != null) {
            StopCoroutine(fadeInCoroutine);
            fadeInCoroutine = null;
        }
        if (thrusterFadeSource != null && thrusterFadeSource.isPlaying) thrusterFadeSource.Stop();
        thrusterLoopSource.volume = CurrentThrusterVolume();
        if (!thrusterLoopSource.isPlaying) thrusterLoopSource.Play();
    }

    private void StopThrusterLoop() {
        PlayFadeTail();
        if (thrusterLoopSource == null || !thrusterLoopSource.isPlaying) return;

        if (fadeOutCoroutine != null) StopCoroutine(fadeOutCoroutine);
        if (thrusterFadeOverlap > 0f) {
            fadeOutCoroutine = StartCoroutine(FadeOutLoop(thrusterFadeOverlap));
        } else {
            thrusterLoopSource.Stop();
        }
    }

    private void PlayFadeTail() {
        if (thrusterFadeSource == null || thrusterFadeClip == null) return;
        if (fadeInCoroutine != null) StopCoroutine(fadeInCoroutine);

        thrusterFadeSource.clip = thrusterFadeClip;
        thrusterFadeSource.volume = 0f;
        thrusterFadeSource.Play();

        if (thrusterFadeInDuration > 0f) {
            fadeInCoroutine = StartCoroutine(FadeInTail(thrusterFadeInDuration));
        } else {
            thrusterFadeSource.volume = CurrentThrusterFadeVolume();
        }
    }

    private IEnumerator FadeInTail(float duration) {
        float t = 0f;
        while (t < duration && thrusterFadeSource != null && thrusterFadeSource.isPlaying) {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / duration);
            thrusterFadeSource.volume = CurrentThrusterFadeVolume() * k;
            yield return null;
        }
        if (thrusterFadeSource != null && thrusterFadeSource.isPlaying) {
            thrusterFadeSource.volume = CurrentThrusterFadeVolume();
        }
        fadeInCoroutine = null;
    }

    private IEnumerator FadeOutLoop(float duration) {
        float startVol = thrusterLoopSource.volume;
        float t = 0f;
        while (t < duration && thrusterLoopSource != null) {
            t += Time.unscaledDeltaTime;
            float k = 1f - Mathf.Clamp01(t / duration);
            thrusterLoopSource.volume = startVol * k;
            yield return null;
        }
        if (thrusterLoopSource != null) {
            thrusterLoopSource.Stop();
            thrusterLoopSource.volume = CurrentThrusterVolume();
        }
        fadeOutCoroutine = null;
    }

    private float CurrentThrusterVolume() {
        float v = thrusterVolumeScale;
        if (Settings.Instance != null) v *= Settings.Instance.sfxVolume;
        return v;
    }

    private float CurrentThrusterFadeVolume() {
        float v = thrusterFadeVolumeScale;
        if (Settings.Instance != null) v *= Settings.Instance.sfxVolume;
        return v;
    }

    public void PlayImpact() {
        PlayImpactClip(impactSource, impactVolumeScale);
    }

    public void PlayAsteroidImpact() {
        AudioSource source = asteroidImpactSource != null ? asteroidImpactSource : impactSource;
        PlayImpactClip(source, asteroidImpactVolumeScale);
    }

    public void NotifyPanelCharging(bool charging) {
        int prev = chargingPanelCount;
        chargingPanelCount += charging ? 1 : -1;
        if (chargingPanelCount < 0) chargingPanelCount = 0;

        if (prev == 0 && chargingPanelCount > 0) StartSolarLoop();
        else if (prev > 0 && chargingPanelCount == 0) StopSolarLoop();
    }

    private void StartSolarLoop() {
        if (solarChargingSource == null || solarChargingClip == null) return;
        if (solarFadeCoroutine != null) StopCoroutine(solarFadeCoroutine);
        if (!solarChargingSource.isPlaying) solarChargingSource.Play();
        solarFadeCoroutine = StartCoroutine(FadeSolar(CurrentSolarVolume(), solarFadeInDuration, false));
    }

    private void StopSolarLoop() {
        if (solarChargingSource == null || !solarChargingSource.isPlaying) return;
        if (solarFadeCoroutine != null) StopCoroutine(solarFadeCoroutine);
        solarFadeCoroutine = StartCoroutine(FadeSolar(0f, solarFadeOutDuration, true));
    }

    private IEnumerator FadeSolar(float targetVolume, float duration, bool stopAtEnd) {
        if (duration <= 0f) {
            solarChargingSource.volume = targetVolume;
        } else {
            float startVol = solarChargingSource.volume;
            float t = 0f;
            while (t < duration && solarChargingSource != null) {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / duration);
                solarChargingSource.volume = Mathf.Lerp(startVol, targetVolume, k);
                yield return null;
            }
            if (solarChargingSource != null) solarChargingSource.volume = targetVolume;
        }
        if (stopAtEnd && solarChargingSource != null) solarChargingSource.Stop();
        solarFadeCoroutine = null;
    }

    private float CurrentSolarVolume() {
        float v = solarChargingVolumeScale;
        if (Settings.Instance != null) v *= Settings.Instance.sfxVolume;
        return v;
    }

    private void PlayImpactClip(AudioSource source, float scale) {
        if (source == null || impactClips == null || impactClips.Length == 0) return;
        AudioClip clip = impactClips[Random.Range(0, impactClips.Length)];
        if (clip == null) return;
        float v = scale;
        if (Settings.Instance != null) v *= Settings.Instance.sfxVolume;
        if (v > 0f) source.PlayOneShot(clip, v);
    }

    private void OnDestroy() {
        if (subscribedSpacecraft != null) {
            subscribedSpacecraft.OnFuelChanged -= HandleFuelChanged;
            subscribedSpacecraft.OnEnergyChanged -= HandleEnergyChanged;
            subscribedSpacecraft = null;
        }
        if (Instance == this) Instance = null;
    }
}
