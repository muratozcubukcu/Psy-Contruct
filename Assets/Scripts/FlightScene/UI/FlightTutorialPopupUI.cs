using UnityEngine;
using UnityEngine.UI;

public class FlightTutorialPopupUI : MonoBehaviour {
    [SerializeField] private GameObject[] allCards;

    private void OnAwake() {
        if(Settings.Instance.showFlightTutorialPopup) allCards[0].SetActive(true);
    }

    public void GoToNextCard() {
        int index = System.Array.IndexOf(allCards, gameObject);
        
        if(index != -1) allCards[index + 1].SetActive(true);
        
        Exit();
    }

    public void Exit() {
        gameObject.SetActive(false);
    }

    public void ToggleDontShowAgain() {
        foreach (Toggle toggle in transform.parent.GetComponentsInChildren<Toggle>()) {
            if (toggle.gameObject == gameObject) continue;
            
            toggle.isOn = !toggle.isOn;
        }
        
        Settings.Instance.ToggleFlightTutorialPopup();
    }
}
