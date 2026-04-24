using UnityEngine;
using UnityEngine.UI;

public class SFXSlider : MonoBehaviour
{
    private Settings settingsInstance;
    [SerializeField] private Slider slider;

    void Start()
    {
        settingsInstance = Settings.Instance;
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.value = settingsInstance.sfxVolume;
        slider.onValueChanged.AddListener(OnSliderChanged);
    }

    private void OnSliderChanged(float value)
    {
        settingsInstance.setSFXVolume(value);
    }
}