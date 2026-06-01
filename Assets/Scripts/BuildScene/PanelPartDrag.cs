using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Handles dragging a part from the side panel onto the build grid.
/// Creates a ghost preview that snaps to grid cells and shows placement validity.
/// </summary>
public class PanelPartDrag : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerEnterHandler, IPointerExitHandler {
    [SerializeField] private PartScriptableObject partData;
    private SpacecraftPartDatabase partDB;
    private BuildFactsPopup buildFactsPopup;
    private GameObject ghostPreview;
    private SpriteRenderer ghostSprite;
    private Color baseColor = Color.white;

    [SerializeField] private GameObject highlight;
    private SpriteRenderer highlightSprite;

    [SerializeField] private Sprite colorblindValid;
    [SerializeField] private Sprite colorblindInvalid;

    private static readonly Color colorValid   = new Color(0.3f, 1f, 0.3f, 0.6f);
    private static readonly Color colorInvalid = new Color(1f, 0.3f, 0.3f, 0.6f);

    private bool colorblindMode;

    private Image itemBackground;
    private Color originalBackgroundColor;
    private Coroutine flashCoroutine;

    public string PartName => partData != null && partData.part != null ? partData.part.name : null;

    public void OnPointerEnter(PointerEventData eventData)
    {
        PartTooltipUI.Instance?.ShowDelayed(partData);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        PartTooltipUI.Instance?.Hide(partData);
    }

    public void Initialize(PartScriptableObject part) {
        partData = part;
        CacheBaseColor();
    }

    private void Awake() {
        CacheBaseColor();
    }

    private void Start() {
        GameObject popupGO = GameObject.Find("BuildFactsPopup");
        if (popupGO != null) buildFactsPopup = popupGO.GetComponent<BuildFactsPopup>();

        highlight = GameObject.Find("Highlight");
        if (highlight != null) highlightSprite = highlight.GetComponent<SpriteRenderer>();

        partDB = SpacecraftPartDatabase.Instance;
        colorblindMode = Settings.Instance != null && Settings.Instance.colorblindMode;

        itemBackground = GetComponent<Image>();
        if (itemBackground != null) originalBackgroundColor = itemBackground.color;
    }
    
    public void OnBeginDrag(PointerEventData eventData) {
        if (partData == null || partData.part == null) return;

        if (Tutorial.instance != null && Tutorial.instance.IsActive
            && !Tutorial.instance.CanDragPart(partData.part.name)) {
            FlashBlocked();
            return;
        }

        PartTooltipUI.Instance?.Hide(partData);
        DragHintAnimator hint = FindAnyObjectByType<DragHintAnimator>();
        if (hint != null) hint.StopHint();

        ghostPreview = Instantiate(partData.part);
        ghostSprite = ghostPreview.GetComponentInChildren<SpriteRenderer>();
        ghostSprite.color = new Color(baseColor.r, baseColor.g, baseColor.b, 0.5f);
        ghostSprite.sortingLayerName = "MidDrag";

        ShipBuildingGrid.Instance?.ShowValidPlacementHighlights(ghostPreview);
        UpdateGhostPosition(eventData);
    }

    public void OnDrag(PointerEventData eventData) {
        if (ghostPreview == null) return;
        UpdateGhostPosition(eventData);
    }

    public void OnEndDrag(PointerEventData eventData) {
        if (ghostPreview == null) return;

        ShipBuildingGrid grid = ShipBuildingGrid.Instance;
        bool placed = false;
        if (grid != null) {
            Vector3 worldPos = ScreenToWorld(eventData.position);
            Vector3? snapPos = grid.PostionToGridPosition(worldPos);

            if (snapPos != null) {
                (int, int) coords = grid.UnityPositionToGridCoordinates((Vector3)snapPos);
                GameObject part = partData.part;
                if (grid.CanPlacePart(ghostPreview, coords)) {
                    if(partDB != null && partDB.PartIsRotatable(part)) {
                        grid.PlacePartAtCoordinates(part, coords, ghostPreview.GetComponent<RotatablePart>().connectingDirection);
                    }
                    else grid.PlacePartAtCoordinates(part, coords);
                    placed = true;
                    if (buildFactsPopup != null) buildFactsPopup.Popup(partData.name);
                    Tutorial.instance?.OnPartSuccessfullyPlaced(partData.part.name);
                }
            }
        }
        
        Destroy(ghostPreview);
        ghostPreview = null;
        ghostSprite = null;
        ShipBuildingGrid.Instance?.ClearValidPlacementHighlights();
        ShipBuildingGrid.Instance?.HandleLeftClick();

        if (!placed) {
            BuildSceneSFX.Instance?.PlayInvalidPlacementSound();
            Tutorial.instance?.RestartDragHint();
        }
    }

    private void UpdateGhostPosition(PointerEventData eventData) {
        if (ghostPreview == null) return;

        ShipBuildingGrid shipGrid = ShipBuildingGrid.Instance;
        if (shipGrid == null) return;

        Vector3 worldPos = ScreenToWorld(eventData.position);
        Vector3? snapPos = shipGrid.PostionToGridPosition(worldPos);

        if (snapPos != null) {
            ghostPreview.transform.position = (Vector3)snapPos;
            (int, int) coords = shipGrid.UnityPositionToGridCoordinates((Vector3)snapPos);
            bool valid = shipGrid.CanPlacePart(ghostPreview, coords);
            
            ghostSprite.color = valid ? colorValid : colorInvalid;
            if (highlight != null) highlight.transform.position = ghostPreview.transform.position;

            if (highlightSprite != null) {
                highlightSprite.color = colorblindMode ? Color.white : ShipBuildingGrid.colorHighlightInvisible;
                if (colorblindMode) highlightSprite.sprite = valid ? colorblindValid : colorblindInvalid;
            }
        } else {
            ghostPreview.transform.position = worldPos;
            if (highlight != null) highlight.transform.position = worldPos;
            if (highlightSprite != null) highlightSprite.color = ShipBuildingGrid.colorHighlightInvisible;
            ghostSprite.color = new Color(baseColor.r, baseColor.g, baseColor.b, 0.5f);
        }
    }

    private Vector3 ScreenToWorld(Vector2 screenPos) {
        Camera cam = Camera.main;
        Vector3 worldPos = cam.ScreenToWorldPoint(new Vector3(
            screenPos.x, screenPos.y, Mathf.Abs(cam.transform.position.z)));
        worldPos.z = 0f;
        return worldPos;
    }

    private void FlashBlocked() {
        if (itemBackground == null) return;
        if (flashCoroutine != null) StopCoroutine(flashCoroutine);
        flashCoroutine = StartCoroutine(FlashBlockedRoutine());
    }

    private IEnumerator FlashBlockedRoutine() {
        Color blocked = new Color(0.7f, 0.1f, 0.1f, 0.9f);
        float duration = 0.45f;
        float elapsed  = 0f;
        while (elapsed < duration) {
            float t = elapsed / duration;
            itemBackground.color = Color.Lerp(blocked, originalBackgroundColor, t);
            elapsed += Time.deltaTime;
            yield return null;
        }
        itemBackground.color = originalBackgroundColor;
    }

    private void CacheBaseColor() {
        if (partData == null || partData.part == null) return;

        SpriteRenderer sr = partData.part.GetComponentInChildren<SpriteRenderer>();
        if (sr != null) baseColor = sr.color;
    }
}
