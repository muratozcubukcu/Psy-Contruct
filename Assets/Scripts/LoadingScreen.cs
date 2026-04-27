using UnityEngine;
using UnityEngine.UI;
using TMPro;
public class LoadingScreen : MonoBehaviour
{
    public static LoadingScreen Instance;
    public TextMeshProUGUI text;
    private Image image;
    
    void Awake()
    {
        Instance = this;
        text = GetComponentInChildren<TextMeshProUGUI>();
        image = GetComponent<Image>();
        Disable();
    }

    public void Enable()
    {
        text.enabled = true;
        image.enabled = true;
    }

    public void Disable()
    {
        text.enabled = false;
        image.enabled = false;
    }
}
