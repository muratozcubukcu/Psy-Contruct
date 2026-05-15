using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
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

    public enum direction { above, left, below, right, none }
    
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
    private Dictionary<SpriteRenderer, Color> originalSpriteColors = new();

    private GameObject selectedPart;
    private (int, int) selectedTileCoords;
    private (int, int) shipStartPos;
    public Dictionary<(int, int), GameObject> placedParts = new();
    public Dictionary<GameObject, GameObject> partStackedOn = new(); //Key: stacked part, value: ship part
    public Dictionary<(int, int), direction> partRotations = new(); //Only use with rotatable parts
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
        shipStartPos = (gridWidth / 2, gridHeight / 2);
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
        gameInput.OnRotatePartPerformedAction += GameInput_OnRotatePartPerformedAction;
        
        gridVisualizer.VisualizeGrid(gridWidth, gridHeight, cellSize, gridOriginPosition);
        
        spacecraft = Spacecraft.GetInstance().gameObject;
        partDB = SpacecraftPartDatabase.Instance;
        
        colorblindMode = Settings.Instance.colorblindMode;
        colorDisconnected = colorblindMode ? Color.black : new Color(1f, 0.4f, 0.4f, 1f);
        
        if(partDB.hasSavedGridState) LoadSpacecraft();
        else CreateSpacecraft();
    }
    
    private void CreateSpacecraft() {
        spacecraft.transform.position = GridCoordinatesToUnityPosition(shipStartPos);
        
        int baseID = partDB.GetPartID(partDB.GetPartGameObject(0));

        SetGridCellValue(shipStartPos, baseID);
    }

    private void LoadSpacecraft() {
        grid.LoadGridState();

        Rigidbody2D spacecraftRB = spacecraft.GetComponent<Rigidbody2D>();
        spacecraftRB.linearVelocity = Vector2.zero;
        spacecraftRB.angularVelocity = 0f;

        Transform shipTransform = spacecraft.transform;
        shipTransform.rotation = Quaternion.Euler(0, 0, 0);
        shipTransform.position = GridCoordinatesToUnityPosition(shipStartPos);

        FindAnyObjectByType<DragHintAnimator>().StopHint();

        if (SavedPlacedPartsValid()) {
            placedParts = partDB.savedPlacedParts;
            partStackedOn = partDB.savedPartStackedOn;
            originalSpriteColors = partDB.savedOriginalSpriteColors;
            foreach (Transform part in shipTransform) {
                part.position += spacecraft.GetComponent<Spacecraft>().centerOfMass;
            }
        } else {
            Vector3 com = spacecraft.GetComponent<Spacecraft>().centerOfMass;
            foreach (Transform part in shipTransform) {
                part.position += com;
            }

            placedParts = new Dictionary<(int, int), GameObject>();
            for (int x = 0; x < gridWidth; x++) {
                for (int y = 0; y < gridHeight; y++) {
                    if ((x, y) == shipStartPos) continue;
                    int partID = grid.GetValue((x, y));
                    if (partID <= 0) continue;

                    if (partDB.PartIsRotatable(partID)) {
                        PlacePartAtCoordinates(partDB.GetPartGameObject(partID), (x, y), partRotations[(x, y)]);
                        continue;
                    }
                    if (partDB.PartIsStackable(partID)) PlacePartAtCoordinates(partDB.GetPartGameObject(1), (x, y));
                    PlacePartAtCoordinates(partDB.GetPartGameObject(partID), (x, y));
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
        if (someTileSelected) DeletePart(selectedTileCoords);
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

    private void GameInput_OnRotatePartPerformedAction(object sender, System.EventArgs e) {
        if (!someTileSelected) return;

        if (selectedPart.TryGetComponent(out RotatablePart rotatable)) rotatable.RotatePart();
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
        if (coords == shipStartPos) return false;
        if (partDB.PartIsStackable(partToBePlaced)) return CanPlaceStackablePart(partToBePlaced, coords);
        
        if (partDB.PartIsRotatable(partToBePlaced)) {
            if (TryFindRotatableConnectingDirection(partToBePlaced, coords, out direction dir)) {
                partToBePlaced.GetComponent<RotatablePart>().SetRotation(dir);
                return true;
            }
            return false;
        }

        for (int i = 0; i < 4; i++) {
            if (PartConnectsInDirection(coords, (direction)i)) return true;
        }
        return false;
    }

    private bool CanPlaceStackablePart(GameObject partToBePlaced, (int, int) coords) {
        if (grid.GetValue(coords) == 1) return true;

        //Checks if the stackable part is being dragged with another part by seeing if any other part has a dynamic rb
        //(Parts only ever have dynamic rb's if they are being dragged)
        foreach (Transform p in spacecraft.transform) {
            GameObject part = p.gameObject;
            if (part.GetComponent<Rigidbody2D>().bodyType == RigidbodyType2D.Dynamic && part != partToBePlaced) {
                return CanPlacePart(part, coords);
            }
        }
        
        return false;
    }
    

    public bool TryFindRotatableConnectingDirection(GameObject partToBePlaced, (int, int) partCoords, out direction dir) {
        direction currDir = partToBePlaced.GetComponent<RotatablePart>().connectingDirection;
        
        for (int i = 0; i < 4; i++) {
            if (PartConnectsInDirection(partCoords, currDir)) {
                dir = currDir;
                return true;
            }
            
            currDir = (currDir == direction.right) ? direction.above : currDir + 1;
        }

        dir = direction.none;
        return false;
    }

    private bool PartConnectsInDirection((int, int) partCoords, direction dir) {
        int x = partCoords.Item1;
        int y = partCoords.Item2;
        GameObject connectingPart;
        
        switch (dir) {
            case direction.above:
                if (shipStartPos == (x, y + 1)) return true;
                if (!placedParts.TryGetValue((x, y + 1), out connectingPart)) break;
                if (PartCanConnect(connectingPart, direction.below)) return true;
                break;
            case direction.below:
                if (shipStartPos == (x, y - 1)) return true;
                if (!placedParts.TryGetValue((x, y - 1), out connectingPart)) break;
                if (PartCanConnect(connectingPart, direction.above)) return true;
                break;
            case direction.left:
                if (shipStartPos == (x - 1, y)) return true;
                if (!placedParts.TryGetValue((x - 1, y), out connectingPart)) break;
                if (PartCanConnect(connectingPart, direction.right)) return true;
                break;
            case direction.right:
                if (shipStartPos == (x + 1, y)) return true;
                if (!placedParts.TryGetValue((x + 1, y), out connectingPart)) break;
                if (PartCanConnect(connectingPart, direction.left)) return true;
                break;
        }
        
        return false;
    }
    
    public Vector3? PostionToGridPosition(Vector3 originalPosition) {
        (int, int) tileCoords;
        grid.GetXY(originalPosition, out tileCoords.Item1, out tileCoords.Item2);

        if (CoordinatesAreOutsideGrid(tileCoords)) return null;
        
        return GridCoordinatesToUnityPosition(tileCoords);
    }

    public void SetSelectedPart(GameObject part) => selectedPart = part;

    public void PlacePartAtCoordinates(GameObject part, (int, int) coordinates, direction dir = direction.none) {
        if (!partDB.PartIsStackable(part) && 
            placedParts.TryGetValue(coordinates, out GameObject existing) && existing != null) {
            
            DeletePart(coordinates);
            if(placedParts.ContainsKey(coordinates)) DeletePart(coordinates); //In case we just deleted a stackable part
        }
        
        SetGridCellValue(coordinates, partDB.GetPartID(part));

        // Spawn part
        GameObject spacecraftPart = Instantiate(part, spacecraft.transform);
        spacecraftPart.SetActive(true);
        spacecraftPart.GetComponent<Rigidbody2D>().freezeRotation = true;
        spacecraftPart.transform.position = GridCoordinatesToUnityPosition(coordinates);
        CacheOriginalSpriteColors(spacecraftPart);
        if(partDB.PartIsRotatable(spacecraftPart)) {
            spacecraftPart.GetComponent<RotatablePart>().SetRotation(dir);
            partRotations[coordinates] = dir;
        }

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

        GameObject part = placedParts[coordinates];
        int x = coordinates.Item1;
        int y = coordinates.Item2;
        
        direction[] snapableDirections = partDB.PartIsRotatable(part) ? 
            new [] { part.GetComponent<RotatablePart>().connectingDirection } : 
            new [] { direction.above, direction.left, direction.below, direction.right };

        foreach (direction dir in snapableDirections) {
            GameObject otherPart;
            switch (dir) {
                case direction.above:
                    if (shipStartPos == (x, y + 1)) return true;
                    if (!placedParts.TryGetValue((x, y + 1), out otherPart)) continue;
                    if (!visitedCells.Contains((x, y + 1))) {
                        if (!PartCanConnect(otherPart, direction.below)) continue;
                        if (PartIsConnectedHelper((x, y + 1), visitedCells)) return true;
                    }
                    break;
                case direction.below:
                    if (shipStartPos == (x, y - 1)) return true;
                    if (!placedParts.TryGetValue((x, y - 1), out otherPart)) continue;
                    if (!visitedCells.Contains((x, y - 1))) {
                        if (!PartCanConnect(otherPart, direction.above)) continue;
                        if (PartIsConnectedHelper((x, y - 1), visitedCells)) return true;
                    }
                    break;
                case direction.left:
                    if (shipStartPos == (x - 1, y)) return true;
                    if (!placedParts.TryGetValue((x - 1, y), out otherPart)) continue;
                    if (!visitedCells.Contains((x - 1, y))) {
                        if (!PartCanConnect(otherPart, direction.right)) continue;
                        if (PartIsConnectedHelper((x - 1, y), visitedCells)) return true;
                    }
                    break;
                case direction.right:
                    if (shipStartPos == (x + 1, y)) return true;
                    if (!placedParts.TryGetValue((x + 1, y), out otherPart)) continue;
                    if (!visitedCells.Contains((x + 1, y))) {
                        if (!PartCanConnect(otherPart, direction.left)) continue;
                        if (PartIsConnectedHelper((x + 1, y), visitedCells)) return true;
                    }
                    break;
                default:
                    continue;
            }
        }
        return false;
    }
    
    private bool PartCanConnect(GameObject part, direction connectingDirection) {
        if (partDB.GetPartID(part) < 0) return false;
        if (partDB.GetPartID(part) <= 1 || partDB.PartIsStackable(part)) return true;

        return connectingDirection == part.GetComponent<RotatablePart>().connectingDirection;
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
        if (partObject == null) return;

        foreach (SpriteRenderer sr in partObject.GetComponentsInChildren<SpriteRenderer>()) {
            originalSpriteColors[sr] = sr.color;
        }
    }
    
    public void SaveGridState(bool save = true) {
        grid.SaveGridState(save);
        partDB.savedPlacedParts = placedParts;
        partDB.savedPartStackedOn = partStackedOn;
        partDB.savedPartRotations = partRotations;
        partDB.savedOriginalSpriteColors = originalSpriteColors;
    }
    
    private void OnDisable() => SaveGridState();
    
    private void OnDestroy() {
        gameInput.OnDeletePartPerformedAction -= GameInput_OnDeletePartPerformedAction;
        gameInput.OnLeftMouseClickPerformedAction -= GameInput_OnLeftMouseClickAction;
    }
}
