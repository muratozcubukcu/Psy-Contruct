using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Every time a scene loads, auto-hooks every UI Button to play clickClip on its onClick.
/// For blocked actions, call PlayInvalid from your handler
/// </summary>
public class MenuButtonSFX : MonoBehaviour {
    public static MenuButtonSFX Instance { get; private set; }

    [Tooltip("AudioSource sounds play through. Put one on this same GameObject.")]
    [SerializeField] private AudioSource audioSource;

    [Tooltip("Played for a normal, successful button click. Auto-hooked to every UI Button's onClick.")]
    [SerializeField] private AudioClip clickClip;

    [Tooltip("Played when an action is blocked (e.g. launch with missing parts). Call MenuButtonSFX.Instance.PlayInvalid()")]
    [SerializeField] private AudioClip invalidClip;

    [Tooltip("Volume multiplier applied on top of Settings.sfxVolume.")]
    [Range(0f, 1f)]
    [SerializeField] private float volumeScale = 1f;

    // Frame on which PlayInvalid was last called. PlayClick checks this and skps if it matches
    // So the auto-hooked click sound doesn't double up with the invalid sound.
    private int suppressClickFrame = -1;

    private void Awake() {
        if (Instance != null && Instance != this) {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        if (audioSource == null) audioSource = GetComponent<AudioSource>();

        SceneManager.sceneLoaded += OnSceneLoaded;
        StartCoroutine(HookNextFrame());
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode) {
        StartCoroutine(HookNextFrame());
    }

    private IEnumerator HookNextFrame() {
        yield return null;
        HookButtonsInActiveScene();
    }

    private void HookButtonsInActiveScene() {
        if (audioSource == null || clickClip == null) return;

        Button[] buttons = FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (Button b in buttons) {
            b.onClick.RemoveListener(PlayClick);
            b.onClick.AddListener(PlayClick);
        }
    }

    public void PlayClick() {
        if (suppressClickFrame == Time.frameCount) {
            suppressClickFrame = -1;
            return;
        }
        PlayClip(clickClip);
    }

    public void PlayInvalid() {
        suppressClickFrame = Time.frameCount;
        PlayClip(invalidClip);
    }

    private void PlayClip(AudioClip clip) {
        if (audioSource == null || clip == null) return;
        float v = volumeScale;
        if (Settings.Instance != null) v *= Settings.Instance.sfxVolume;
        if (v > 0f) audioSource.PlayOneShot(clip, v);
    }

    private void OnDestroy() {
        if (Instance == this) {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            Instance = null;
        }
    }
}
