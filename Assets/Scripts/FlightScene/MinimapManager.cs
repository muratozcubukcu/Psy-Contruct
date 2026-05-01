using UnityEngine;
using UnityEngine.UI;

public class MinimapManager : MonoBehaviour {
    public static MinimapManager Instance;
    
    [SerializeField] private GameObject minimap;
    [SerializeField] private Camera minimapCamera;
    
    [SerializeField] private GameObject spacecraftIcon;
    [SerializeField] private GameObject marsIcon;
    [SerializeField] private GameObject psycheIcon;

    public bool highlightBorder = false;
    private Outline minimapBorder;

    private void Awake() {
        if (Instance != null && Instance != this) {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        
        minimapCamera.transform.position = Mars.Instance.transform.position + new Vector3(0, 0, -10);
        
        marsIcon.transform.position = Mars.Instance.transform.position;
        psycheIcon.transform.position = PsycheAsteroid.Instance.transform.position;

        minimapBorder = minimap.GetComponentInChildren<Outline>();
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
        if (highlightBorder) minimapBorder.effectColor = Color.green;
        else minimapBorder.effectColor = Color.black;
    }
    
    private void OnDestroy() {
        if (Instance == this) Instance = null;
    }
}
