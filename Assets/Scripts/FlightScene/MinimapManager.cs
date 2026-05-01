using UnityEngine;

public class MinimapManager : MonoBehaviour {
    [SerializeField] private GameObject minimap;
    [SerializeField] private Camera minimapCamera;
    
    [SerializeField] private GameObject spacecraftIcon;
    [SerializeField] private GameObject marsIcon;
    [SerializeField] private GameObject psycheIcon;

    private void Awake() {
        minimapCamera.transform.position = Mars.Instance.transform.position + new Vector3(0, 0, -10);
        
        marsIcon.transform.position = Mars.Instance.transform.position;
        psycheIcon.transform.position = PsycheAsteroid.Instance.transform.position;
    }
    
    private void Start() { 
        Mars.Instance.minimapTrigger.OnEnterMinimapRange += Mars_OnEnterMinimapRange;
    }
    
    private void Mars_OnEnterMinimapRange(object sender, MinimapTrigger.MinimapEventArgs e) {
        if (minimap == null) return;
        if(e.entering) minimap.SetActive(true);
        else minimap.SetActive(false);
    }
    
    private void Update() {
        if (!minimap.activeSelf) return;
        
        spacecraftIcon.transform.position = Spacecraft.GetInstance().transform.position;
    }
}
