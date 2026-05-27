using UnityEngine;

public class RandomizeButton : MonoBehaviour
{
    private Settings settingsInstance;

    void Awake()
    {
        settingsInstance = Settings.Instance;
    }

    public void randomizeLocations()
    {
        settingsInstance.randomizeLocations();

    }
}
