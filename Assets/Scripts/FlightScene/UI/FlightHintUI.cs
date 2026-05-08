using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

public class FlightHintUI : MonoBehaviour
{
    [SerializeField] private float inactivityThreshold = 3f; // seconds before hint appears
    [SerializeField] private float displayDuration = 5f;
    [SerializeField] private float fadeDuration = 1f;
    [SerializeField] private string hintMessage = "Press 1, 2, 3, or 4 to fire your engines!";

    private CanvasGroup canvasGroup;
    private bool isShowing = false;
    private float timeSinceLastInput = 0f;
    private bool engineWasActive = false;

    private void Start()
    {
        CreateHintUI();
        GameInput.Instance.OnEnginePerformedAction += OnEngineActive;
        GameInput.Instance.OnEngineCanceledAction += OnEngineActive;
    }

    private void OnEngineActive(object sender, GameInput.EngineEventArgs e)
    {
        timeSinceLastInput = 0f;
        engineWasActive = true;

        if (isShowing) StartCoroutine(FadeOut());
    }

    private void Update()
    {
        if (!engineWasActive)
        {
            timeSinceLastInput += Time.deltaTime;
            if (timeSinceLastInput >= inactivityThreshold && !isShowing)
            {
                StartCoroutine(ShowHint());
            }
        }
    }

    private IEnumerator ShowHint()
    {
        isShowing = true;
        yield return StartCoroutine(Fade(0f, 1f, fadeDuration));
        yield return new WaitForSeconds(displayDuration);
        yield return StartCoroutine(Fade(1f, 0f, fadeDuration));
        isShowing = false;

        // Reset so it can show again if still not flying
        if (!engineWasActive) timeSinceLastInput = 0f;
    }

    private IEnumerator FadeOut()
    {
        isShowing = false;
        yield return StartCoroutine(Fade(canvasGroup.alpha, 0f, fadeDuration));
    }

    private IEnumerator Fade(float from, float to, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            canvasGroup.alpha = Mathf.Lerp(from, to, elapsed / duration);
            yield return null;
        }
        canvasGroup.alpha = to;
    }

    private void CreateHintUI()
    {
        // Canvas
        GameObject canvasObj = new GameObject("HintCanvas");
        canvasObj.transform.SetParent(transform);
        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        // Panel
        GameObject panel = new GameObject("HintPanel");
        panel.transform.SetParent(canvasObj.transform, false);
        canvasGroup = panel.AddComponent<CanvasGroup>();
        canvasGroup.alpha = 0f;

        RectTransform panelRect = panel.AddComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.25f, 0.02f);
        panelRect.anchorMax = new Vector2(0.75f, 0.10f);
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;

        Image panelImage = panel.AddComponent<Image>();
        panelImage.color = new Color(0.05f, 0.05f, 0.15f, 0.85f);

        // Text
        GameObject textObj = new GameObject("HintText");
        textObj.transform.SetParent(panel.transform, false);
        RectTransform textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(20, 5);
        textRect.offsetMax = new Vector2(-20, -5);

        TextMeshProUGUI text = textObj.AddComponent<TextMeshProUGUI>();
        text.text = hintMessage;
        text.fontSize = 18;
        text.fontStyle = FontStyles.Italic;
        text.color = Color.white;
        text.alignment = TextAlignmentOptions.Center;
    }

    private void OnDestroy()
    {
        if (GameInput.Instance != null)
        {
            GameInput.Instance.OnEnginePerformedAction -= OnEngineActive;
            GameInput.Instance.OnEngineCanceledAction -= OnEngineActive;
        }
    }
}