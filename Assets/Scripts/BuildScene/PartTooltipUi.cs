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

    private Coroutine showCoroutine;
    private Canvas parentCanvas;
    private CanvasGroup canvasGroup;

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
        if (showCoroutine != null) StopCoroutine(showCoroutine);
        showCoroutine = StartCoroutine(ShowAfterDelay(part));
    }

    /// <summary>Call this when the pointer exits a part item.</summary>
    public void Hide()
    {
        if (showCoroutine != null) { StopCoroutine(showCoroutine); showCoroutine = null; }
        canvasGroup.alpha = 0;
    }

    private IEnumerator ShowAfterDelay(PartScriptableObject part)
    {
        yield return new WaitForSeconds(hoverDelay);
        Populate(part);
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

    private void Update() {
    if (canvasGroup.alpha == 0) return;

    Vector2 screenPoint = Input.mousePosition;
    Vector2 localPoint;

    RectTransform canvasRect = parentCanvas.GetComponent<RectTransform>();

    RectTransformUtility.ScreenPointToLocalPointInRectangle(
        canvasRect,
        screenPoint,
        parentCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : parentCanvas.worldCamera,
        out localPoint
    );

    tooltipPanel.anchoredPosition = localPoint + offset;

    // Clamp so it never goes off the right or bottom edge
    Vector2 pos = tooltipPanel.anchoredPosition;
    float canvasW = canvasRect.rect.width;
    float canvasH = canvasRect.rect.height;

    // Right edge
    if (pos.x + tooltipPanel.rect.width > canvasW / 2f)
        pos.x -= tooltipPanel.rect.width + (offset.x * 2);

    // Bottom edge
    if (pos.y - tooltipPanel.rect.height < -canvasH / 2f)
        pos.y += tooltipPanel.rect.height + (offset.y * 2);

    tooltipPanel.anchoredPosition = pos;
}
}