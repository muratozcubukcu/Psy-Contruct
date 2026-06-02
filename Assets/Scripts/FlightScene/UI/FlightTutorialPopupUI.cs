using UnityEngine;
using UnityEngine.UI;

public class FlightTutorialPopupUI : MonoBehaviour {
    [SerializeField] private GameObject[] allCards;

    private void Awake() {
        allCards[0].SetActive(Settings.Instance.showFlightTutorialPopup);
        if (Settings.Instance.showFlightTutorialPopup) Time.timeScale = 0f;
    }

    public void GoToNextCard(GameObject currCard) {
        int index = System.Array.IndexOf(allCards, currCard);
        
        if(index == -1 || index >= allCards.Length) {
            Exit();
            return;
        }
        
        allCards[index + 1].SetActive(true);
        allCards[index].SetActive(false);
    }

    public void Exit() {
        gameObject.SetActive(false);
        Time.timeScale = 1f;
    }

    public void ToggleDontShowAgain(GameObject currCard) {
        foreach (Toggle toggle in transform.GetComponentsInChildren<Toggle>(true)) {
            if (toggle.transform.parent.gameObject == currCard) continue;
        
            toggle.SetIsOnWithoutNotify(!toggle.isOn);
        }
    
        Settings.Instance.ToggleFlightTutorialPopup();
    }
}
