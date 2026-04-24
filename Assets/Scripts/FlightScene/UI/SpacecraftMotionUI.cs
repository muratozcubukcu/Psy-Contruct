using TMPro;
using UnityEngine;

public class SpacecraftMotionUI : MonoBehaviour {
    public static SpacecraftMotionUI Instance;
    
    [SerializeField] private TextMeshProUGUI spacecraftSpeedText;
    [SerializeField] private RectTransform spacecraftMotionTransform;
    

    private void Awake() {
        if (Instance != null && Instance != this) {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    public void UpdateMotion(float speed, Vector2 direction) {
        spacecraftSpeedText.text = speed.ToString("F1"); //F1 caps it at one decimal place
        
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        spacecraftMotionTransform.rotation = Quaternion.Euler(0, 0, angle);
    }

    private void OnDestroy() {
        if (Instance == this) Instance = null;
    }
}
