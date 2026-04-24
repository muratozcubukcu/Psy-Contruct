using UnityEngine;

public class SettingsTabs : MonoBehaviour
{
    [SerializeField] private GameObject generalPanel;
    [SerializeField] private GameObject visualPanel;
    [SerializeField] private GameObject audioPanel;

    void Start()
    {
        ShowGeneral(); // default tab on open
    }

    public void ShowGeneral()
    {
        generalPanel.SetActive(true);
        visualPanel.SetActive(false);
        audioPanel.SetActive(false);
    }

    public void ShowVisual()
    {
        generalPanel.SetActive(false);
        visualPanel.SetActive(true);
        audioPanel.SetActive(false);
    }

    public void ShowAudio()
    {
        generalPanel.SetActive(false);
        visualPanel.SetActive(false);
        audioPanel.SetActive(true);
    }
}