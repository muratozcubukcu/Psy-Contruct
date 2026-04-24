using System;
using UnityEngine;
using System.Collections;

public class RepairQuickTimeUI : MonoBehaviour {
    public static RepairQuickTimeUI Instance;
    
    [SerializeField] float barSpeed;
    
    private RectTransform quickTimeBarTransform;
    private float barHeight;
    private float redZoneWidth;
    private float yellowZoneWidth;
    private float greenZoneWidth;
    private int direction = 1;
    private bool doQuickTime = false;
    
    private void Awake() {
        if (Instance != null && Instance != this) {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        
        foreach (Transform child in transform) {
            if (child.name == "RedZone") redZoneWidth = child.GetComponent<RectTransform>().rect.width;
            else if (child.name == "YellowZone") yellowZoneWidth = child.GetComponent<RectTransform>().rect.width;
            else if (child.name == "GreenZone") greenZoneWidth = child.GetComponent<RectTransform>().rect.width;
            else if (child.name == "Bar") quickTimeBarTransform = child.gameObject.GetComponent<RectTransform>();
        }

        barHeight = quickTimeBarTransform.anchoredPosition.y;
    }

    private void Start() {
        GameInput.Instance.OnRepairShipCanceledAction += GameInput_OnRepairShipCanceledAction;
        gameObject.SetActive(false);
    }

    private void OnEnable() {
        doQuickTime = true;
        quickTimeBarTransform.anchoredPosition = new Vector2(-redZoneWidth / 2, barHeight);
    }
    
    private void Update() {
        if (!doQuickTime) return;
        quickTimeBarTransform.anchoredPosition += direction * barSpeed * Time.deltaTime * Vector2.right;

        if (Math.Abs(quickTimeBarTransform.anchoredPosition.x) >= redZoneWidth / 2) {
            quickTimeBarTransform.anchoredPosition = new Vector2(redZoneWidth / 2 * direction, barHeight);
            direction *= -1;
        }
    }
    
    private void GameInput_OnRepairShipCanceledAction(object sender, System.EventArgs e) {
        if (!gameObject.activeInHierarchy) return;
        
        doQuickTime = false;
        
        Spacecraft.GetInstance().Heal(GetQuickTimeSuccess());
        
        StartCoroutine(DisableQuickTimeDelayed());
    }

    private float GetQuickTimeSuccess() {
        if (Math.Abs(quickTimeBarTransform.anchoredPosition.x) <= greenZoneWidth / 2) return 25f;
        if (Math.Abs(quickTimeBarTransform.anchoredPosition.x) <= yellowZoneWidth / 2) return 10f;

        return 0f;
    }

    private IEnumerator DisableQuickTimeDelayed() {
        yield return new WaitForSeconds(1f);
        Debug.Log("Done!");
        gameObject.SetActive(false);
    }
    
}
