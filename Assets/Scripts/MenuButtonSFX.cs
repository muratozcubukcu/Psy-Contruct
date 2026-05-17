using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// every time a scene loads, finds every UI Button in it and plays a randomly-picked click clip on its onClick event.
/// </summary>
public class MenuButtonSFX : MonoBehaviour {
    public static MenuButtonSFX Instance { get; private set; }

    [Tooltip("AudioSource the click sound plays through. Put one on this same GameObject.")]
    [SerializeField] private AudioSource audioSource;

    [Tooltip("One is picked at random on each click. Add as many as you want.")]
    [SerializeField] private AudioClip[] clickClips;

    [Tooltip("Volume multiplier applied on top of Settings.sfxVolume.")]
    [Range(0f, 1f)]
    [SerializeField] private float volumeScale = 1f;

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
        if (audioSource == null || clickClips == null || clickClips.Length == 0) return;

        Button[] buttons = FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (Button b in buttons) {
            b.onClick.RemoveListener(PlayClick);
            b.onClick.AddListener(PlayClick);
        }
    }

    public void PlayClick() {
        if (clickClips == null || clickClips.Length == 0) return;
        AudioClip clip = clickClips[Random.Range(0, clickClips.Length)];
        if (clip == null) return;
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
