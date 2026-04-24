using UnityEngine;

/// <summary>
/// Charges spacecraft energy when the solar panel is facing the sun.
/// Uses the dot product between the panel's facing direction and the direction to the sun.
/// </summary>
public class SolarPanel : MonoBehaviour {

    [SerializeField] private Spacecraft spacecraft;

    [Header("Charging Settings")]
    [SerializeField] private float chargeRate;
    [SerializeField] private float facingThreshold = 0f;

    private Sun sun;

    public bool IsCharging { get; private set; }

    public void Awake() => enabled = false;

    private void OnEnable() {
        sun = Sun.Instance;
        if (spacecraft == null)
            spacecraft = GetComponentInParent<Spacecraft>();
    }

    private void Update() {
        if (sun == null) {
            sun = Sun.Instance;
            if (sun == null) {
                IsCharging = false;
                return;
            }
        }

        Vector2 directionToSun = (sun.transform.position - transform.position).normalized;
        float dot = Vector2.Dot(transform.up, directionToSun);

        IsCharging = dot > facingThreshold;

        if (IsCharging && spacecraft != null) {
            float chargeAmount = chargeRate * dot * Time.deltaTime;
            spacecraft.AddEnergy(chargeAmount);
        }
    }
}
