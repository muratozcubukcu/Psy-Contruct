using UnityEngine;

public class RandomizeButton : MonoBehaviour
{
    private Settings settingsInstance;

    void Start()
    {
        settingsInstance = Settings.Instance;
    }

    public void randomizeLocations()
    {
        settingsInstance.randomizeLocations();
    }
}
