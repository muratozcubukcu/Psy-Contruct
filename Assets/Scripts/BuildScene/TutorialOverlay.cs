using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// All tutorial UI is created at runtime — no prefabs or scene objects needed.
// Tutorial.cs calls Create() once in Start(), then calls ShowStep() / Hide().
public class TutorialOverlay : MonoBehaviour {

    private Tutorial manager;

    private RectTransform darkTop, darkBottom, darkLeft, darkRight;

    private CanvasGroup borderGroup;
    private RectTransform borderTop, borderBottom, borderLeft, borderRight;

    private Coroutine pulseCoroutine;

    private static readonly Color DarkColor   = new Color(0f,   0f,   0f,   0.78f);
    private static readonly Color BorderColor = new Color(0.7f, 0.85f, 1f,  1f);

    public static TutorialOverlay Create(Tutorial mgr) {
        GameObject go = new GameObject("TutorialOverlay");
        TutorialOverlay ov = go.AddComponent<TutorialOverlay>();
        ov.manager = mgr;
        ov.BuildUI();
        return ov;
    }

    private void BuildUI() {
        Canvas canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 200;

        CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        gameObject.AddComponent<GraphicRaycaster>();

        darkTop    = MakeDarkPanel("Dark_Top");
        darkBottom = MakeDarkPanel("Dark_Bottom");
        darkLeft   = MakeDarkPanel("Dark_Left");
        darkRight  = MakeDarkPanel("Dark_Right");

        GameObject borderRoot = new GameObject("SpotlightBorder");
        borderRoot.transform.SetParent(transform, false);
        borderGroup = borderRoot.AddComponent<CanvasGroup>();
        borderTop    = MakeBorderStrip(borderRoot.transform, "BT");
        borderBottom = MakeBorderStrip(borderRoot.transform, "BB");
        borderLeft   = MakeBorderStrip(borderRoot.transform, "BL");
        borderRight  = MakeBorderStrip(borderRoot.transform, "BR");

        SetAllInactive();
    }

    private RectTransform MakeDarkPanel(string name) {
        GameObject go = new GameObject(name);
        go.transform.SetParent(transform, false);
        Image img = go.AddComponent<Image>();
        img.color = DarkColor;
        img.raycastTarget = false;
        return go.GetComponent<RectTransform>();
    }

    private RectTransform MakeBorderStrip(Transform parent, string name) {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        Image img = go.AddComponent<Image>();
        img.color = BorderColor;
        img.raycastTarget = false;
        return go.GetComponent<RectTransform>();
    }

    public void ShowStep(RectTransform[] targets) {
        SetAllInactive();

        if (targets != null && targets.Length > 0) {
            Canvas.ForceUpdateCanvases();

            float sL = 1f, sB = 1f, sR = 0f, sT = 0f;
            Vector3[] corners = new Vector3[4];
            foreach (RectTransform t in targets) {
                if (t == null) continue;
                t.GetWorldCorners(corners);
                float l  = corners[0].x / Screen.width;
                float b  = corners[0].y / Screen.height;
                float r  = corners[2].x / Screen.width;
                float tp = corners[2].y / Screen.height;
                if (l  < sL) sL = l;
                if (b  < sB) sB = b;
                if (r  > sR) sR = r;
                if (tp > sT) sT = tp;
            }

            const float PadX = 6f / 1920f;
            const float PadY = 6f / 1080f;
            sL = Mathf.Max(0f, sL - PadX);
            sB = Mathf.Max(0f, sB - PadY);
            sR = Mathf.Min(1f, sR + PadX);
            sT = Mathf.Min(1f, sT + PadY);

            const float SidebarThreshold = 0.20f;
            if (sR >= SidebarThreshold) {
                SetPanel(darkTop,    0f, 1f, sT, 1f);
                SetPanel(darkBottom, 0f, 1f, 0f, sB);
                SetPanel(darkLeft,   0f, sL, sB, sT);
                SetPanel(darkRight,  sR, 1f, sB, sT);
                darkTop.gameObject.SetActive(true);
                darkBottom.gameObject.SetActive(true);
                darkLeft.gameObject.SetActive(true);
                darkRight.gameObject.SetActive(true);
            }

            const float BX = 12f / 1920f;
            const float BY = 12f / 1080f;
            float insetY = Mathf.Min(BY * 0.5f, Mathf.Max(0f, (sT - sB) * 0.5f));

            borderTop.anchorMin    = new Vector2(sL, sT);
            borderTop.anchorMax    = new Vector2(sR, sT);
            borderTop.offsetMin    = new Vector2(0,  -BY * 1080f * 0.5f);
            borderTop.offsetMax    = new Vector2(0,   BY * 1080f * 0.5f);
            borderBottom.anchorMin = new Vector2(sL, sB);
            borderBottom.anchorMax = new Vector2(sR, sB);
            borderBottom.offsetMin = new Vector2(0,  -BY * 1080f * 0.5f);
            borderBottom.offsetMax = new Vector2(0,   BY * 1080f * 0.5f);
            borderLeft.anchorMin   = new Vector2(sL, sB + insetY);
            borderLeft.anchorMax   = new Vector2(sL, sT - insetY);
            borderLeft.offsetMin   = new Vector2(-BX * 1920f * 0.5f, 0);
            borderLeft.offsetMax   = new Vector2( BX * 1920f * 0.5f, 0);
            borderRight.anchorMin  = new Vector2(sR, sB + insetY);
            borderRight.anchorMax  = new Vector2(sR, sT - insetY);
            borderRight.offsetMin  = new Vector2(-BX * 1920f * 0.5f, 0);
            borderRight.offsetMax  = new Vector2( BX * 1920f * 0.5f, 0);
            borderTop.gameObject.SetActive(true);
            borderBottom.gameObject.SetActive(true);
            borderLeft.gameObject.SetActive(true);
            borderRight.gameObject.SetActive(true);

        }

        if (pulseCoroutine != null) StopCoroutine(pulseCoroutine);
        pulseCoroutine = StartCoroutine(PulseBorder());
    }

    public void Hide() {
        if (pulseCoroutine != null) { StopCoroutine(pulseCoroutine); pulseCoroutine = null; }
        gameObject.SetActive(false);
    }

    private void SetAllInactive() {
        darkTop?.gameObject.SetActive(false);
        darkBottom?.gameObject.SetActive(false);
        darkLeft?.gameObject.SetActive(false);
        darkRight?.gameObject.SetActive(false);
        borderTop?.gameObject.SetActive(false);
        borderBottom?.gameObject.SetActive(false);
        borderLeft?.gameObject.SetActive(false);
        borderRight?.gameObject.SetActive(false);
        if (pulseCoroutine != null) { StopCoroutine(pulseCoroutine); pulseCoroutine = null; }
    }

    private static void SetPanel(RectTransform rt, float xMin, float xMax, float yMin, float yMax) {
        rt.anchorMin = new Vector2(xMin, yMin);
        rt.anchorMax = new Vector2(xMax, yMax);
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    private IEnumerator PulseBorder() {
        while (true) {
            if (borderGroup != null)
                borderGroup.alpha = Mathf.Lerp(0.45f, 1f, Mathf.PingPong(Time.time * 1.8f, 1f));
            yield return null;
        }
    }
}
