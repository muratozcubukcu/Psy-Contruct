using System.Collections;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.SceneManagement;

/// <summary>
/// Music singleton.
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
    [Tooltip("How many musical blocks each flight arrangement phrase lasts before chord/arpeggio layers change.")]
    [SerializeField] private int flightLayerPhraseBlocks = 1;

    [Header("Tuning")]
    [Tooltip("Master music multiplier applied on top of Settings.musicVolume.")]
    [Range(0f, 1f)]
    [SerializeField] private float musicVolumeScale = 0.25f;
    [Tooltip("Seconds for chords/bass/arpeggio to fade in/out when toggled.")]
    [Range(0f, 10f)]
    [SerializeField] private float layerFadeDuration = 20f;
    [Tooltip("Seconds for chords/bass/arpeggio to fade out when toggled off.")]
    [Range(0f, 10f)]
    [SerializeField] private float layerFadeOutDuration = 4f;
    [Tooltip("Seconds for drum layers to fade in/out when entering scenes or changing grooves.")]
    [Range(0f, 10f)]
    [SerializeField] private float drumLayerFadeDuration = 16f;
    [Tooltip("Seconds for drum layers to fade out when toggled off.")]
    [Range(0f, 10f)]
    [SerializeField] private float drumLayerFadeOutDuration = 3f;

    [Header("Scene Names")]
    [SerializeField] private string mainMenuSceneName = "MainMenuScene";
    [SerializeField] private string buildSceneName = "BuildScene";
    [SerializeField] private string flightSceneName = "FlightScene";

    // Drum rotation: 0=drums1, 1=drums1.5, 2=drums2, 3=drums2.5
    private int drumsPhase;
    private bool chordsActive;
    private bool drumsActive;
    private bool bassActive;
    private bool arpActive;
    private bool inFlightScene;
    private int currentFlightLayerPhrase = -1;
    private int flightLayerState;

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
        src.Stop();
        src.playOnAwake = false;
        src.clip = clip;
        src.loop = true;
        src.mute = false;
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
        if (sceneName == mainMenuSceneName) {
            ResetToBassOnly();
            return;
        }

        bool inBuild = sceneName == buildSceneName;
        bool inCutscene = IsCutscene(sceneName);
        inFlightScene = sceneName == flightSceneName;

        bassActive = true;
        drumsActive = inBuild || inCutscene || inFlightScene;
        chordsActive = inBuild || inCutscene || inFlightScene;
        arpActive = inCutscene;
        currentFlightLayerPhrase = -1;
        flightLayerState = 0;

        if (inFlightScene) {
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

    private void ResetToBassOnly() {
        if (drumSwapCoroutine != null) {
            StopCoroutine(drumSwapCoroutine);
            drumSwapCoroutine = null;
        }

        inFlightScene = false;
        drumsPhase = 0;
        currentFlightLayerPhrase = -1;
        flightLayerState = 0;

        bassActive = true;
        drumsActive = false;
        chordsActive = false;
        arpActive = false;
    }

    private bool IsCutscene(string sceneName) {
        return !string.IsNullOrEmpty(sceneName) && sceneName.Contains("Cutscene");
    }

    private IEnumerator DrumRotationLoop() {
        double phaseDuration = PhaseBlocks(drumsPhase) * blockLength;
        double minNextDsp = AudioSettings.dspTime + phaseDuration;
        double elapsed = minNextDsp - startDspTime;
        double nextChangeDsp = startDspTime + Mathf.Ceil((float)(elapsed / blockLength)) * blockLength;

        while (true) {
            while (AudioSettings.dspTime < nextChangeDsp) yield return null;
            // 0 -> 1.5 transition -> 2 -> 2.5 transition -> back to 1.
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
        if (inFlightScene) UpdateFlightLayerVariation();

        UpdateLayerVolume(chordsSource, chordsActive, layerFadeDuration, layerFadeOutDuration, musicVol);
        UpdateLayerVolume(drums1Source, drumsActive && drumsPhase == 0, drumLayerFadeDuration, drumLayerFadeOutDuration, musicVol);
        UpdateLayerVolume(drums15Source, drumsActive && drumsPhase == 1, drumLayerFadeDuration, drumLayerFadeOutDuration, musicVol);
        UpdateLayerVolume(drums2Source, drumsActive && drumsPhase == 2, drumLayerFadeDuration, drumLayerFadeOutDuration, musicVol);
        UpdateLayerVolume(drums25Source, drumsActive && drumsPhase == 3, drumLayerFadeDuration, drumLayerFadeOutDuration, musicVol);
        UpdateLayerVolume(bassSource, bassActive, layerFadeDuration, layerFadeOutDuration, musicVol);
        UpdateLayerVolume(arpSource, arpActive, layerFadeDuration, layerFadeOutDuration, musicVol);
    }

    private void UpdateFlightLayerVariation() {
        int phraseBlocks = Mathf.Max(1, flightLayerPhraseBlocks);
        float phraseLength = blockLength * phraseBlocks;
        float elapsed = Mathf.Max(0f, (float)(AudioSettings.dspTime - startDspTime));
        int phrase = Mathf.FloorToInt(elapsed / phraseLength);

        if (phrase != currentFlightLayerPhrase) {
            currentFlightLayerPhrase = phrase;
            flightLayerState = flightLayerState == 0 ? Random.Range(0, 3) : 0;
        }

        // 0: full, 1: arp only, 2: chords only. Dropouts resolve after one block.
        chordsActive = flightLayerState != 1;
        arpActive = flightLayerState != 2;
    }

    private void UpdateLayerVolume(AudioSource src, bool active, float fadeInDuration, float fadeOutDuration, float musicVol) {
        if (src == null) return;
        float target = active ? musicVol : 0f;
        float fadeDuration = active ? fadeInDuration : fadeOutDuration;
        if (fadeDuration <= 0f) {
            src.volume = target;
            return;
        }
        float step = Mathf.Max(target, src.volume) / fadeDuration * Time.unscaledDeltaTime;
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
