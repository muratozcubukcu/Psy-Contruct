using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Video;
using UnityEngine.InputSystem;

/// <summary>
/// Plays a video, then loads the next scene. Skippable with click / Space / Esc.
/// WebGL can't use VideoClip assets, so we point the VideoPlayer at a URL instead.
/// </summary>
public class CutsceneController : MonoBehaviour {
    [Tooltip("Filename of the video, relative to StreamingAssets. E.g. 'Opening Scene - Storyboard.mp4'.")]
    [SerializeField] private string videoFilename;

    [Tooltip("Scene to load when the video ends (or when the player skips).")]
    [SerializeField] private string nextSceneName;

    [Tooltip("VideoPlayer that owns the clip. Auto-found on this GameObject if left empty.")]
    [SerializeField] private VideoPlayer videoPlayer;

    [Tooltip("Allow the player to skip with click, Space, Enter, or Escape.")]
    [SerializeField] private bool skippable = true;

    private bool loaded;

    private void Awake() {
        if (videoPlayer == null) videoPlayer = GetComponent<VideoPlayer>();
        if (videoPlayer == null) videoPlayer = GetComponentInChildren<VideoPlayer>();
    }

    private void Start() {
        // Isolate the cutscene camera so the spacecraft and background don't show through the video.
        if (videoPlayer != null && videoPlayer.targetCamera != null) {
            Camera cam = videoPlayer.targetCamera;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            // Exclude the SpaceCraft layer
            int spaceCraftLayer = LayerMask.NameToLayer("SpaceCraft");
            if (spaceCraftLayer >= 0) cam.cullingMask &= ~(1 << spaceCraftLayer);
        }

        if (videoPlayer == null) {
            Debug.LogError("[Cutscene] No VideoPlayer found. Skipping to next scene.");
            LoadNext();
            return;
        }

        // Resolve URL from StreamingAssets so the same scene works on Editor, Standalone, and WebGL. 
        // For some reason VideoClip references are unsupported in WebGL.
        if (!string.IsNullOrEmpty(videoFilename)) {
            videoPlayer.source = VideoSource.Url;
            videoPlayer.url = Path.Combine(Application.streamingAssetsPath, videoFilename)
                                  .Replace('\\', '/');
        }

        videoPlayer.errorReceived += OnVideoError;
        videoPlayer.loopPointReached += OnVideoEnd;
        videoPlayer.Play();
    }

    private void OnVideoError(VideoPlayer src, string message) {
        Debug.LogError($"CutsceneController: video error: {message}");
    }

    private void Update() {
        if (!skippable || loaded) return;
        Keyboard kb = Keyboard.current;
        UnityEngine.InputSystem.Mouse mouse = UnityEngine.InputSystem.Mouse.current;
        bool keySkip = kb != null && (kb.spaceKey.wasPressedThisFrame
                                   || kb.escapeKey.wasPressedThisFrame
                                   || kb.enterKey.wasPressedThisFrame);
        bool mouseSkip = mouse != null && mouse.leftButton.wasPressedThisFrame;
        if (keySkip || mouseSkip) LoadNext();
    }

    private void OnVideoEnd(VideoPlayer src) => LoadNext();

    private void LoadNext() {
        if (loaded) return;
        loaded = true;
        if (string.IsNullOrEmpty(nextSceneName)) {
            Debug.LogError("CutsceneController: nextSceneName not set; staying on this scene.");
            return;
        }

        if (nextSceneName == "FlightScene" && GameInput.Instance != null) {
            GameInput.Instance.SetFlightScene();
            return;
        }
        if (nextSceneName == "BuildScene" && GameInput.Instance != null) {
            GameInput.Instance.SetBuildScene();
            return;
        }

        SceneManager.LoadScene(nextSceneName);
    }

    private void OnDestroy() {
        if (videoPlayer != null) {
            videoPlayer.loopPointReached -= OnVideoEnd;
            videoPlayer.errorReceived -= OnVideoError;
        }
    }
}
