using UnityEngine;

public class Settings : MonoBehaviour
{
    public static Settings Instance;

    public bool colorblindMode {get; private set;} = false;
    public bool tutorialEnabled {get; private set;} = true;
    public bool hintsEnabled { get; private set; } = true;
    public bool timerEnabled { get; private set; } = true;
    public float musicVolume { get; private set; } = 1f;
    public float sfxVolume { get; private set; } = 1f;
    
    public bool showFlightTutorialPopup { get; private set; } = true;


    // Difficulty: 0 = Easy, 1 = Medium, 2 = Hard
    public int difficulty { get; private set; } = 1;
    public string DifficultyLabel => difficulty switch { 0 => "EASY", 2 => "HARD", _ => "MEDIUM" };

    public Vector3 psychePosition = new Vector3(420,138,0);
    public Vector3 marsPosition = new Vector3(203,174,0);

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

    public void setMusicVolume(float value) {
        musicVolume = value;
        // TODO: AudioManager.Instance.SetMusicVolume(musicVolume);
    }

    public void randomizeLocations()
    {
        float newPsycheAngle = Random.Range(0f,360f);
        float newMarsAngle = newPsycheAngle + Random.Range(-30, 30);
        psychePosition = Quaternion.Euler(0,0,newPsycheAngle) * new Vector3(550,0,0);
        marsPosition = Quaternion.Euler(0,0,newMarsAngle) * new Vector3(300,0,0);
        Debug.Log(psychePosition);
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

    public bool ToggleFlightTutorialPopup() {
        showFlightTutorialPopup = !showFlightTutorialPopup;
        Debug.Log(showFlightTutorialPopup);
        return showFlightTutorialPopup;
    }

    public bool toggleTutorial(bool newSetting) {
        tutorialEnabled = newSetting;
        return tutorialEnabled;
    }
}
