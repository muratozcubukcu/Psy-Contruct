using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.U2D;
using UnityEngine;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(Collider2D))]

//Script that allows ship parts to be dragged around the grid. Also connects parts together with joints.
public class PartDrag : MonoBehaviour {
    [SerializeField] private GameObject selectedObject;
    [SerializeField] private GameObject objectVisual;
    
    [SerializeField] private GameObject highlight;
    private SpriteRenderer highlightSprite;

    [SerializeField] private Sprite colorblindValid;
    [SerializeField] private Sprite colorblindInvalid;
    private static readonly Color colorValid   = new Color(0.3f, 1f, 0.3f, 0.6f);
    private static readonly Color colorInvalid = new Color(1f, 0.3f, 0.3f, 0.6f);
    
    private GameObject stackedPart;
    private bool colorblindMode;
    private bool draggingStackablePart;
    private Vector3 screenPoint;
    private Vector3 offset;
    private Vector3 originalPosition;
    private Collider2D partCollider;
    private Quaternion lockedRotation;
    private ShipBuildingGrid shipGrid;
    private SpacecraftPartDatabase partDB;
    private SpriteRenderer objectSprite;
    private Color baseColor;
    private Color stackablePartBaseColor;
    private Sprite baseHighlightSprite;
    private string midDragLayer = "MidDrag";
    private string stackablePartLayer = "StackablePart";
    private string defaultLayer = "Default";
    private string spacecraftLayer = "SpaceCraft";

    private void Awake() {
        partCollider = GetComponent<Collider2D>();
        objectSprite = objectVisual.GetComponent<SpriteRenderer>();
        
        lockedRotation = transform.rotation;
        
        shipGrid = ShipBuildingGrid.Instance;
        highlight = GameObject.Find("Highlight");
        highlightSprite = highlight.GetComponent<SpriteRenderer>();
        baseHighlightSprite = highlightSprite.sprite;
        partDB = SpacecraftPartDatabase.Instance;

        colorblindMode = Settings.Instance.colorblindMode;
    }

    private void Start() {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnEnable() {
        shipGrid = ShipBuildingGrid.Instance;
    }

    private void OnMouseDown() {
        if (!Spacecraft.IsBuildMode) return;
        
        (int, int) currCoords = shipGrid.UnityPositionToGridCoordinates(transform.position);
        draggingStackablePart = false;
        stackedPart = null;
        
        if (highlight == null) {
            highlight = GameObject.Find("Highlight");
            highlightSprite = highlight.GetComponent<SpriteRenderer>();
            colorblindMode = Settings.Instance.colorblindMode;
        }

        if (partDB.PartIsStackable(shipGrid.GetGridCellValueByWorldPosition(transform.position))) {
            if(partDB.GetPartID(gameObject) == 1) {
                stackedPart = shipGrid.GetPlacedPartByWorldPosition(transform.position);
                stackedPart.GetComponent<PartDrag>().OnMouseDown();
            } else {
                draggingStackablePart = true;
                shipGrid.placedParts[currCoords] = shipGrid.partStackedOn[gameObject];
                shipGrid.partStackedOn.Remove(gameObject);
            }
        }

        originalPosition = transform.position;
        baseColor = objectSprite.color;
        screenPoint = Camera.main.WorldToScreenPoint(gameObject.transform.position);

        offset = gameObject.transform.position - Camera.main.ScreenToWorldPoint(
            new Vector3(Input.mousePosition.x, Input.mousePosition.y, screenPoint.z)
        );

        Debug.Log($"Part is connected: {shipGrid.PartIsConnected(currCoords)}");

        if(!draggingStackablePart) shipGrid.RemovePlacedPartAtWorldPosition(originalPosition);
        shipGrid.SetGridCellValueByUnityPosition(originalPosition, draggingStackablePart ? 1 : -1);
        
        shipGrid.SetSelectedPart(gameObject);

        SetSortingLayer(midDragLayer);
        SetLayer(midDragLayer);
        
        // Enable physics temporarily for dragging
        Rigidbody2D rb = GetComponent<Rigidbody2D>();
        if (rb != null) {
            rb.bodyType = RigidbodyType2D.Dynamic;
            rb.simulated = true;
        }
    }

    void OnMouseDrag() {
        if (!Spacecraft.IsBuildMode) return;
        
        Vector3 curScreenPoint = new Vector3(Input.mousePosition.x, Input.mousePosition.y, screenPoint.z);
        Vector3 curPosition = Camera.main.ScreenToWorldPoint(curScreenPoint) + offset;
        transform.rotation = lockedRotation;

        // Snap to grid and show valid/invalid placement color feedback
        if (shipGrid == null) shipGrid = ShipBuildingGrid.Instance;
        if (highlight == null) highlight = GameObject.Find("Highlight");
        if (shipGrid != null) {
            Vector3? snapPos = shipGrid.PostionToGridPosition(curPosition);
            if (snapPos != null) {
                transform.position = (Vector3)snapPos;
                (int, int) coords = shipGrid.UnityPositionToGridCoordinates((Vector3)snapPos);
                bool valid = shipGrid.CanPlacePart(gameObject, coords) || CanSwapPart(gameObject, originalPosition);
                objectSprite.color = valid ? colorValid : colorInvalid;
                
                highlight.transform.position = transform.position;
                highlightSprite.color = colorblindMode ? Color.white : ShipBuildingGrid.colorHighlightInvisible;
                if (colorblindMode) highlightSprite.sprite = valid ? colorblindValid : colorblindInvalid;
            } else {
                transform.position = curPosition;
                highlight.transform.position = curPosition;
                highlightSprite.color = ShipBuildingGrid.colorHighlightInvisible;
                objectSprite.color = new Color(baseColor.r, baseColor.g, baseColor.b, 0.5f);
            }
        } else {
            transform.position = curPosition;
            highlight.transform.position = curPosition;
            highlightSprite.color = ShipBuildingGrid.colorHighlightInvisible;
        }
        
        if(stackedPart != null) stackedPart.GetComponent<PartDrag>().OnMouseDrag();
    }

    void OnMouseUp() {
        if (!Spacecraft.IsBuildMode) return;
        if (shipGrid == null || partCollider == null) return;
        
        objectSprite.color = baseColor;

        transform.rotation = lockedRotation;
        

        Vector3? nullableGridSnapPosition = shipGrid.PostionToGridPosition(transform.position);
        if (nullableGridSnapPosition == null) {
            PlacePart(gameObject, originalPosition); //Place part bc the part needs to be placed to be deleted
            if(stackedPart != null) stackedPart.GetComponent<PartDrag>().OnMouseUp();
            shipGrid.DeletePart(shipGrid.UnityPositionToGridCoordinates(originalPosition));
            return;
        }

        Vector3 gridSnapPosition = (Vector3)nullableGridSnapPosition;

        int gridCellValue = shipGrid.GetGridCellValue(shipGrid.UnityPositionToGridCoordinates(gridSnapPosition));
        if (gridCellValue == -1 || (draggingStackablePart && gridCellValue == 1)) {
            if (TryPlacePart(gameObject, gridSnapPosition)) {
                if(stackedPart != null) stackedPart.GetComponent<PartDrag>().OnMouseUp();
                return;
            }
        } else {
            GameObject partToBeSwapped = shipGrid.GetPlacedPartByWorldPosition(gridSnapPosition);
            if (partToBeSwapped != null && TrySwapPart(gameObject, originalPosition, partToBeSwapped.gameObject, gridSnapPosition)) {
                if(stackedPart != null) stackedPart.GetComponent<PartDrag>().OnMouseUp();
                return;
            }
        }
        
        PlacePart(gameObject, originalPosition);
        shipGrid.HandleLeftClick();
        highlightSprite.color = ShipBuildingGrid.colorHighlight;

        if(stackedPart != null) stackedPart.GetComponent<PartDrag>().OnMouseUp();
    }

    private bool TryPlacePart(GameObject part, Vector3 worldPosition) {
        if (!shipGrid.CanPlacePart(part, shipGrid.UnityPositionToGridCoordinates(worldPosition))) return false;
        
        PlacePart(part, worldPosition);
        return true;
    }

    private void PlacePart(GameObject part, Vector3 worldPosition) {
        part.transform.position = worldPosition;
        
        if(partDB.PartIsStackable(part)) {
            SetSortingLayer(stackablePartLayer, part);
            shipGrid.partStackedOn[part] = shipGrid.GetPlacedPartByWorldPosition(worldPosition);
        }
        else SetSortingLayer(defaultLayer, part);

        // Update BOTH grid + dictionary at the new cell
        shipGrid.SetGridCellValueByUnityPosition(part.transform.position, partDB.GetPartID(part));
        shipGrid.SetPlacedPartAtWorldPosition(part.transform.position, part.gameObject);
        
        if(partDB.PartIsStackable(part)) SetSortingLayer(stackablePartLayer, part);
        else SetSortingLayer(defaultLayer, part);
        
        SetLayer(spacecraftLayer, part);
        
        SetKinematicRB(part);
    }

    private bool CanSwapPart(GameObject draggedPart, Vector3 draggedOGPosition, GameObject otherPart, Vector3 otherOGPosition) {
        if (partDB.GetPartID(otherPart) == 0) return false;
        
        int otherID = partDB.GetPartID(otherPart);
        int draggedID = partDB.GetPartID(draggedPart);

        if (partDB.PartIsStackable(otherID) && partDB.PartIsStackable(draggedID)) return true;
        
        shipGrid.SetGridCellValueByUnityPosition(otherOGPosition, -1);
        shipGrid.SetGridCellValueByUnityPosition(draggedOGPosition, otherID);
        bool canPlaceDraggedPart = shipGrid.CanPlacePart(draggedPart, shipGrid.UnityPositionToGridCoordinates(otherOGPosition));
        shipGrid.SetGridCellValueByUnityPosition(draggedOGPosition, -1);
        
        shipGrid.SetGridCellValueByUnityPosition(otherOGPosition, draggedID);
        bool canPlaceOtherPart = shipGrid.CanPlacePart(otherPart, shipGrid.UnityPositionToGridCoordinates(draggedOGPosition));
        shipGrid.SetGridCellValueByUnityPosition(otherOGPosition, -1);
        
        shipGrid.SetGridCellValueByUnityPosition(otherOGPosition, otherID);
        
        if (canPlaceDraggedPart && canPlaceOtherPart) return true;
        
        return false;
    }

    private bool CanSwapPart(GameObject draggedPart, Vector3 draggedOGPosition) {
        Vector3? nullableGridSnapPosition = shipGrid.PostionToGridPosition(transform.position);
        if (nullableGridSnapPosition == null) return false;
        Vector3 gridSnapPosition = (Vector3)nullableGridSnapPosition;
        
        GameObject partToBeSwapped = shipGrid.GetPlacedPartByWorldPosition(gridSnapPosition);
        if (partToBeSwapped == null) return false;

        return CanSwapPart(draggedPart, draggedOGPosition, partToBeSwapped, gridSnapPosition);
    }

    private bool TrySwapPart(GameObject draggedPart, Vector3 draggedOGPosition, GameObject otherPart, Vector3 otherOGPosition) {
        if (draggedPart == otherPart) return true;
        if (!CanSwapPart(draggedPart, draggedOGPosition, otherPart, otherOGPosition)) return false;
        
        //If swapping a part that has a stacked part on top of it with another stacked part,
        //just swap the stacked parts and leave the ship parts where they are.
        GameObject draggedStackedPart = draggedPart.GetComponent<PartDrag>().stackedPart;
        if (draggedStackedPart != null && partDB.PartIsStackable(otherPart)) {
            PlacePart(draggedPart, draggedOGPosition);
            return TrySwapPart(draggedStackedPart, draggedOGPosition, otherPart, otherOGPosition);
        }
        
        //When swapping with a stackable part, we need to swap the ship part that it is stacked on first
        if (partDB.PartIsStackable(otherPart) && !partDB.PartIsStackable(draggedPart)) {
            foreach (Transform part in Spacecraft.GetInstance().transform) {
                if(part.position == otherOGPosition && part.gameObject != draggedPart && part.gameObject != otherPart) {
                    PlacePart(part.gameObject, draggedOGPosition);
                }
            }
        }
        
        PlacePart(otherPart, draggedOGPosition);
        PlacePart(draggedPart, otherOGPosition);
        
        
        return true;
    }

    private void SetKinematicRB(GameObject obj = null) {
        if (!Spacecraft.IsBuildMode) return;
        if(obj == null) obj = gameObject;

        Rigidbody2D rb = obj.GetComponent<Rigidbody2D>();
        if (rb != null) {
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.simulated = true; 
        }
    }

    private void SetSortingLayer(string layer, GameObject obj = null) {
        if (obj == null) obj = gameObject;
        
        obj.GetComponent<PartDrag>().objectSprite.sortingLayerName = layer;

        Canvas canvas = obj.GetComponentInChildren<Canvas>();
        if (canvas == null) return;

        canvas.sortingLayerName = layer;
    }

    private void SetLayer(string layer, GameObject obj = null) {
        if (obj == null) obj = gameObject;
        obj.layer = LayerMask.NameToLayer(layer);
    }
    
    private void Update() {
        if (transform.rotation != lockedRotation) transform.rotation = lockedRotation;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode) {
        if (scene.name == "BuildScene") {
            shipGrid = ShipBuildingGrid.Instance;
        }
    }
}