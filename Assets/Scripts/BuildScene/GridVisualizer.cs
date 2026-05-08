using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// A container class for Grid that translates it into sprites so that it is visible without gizmos
/// </summary>
public class GridVisualizer : MonoBehaviour {
    
    [SerializeField] private Color gridColor = Color.white;
    [SerializeField] private float lineWidth = 0.05f;
    
    private List<GameObject> gridLines = new List<GameObject>();
    private Sprite lineSprite;
    
    private void Awake() {
        CreateLineSprite();
    }
    
    /// <summary>
    /// creates the sprite that is stretched to create the lines of the grid
    /// </summary>
    private void CreateLineSprite() {
        Texture2D texture = new Texture2D(1, 1);
        texture.SetPixel(0, 0, Color.white);
        texture.Apply();
        lineSprite = Sprite.Create(texture, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
    }
    
    /// <summary>
    /// draws the grid as a set of stretched sprites
    /// </summary>
    /// <param name="grid">The grid to be visualized
    /// (note: this parameter should be removed and the grid should be set at construction)</param>
    /// <param name="width">the width of the grid in tiles</param>
    /// <param name="height">the height of the grid in tiles</param>
    /// <param name="cellSize">the width and height of the tiles in game units</param>
    /// <param name="originPosition">the lower left corner of the grid</param>
    public void VisualizeGrid(int width, int height, float cellSize, Vector3 originPosition) {
        
        //erase previous drawings of the grid
        foreach (var line in gridLines) {
            if (line != null) Destroy(line);
        }
        gridLines.Clear();
        
        //draw vertical lines
        for (int x = 0; x <= width; x++) {
            GameObject lineObj = new GameObject("GridLine_V_" + x);
            lineObj.transform.SetParent(transform);
            SpriteRenderer sr = lineObj.AddComponent<SpriteRenderer>();
            sr.sprite = lineSprite;
            sr.color = gridColor;
            sr.sortingOrder = 0;
            
            float xPos = originPosition.x + (x * cellSize);
            float yPos = originPosition.y + (height * cellSize / 2f);
            lineObj.transform.position = new Vector3(xPos, yPos, 0);
            lineObj.transform.localScale = new Vector3(lineWidth, height * cellSize, 1f);
            
            gridLines.Add(lineObj);
        }
        
        //draw horizontal lines
        for (int y = 0; y <= height; y++) {
            GameObject lineObj = new GameObject("GridLine_H_" + y);
            lineObj.transform.SetParent(transform);
            SpriteRenderer sr = lineObj.AddComponent<SpriteRenderer>();
            sr.sprite = lineSprite;
            sr.color = gridColor;
            sr.sortingOrder = 0;
            
            float xPos = originPosition.x + (width * cellSize / 2f);
            float yPos = originPosition.y + (y * cellSize);
            lineObj.transform.position = new Vector3(xPos, yPos, 0);
            lineObj.transform.localScale = new Vector3(width * cellSize, lineWidth, 1f);
            
            gridLines.Add(lineObj);
        }
    }
}

