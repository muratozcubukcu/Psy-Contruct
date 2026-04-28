using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Class that controls the ship building grid. allows the user to place objects and connect them to the base part.
/// </summary>
public class ShipBuildingGrid : MonoBehaviour {
    public static ShipBuildingGrid Instance { get; private set; }
    
    [SerializeField] private GameInput gameInput;
    [SerializeField] private GridVisualizer gridVisualizer;
    [SerializeField] private GameObject highlight;

    private bool colorblindMode = false;

    private GameObject spacecraft;
    private Grid grid;
    private int gridWidth = 5;
    private int gridHeight = 7;
    private float cellSize = 1f;
    private Vector3 gridOriginPosition = new(-2.5f, -4f);
    [SerializeField] private Sprite baseHighlightSprite;
    public static readonly Color colorHighlight   = new Color(1f, 1f, 0.3f, 0.4f);
    [SerializeField] private Sprite colorblindHighlight;
    public static readonly Color colorHighlightInvisible   = new Color(1f, 1f, 0.3f, 0f);
    private static Color colorDisconnected = new Color(1f, 0.4f, 0.4f, 1f);
    private readonly Dictionary<SpriteRenderer, Color> originalSpriteColors = new();

    private GameObject selectedPart;
    private (int, int) selectedTileCoords;
    public Dictionary<(int, int), GameObject> placedParts = new();
    public Dictionary<GameObject, GameObject> partStackedOn = new(); //Key: stacked part, value: ship part
    private bool someTileSelected = false;
    private SpriteRenderer highlightSprite;
    
    private SpacecraftPartDatabase partDB;
    
    private void Awake() {
        if (Instance != null && Instance != this) {
            Destroy(gameObject);
            return;
        }
        
        Instance = this;
        
        grid = new Grid(gridWidth, gridHeight, cellSize, gridOriginPosition);
        partDB = SpacecraftPartDatabase.Instance;
    }
    
    private void Start() {
        gameInput = GameObject.Find("GameInput").GetComponent<GameInput>();
        highlight = GameObject.Find("Highlight");
        highlightSprite = highlight.GetComponent<SpriteRenderer>();
        highlightSprite.color = colorHighlightInvisible;
        highlightSprite.sprite = colorblindMode ? colorblindHighlight : baseHighlightSprite;

        gameInput.OnDeletePartPerformedAction += GameInput_OnDeletePartPerformedAction;
        gameInput.OnLeftMouseClickPerformedAction += GameInput_OnLeftMouseClickAction;
        
        gridVisualizer.VisualizeGrid(grid, gridWidth, gridHeight, cellSize, gridOriginPosition);
        
        spacecraft = Spacecraft.GetInstance().gameObject;
        partDB = SpacecraftPartDatabase.Instance;
        
        colorblindMode = Settings.Instance.colorblindMode;
        colorDisconnected = colorblindMode ? Color.black : new Color(1f, 0.4f, 0.4f, 1f);
        
        if(partDB.hasSavedGridState) LoadSpacecraft();
        else CreateSpacecraft();
    }
    
    private void CreateSpacecraft() {
        spacecraft.transform.position = GridCoordinatesToUnityPosition(gridWidth / 2, gridHeight / 2);
        
        int baseID = partDB.GetPartID(partDB.GetPartGameObject(0));
        (int, int) baseCoords = (gridWidth / 2, gridHeight / 2);

        SetGridCellValue(baseCoords, baseID);
    }

    private void LoadSpacecraft() {
        grid.LoadGridState();

        Rigidbody2D spacecraftRB = spacecraft.GetComponent<Rigidbody2D>();
        spacecraftRB.linearVelocity = Vector2.zero;
        spacecraftRB.angularVelocity = 0f;
        
        Transform shipTransform = spacecraft.transform;
        shipTransform.rotation = Quaternion.Euler(0, 0, 0);
        shipTransform.position = GridCoordinatesToUnityPosition(gridWidth / 2, gridHeight / 2);

        if (SavedPlacedPartsValid()) {
            placedParts = partDB.savedPlacedParts;
            partStackedOn = partDB.savedPartStackedOn;
            foreach (Transform part in shipTransform) {
                part.position += spacecraft.GetComponent<Spacecraft>().centerOfMass;
            }
        } else {
            // Spacecraft was destroyed along with its parts. Rebuild from grid IDs.
            placedParts = new Dictionary<(int, int), GameObject>();
            (int, int) baseCoords = (gridWidth / 2, gridHeight / 2);

            for (int x = 0; x < gridWidth; x++) {
                for (int y = 0; y < gridHeight; y++) {
                    if ((x, y) == baseCoords) continue;

                    int partID = grid.GetValue((x, y));
                    if (partID <= 0) continue;

                    GameObject prefab = partDB.GetPartGameObject(partID);
                    if (prefab == null) continue;

                    GameObject partObject = Instantiate(prefab, spacecraft.transform);
                    partObject.SetActive(true);
                    partObject.transform.position = GridCoordinatesToUnityPosition(x, y);
                    CacheOriginalSpriteColors(partObject);
                    placedParts[(x, y)] = partObject;
                }
            }
        }
        
        spacecraft.GetComponent<Spacecraft>().SetPartRigidBodies(true, RigidbodyType2D.Kinematic);
    }

    private bool SavedPlacedPartsValid() {
        if (partDB.savedPlacedParts == null || partDB.savedPlacedParts.Count == 0) return false;
        foreach (var kvp in partDB.savedPlacedParts) {
            if (kvp.Value == null) return false;
        }
        return true;
    }

    public void ResetGrid() {
        foreach (var placedPart in placedParts) {
            GameObject partObject = placedPart.Value;
            if (partObject == null || partObject == spacecraft) continue;
            Destroy(partObject);
        }

        placedParts.Clear();
        partStackedOn.Clear();
        originalSpriteColors.Clear();

        for (int x = 0; x < gridWidth; x++) {
            for (int y = 0; y < gridHeight; y++) {
                SetGridCellValue((x, y), -1);
            }
        }

        foreach (Transform part in spacecraft.transform) {
            if(partDB.GetPartID(part.gameObject) != 0) Destroy(part.gameObject);
        }
        CreateSpacecraft();
        DeselectPart();
    }

    private void SetGridCellValue((int, int) coordinates, int value) { 
        grid.SetValue(coordinates.Item1, coordinates.Item2, value);
    }
    
    public int GetGridCellValue((int, int) coordinates) => grid.GetValue(coordinates);

    public void SetGridCellValueByUnityPosition(Vector3 position, int value) {
        (int, int) coordinates = UnityPositionToGridCoordinates(position);
        
        SetGridCellValue(coordinates, value);
    }
    
    public Vector3 GridCoordinatesToUnityPosition(int x, int y) => GridCoordinatesToUnityPosition((x, y));

    public Vector3 GridCoordinatesToUnityPosition((int, int) gridCoords) {
        float x = gridOriginPosition.x + cellSize / 2 + (cellSize * gridCoords.Item1);
        float y = gridOriginPosition.y + cellSize / 2 + (cellSize * gridCoords.Item2);

        return new Vector3(x, y);
    }

    public (int, int) UnityPositionToGridCoordinates(Vector3 unityPosition) {
        int x;
        int y;
        
        grid.GetXY(unityPosition, out x, out y);

        return (x, y);
    }

    private void GameInput_OnDeletePartPerformedAction(object sender, System.EventArgs e) {
        if (!someTileSelected) return;

        DeletePart(selectedTileCoords);
    }

    public void DeletePart((int, int) partCoords) {
        // Find the real part object in this tile
        if (!placedParts.TryGetValue(partCoords, out GameObject partToDelete) || partToDelete == null) return;

        // Don't allow deleting the base/root part (optional safety)
        if (partToDelete == spacecraft) return;
        
        if (partToDelete == selectedPart) DeselectPart();
        
        if (partDB.PartIsStackable(partToDelete)) {
            placedParts[partCoords] = partStackedOn[partToDelete];
            partStackedOn.Remove(partToDelete);
        } 
        else placedParts.Remove(partCoords);
        
        Destroy(partToDelete);

        int newGridValue = placedParts.ContainsKey(partCoords) ? partDB.GetPartID(placedParts[partCoords]) : -1;
        SetGridCellValue(partCoords, newGridValue);
    }

    private void GameInput_OnLeftMouseClickAction(object sender, System.EventArgs e) {
        HandleLeftClick();
    }
    
    public void HandleLeftClick() {
        Vector3 mousePosition = Mouse.GetMouseWorldPosition();

        (int, int) clickCoords;
        grid.GetXY(mousePosition, out clickCoords.Item1, out clickCoords.Item2);

        if (CoordinatesAreOutsideGrid(clickCoords)) {
            DeselectPart();
            return;
        }

        Vector3? snapped = PostionToGridPosition(mousePosition);
        if (snapped == null) {
            DeselectPart();
            return;
        }

        if (highlight == null) {
            highlight = GameObject.Find("Highlight");
            highlightSprite = highlight.GetComponent<SpriteRenderer>();
        }

        highlight.transform.position = snapped.Value;
        highlightSprite.color = colorHighlight;
        if (colorblindMode) highlightSprite.sprite = colorblindHighlight;

        someTileSelected = true;
        selectedTileCoords = clickCoords;

        // select actual object tracked in that tile
        if (placedParts.TryGetValue(selectedTileCoords, out GameObject partInTile)) {
            selectedPart = partInTile;
        } else {
            selectedPart = null;
        }
    }

    private void DeselectPart() {
        highlightSprite.color = colorHighlightInvisible;
        someTileSelected = false;
        selectedPart = null;
    }

    public bool CanPlacePart(GameObject partToBePlaced, (int, int) coords) {
        if (partDB.PartIsStackable(partToBePlaced)) return CanPlaceStackablePart(partToBePlaced, coords);
        
        List<string> possibleConnectionsOfPartToBePlaced = partDB.GetSnapableDirections(partToBePlaced);
        int x = coords.Item1;
        int y = coords.Item2;
        
        if (grid.GetValue(coords) != -1) return false; 

        foreach (string snapableDirection in possibleConnectionsOfPartToBePlaced) {
            switch (snapableDirection) {
                case "above":
                    if (PartCanConnect(grid.GetValue((x, y + 1)), "below")) return true;
                    break;
                case "below":
                    if (PartCanConnect(grid.GetValue((x, y - 1)), "above")) return true;
                    break;
                case "left":
                    if (PartCanConnect(grid.GetValue((x - 1, y)), "right")) return true;
                    break;
                case "right":
                    if (PartCanConnect(grid.GetValue((x + 1, y)), "left"))  return true;
                    break;
                default:
                    continue;
            }
        }
        
        return false;
    }

    private bool CanPlaceStackablePart(GameObject partToBePlaced, (int, int) coords) {
        if (grid.GetValue(coords) == 1) return true;

        foreach (Transform p in spacecraft.transform) {
            GameObject part = p.gameObject;
            if (part.GetComponent<Rigidbody2D>().bodyType == RigidbodyType2D.Dynamic && part != partToBePlaced) {
                return CanPlacePart(part, coords);
            }
        }
        
        return false;
    }

    //Ex: If part is bottomEngine, and the connectingDirection is "above" it can connect
    private bool PartCanConnect(int partID, string connectingDirection) {
        if (partID < 0) return false;
        
        List<string> snapableDirections = partDB.GetSnapableDirections(partID);

        return snapableDirections.Contains(connectingDirection);
    }
    
    public Vector3? PostionToGridPosition(Vector3 originalPosition) {
        (int, int) tileCoords;
        grid.GetXY(originalPosition, out tileCoords.Item1, out tileCoords.Item2);

        if (CoordinatesAreOutsideGrid(tileCoords)) return null;
        
        return GridCoordinatesToUnityPosition(tileCoords);
    }

    public void SetSelectedPart(GameObject part) => selectedPart = part;

    public void PlacePartAtCoordinates(GameObject part, (int, int) coordinates) {
        if (!partDB.PartIsStackable(part) && 
            placedParts.TryGetValue(coordinates, out GameObject existing) && existing != null) {
            
            // Don't allow swapping the base/root part (optional safety)
            if (existing == spacecraft) return;

            Destroy(existing);
            placedParts.Remove(coordinates);
            SetGridCellValue(coordinates, -1);
        }
        
        SetGridCellValue(coordinates, partDB.GetPartID(part));

        // Spawn part
        GameObject spacecraftPart = Instantiate(part, spacecraft.transform);
        spacecraftPart.SetActive(true);
        spacecraftPart.transform.position = GridCoordinatesToUnityPosition(coordinates);
        CacheOriginalSpriteColors(spacecraftPart);

        // Track in dictionary
        if (partDB.PartIsStackable(part)) partStackedOn[spacecraftPart] = placedParts[coordinates];
        placedParts[coordinates] = spacecraftPart;

        // Keep selection synced if placing in selected tile
        if (someTileSelected && selectedTileCoords.Equals(coordinates)){
            selectedPart = spacecraftPart;
        }
        ClearDisconnectedHighlights();
    }

    private bool CoordinatesAreOutsideGrid((int, int) coordinates) {
        if (coordinates.Item1 < 0 || coordinates.Item2 < 0 ||
            coordinates.Item1 >= gridWidth || coordinates.Item2 >= gridHeight) {
            
            return true;
        }

        return false;
    }


    public void RemovePlacedPartAtWorldPosition(Vector3 worldPos) {
        (int, int) coords = UnityPositionToGridCoordinates(worldPos);
        placedParts.Remove(coords);
    }

    public void SetPlacedPartAtWorldPosition(Vector3 worldPos, GameObject partObject) {
        (int, int) coords = UnityPositionToGridCoordinates(worldPos);
        
        if(partDB.PartIsStackable(partObject)) partStackedOn[partObject] = placedParts[coords];
        placedParts[coords] = partObject;
    }

    public GameObject GetPlacedPartByWorldPosition(Vector3 worldPos) {
        (int, int) coords = UnityPositionToGridCoordinates(worldPos);
        
        return placedParts.ContainsKey(coords) ? placedParts[coords] : null;
    }

    public int GetGridCellValueByWorldPosition(Vector3 worldPos) {
        (int, int) coords = UnityPositionToGridCoordinates(worldPos);
        return GetGridCellValue(coords);
    }

    public void RemoveDisconnectedParts() {
        List<GameObject> allParts = placedParts.Values.ToList();
        allParts.AddRange(partStackedOn.Values.ToList());
        
        foreach (GameObject partObject in allParts) {
            (int, int) partCoords = UnityPositionToGridCoordinates(partObject.transform.position);
            if(!PartIsConnected(partCoords)) DeletePart(partCoords);
        }
    }

    public bool PartIsConnected((int, int) coordinates) => PartIsConnectedHelper(coordinates, new HashSet<(int, int)>());

    private bool PartIsConnectedHelper((int, int) coordinates, HashSet<(int, int)> visitedCells) {
        if (!placedParts.ContainsKey(coordinates)) return false;
        
        visitedCells.Add(coordinates);
        
        int partID = GetGridCellValue(coordinates);
        int x = coordinates.Item1;
        int y = coordinates.Item2;
        
        List<string> snapableDirections = partDB.GetSnapableDirections(partID);

        foreach (string dir in snapableDirections) {
            int otherPart;
            switch (dir) {
                case "above":
                    otherPart = GetGridCellValue((x, y + 1));
                    if (otherPart == 0) return true;
                    if (otherPart > 0 && !visitedCells.Contains((x, y + 1))) {
                        if (!PartCanConnect(otherPart, "below")) continue;
                        if (PartIsConnectedHelper((x, y + 1), visitedCells)) return true;
                    }
                    break;
                case "below":
                    otherPart = GetGridCellValue((x, y - 1));
                    if (otherPart == 0) return true;
                    if (otherPart > 0 && !visitedCells.Contains((x, y - 1))) {
                        if (!PartCanConnect(otherPart, "above")) continue;
                        if (PartIsConnectedHelper((x, y - 1), visitedCells)) return true;
                    }
                    break;
                case "left":
                    otherPart = GetGridCellValue((x - 1, y));
                    if (otherPart == 0) return true;
                    if (otherPart > 0 && !visitedCells.Contains((x - 1, y))) {
                        if (!PartCanConnect(otherPart, "right")) continue;
                        if (PartIsConnectedHelper((x - 1, y), visitedCells)) return true;
                    }
                    break;
                case "right":
                    otherPart = GetGridCellValue((x + 1, y));
                    if (otherPart == 0) return true;
                    if (otherPart > 0 && !visitedCells.Contains((x + 1, y))) {
                        if (!PartCanConnect(otherPart, "left")) continue;
                        if (PartIsConnectedHelper((x + 1, y), visitedCells)) return true;
                    }
                    break;
                default:
                    continue;
            }
        }
        return false;
    }

    // Checks every placed part to see if it is connected to the spacecraft core.
    // If a part is not connected, it highlights that part by changing its sprite color.
    // Returns true if any disconnected parts were found.
    public bool HighlightDisconnectedParts() {
        // Track whether we find at least one disconnected part
        bool foundDisconnectedPart = false;

        // First clear any previous highlights so we start fresh
        ClearDisconnectedHighlights();

        // Loop through all parts currently placed on the ship grid
        foreach (var placedPart in placedParts) {
            // Get the grid coordinates of the part
            (int, int) coords = placedPart.Key;

            // Get the actual GameObject for that part
            GameObject partObject = placedPart.Value;

            // Skip if the part object no longer exists
            if (partObject == null) continue;

            // Use the existing connectivity function to check
            // whether this part can reach the spacecraft core
            if (!PartIsConnected(coords)) {
                HighlightDisconnectedPart(partObject);
                if(partStackedOn.TryGetValue(partObject, out GameObject shipPart)) HighlightDisconnectedPart(shipPart);

                // Mark that we found at least one disconnected part
                foundDisconnectedPart = true;
            }
        }

        // Return whether any disconnected parts were found
        return foundDisconnectedPart;
    }

    private void HighlightDisconnectedPart(GameObject part) {
        SpriteRenderer[] spriteRenderers = part.GetComponentsInChildren<SpriteRenderer>();

        // Highlight the disconnected part by setting its color
        foreach (SpriteRenderer sr in spriteRenderers) {
            sr.color = colorDisconnected;
        }
    }

    // Restores all ship parts to their original sprite colors.
    // This is called before we check for disconnected parts so that
    // previously highlighted parts do not stay red after the ship is fixed.
    public void ClearDisconnectedHighlights() {

        List<GameObject> allParts = placedParts.Values.ToList();
        allParts.AddRange(partStackedOn.Values.ToList());

        // Loop through every part currently placed on the ship grid
        foreach (GameObject partObject in allParts) {
            // Skip if the object was destroyed or is missing
            if (partObject == null) continue;

            // Get all sprite renderers on the part and its children
            // (some parts may have multiple sprites or child objects)
            SpriteRenderer[] spriteRenderers = partObject.GetComponentsInChildren<SpriteRenderer>();

            // Restore the original color of each sprite renderer
            foreach (SpriteRenderer sr in spriteRenderers) {
                // If we cached the sprite's original color earlier,
                // restore it instead of leaving the highlight color
                if (sr != null && originalSpriteColors.TryGetValue(sr, out Color originalColor)) {
                    sr.color = originalColor;
                }
            }
        }
    }

    public IEnumerator FadeClearDisconnectedHighlights() {
        float fadeTime = 2f;
        float elapsedTime = 0f;
        List<SpriteRenderer> srList = new List<SpriteRenderer>();

        List<GameObject> allParts = placedParts.Values.ToList();
        allParts.AddRange(partStackedOn.Values.ToList());
                
        foreach (GameObject placedPart in allParts) {
            SpriteRenderer sr = placedPart.GetComponentInChildren<SpriteRenderer>();
            if(sr.color == colorDisconnected) srList.Add(sr);
        }
        
        yield return new WaitForSeconds(0.5f);

        while (fadeTime > elapsedTime) {
            elapsedTime += Time.deltaTime;
            foreach (SpriteRenderer sr in srList) {
                if (sr.gameObject == null) continue; //In case object gets deleted during coroutine
                
                float t = Mathf.SmoothStep(0f, 1f, elapsedTime / fadeTime);
                sr.color = Color.Lerp(colorDisconnected, originalSpriteColors[sr], t);
            }
            yield return null;
        }
    }

    private void CacheOriginalSpriteColors(GameObject partObject) {
        // If the part object does not exist, exit early
        if (partObject == null) return;

        // Get all sprite renderers on the part and any of its children
        // Some parts may have multiple sprites or nested objects
        SpriteRenderer[] spriteRenderers = partObject.GetComponentsInChildren<SpriteRenderer>();

        // Loop through each sprite renderer found
        foreach (SpriteRenderer sr in spriteRenderers) {
            // Only cache the color if:
            // 1. The sprite renderer exists
            // 2. We haven't already stored its original color
            if (sr != null && !originalSpriteColors.ContainsKey(sr)) {
                // Store the sprite renderer and its original color
                // so we can restore it later after removing highlights
                originalSpriteColors[sr] = sr.color;
            }
        }
    }
    
    public void SaveGridState(bool save = true) {
        grid.SaveGridState(save);
        partDB.savedPlacedParts = placedParts;
        partDB.savedPartStackedOn = partStackedOn;
    }
    
    private void OnDisable() => SaveGridState();
    
    private void OnDestroy() {
        gameInput.OnDeletePartPerformedAction -= GameInput_OnDeletePartPerformedAction;
        gameInput.OnLeftMouseClickPerformedAction -= GameInput_OnLeftMouseClickAction;
    }
}
