using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>
/// Animates a ghost sprite from the sidebar item for the current tutorial step
/// toward the grid center. Tutorial.cs destroys and re-creates this component
/// each step so it always targets the correct part.
/// </summary>
public class DragHintAnimator : MonoBehaviour {

    private Canvas        ghostCanvas;
    private RectTransform ghostRect;
    private Image         ghostImage;
    private readonly List<Transform> anchorTransforms = new List<Transform>();
    private Camera        mainCam;
    private bool          animating;
    private float         elapsed;
    private bool          isDragging = true;
    private float         searchTimer;
    private int           anchorIndex;

    private const float DragDuration  = 1.5f;
    private const float PauseDuration = 0.5f;
    private const float StartAlpha    = 0.7f;
    private const float SearchDelay   = 0.3f;

    private void Awake() {
        mainCam = Camera.main;
    }

    private void Update() {
        if (!animating) {
            searchTimer += Time.deltaTime;
            if (searchTimer < SearchDelay) return;
            TrySetup();
            return;
        }

        if (anchorTransforms.Count == 0 || ghostRect == null || mainCam == null) return;

        Transform anchorTransform = anchorTransforms[Mathf.Clamp(anchorIndex, 0, anchorTransforms.Count - 1)];
        if (anchorTransform == null) {
            StopHint();
            return;
        }

        Vector3 anchorPos  = anchorTransform.position;
        Vector3 gridTarget = mainCam.WorldToScreenPoint(new Vector3(0f, 0.5f, 0f));
        Color   c          = ghostImage.color;
        elapsed += Time.deltaTime;

        if (isDragging) {
            float t     = Mathf.Clamp01(elapsed / DragDuration);
            float eased = 1f - (1f - t) * (1f - t);
            ghostRect.position = Vector3.Lerp(anchorPos, gridTarget, eased);
            ghostImage.color   = new Color(c.r, c.g, c.b, Mathf.Lerp(StartAlpha, 0f, t));
            if (t >= 1f) { isDragging = false; elapsed = 0f; }
            return;
        }

        if (elapsed < PauseDuration) return;
        anchorIndex = (anchorIndex + 1) % anchorTransforms.Count;
        Transform nextAnchor = anchorTransforms[anchorIndex];
        ApplyAnchorVisual(nextAnchor);
        ghostRect.position = nextAnchor != null ? nextAnchor.position : anchorPos;
        isDragging = true;
        elapsed    = 0f;
    }

    public void StopHint() {
        if (ghostCanvas != null) Destroy(ghostCanvas.gameObject);
        Destroy(this);
    }

    private void TrySetup() {
        if (Tutorial.instance == null || !Tutorial.instance.IsActive) return;

        string[] partNames = Tutorial.instance.GetCurrentHintPartNames();
        if (partNames == null || partNames.Length == 0) return;

        anchorTransforms.Clear();
        foreach (PanelPartDrag drag in FindObjectsByType<PanelPartDrag>(FindObjectsSortMode.None)) {
            foreach (string partName in partNames) {
                if (drag.PartName == partName) {
                    anchorTransforms.Add(drag.transform);
                    break;
                }
            }
        }

        if (anchorTransforms.Count == 0) return;

        Canvas.ForceUpdateCanvases();

        GameObject canvasGO = new GameObject("DragHintCanvas");
        ghostCanvas = canvasGO.AddComponent<Canvas>();
        ghostCanvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        ghostCanvas.sortingOrder = 300;
        canvasGO.AddComponent<GraphicRaycaster>();

        float ghostSize = GetAnchorVisualSize(anchorTransforms[0]);

        GameObject go = new GameObject("DragHintGhost");
        go.transform.SetParent(canvasGO.transform, false);

        ghostRect           = go.AddComponent<RectTransform>();
        ghostRect.sizeDelta = new Vector2(ghostSize, ghostSize);
        ghostRect.position  = anchorTransforms[0].position;

        ghostImage                = go.AddComponent<Image>();
        ghostImage.raycastTarget  = false;
        ghostImage.preserveAspect = true;
        ApplyAnchorVisual(anchorTransforms[0]);

        anchorIndex = 0;
        isDragging = true;
        elapsed    = 0f;
        animating  = true;
    }

    private void ApplyAnchorVisual(Transform target) {
        if (target == null || ghostImage == null) return;

        Sprite ghostSprite = null;
        Color  ghostColor  = Color.white;
        Transform swatch   = target.Find("Swatch");
        if (swatch != null) {
            Image img = swatch.GetComponent<Image>();
            if (img != null) { ghostSprite = img.sprite; ghostColor = img.color; }
        }

        ghostImage.sprite = ghostSprite;
        ghostImage.color = ghostSprite != null
            ? new Color(ghostColor.r, ghostColor.g, ghostColor.b, StartAlpha)
            : new Color(1f, 1f, 1f, StartAlpha);
    }

    private float GetAnchorVisualSize(Transform target) {
        if (target == null) return 50f;

        RectTransform visualRect = null;
        Transform swatch = target.Find("Swatch");
        if (swatch != null) visualRect = swatch.GetComponent<RectTransform>();
        if (visualRect == null) visualRect = target.GetComponent<RectTransform>();

        if (visualRect == null) return 50f;

        Vector3[] corners = new Vector3[4];
        visualRect.GetWorldCorners(corners);
        float width = Mathf.Abs(corners[2].x - corners[0].x);
        float height = Mathf.Abs(corners[2].y - corners[0].y);
        return Mathf.Clamp(Mathf.Max(width, height), 36f, 72f);
    }
}
