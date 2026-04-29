using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Self-building minimap. Tracks the spacecraft, every PlanetGravitySource, and the
/// slingshot planner's path. Ship at center, world-up orientation, off-map bodies clamp
/// to the edge.
/// </summary>
public class MinimapUI : MonoBehaviour {

    public enum Corner { TopRight, TopLeft, BottomRight, BottomLeft }

    [Header("Layout")]
    [SerializeField] private float diameterPx = 220f;
    [SerializeField] private Vector2 screenPadding = new Vector2(20f, 20f);
    [SerializeField] private Corner anchorCorner = Corner.TopRight;

    [Header("Scale")]
    [SerializeField] private float worldUnitsAcross = 200f;
    [SerializeField] private bool clampOffMapToEdge = true;

    [Header("Style")]
    [SerializeField] private Color backgroundColor = new Color(0f, 0f, 0f, 0.55f);
    [SerializeField] private Color borderColor = new Color(1f, 1f, 1f, 0.5f);
    [SerializeField] private Color gravityRingColor = new Color(0.5f, 0.7f, 1f, 0.25f);
    [SerializeField] private float borderThickness = 2f;
    [SerializeField] private float shipDotPx = 10f;
    [SerializeField] private float planetDotPx = 14f;

    [Header("Body Colors")]
    [SerializeField] private Color marsColor = new Color(1f, 0.45f, 0.2f, 1f);
    [SerializeField] private Color earthColor = new Color(0.4f, 0.7f, 1f, 1f);
    [SerializeField] private Color sunColor = new Color(1f, 0.95f, 0.5f, 1f);
    [SerializeField] private Color psycheColor = new Color(0.85f, 0.65f, 1f, 1f);
    [SerializeField] private Color shipColor = Color.white;
    [SerializeField] private Color defaultBodyColor = new Color(0.7f, 0.7f, 0.7f, 1f);

    [Header("Optional")]
    [SerializeField] private MarsSlingshotPlanner planner;
    [SerializeField] private int plannerPathDots = 24;
    [SerializeField] private Color plannerPathColor = new Color(1f, 0.85f, 0.2f, 0.8f);

    private Canvas canvas;
    private RectTransform mapRect;
    private RectTransform shipDot;
    private readonly List<TrackedBody> bodies = new();
    private readonly List<RectTransform> plannerDots = new();

    private Sprite circleSprite;
    private Sprite ringSprite;

    private struct TrackedBody {
        public Transform world;
        public RectTransform dot;
        public RectTransform gravityRing;
        public PlanetGravitySource gravity;
    }

    private void Awake() {
        circleSprite = CreateCircleSprite(64);
        ringSprite = CreateRingSprite(128, 0.88f);
        BuildCanvas();
        BuildMap();
    }

    private void Start() {
        RegisterTrackedBodies();
        BuildPlannerDots();
        if (planner == null) planner = FindFirstObjectByType<MarsSlingshotPlanner>();
    }

    private void LateUpdate() {
        Spacecraft ship = Spacecraft.GetInstance();
        if (ship == null) {
            if (canvas != null) canvas.enabled = false;
            return;
        }
        if (canvas != null) canvas.enabled = true;

        Vector2 shipWorld = ship.transform.position;
        float pxPerUnit = diameterPx / Mathf.Max(0.001f, worldUnitsAcross);
        float radiusPx = diameterPx * 0.5f;

        for (int i = 0; i < bodies.Count; i++) {
            TrackedBody b = bodies[i];
            if (b.world == null) continue;

            Vector2 delta = ((Vector2)b.world.position - shipWorld) * pxPerUnit;
            bool offMap = delta.magnitude > radiusPx;
            if (offMap) {
                if (clampOffMapToEdge) {
                    delta = delta.normalized * radiusPx;
                    b.dot.gameObject.SetActive(true);
                } else {
                    b.dot.gameObject.SetActive(false);
                }
                if (b.gravityRing != null) b.gravityRing.gameObject.SetActive(false);
            } else {
                b.dot.gameObject.SetActive(true);
                if (b.gravityRing != null) {
                    float ringPx = b.gravity.GetGravityRadius() * pxPerUnit * 2f;
                    b.gravityRing.gameObject.SetActive(true);
                    b.gravityRing.sizeDelta = new Vector2(ringPx, ringPx);
                    b.gravityRing.anchoredPosition = delta;
                }
            }
            b.dot.anchoredPosition = delta;
        }

        if (planner != null && plannerDots.Count > 0) {
            Vector3[] pts = planner.GetPathPoints();
            bool show = pts != null && pts.Length >= 2;
            for (int i = 0; i < plannerDots.Count; i++) {
                if (!show) {
                    plannerDots[i].gameObject.SetActive(false);
                    continue;
                }
                float t = (float)i / (plannerDots.Count - 1);
                int idx = Mathf.RoundToInt(t * (pts.Length - 1));
                Vector2 worldPt = pts[idx];
                Vector2 delta = (worldPt - shipWorld) * pxPerUnit;
                if (delta.magnitude > radiusPx) {
                    plannerDots[i].gameObject.SetActive(false);
                } else {
                    plannerDots[i].gameObject.SetActive(true);
                    plannerDots[i].anchoredPosition = delta;
                }
            }
        }

        shipDot.anchoredPosition = Vector2.zero;
        shipDot.localRotation = Quaternion.Euler(0f, 0f, ship.transform.eulerAngles.z);
    }

    private void BuildCanvas() {
        canvas = GetComponentInParent<Canvas>();
        if (canvas == null) {
            GameObject canvasGO = new GameObject("MinimapCanvas",
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGO.transform.SetParent(transform, false);
            canvas = canvasGO.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 200;
            CanvasScaler scaler = canvasGO.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
        }
    }

    private void BuildMap() {
        Vector2 anchor = AnchorForCorner(anchorCorner);
        Vector2 pos = new Vector2(
            (anchor.x < 0.5f ?  1f : -1f) * screenPadding.x,
            (anchor.y < 0.5f ?  1f : -1f) * screenPadding.y);

        GameObject bgGO = new GameObject("MinimapBackground", typeof(RectTransform), typeof(Image));
        bgGO.transform.SetParent(canvas.transform, false);
        mapRect = (RectTransform)bgGO.transform;
        mapRect.anchorMin = mapRect.anchorMax = mapRect.pivot = anchor;
        mapRect.sizeDelta = new Vector2(diameterPx, diameterPx);
        mapRect.anchoredPosition = pos;
        Image bgImg = bgGO.GetComponent<Image>();
        bgImg.sprite = circleSprite;
        bgImg.color = backgroundColor;
        bgImg.raycastTarget = false;

        GameObject borderGO = new GameObject("Border", typeof(RectTransform), typeof(Image));
        borderGO.transform.SetParent(mapRect, false);
        RectTransform borderRect = (RectTransform)borderGO.transform;
        borderRect.anchorMin = borderRect.anchorMax = new Vector2(0.5f, 0.5f);
        borderRect.pivot = new Vector2(0.5f, 0.5f);
        borderRect.sizeDelta = new Vector2(diameterPx, diameterPx);
        borderRect.anchoredPosition = Vector2.zero;
        Image borderImg = borderGO.GetComponent<Image>();
        borderImg.sprite = CreateRingSprite(128, Mathf.Clamp01(1f - borderThickness * 2f / diameterPx));
        borderImg.color = borderColor;
        borderImg.raycastTarget = false;

        bgGO.AddComponent<Mask>().showMaskGraphic = true;

        shipDot = MakeDot("ShipDot", shipColor, shipDotPx);
        shipDot.SetParent(mapRect, false);
    }

    private RectTransform MakeDot(string objName, Color color, float sizePx) {
        GameObject go = new GameObject(objName, typeof(RectTransform), typeof(Image));
        RectTransform rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(sizePx, sizePx);
        Image img = go.GetComponent<Image>();
        img.sprite = circleSprite;
        img.color = color;
        img.raycastTarget = false;
        return rt;
    }

    private RectTransform MakeRing(string objName, Color color) {
        GameObject go = new GameObject(objName, typeof(RectTransform), typeof(Image));
        RectTransform rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = Vector2.zero;
        Image img = go.GetComponent<Image>();
        img.sprite = ringSprite;
        img.color = color;
        img.raycastTarget = false;
        return rt;
    }

    private void RegisterTrackedBodies() {
        var sources = FindObjectsByType<PlanetGravitySource>(FindObjectsSortMode.None);
        for (int i = 0; i < sources.Length; i++) {
            PlanetGravitySource src = sources[i];
            Transform body = src.transform.parent != null ? src.transform.parent : src.transform;
            Color c = ColorForBody(body.name);

            RectTransform ring = MakeRing("Ring_" + body.name, gravityRingColor);
            ring.SetParent(mapRect, false);

            RectTransform dot = MakeDot("Dot_" + body.name, c, planetDotPx);
            dot.SetParent(mapRect, false);

            bodies.Add(new TrackedBody {
                world = body,
                dot = dot,
                gravityRing = ring,
                gravity = src,
            });
        }

        if (PsycheAsteroid.Instance != null) {
            bool already = false;
            for (int i = 0; i < bodies.Count; i++) {
                if (bodies[i].world == PsycheAsteroid.Instance.transform) { already = true; break; }
            }
            if (!already) {
                RectTransform dot = MakeDot("Dot_Psyche", psycheColor, planetDotPx);
                dot.SetParent(mapRect, false);
                bodies.Add(new TrackedBody {
                    world = PsycheAsteroid.Instance.transform,
                    dot = dot,
                    gravityRing = null,
                    gravity = null,
                });
            }
        }
    }

    private void BuildPlannerDots() {
        if (plannerPathDots <= 0) return;
        for (int i = 0; i < plannerPathDots; i++) {
            RectTransform d = MakeDot("PlannerDot_" + i, plannerPathColor, 4f);
            d.SetParent(mapRect, false);
            plannerDots.Add(d);
        }
    }

    private Color ColorForBody(string name) {
        if (string.IsNullOrEmpty(name)) return defaultBodyColor;
        string n = name.ToLowerInvariant();
        if (n.Contains("mars")) return marsColor;
        if (n.Contains("earth")) return earthColor;
        if (n.Contains("sun")) return sunColor;
        if (n.Contains("psyche")) return psycheColor;
        return defaultBodyColor;
    }

    private static Vector2 AnchorForCorner(Corner c) {
        switch (c) {
            case Corner.TopLeft:     return new Vector2(0f, 1f);
            case Corner.TopRight:    return new Vector2(1f, 1f);
            case Corner.BottomLeft:  return new Vector2(0f, 0f);
            case Corner.BottomRight: return new Vector2(1f, 0f);
            default: return new Vector2(1f, 1f);
        }
    }

    private static Sprite CreateCircleSprite(int size) {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;
        Color32[] pixels = new Color32[size * size];
        float r = size * 0.5f;
        Vector2 c = new Vector2(r, r);
        for (int y = 0; y < size; y++) {
            for (int x = 0; x < size; x++) {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), c);
                float a = Mathf.Clamp01(r - d);
                byte alpha = (byte)Mathf.RoundToInt(a * 255f);
                pixels[y * size + x] = new Color32(255, 255, 255, alpha);
            }
        }
        tex.SetPixels32(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
    }

    private static Sprite CreateRingSprite(int size, float innerRatio) {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;
        Color32[] pixels = new Color32[size * size];
        float r = size * 0.5f;
        float ri = r * innerRatio;
        Vector2 c = new Vector2(r, r);
        for (int y = 0; y < size; y++) {
            for (int x = 0; x < size; x++) {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), c);
                float aOuter = Mathf.Clamp01(r - d);
                float aInner = Mathf.Clamp01(d - ri);
                byte alpha = (byte)Mathf.RoundToInt(Mathf.Min(aOuter, aInner) * 255f);
                pixels[y * size + x] = new Color32(255, 255, 255, alpha);
            }
        }
        tex.SetPixels32(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
    }
}
