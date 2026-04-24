using TMPro;
using UnityEngine;

public class DifficultyButton : MonoBehaviour
{
    private Settings settingsInstance;
    [SerializeField] private TextMeshProUGUI text;

    void Start()
    {
        settingsInstance = Settings.Instance;
        text.text = settingsInstance.DifficultyLabel;
    }

    public void cycleDifficulty()
    {
        settingsInstance.cycleDifficulty();
        text.text = settingsInstance.DifficultyLabel;
    }
}