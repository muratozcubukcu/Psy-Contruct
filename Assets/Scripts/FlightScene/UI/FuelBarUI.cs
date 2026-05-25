using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// UI component that displays the spacecraft's fuel as a filled bar.
/// </summary>
[RequireComponent(typeof(Image))]
public class FuelBarUI : MonoBehaviour {
    
    [SerializeField] private GameObject tankDivider;
    
    private float barWidth = 160f;
    private Image fuelBarImage;
    private Spacecraft spacecraft;

    private void Awake() {
        spacecraft = Spacecraft.GetInstance();
        fuelBarImage = GetComponent<Image>();

        fuelBarImage.fillAmount = 1f;
    }

    private void Start() {
        spacecraft.OnFuelChanged += Spacecraft_OnFuelChanged;
        UpdateFuelBar(spacecraft.FuelPercentage);
        CreateTankDividers();
    }

    private void CreateTankDividers() {
        int tankCount = spacecraft.GetComponentsInChildren<FuelTank>().Length;
        if (tankCount <= 1) return;
        
        float distanceBetweenTicks = barWidth / tankCount;
        float nextTickXPos = -(barWidth / 2) + distanceBetweenTicks;
        
        for (int i = 1; i < tankCount; i++) {
            GameObject tick = Instantiate(tankDivider, transform, false);
            tick.SetActive(true);
            tick.GetComponent<RectTransform>().localPosition = new Vector2(nextTickXPos, 0);
            nextTickXPos += distanceBetweenTicks;
        }
    }

    private void Spacecraft_OnFuelChanged(object sender, float fuelPercentage) {
        UpdateFuelBar(fuelPercentage);
    }

    private void UpdateFuelBar(float fuelPercentage) {
        if (fuelBarImage != null) {
            fuelBarImage.fillAmount = Mathf.Clamp01(fuelPercentage);
        }
    }

    private void OnDestroy() {
        if (spacecraft != null) {
            spacecraft.OnFuelChanged -= Spacecraft_OnFuelChanged;
        }
    }
}
