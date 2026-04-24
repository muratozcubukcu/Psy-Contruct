using TMPro;
using UnityEngine;

public class TimerButton : MonoBehaviour
{
    private Settings settingsInstance;
    [SerializeField] private TextMeshProUGUI text;

    void Start()
    {
        settingsInstance = Settings.Instance;
        text.text = settingsInstance.timerEnabled ? "ON" : "OFF";
    }

    public void toggleTimer()
    {
        text.text = settingsInstance.toggleTimer() ? "ON" : "OFF";
    }
}