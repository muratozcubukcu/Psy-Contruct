using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Standalone drag hint that finds the BuildPanelCanvas at runtime and
/// animates a ghost sprite from the first part item toward the grid.
/// Attach this to any GameObject in the BuildScene (e.g. BuildManager).
/// It will find everything it needs on its own.
/// </summary>
public class DragHintAnimator : MonoBehaviour {

    private RectTransform ghostRect;
    private Image ghostImage;
    private Transform anchorTransform;
    private Camera mainCam;
    private bool animating;
    private float elapsed;
    private bool isDragging = true;

    private const float dragDuration = 1.5f;
    private const float pauseDuration = 0.5f;
    private const float startAlpha = 0.7f;
    private const float searchDelay = 1f;
    private float searchTimer;

    private void Awake() {
        mainCam = Camera.main;
    }

    private void Update() {
        // If not yet set up, wait then search for the panel
        if (!animating) {
            searchTimer += Time.deltaTime;
            if (searchTimer < searchDelay) return;
            TrySetup();
            return;
        }
        
        Vector3 anchorPos = anchorTransform.position;
        Vector3 endPos;
        Color c = ghostImage.color;

        // Grid center is (0, -0.5), one row above = (0, 0.5)
        Vector3 gridTarget = new Vector3(0f, 0.5f, 0f);
        endPos = mainCam.WorldToScreenPoint(gridTarget);

        elapsed += Time.deltaTime;

        if (isDragging) {
            float t = Mathf.Clamp01(elapsed / dragDuration);
            float eased = 1f - (1f - t) * (1f - t);
            ghostRect.position = Vector3.Lerp(anchorPos, endPos, eased);
            ghostImage.color = new Color(c.r, c.g, c.b, Mathf.Lerp(startAlpha, 0f, t));

            if (t < 1f) return;
            
            isDragging = false;
            elapsed = 0f;
        }

        if (elapsed < pauseDuration) return;
        
        ghostRect.position = anchorPos;
        ghostImage.color = new Color(c.r, c.g, c.b, startAlpha);
        isDragging = true;
        elapsed = 0f;
    }

    private void TrySetup() {
        // Find the BuildPanelCanvas
        GameObject canvasGO = GameObject.Find("BuildPanelCanvas");
        if (canvasGO == null) return;

        Canvas canvas = canvasGO.GetComponent<Canvas>();
        if (canvas == null) return;

        // Find the Content container, then get the first part item
        Transform content = canvasGO.transform.Find("SidePanel/Content");
        if (content == null || content.childCount == 0) return;

        Transform firstItem = content.GetChild(0);
        anchorTransform = firstItem;

        // Get the swatch image from the first item for the ghost sprite
        Transform swatch = firstItem.Find("Swatch");
        Sprite ghostSprite = null;
        Color ghostColor = Color.white;
        if (swatch != null) {
            Image swatchImg = swatch.GetComponent<Image>();
            if (swatchImg != null) {
                ghostSprite = swatchImg.sprite;
                ghostColor = swatchImg.color;
            }
        }

        // Calculate the size of 1 grid cell in canvas units to match real part size
        float canvasScale = canvas.scaleFactor;
        float ghostSize = 50f; // fallback
        if (canvasScale > 0f) {
            Vector3 cellBottom = mainCam.WorldToScreenPoint(new Vector3(0f, 0f, 0f));
            Vector3 cellTop = mainCam.WorldToScreenPoint(new Vector3(0f, 1f, 0f));
            ghostSize = Mathf.Abs(cellTop.y - cellBottom.y) / canvasScale;
        }

        // Create ghost image on the canvas
        GameObject ghostGO = new GameObject("DragHintGhost");
        ghostGO.transform.SetParent(canvas.transform, false);
        ghostGO.transform.SetAsLastSibling();

        ghostRect = ghostGO.AddComponent<RectTransform>();
        ghostRect.sizeDelta = new Vector2(ghostSize, ghostSize);

        ghostImage = ghostGO.AddComponent<Image>();
        ghostImage.raycastTarget = false;
        ghostImage.preserveAspect = true;
        if (ghostSprite != null) {
            ghostImage.sprite = ghostSprite;
            ghostImage.color = new Color(ghostColor.r, ghostColor.g, ghostColor.b, startAlpha);
        } else {
            ghostImage.color = new Color(1f, 1f, 1f, startAlpha);
        }

        ghostRect.position = anchorTransform.position;
        elapsed = 0f;
        isDragging = true;
        animating = true;
    }

    public void StopHint() {
        if (ghostRect != null) {
            Destroy(ghostRect.gameObject);
        }
        Destroy(this);
    }
}
