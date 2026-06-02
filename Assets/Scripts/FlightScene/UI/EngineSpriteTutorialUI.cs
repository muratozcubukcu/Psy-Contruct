using UnityEngine;
using UnityEngine.UI;


public class EngineSpriteTutorialUI : MonoBehaviour {
    [SerializeField] private RawImage engineImage;
    [SerializeField] private Texture engineOffImg;
    [SerializeField] private Texture engineOnImg;

    private bool engineOn = true;

    private void ToggleEngine() {
        engineOn = !engineOn;
        engineImage.texture = engineOn ? engineOnImg : engineOffImg;
    }
}
