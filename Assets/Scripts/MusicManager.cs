using System.Collections;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.SceneManagement;

/// <summary>
/// Music singleton. All stems loop in sync; layers mute/unmute per scene.
/// MainMenu: chords + drums. BuildScene: + bass. FlightScene: + arp + drum rotation
/// (drums1 x N blocks -> 1.5 -> 2 -> 2.5 -> repeat, swaps on block boundaries).
/// </summary>
public class MusicManager : MonoBehaviour {
    public static MusicManager Instance { get; private set; }

    [Header("Loop AudioSources (always playing, muted when not needed)")]
    [SerializeField] private AudioSource chordsSource;
    [SerializeField] private AudioSource drums1Source;
    [SerializeField] private AudioSource drums15Source; // transition from drums 1 -> drums 2
    [SerializeField] private AudioSource drums2Source;
    [SerializeField] private AudioSource drums25Source; // transition from drums 2 -> drums 1
    [SerializeField] private AudioSource bassSource;
    [SerializeField] private AudioSource arpSource;

    [Header("Clips")]
    [SerializeField] private AudioClip chordsClip;
    [SerializeField] private AudioClip drums1Clip;
    [SerializeField] private AudioClip drums15Clip;
    [SerializeField] private AudioClip drums2Clip;
    [SerializeField] private AudioClip drums25Clip;
    [SerializeField] private AudioClip bassClip;
    [SerializeField] private AudioClip arpClip;

    [Header("Timing")]
    [Tooltip("Musical block length in seconds. All stems should be exact multiples of this. Measured from the 38.43s short stems (drums2/1.5/2.5/bass/arp).")]
    [SerializeField] private float blockLength = 38.426122f;
    [Tooltip("How many blocks drums1 holds before the cycle moves into the transition + drum2 + transition phases (each 1 block). Only runs while in FlightScene.")]
    [FormerlySerializedAs("blocksPerMainDrum")]
    [SerializeField] private int blocksOfDrum1 = 4;

    [Header("Tuning")]
    [Tooltip("Master music multiplier applied on top of Settings.musicVolume.")]
    [Range(0f, 1f)]
    [SerializeField] private float musicVolumeScale = 1f;
    [Tooltip("Seconds for chords/bass/arp to fade in/out when toggled (scene transitions).")]
    [Range(0f, 5f)]
    [SerializeField] private float layerFadeDuration = 1.5f;
    [Tooltip("Seconds for drum layers (1, 1.5, 2, 2.5) to fade between phases. Keep tiny (0.05) so swaps are crisp on-beat instead of smeared.")]
    [Range(0f, 1f)]
    [SerializeField] private float drumLayerFadeDuration = 0.05f;

    [Header("Scene Names")]
    [SerializeField] private string buildSceneName = "BuildScene";
    [SerializeField] private string flightSceneName = "FlightScene";

    // Drum rotation: 0=drums1, 1=drums1.5, 2=drums2, 3=drums2.5
    private int drumsPhase;
    private bool chordsActive;
    private bool bassActive;
    private bool arpActive;

    private double startDspTime;
    private Coroutine drumSwapCoroutine;

    private void Awake() {
        if (Instance != null && Instance != this) {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        ConfigureLoopSource(chordsSource, chordsClip);
        ConfigureLoopSource(drums1Source, drums1Clip);
        ConfigureLoopSource(drums15Source, drums15Clip);
        ConfigureLoopSource(drums2Source, drums2Clip);
        ConfigureLoopSource(drums25Source, drums25Clip);
        ConfigureLoopSource(bassSource, bassClip);
        ConfigureLoopSource(arpSource, arpClip);

        // Schedule every stem to start at the same dspTime so they stay phase-locked forever.
        startDspTime = AudioSettings.dspTime + 0.1;
        ScheduleLoop(chordsSource, startDspTime);
        ScheduleLoop(drums1Source, startDspTime);
        ScheduleLoop(drums15Source, startDspTime);
        ScheduleLoop(drums2Source, startDspTime);
        ScheduleLoop(drums25Source, startDspTime);
        ScheduleLoop(bassSource, startDspTime);
        ScheduleLoop(arpSource, startDspTime);

        SceneManager.sceneLoaded += OnSceneLoaded;
        ApplyLayersForScene(SceneManager.GetActiveScene().name);
    }

    private void ConfigureLoopSource(AudioSource src, AudioClip clip) {
        if (src == null) return;
        src.clip = clip;
        src.loop = true;
        src.playOnAwake = false;
        src.volume = 0f;
    }

    private void ScheduleLoop(AudioSource src, double dspTime) {
        if (src == null || src.clip == null) return;
        src.PlayScheduled(dspTime);
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode) {
        ApplyLayersForScene(scene.name);
    }

    private void ApplyLayersForScene(string sceneName) {
        chordsActive = true;
        bassActive = sceneName == buildSceneName || sceneName == flightSceneName;
        arpActive = sceneName == flightSceneName;

        bool inFlight = sceneName == flightSceneName;
        if (inFlight) {
            if (drumSwapCoroutine == null) {
                drumsPhase = 0; // start fresh on drum 1 each time the player enters flight
                drumSwapCoroutine = StartCoroutine(DrumRotationLoop());
            }
        } else {
            if (drumSwapCoroutine != null) {
                StopCoroutine(drumSwapCoroutine);
                drumSwapCoroutine = null;
            }
            drumsPhase = 0; // outside flight, drum layer stays on drum 1
        }
    }

    private IEnumerator DrumRotationLoop() {
        double phaseDuration = PhaseBlocks(drumsPhase) * blockLength;
        double minNextDsp = AudioSettings.dspTime + phaseDuration;
        double elapsed = minNextDsp - startDspTime;
        double nextChangeDsp = startDspTime + Mathf.Ceil((float)(elapsed / blockLength)) * blockLength;

        while (true) {
            while (AudioSettings.dspTime < nextChangeDsp) yield return null;
            drumsPhase = (drumsPhase + 1) % 4;
            nextChangeDsp += PhaseBlocks(drumsPhase) * blockLength;
        }
    }

    private int PhaseBlocks(int phase) {
        // Only drums1 holds long; everything else (1.5, 2, 2.5) is 1 block.
        return phase == 0 ? blocksOfDrum1 : 1;
    }

    private void Update() {
        float musicVol = CurrentMusicVolume();
        UpdateLayerVolume(chordsSource, chordsActive, layerFadeDuration, musicVol);
        UpdateLayerVolume(drums1Source, drumsPhase == 0, drumLayerFadeDuration, musicVol);
        UpdateLayerVolume(drums15Source, drumsPhase == 1, drumLayerFadeDuration, musicVol);
        UpdateLayerVolume(drums2Source, drumsPhase == 2, drumLayerFadeDuration, musicVol);
        UpdateLayerVolume(drums25Source, drumsPhase == 3, drumLayerFadeDuration, musicVol);
        UpdateLayerVolume(bassSource, bassActive, layerFadeDuration, musicVol);
        UpdateLayerVolume(arpSource, arpActive, layerFadeDuration, musicVol);
    }

    private void UpdateLayerVolume(AudioSource src, bool active, float fadeDuration, float musicVol) {
        if (src == null) return;
        float target = active ? musicVol : 0f;
        if (fadeDuration <= 0f) {
            src.volume = target;
            return;
        }
        float step = 1f / fadeDuration * Time.unscaledDeltaTime;
        src.volume = Mathf.MoveTowards(src.volume, target, step);
    }

    private float CurrentMusicVolume() {
        float v = musicVolumeScale;
        if (Settings.Instance != null) v *= Settings.Instance.musicVolume;
        return v;
    }

    private void OnDestroy() {
        if (Instance == this) {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            Instance = null;
        }
    }
}
