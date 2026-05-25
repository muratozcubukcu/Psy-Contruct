using System;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(Collider2D))]

//Script that allows ship parts to be dragged around the grid. Also connects parts together with joints.
public class PartDrag : MonoBehaviour {
    [SerializeField] private GameObject selectedObject;
    [SerializeField] private GameObject objectVisual;
    
    private GameObject highlight;
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
    private Vector3 correctPosition; //Used to fix some uncommon bugs
    private Collider2D partCollider;
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
        screenPoint = Camera.main.WorldToScreenPoint(transform.position);

        offset = transform.position - Camera.main.ScreenToWorldPoint(
            new Vector3(Input.mousePosition.x, Input.mousePosition.y, screenPoint.z)
        );

        if(!draggingStackablePart) shipGrid.RemovePlacedPartAtWorldPosition(originalPosition);
        shipGrid.SetGridCellValueByUnityPosition(originalPosition, draggingStackablePart ? 1 : -1);
        
        shipGrid.SetSelectedPart(gameObject);

        SetSortingLayer(midDragLayer);
        SetLayer(midDragLayer);
        
        //Enable physics temporarily for dragging
        Rigidbody2D rb = GetComponent<Rigidbody2D>();
        if (rb != null) {
            rb.bodyType = RigidbodyType2D.Dynamic;
            rb.simulated = true;
        }
    }

    void OnMouseDrag() {
        if (!Spacecraft.IsBuildMode) return;
        
        Vector3 curScreenPoint = new Vector3(Input.mousePosition.x, Input.mousePosition.y, screenPoint.z);
        Vector3 curPos = Camera.main.ScreenToWorldPoint(curScreenPoint) + offset;
        Vector3? snapPos = shipGrid.PostionToGridPosition(curPos);
        
        if (snapPos != null) {
            transform.position = (Vector3)snapPos;
            (int, int) coords = shipGrid.UnityPositionToGridCoordinates((Vector3)snapPos);
            bool valid = shipGrid.CanPlacePart(gameObject, coords) || CanSwapPart(gameObject, shipGrid.GetPlacedPartByWorldPosition(transform.position));
            objectSprite.color = valid ? colorValid : colorInvalid;
            
            highlight.transform.position = transform.position;
            highlightSprite.color = colorblindMode ? Color.white : ShipBuildingGrid.colorHighlightInvisible;
            if (colorblindMode) highlightSprite.sprite = valid ? colorblindValid : colorblindInvalid;
        } else {
            transform.position = curPos;
            highlight.transform.position = curPos;
            highlightSprite.color = ShipBuildingGrid.colorHighlightInvisible;
            objectSprite.color = new Color(baseColor.r, baseColor.g, baseColor.b, 0.5f);
        }
        
        if(stackedPart != null) stackedPart.GetComponent<PartDrag>().OnMouseDrag();

        correctPosition = transform.position;
    }

    void OnMouseUp() {
        if (!Spacecraft.IsBuildMode) return;
        if (shipGrid == null || partCollider == null) return;
        
        transform.position = correctPosition;
            
        objectSprite.color = baseColor;
        
        Vector3? nullableSnapPos = shipGrid.PostionToGridPosition(transform.position);
        if (nullableSnapPos == null) {
            //Delete part
            PlacePart(gameObject, originalPosition); //Place part bc the part needs to be placed to be deleted
            shipGrid.DeletePart(shipGrid.UnityPositionToGridCoordinates(originalPosition));
            if(stackedPart != null) stackedPart.GetComponent<PartDrag>().OnMouseUp();
            return;
        }

        Vector3 snapPos = (Vector3)nullableSnapPos;

        int gridCellValue = shipGrid.GetGridCellValue(shipGrid.UnityPositionToGridCoordinates(snapPos));
        if (gridCellValue == -1 || (draggingStackablePart && gridCellValue == 1)) {
            if (TryPlacePart(gameObject, snapPos)) {
                //Place part
                if(stackedPart != null) stackedPart.GetComponent<PartDrag>().OnMouseUp();
                return;
            }
        } else {
            GameObject partToBeSwapped = shipGrid.GetPlacedPartByWorldPosition(snapPos);
            if (partToBeSwapped != null && TrySwapPart(gameObject, originalPosition, partToBeSwapped.gameObject, snapPos)) {
                //Swap part
                if(stackedPart != null) stackedPart.GetComponent<PartDrag>().OnMouseUp();
                return;
            }
        }
        
        //Place part back where it originally was
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
            part.transform.position += new Vector3(0,0,-1);
            shipGrid.partStackedOn[part] = shipGrid.GetPlacedPartByWorldPosition(worldPosition);
        }
        else SetSortingLayer(defaultLayer, part);
        
        if(part.TryGetComponent(out RotatablePart rotatable)) {
            rotatable.TryAutoSetRotation(shipGrid.UnityPositionToGridCoordinates(worldPosition));
        }

        // Update BOTH grid + dictionary at the new cell
        shipGrid.SetGridCellValueByUnityPosition(part.transform.position, partDB.GetPartID(part));
        shipGrid.SetPlacedPartAtWorldPosition(part.transform.position, part.gameObject);
        
        if(partDB.PartIsStackable(part)) SetSortingLayer(stackablePartLayer, part);
        else SetSortingLayer(defaultLayer, part);
        
        SetLayer(spacecraftLayer, part);
        
        BuildSceneSFX.Instance.PlayRandomBuildSound();
        
        SetKinematicRB(part);
    }
    
    private bool CanSwapPart(GameObject draggedPart, GameObject otherPart) {
        if (partDB.GetPartID(otherPart) <= 0) return false;
        
        int otherID = partDB.GetPartID(otherPart);
        int draggedID = partDB.GetPartID(draggedPart);

        if (partDB.PartIsStackable(otherID) && partDB.PartIsStackable(draggedID)) return true;

        return !partDB.PartIsStackable(draggedPart);
    }

    private bool TrySwapPart(GameObject draggedPart, Vector3 draggedOGPosition, GameObject otherPart, Vector3 otherOGPosition) {
        if (draggedPart == otherPart) return true;
        if (!CanSwapPart(draggedPart, otherPart)) return false;
        
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

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode) {
        if (scene.name == "BuildScene") {
            shipGrid = ShipBuildingGrid.Instance;
        }
    }
}