using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

public class FlightTutorial : MonoBehaviour
{


    private TextMeshProUGUI text;
    private RectTransform textbox;
    private Image image;

    void Start()
    {
        text = GetComponentInChildren<TextMeshProUGUI>();
        textbox = GetComponentInChildren<RectTransform>();
        image = GetComponent<Image>();
        Debug.Log("Start");
        StartCoroutine(delayedFadeAway());
    }

    private IEnumerator delayedFadeAway()
    {
        yield return new WaitForSeconds(8);
        yield return StartCoroutine(Fade(1f, 0f, 2f));
    }

    private IEnumerator Fade(float from, float to, float duration)
    {
        float elapsed = 0f;
        Color imageToColor = image.color;
        Color textToColor = text.color;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            imageToColor.a = Mathf.Lerp(from, to, elapsed / duration);
            textToColor.a = imageToColor.a;
            image.color = imageToColor;
            text.color = textToColor;
            yield return null;
        }
    }
}
