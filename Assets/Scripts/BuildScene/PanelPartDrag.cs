using UnityEngine;
using UnityEngine.EventSystems;

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

    public void OnPointerEnter(PointerEventData eventData)
    {
        PartTooltipUI.Instance?.ShowDelayed(partData);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        PartTooltipUI.Instance?.Hide();
    }

    public void Initialize(PartScriptableObject part) {
        partData = part;

        // Cache the part's visual color for the ghost preview
        SpriteRenderer sr = part.part.GetComponentInChildren<SpriteRenderer>();
        if (sr != null) baseColor = sr.color;
    }

    private void Awake() {
        SpriteRenderer sr = partData.part.GetComponentInChildren<SpriteRenderer>();
        if (sr != null) baseColor = sr.color;
    }

    private void Start() {
        buildFactsPopup = GameObject.Find("BuildFactsPopup").GetComponent<BuildFactsPopup>();
        highlight = GameObject.Find("Highlight");
        highlightSprite = highlight.GetComponent<SpriteRenderer>();
        
        partDB = SpacecraftPartDatabase.Instance;
        colorblindMode = Settings.Instance.colorblindMode;
    }
    
    public void OnBeginDrag(PointerEventData eventData) {
        PartTooltipUI.Instance?.Hide();
        // Notify the drag hint to stop
        DragHintAnimator hint = FindAnyObjectByType<DragHintAnimator>();
        if (hint != null) hint.StopHint();

        // Advance the tutorial if necessary
        Tutorial.instance.partAdded(partData.part.name);

        ghostPreview = Instantiate(partData.part);
        ghostSprite = ghostPreview.GetComponentInChildren<SpriteRenderer>();
        ghostSprite.color = new Color(baseColor.r, baseColor.g, baseColor.b, 0.5f);
        ghostSprite.sortingLayerName = "MidDrag";

        UpdateGhostPosition(eventData);
    }

    public void OnDrag(PointerEventData eventData) {
        if (ghostPreview == null) return;
        UpdateGhostPosition(eventData);
    }

    public void OnEndDrag(PointerEventData eventData) {
        if (ghostPreview == null) return;

        ShipBuildingGrid grid = ShipBuildingGrid.Instance;
        if (grid != null) {
            Vector3 worldPos = ScreenToWorld(eventData.position);
            Vector3? snapPos = grid.PostionToGridPosition(worldPos);

            if (snapPos != null) {
                (int, int) coords = grid.UnityPositionToGridCoordinates((Vector3)snapPos);
                GameObject part = partData.part;
                if (grid.CanPlacePart(ghostPreview, coords)) {
                    if(partDB.PartIsRotatable(part)) {
                        grid.PlacePartAtCoordinates(part, coords, ghostPreview.GetComponent<RotatablePart>().connectingDirection);
                    }
                    else grid.PlacePartAtCoordinates(part, coords);
                    buildFactsPopup.Popup(partData.name);
                }
            }
        }
        
        Destroy(ghostPreview);
        ghostPreview = null;
        ghostSprite = null;
        ShipBuildingGrid.Instance.HandleLeftClick();
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
            highlight.transform.position = ghostPreview.transform.position;
            
            highlightSprite.color = colorblindMode ? Color.white : ShipBuildingGrid.colorHighlightInvisible;
            if (colorblindMode) highlightSprite.sprite = valid ? colorblindValid : colorblindInvalid;
        } else {
            ghostPreview.transform.position = worldPos;
            highlight.transform.position = worldPos;
            highlightSprite.color = ShipBuildingGrid.colorHighlightInvisible;
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
}
