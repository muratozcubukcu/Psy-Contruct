using TMPro;
using UnityEngine;

public class HintsButton : MonoBehaviour
{
    private Settings settingsInstance;
    [SerializeField] private TextMeshProUGUI text;

    void Start()
    {
        settingsInstance = Settings.Instance;
        text.text = settingsInstance.hintsEnabled ? "ON" : "OFF";
    }

    public void toggleHints()
    {
        text.text = settingsInstance.toggleHints() ? "ON" : "OFF";
    }
}