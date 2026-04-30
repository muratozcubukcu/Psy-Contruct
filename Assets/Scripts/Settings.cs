using UnityEngine;

public class Settings : MonoBehaviour
{
    public static Settings Instance;

    public bool colorblindMode {get; private set;} = false;
    public bool hintsEnabled { get; private set; } = true;
    public bool timerEnabled { get; private set; } = true;
    public float brightness { get; private set; } = 1f;
    public float musicVolume { get; private set; } = 1f;
    public float sfxVolume { get; private set; } = 1f;


    // Difficulty: 0 = Easy, 1 = Medium, 2 = Hard
    public int difficulty { get; private set; } = 1;
    public string DifficultyLabel => difficulty switch { 0 => "EASY", 2 => "HARD", _ => "MEDIUM" };

    void Awake() {
        if (Instance != null && Instance != this) {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(this);
    }

    public bool toggleColorblindMode() {
        colorblindMode = !colorblindMode;
        return colorblindMode;
    }

    public void setBrightness(float value) {
        brightness = value;
        // TODO: Apply to a post-processing volume or overlay CanvasGroup alpha
    }

    public void setMusicVolume(float value) {
        musicVolume = value;
        // TODO: AudioManager.Instance.SetMusicVolume(musicVolume);
    }

    public void setSFXVolume(float value) {
        sfxVolume = value;
        // TODO: AudioManager.Instance.SetSFXVolume(sfxVolume);
    }

    public bool toggleHints() {
        hintsEnabled = !hintsEnabled;
        // TODO: Notify hint system
        return hintsEnabled;
    }

    public bool toggleTimer() {
        timerEnabled = !timerEnabled;
        // TODO: Notify turn timer system
        return timerEnabled;
    }

    public int cycleDifficulty() {
        difficulty = (difficulty + 1) % 3;
        // TODO: Apply difficulty modifiers to game systems
        return difficulty;
    }
}
