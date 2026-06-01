using TMPro;
using UnityEngine;

public class SpacecraftMotionUI : MonoBehaviour {
    public static SpacecraftMotionUI Instance;
    
    private const float PsycheTopSpeedMph = 124000f;

    [SerializeField] private TextMeshProUGUI spacecraftSpeedText;
    [SerializeField] private RectTransform spacecraftMotionTransform;
    [SerializeField] private float gameTopSpeed = 10f;
    

    private void Awake() {
        if (Instance != null && Instance != this) {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    public void UpdateMotion(float speed, Vector2 direction) {
        float speedRatio = gameTopSpeed > 0f ? Mathf.Clamp01(speed / gameTopSpeed) : 0f;
        float displayedSpeed = speedRatio * PsycheTopSpeedMph;
        spacecraftSpeedText.text = $"{displayedSpeed:N0} MPH";
        
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        spacecraftMotionTransform.rotation = Quaternion.Euler(0, 0, angle);
    }

    private void OnDestroy() {
        if (Instance == this) Instance = null;
    }
}
