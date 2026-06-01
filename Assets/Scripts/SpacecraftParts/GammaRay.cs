using UnityEngine;
using UnityEngine.SceneManagement;
//Class defines the behavior of the gamma ray part. 

public class GammaRay : MonoBehaviour
{

    [SerializeField] private Spacecraft spacecraft;
    [SerializeField] private Sprite defaultSprite;
    [SerializeField] private Sprite glowSprite;

    // Start with the component turned off
    public void Awake()
    {
        enabled = false;
        OrbitAssist.OnEnteredOrbit += activateGlow;
        SceneManager.sceneLoaded += deactivateGlow;

    } 
    // Update is called once per frame
    void Update()
    {
        
    }

    void activateGlow(object sender, System.EventArgs e)
    {
        GetComponentInChildren<SpriteRenderer>().sprite = glowSprite;
    }

    void deactivateGlow(Scene scene, LoadSceneMode mode)
    {
        GetComponentInChildren<SpriteRenderer>().sprite = defaultSprite;
    }

    void OnDestroy()
    {
        OrbitAssist.OnEnteredOrbit -= activateGlow;
        SceneManager.sceneLoaded -= deactivateGlow;
    }
}
