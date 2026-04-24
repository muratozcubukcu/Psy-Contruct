using UnityEngine;
using UnityEngine.UI;

public class MusicSlider : MonoBehaviour
{
    private Settings settingsInstance;
    [SerializeField] private Slider slider;

    void Start()
    {
        settingsInstance = Settings.Instance;
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.value = settingsInstance.musicVolume;
        slider.onValueChanged.AddListener(OnSliderChanged);
    }

    private void OnSliderChanged(float value)
    {
        settingsInstance.setMusicVolume(value);
    }
}