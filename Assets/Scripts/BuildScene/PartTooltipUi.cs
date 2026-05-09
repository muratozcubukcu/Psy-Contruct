using System.Collections;
using UnityEngine;
using TMPro;

/// <summary>
/// Singleton tooltip panel that appears after hovering over a sidebar part.
/// Attach this to a UI panel GameObject in the Canvas named "PartTooltip".
/// </summary>
public class PartTooltipUI : MonoBehaviour
{
    public static PartTooltipUI Instance { get; private set; }

    [Header("UI References")]
    [SerializeField] private RectTransform tooltipPanel;
    [SerializeField] private TextMeshProUGUI partNameText;
    [SerializeField] private TextMeshProUGUI descriptionText;
    [SerializeField] private TextMeshProUGUI connectsToText;
    [SerializeField] private TextMeshProUGUI massText;

    [Header("Settings")]
    [SerializeField] private float hoverDelay = 0.6f;
    [SerializeField] private Vector2 offset = new Vector2(15f, -15f);

    [Header("Positioning")]
    [SerializeField] private RectTransform sidebarRect;

    private Coroutine showCoroutine;
    private Canvas parentCanvas;
    private CanvasGroup canvasGroup;
    private PartScriptableObject currentPart;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        parentCanvas = GetComponentInParent<Canvas>();

        canvasGroup = tooltipPanel.gameObject.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = tooltipPanel.gameObject.AddComponent<CanvasGroup>();

        canvasGroup.alpha = 0;
        canvasGroup.blocksRaycasts = false;
        canvasGroup.interactable = false;
    }

    /// <summary>Call this when the pointer enters a part item.</summary>
    public void ShowDelayed(PartScriptableObject part)
    {
        currentPart = part;
        if (showCoroutine != null) StopCoroutine(showCoroutine);
        canvasGroup.alpha = 0;
        showCoroutine = StartCoroutine(ShowAfterDelay(part));
    }

    /// <summary>Call this when the pointer exits a part item.</summary>
    public void Hide(PartScriptableObject part)
    {
        // Only hide if we're still showing this part, ignore if already moved to new one
        if (currentPart != part) return;
        if (showCoroutine != null) { StopCoroutine(showCoroutine); showCoroutine = null; }
        canvasGroup.alpha = 0;
        currentPart = null;
    }

    private IEnumerator ShowAfterDelay(PartScriptableObject part)
    {
        yield return new WaitForSeconds(hoverDelay);
        Populate(part);
        UpdatePosition();
        canvasGroup.alpha = 1;
    }

    private void Populate(PartScriptableObject part)
    {
        partNameText.text = part.name;
        descriptionText.text = string.IsNullOrEmpty(part.description)
            ? "No description available."
            : part.description;
        connectsToText.text = string.IsNullOrEmpty(part.connectsTo)
            ? "Connects to: —"
            : $"Connects to: {part.connectsTo}";
        massText.text = $"Mass: {part.mass} kg";
    }

    private void UpdatePosition()
    {
        Vector2 screenPoint = Input.mousePosition;
        RectTransform canvasRect = parentCanvas.GetComponent<RectTransform>();

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRect,
            screenPoint,
            parentCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : parentCanvas.worldCamera,
            out Vector2 localPoint
        );

        // Lock X to right of sidebar, only follow mouse on Y
        Vector2 sidebarScreenPos;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRect,
            new Vector2(sidebarRect.position.x + sidebarRect.rect.width * parentCanvas.scaleFactor, 0),
            parentCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : parentCanvas.worldCamera,
            out sidebarScreenPos
        );
        tooltipPanel.anchoredPosition = new Vector2(sidebarScreenPos.x + 5f, localPoint.y + offset.y);

        // Clamp bottom edge
        Vector2 pos = tooltipPanel.anchoredPosition;
        float canvasH = canvasRect.rect.height;
        if (pos.y - tooltipPanel.rect.height < -canvasH / 2f)
            pos.y += tooltipPanel.rect.height;
        tooltipPanel.anchoredPosition = pos;
    }

    private void Update()
    {
        if (canvasGroup.alpha == 0) return;
        UpdatePosition();
    }
}