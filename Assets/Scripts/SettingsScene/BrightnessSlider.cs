using UnityEngine;
using UnityEngine.UI;

public class BrightnessSlider : MonoBehaviour
{
    private Settings settingsInstance;
    [SerializeField] private Slider slider;

    void Start()
    {
        settingsInstance = Settings.Instance;
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.value = settingsInstance.brightness;
        slider.onValueChanged.AddListener(OnSliderChanged);
    }

    private void OnSliderChanged(float value)
    {
        settingsInstance.setBrightness(value);
    }
}