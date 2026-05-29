using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>Runs the build scene tutorial.</summary>
public class Tutorial : MonoBehaviour {
    public static Tutorial instance;

    private enum Step {
        Welcome = 0,
        PlaceShipBody,
        PlaceEngine,
        PlaceFuelTank,
        PlaceSolarPanel,
        PlaceSatelliteDish,
        PlaceSensors,
        Launch,
        Complete
    }

    private struct StepData {
        public string[] allowedParts;
        public string[] requiredParts;
        public string instruction;
        public string explanation;
    }

    private static readonly StepData[] stepDefs = {
        default,
        new StepData {
            allowedParts  = new[] { "ShipPart" },
            requiredParts = new[] { "ShipPart" },
            instruction   = "Place the Spacecraft Body",
            explanation   = "Drag it from the panel on the left onto the grid it must connect to the core in the center of the grid."
        },
        new StepData {
            allowedParts  = new[] { "EnginePart" },
            requiredParts = new[] { "EnginePart" },
            instruction   = "Add an Engine",
            explanation   = "Engines propel your spacecraft. Fire them in flight by pressing the number shown on each one."
        },
        new StepData {
            allowedParts  = new[] { "FuelTankPart" },
            requiredParts = new[] { "FuelTankPart" },
            instruction   = "Add a Fuel Tank",
            explanation   = "Without fuel your engines won't fire. Fuel tanks extend how long you can fly."
        },
        new StepData {
            allowedParts  = new[] { "SolarPanelPart" },
            requiredParts = new[] { "SolarPanelPart" },
            instruction   = "Add a Solar Panel",
            explanation   = "Engines need energy too. Solar panels charge your batteries while facing the Sun."
        },
        new StepData {
            allowedParts  = new[] { "SatelliteDishPart" },
            requiredParts = new[] { "SatelliteDishPart" },
            instruction   = "Add a Satellite Dish",
            explanation   = "Point it at Earth and press Spacebar mid-flight to call for repair assistance."
        },
        new StepData {
            allowedParts  = new[] { "GammaRayPart", "MagnetometerPart", "MultispectralImagerPart", "NeutronSpectrometerPart" },
            requiredParts = new[] { "GammaRayPart", "MagnetometerPart", "MultispectralImagerPart", "NeutronSpectrometerPart" },
            instruction   = "Add the 4 Scientific Sensors",
            explanation   = "These instruments are the heart of the Psyche mission. All four must be on board."
        },
        new StepData {
            allowedParts  = new string[0],
            requiredParts = new string[0],
            instruction   = "Ready to Launch!",
            explanation   = "Your build is complete. Press the launch button whenever you're ready."
        },
    };

    private const int FirstStep = (int)Step.PlaceShipBody;
    private const int LastStep  = (int)Step.Launch;

    private Step currentStep = Step.Welcome;
    private readonly HashSet<string> sensorsPlaced  = new HashSet<string>();

    private readonly HashSet<string> unlockedParts  = new HashSet<string> { "ShipPart" };

    [SerializeField] private RectTransform launchButtonRect;

    [SerializeField] private GameObject welcomePanelGO;
    [SerializeField] private GameObject skipButtonGO;
    [SerializeField] private GameObject      dialogPanel;
    [SerializeField] private TextMeshProUGUI dialogTitle;
    [SerializeField] private TextMeshProUGUI dialogBody;
    [SerializeField] private TextMeshProUGUI dialogStepCounter;

    private TutorialOverlay overlay;

    private readonly Dictionary<PanelPartDrag, CanvasGroup> sidebarGroups
        = new Dictionary<PanelPartDrag, CanvasGroup>();

    private readonly Dictionary<PanelPartDrag, GameObject> glowBorders
        = new Dictionary<PanelPartDrag, GameObject>();
    private Coroutine glowCoroutine;

    public string DebugStep => currentStep.ToString();
    public bool IsActive => enabled
        && Settings.Instance != null
        && Settings.Instance.tutorialEnabled
        && currentStep != Step.Complete;
    public bool BlocksLaunch => IsActive && currentStep != Step.Launch;

    public bool CanDragPart(string partName) {
        if (!IsActive) return true;
        if (currentStep == Step.Welcome) return false;
        if (unlockedParts.Contains(partName)) return true;
        if (currentStep == Step.Launch) return false;
        StepData data = stepDefs[(int)currentStep];
        if (data.allowedParts == null) return false;
        foreach (string a in data.allowedParts)
            if (a == partName) return true;
        return false;
    }

    public void OnPartSuccessfullyPlaced(string partName) {
        if (!IsActive) return;

        if (currentStep == Step.PlaceSensors) {
            StepData data = stepDefs[(int)currentStep];
            foreach (string req in data.requiredParts)
                if (req == partName) { sensorsPlaced.Add(partName); break; }
            bool allDone = true;
            foreach (string req in data.requiredParts)
                if (!sensorsPlaced.Contains(req)) { allDone = false; break; }
            if (allDone) AdvanceStep();
            else {
                if (dialogStepCounter != null)
                    dialogStepCounter.text = $"{sensorsPlaced.Count} of {data.requiredParts.Length} sensors placed";
                RestartDragHint();
            }
            return;
        }

        StepData sd = stepDefs[(int)currentStep];
        foreach (string req in sd.requiredParts)
            if (req == partName) { AdvanceStep(); return; }
    }

    public string GetCurrentHintPartName() {
        string[] names = GetCurrentHintPartNames();
        return names != null && names.Length > 0 ? names[0] : null;
    }

    public string[] GetCurrentHintPartNames() {
        if (!IsActive || currentStep == Step.Welcome || currentStep == Step.Complete) return null;

        StepData data = stepDefs[(int)currentStep];
        if (data.allowedParts == null || data.allowedParts.Length == 0) return null;

        if (currentStep != Step.PlaceSensors) return data.allowedParts;

        List<string> remaining = new List<string>();
        foreach (string part in data.requiredParts)
            if (!sensorsPlaced.Contains(part)) remaining.Add(part);

        return remaining.ToArray();
    }

    public void RestartDragHint() {
        if (!IsActive || currentStep == Step.Welcome || currentStep == Step.Launch || currentStep == Step.Complete) return;

        FindAnyObjectByType<DragHintAnimator>(FindObjectsInactive.Include)?.StopHint();
        gameObject.AddComponent<DragHintAnimator>();
    }

    public void SkipCurrentStep() {
        if (!IsActive || currentStep == Step.Welcome || currentStep == Step.Complete) return;
        AdvanceStep();
    }

    public void OnWelcomeContinued() {
        if (welcomePanelGO != null) welcomePanelGO.SetActive(false);
        AdvanceStep();
    }

    public void SkipTutorial() {
        ClearGlowBorders();
        RestoreSidebarAlpha();
        FindAnyObjectByType<DragHintAnimator>(FindObjectsInactive.Include)?.StopHint();
        HideTutorialUI();
        Settings.Instance.toggleTutorial(false);
        enabled = false;
    }

    void Awake() {
        instance = this;
        HideTutorialUI();
    }

    void Start() {
        if (Settings.Instance == null) {
            StartCoroutine(StartWhenSettingsReady());
            return;
        }

        StartTutorial();
    }

    private IEnumerator StartWhenSettingsReady() {
        while (Settings.Instance == null) yield return null;
        StartTutorial();
    }

    private void StartTutorial() {
        if (!Settings.Instance.tutorialEnabled) {
            HideTutorialUI();
            enabled = false;
            return;
        }

        Image legacyBg = GetComponent<Image>();
        if (legacyBg != null) legacyBg.enabled = false;

        overlay = TutorialOverlay.Create(this);

        if (dialogPanel != null) dialogPanel.SetActive(false);

        if (welcomePanelGO != null) {
            welcomePanelGO.SetActive(true);
            if (skipButtonGO != null) skipButtonGO.SetActive(true);
        } else {
            if (skipButtonGO != null) skipButtonGO.SetActive(true);
            StartCoroutine(StartAfterDelay());
        }
    }

    private void HideTutorialUI() {
        if (welcomePanelGO != null) welcomePanelGO.SetActive(false);
        if (skipButtonGO   != null) skipButtonGO.SetActive(false);
        if (dialogPanel    != null) dialogPanel.SetActive(false);
        overlay?.Hide();
    }

    private IEnumerator StartAfterDelay() {
        yield return null;
        yield return null;
        AdvanceStep();
    }

    private void AdvanceStep() {
        if ((int)currentStep > 0 && (int)currentStep < stepDefs.Length) {
            StepData done = stepDefs[(int)currentStep];
            if (done.allowedParts != null)
                foreach (string p in done.allowedParts)
                    unlockedParts.Add(p);
        }

        currentStep = (Step)((int)currentStep + 1);

        if (currentStep == Step.Complete) {
            ClearGlowBorders();
            RestoreSidebarAlpha();
            FindAnyObjectByType<DragHintAnimator>(FindObjectsInactive.Include)?.StopHint();
            if (skipButtonGO != null) skipButtonGO.SetActive(false);
            if (dialogPanel  != null) dialogPanel.SetActive(false);
            overlay?.Hide();
            return;
        }
        ApplyStep(currentStep);
    }

    private void ApplyStep(Step step) {
        StepData data = stepDefs[(int)step];
        int displayStep = (int)step - FirstStep + 1;
        int totalSteps  = LastStep - FirstStep + 1;

        RefreshSidebarState(data.allowedParts);

        if (dialogPanel != null) dialogPanel.SetActive(true);
        if (dialogTitle != null)       dialogTitle.text       = data.instruction;
        if (dialogBody != null)        dialogBody.text        = data.explanation;
        if (dialogStepCounter != null) dialogStepCounter.text = $"Step {displayStep} of {totalSteps}";

        RectTransform[] targets;
        if (step == Step.Launch) {
            targets = launchButtonRect != null ? new[] { launchButtonRect } : null;
        } else {
            targets = FindSpotlightTargets(data.allowedParts ?? new string[0]);
        }

        overlay?.ShowStep(targets);

        DragHintAnimator old = FindAnyObjectByType<DragHintAnimator>(FindObjectsInactive.Include);
        if (old != null) { old.StopHint(); }
        RestartDragHint();
    }

    private void RefreshSidebarState(string[] currentAllowed) {
        if (currentAllowed == null || currentAllowed.Length == 0) {
            ClearGlowBorders();
            RestoreSidebarAlpha();
            return;
        }

        HashSet<string> requiredSet = new HashSet<string>(currentAllowed);
        PanelPartDrag[] all =
            FindObjectsByType<PanelPartDrag>(FindObjectsSortMode.None);

        ClearGlowBorders();

        foreach (PanelPartDrag drag in all) {
            if (string.IsNullOrEmpty(drag.PartName)) continue;

            if (!sidebarGroups.TryGetValue(drag, out CanvasGroup cg) || cg == null) {
                cg = drag.GetComponent<CanvasGroup>();
                if (cg == null) cg = drag.gameObject.AddComponent<CanvasGroup>();
                sidebarGroups[drag] = cg;
            }
            bool accessible = requiredSet.Contains(drag.PartName) || unlockedParts.Contains(drag.PartName);
            cg.alpha = accessible ? 1f : 0.3f;

            if (requiredSet.Contains(drag.PartName)) {
                glowBorders[drag] = CreateGlowBorder(drag.gameObject);
            }
        }

        if (glowCoroutine != null) StopCoroutine(glowCoroutine);
        if (glowBorders.Count > 0)
            glowCoroutine = StartCoroutine(PulseGlows());
    }

    private void RestoreSidebarAlpha() {
        foreach (CanvasGroup cg in sidebarGroups.Values)
            if (cg != null) cg.alpha = 1f;
    }

    private void ClearGlowBorders() {
        if (glowCoroutine != null) { StopCoroutine(glowCoroutine); glowCoroutine = null; }
        foreach (GameObject g in glowBorders.Values)
            if (g != null) Destroy(g);
        glowBorders.Clear();
    }

    private GameObject CreateGlowBorder(GameObject itemGO) {
        GameObject root = new GameObject("GlowBorder", typeof(RectTransform), typeof(CanvasGroup));
        root.transform.SetParent(itemGO.transform, false);
        root.transform.SetAsLastSibling();

        CanvasGroup cg = root.GetComponent<CanvasGroup>();
        cg.blocksRaycasts = false;
        cg.interactable   = false;

        RectTransform rt = (RectTransform)root.transform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;

        Color gold = new Color(1f, 0.85f, 0.2f, 1f);
        const float T = 5f;

        AddStrip(root.transform, "Top",    new Vector2(0,1), new Vector2(1,1), new Vector2(0,-T),  new Vector2(0,0),  gold);
        AddStrip(root.transform, "Bottom", new Vector2(0,0), new Vector2(1,0), new Vector2(0,0),   new Vector2(0,T),  gold);
        AddStrip(root.transform, "Left",   new Vector2(0,0), new Vector2(0,1), new Vector2(0,T),   new Vector2(T,-T),  gold);
        AddStrip(root.transform, "Right",  new Vector2(1,0), new Vector2(1,1), new Vector2(-T,T),  new Vector2(0,-T),  gold);

        return root;
    }

    private static void AddStrip(Transform parent, string name,
        Vector2 anchorMin, Vector2 anchorMax,
        Vector2 offsetMin, Vector2 offsetMax, Color color) {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        Image img = go.GetComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        RectTransform rt = (RectTransform)go.transform;
        rt.anchorMin = anchorMin; rt.anchorMax = anchorMax;
        rt.offsetMin = offsetMin; rt.offsetMax = offsetMax;
    }

    private IEnumerator PulseGlows() {
        while (true) {
            float a = Mathf.Lerp(0.35f, 1f, Mathf.PingPong(Time.time * 2f, 1f));
            foreach (GameObject g in glowBorders.Values) {
                if (g == null) continue;
                CanvasGroup cg = g.GetComponent<CanvasGroup>();
                if (cg != null) cg.alpha = a;
            }
            yield return null;
        }
    }

    private RectTransform[] FindSpotlightTargets(string[] partNames) {
        PanelPartDrag[] all =
            FindObjectsByType<PanelPartDrag>(FindObjectsSortMode.None);
        List<RectTransform> result = new List<RectTransform>();
        foreach (string name in partNames)
            foreach (PanelPartDrag drag in all)
                if (drag.PartName == name) {
                    RectTransform rt = drag.GetComponent<RectTransform>();
                    if (rt != null) result.Add(rt);
                    break;
                }
        return result.ToArray();
    }
}
