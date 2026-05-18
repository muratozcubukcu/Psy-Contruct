using System;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Charges spacecraft energy when the solar panel is facing the sun.
/// Uses the dot product between the panel's facing direction and the direction to the sun.
/// </summary>
public class SolarPanel : MonoBehaviour {

    [SerializeField] private Spacecraft spacecraft;
    [SerializeField] private GameObject solarPanelVisualDefault;
    [SerializeField] private GameObject solarPanelVisualShining;

    [Header("Charging Settings")]
    [SerializeField] private float chargeRate;
    [SerializeField] private float facingThreshold;

    private Sun sun;

    private bool isCharging;
    private bool prevState;

    public void Awake() => enabled = false;

    private void Start() {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnEnable() {
        sun = Sun.Instance;
        
        spacecraft = Spacecraft.GetInstance();
    }

    private void Update() {
        if (sun == null) {
            sun = Sun.Instance;
            if (sun == null) {
                isCharging = false;
                return;
            }
        }

        Vector2 directionToSun = (sun.transform.position - transform.position).normalized;
        float dot = Vector2.Dot(transform.up, directionToSun);

        isCharging = dot > facingThreshold;

        if (isCharging && spacecraft != null) {
            float chargeAmount = chargeRate * dot * Time.deltaTime;
            spacecraft.AddEnergy(chargeAmount);
        }

        if (isCharging != prevState) SwapVisuals();
        prevState = isCharging;
    }

    private void SwapVisuals() {
        if (isCharging) {
            solarPanelVisualShining.SetActive(true);
            solarPanelVisualDefault.SetActive(false);
            return;
        }
        
        solarPanelVisualShining.SetActive(false);
        solarPanelVisualDefault.SetActive(true);
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode) {
        solarPanelVisualShining.SetActive(false);
        solarPanelVisualDefault.SetActive(true);
    }

    private void OnDestroy() {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }
}
