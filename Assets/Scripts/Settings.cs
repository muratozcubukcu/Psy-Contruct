using UnityEngine;

public class Settings : MonoBehaviour
{
    public static Settings Instance;

    private float volume = 1f;
    public bool colorblindMode {get; private set;} = false;
    public bool tutorialEnabled {get; private set;} = true;
    void Awake()
    {
        if (Instance != null && Instance != this) {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(this);
    }

    public bool toggleColorblindMode()
    {
        colorblindMode = !colorblindMode;
        return colorblindMode;
    }

    public bool toggleTutorial()
    {
        tutorialEnabled = !tutorialEnabled;
        return tutorialEnabled;
    }

    public bool toggleTutorial(bool newSetting)
    {
        tutorialEnabled = newSetting;
        return tutorialEnabled;
    }
}
