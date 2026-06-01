using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Creates a side panel in the Build Scene that displays all available parts.
/// Players can drag parts from this panel onto the grid to place them.
/// Auto-created by ShipBuildingGrid
/// </summary>
public class BuildPanelUI : MonoBehaviour {
    private static readonly Color PartButtonColor = new(0.18431373f, 0f, 0.5647059f, 1f);
    private const float PartButtonHeight = 96f;
    private const float PanelWidth = 320f;
    private const float PartIconWidth = 56f;
    private const float PartTextLeftPadding = 76f;

    private void Start() {
        SpacecraftPartDatabase partDB = SpacecraftPartDatabase.Instance;
        if (partDB == null) {
            Debug.LogError("BuildPanelUI: SpacecraftPartDatabase.Instance is null!");
            return;
        }

        PartScriptableObject[] allParts = partDB.GetAllParts();
        if (allParts == null || allParts.Length == 0) {
            Debug.LogError("BuildPanelUI: No parts found in database!");
            return;
        }

        CreatePanel(allParts);
    }

    private void CreatePanel(PartScriptableObject[] allParts) {
        //Canvas
        GameObject canvasGO = new GameObject("BuildPanelCanvas");
        Canvas canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0f;

        canvasGO.AddComponent<GraphicRaycaster>();

        //Panel background, anchored to left side of screen
        GameObject panelGO = new GameObject("SidePanel");
        panelGO.transform.SetParent(canvasGO.transform, false);

        RectTransform panelRect = panelGO.AddComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0, 0);
        panelRect.anchorMax = new Vector2(0, 1);
        panelRect.pivot = new Vector2(0, 0.5f);
        panelRect.sizeDelta = new Vector2(PanelWidth, 0);
        panelRect.anchoredPosition = Vector2.zero;

        Image panelBg = panelGO.AddComponent<Image>();
        panelBg.color = new Color(0.08f, 0.08f, 0.18f, 0.92f);

        //Title
        GameObject titleGO = new GameObject("Title");
        titleGO.transform.SetParent(panelGO.transform, false);

        RectTransform titleRect = titleGO.AddComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0, 1);
        titleRect.anchorMax = new Vector2(1, 1);
        titleRect.pivot = new Vector2(0.5f, 1);
        titleRect.sizeDelta = new Vector2(0, 40);
        titleRect.anchoredPosition = new Vector2(0, -5);

        TextMeshProUGUI titleText = titleGO.AddComponent<TextMeshProUGUI>();
        titleText.text = "PARTS";
        titleText.fontSize = 22;
        titleText.fontStyle = FontStyles.Bold;
        titleText.alignment = TextAlignmentOptions.Center;
        titleText.color = Color.white;

        // Content area
        GameObject contentGO = new GameObject("Content");
        contentGO.transform.SetParent(panelGO.transform, false);

        RectTransform contentRect = contentGO.AddComponent<RectTransform>();
        contentRect.anchorMin = Vector2.zero;
        contentRect.anchorMax = Vector2.one;
        contentRect.offsetMin = new Vector2(8, 8);
        contentRect.offsetMax = new Vector2(-8, -50);

        contentGO.AddComponent<RectMask2D>();

        VerticalLayoutGroup layout = contentGO.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 6;
        layout.padding = new RectOffset(0, 0, 0, 0);
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlHeight = false;
        layout.childControlWidth = true;
        layout.childForceExpandHeight = false;
        layout.childForceExpandWidth = true;

        //Create one item per placeable part
        int count = 0;
        foreach (PartScriptableObject part in allParts) {
            if (part == null) continue;
            if (part.partID == 0) continue;
            if (part.part == null) continue;
            CreatePartItem(contentGO.transform, part);
            count++;
        }

        Debug.Log($"BuildPanelUI: Created panel with {count} parts");
    }

    private void CreatePartItem(Transform parent, PartScriptableObject partSO) {
        GameObject itemGO = new GameObject(partSO.part.name + "_Item");
        itemGO.transform.SetParent(parent, false);

        RectTransform itemRect = itemGO.AddComponent<RectTransform>();
        itemRect.sizeDelta = new Vector2(0, PartButtonHeight);

        Image itemBg = itemGO.AddComponent<Image>();
        itemBg.color = PartButtonColor;
        itemBg.raycastTarget = true;

        // LayoutElement to ensure the layout knows our height
        LayoutElement le = itemGO.AddComponent<LayoutElement>();
        le.minHeight = PartButtonHeight;
        le.preferredHeight = PartButtonHeight;

        // Attach drag handler
        PanelPartDrag drag = itemGO.AddComponent<PanelPartDrag>();
        drag.Initialize(partSO);

        // Part color swatch
        GameObject swatchGO = new GameObject("Swatch");
        swatchGO.transform.SetParent(itemGO.transform, false);

        RectTransform swatchRect = swatchGO.AddComponent<RectTransform>();
        swatchRect.anchorMin = new Vector2(0, 0.1f);
        swatchRect.anchorMax = new Vector2(0, 0.9f);
        swatchRect.pivot = new Vector2(0, 0.5f);
        swatchRect.anchoredPosition = new Vector2(8, 0);
        swatchRect.sizeDelta = new Vector2(PartIconWidth, 0);

        Image swatchImg = swatchGO.AddComponent<Image>();
        swatchImg.raycastTarget = false;

        // Prefer the SO-defined icon. Fall back to the prefab's SpriteRenderer.
        SpriteRenderer partVisual = partSO.part.GetComponentInChildren<SpriteRenderer>();
        Color partColor = partVisual != null ? partVisual.color : Color.white;
        Sprite partSprite = partSO.icon != null
            ? partSO.icon
            : (partVisual != null ? partVisual.sprite : null);
        if (partSprite != null) {
            swatchImg.sprite = partSprite;
            swatchImg.color = partSO.icon != null ? Color.white : partColor;
            swatchImg.preserveAspect = true;
            swatchImg.type = Image.Type.Simple;
        } else {
            swatchImg.color = partColor;
        }

        //Part name
        GameObject nameGO = new GameObject("Name");
        nameGO.transform.SetParent(itemGO.transform, false);

        RectTransform nameRect = nameGO.AddComponent<RectTransform>();
        nameRect.anchorMin = new Vector2(0, 0);
        nameRect.anchorMax = new Vector2(1, 1);
        nameRect.offsetMin = new Vector2(PartTextLeftPadding, 8);
        nameRect.offsetMax = new Vector2(-10, -8);

        TextMeshProUGUI nameText = nameGO.AddComponent<TextMeshProUGUI>();
        nameText.text = FormatPartName(partSO.part.name);
        nameText.fontSize = 16;
        nameText.alignment = TextAlignmentOptions.MidlineLeft;
        nameText.color = Color.white;
        nameText.textWrappingMode = TextWrappingModes.Normal;
        nameText.raycastTarget = false;
    }

    private string FormatPartName(string rawName) {
        if (rawName.EndsWith("Part"))
            rawName = rawName.Substring(0, rawName.Length - 4);

        // Insert spaces before uppercase letters: "SolarPanel" -> "Solar Panel"
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < rawName.Length; i++) {
            if (i > 0 && char.IsUpper(rawName[i]) && !char.IsUpper(rawName[i - 1]))
                sb.Append(' ');
            sb.Append(rawName[i]);
        }
        return sb.ToString();
    }
}
