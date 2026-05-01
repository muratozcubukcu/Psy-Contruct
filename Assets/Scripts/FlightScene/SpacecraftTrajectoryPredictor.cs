using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// =============================================================================
// SpacecraftTrajectoryPredictor
// -----------------------------------------------------------------------------
// What it does:
//   Draws a glowing line in front of the ship showing where it WILL go over the
//   next few seconds, based on its current speed and the gravity of every
//   planet. The line updates every frame.
//
// How it works:
//   Each frame we copy the ship's position and velocity, then "fast-forward"
//   that copy in tiny time steps. At each step we add up gravity from every
//   planet, push the velocity forward, and record where the ship would be.
//   The list of recorded positions is fed into a LineRenderer to draw the arc.
//   The real ship is never touched - we only simulate a virtual copy.
//
// How to use it:
//   1. Drop this component on a GameObject that has a LineRenderer (the
//      [RequireComponent] attribute will add one automatically).
//   2. Make sure your scene has a Spacecraft and one or more
//      PlanetGravitySource components. Leave 'gravitySources' empty and it
//      auto-finds them on Start.
//   3. Press T (or whatever 'toggleKey' is set to) at runtime to show/hide
//      the line. You can also call Toggle() from a UI button.
// =============================================================================

/// <summary>Draws the spacecraft's predicted trajectory by simulating forward under gravity.</summary>
[RequireComponent(typeof(LineRenderer))]
public class SpacecraftTrajectoryPredictor : MonoBehaviour {

    [Header("Prediction")]
    [Tooltip("How far ahead in seconds to simulate.")]
    // How many seconds into the future the line should reach.
    [SerializeField] private float predictionSeconds = 6f;

    [Tooltip("Simulation step in seconds. Smaller = more accurate but more cost.")]
    // Size of each fake "tick" of the simulation. Smaller = smoother and
    // more accurate line, but more math per frame.
    [SerializeField] private float stepSeconds = 0.05f;

    [Tooltip("Stop simulating if the predicted ship gets within this distance of any gravity source center (avoids the line spiraling into a planet).")]
    // If the simulated ship gets this close to a planet center we stop drawing.
    // Without this the line would keep curling into the planet forever.
    [SerializeField] private float crashCutoffDistance = 1.5f;

    [Header("Display")]
    // Master on/off switch. Set false to hide the line entirely.
    [SerializeField] private bool drawTrajectory = true;
    // Thickness of the line in world units.
    [SerializeField] private float lineWidth = 0.15f;
    // Color at the start of the line (closest to the ship).
    [SerializeField] private Color startColor = new Color(0.4f, 0.9f, 1f, 1f);

    [SerializeField] private Color slingshotStartColor = new Color(0.2f, 1f, 0.3f, 1f);  
    [SerializeField] private Color slingshotEndColor = new Color(0.2f, 1f, 0.3f, 0.1f);

    // Color at the far end of the line (fades out).
    [SerializeField] private Color endColor   = new Color(0.4f, 0.9f, 1f, 0.1f);

    [Header("Toggle Hotkey")]
    [Tooltip("Press this key to flip the trajectory on/off at runtime.")]
    // Keyboard key that toggles the line on/off when pressed.
    [SerializeField] private Key toggleKey = Key.T;

    [Tooltip("Disable to ignore the hotkey.")]
    // If false, the hotkey does nothing (useful when a menu has focus).
    [SerializeField] private bool hotkeyEnabled = true;

    [Header("Sources")]
    [Tooltip("If empty, the predictor finds all PlanetGravitySource objects in the scene at Start.")]
    // List of gravity sources used in the prediction. Leave empty to auto-fill.
    [SerializeField] private List<PlanetGravitySource> gravitySources = new();

    [SerializeField] private MarsSlingshotPlanner slingshotPlanner;

    /// <summary>Public on/off switch. Other scripts (UI, settings) can flip this to show/hide the line.</summary>
    public bool DrawTrajectory {
        get => drawTrajectory;
        set => drawTrajectory = value;
    }

    /// <summary>Flip the trajectory display state. Wire to a UI button or keybind.</summary>
    public void Toggle() => drawTrajectory = !drawTrajectory;

    // The LineRenderer that actually draws the curve. Cached in Awake().
    private LineRenderer line;

    private void Awake() {
        // Grab the LineRenderer that [RequireComponent] guarantees exists.
        line = GetComponent<LineRenderer>();

        // Use world-space points so we can feed in raw world positions later.
        line.useWorldSpace = true;

        // Round off the bevels so the curve looks smooth, not jagged.
        line.numCornerVertices = 4;
        line.numCapVertices = 4;

        // Make sure there's a material that supports vertex color, otherwise
        // the start/end colors won't show up.
        if (line.sharedMaterial == null) {
            line.material = new Material(Shader.Find("Sprites/Default"));
        }
    }

    private void Start() {
        // If the list in the inspector isnht filled auto-find
        // every gravity source in the scene so the prediction is complete.
        if (gravitySources == null || gravitySources.Count == 0) {
            gravitySources = new List<PlanetGravitySource>(FindObjectsByType<PlanetGravitySource>(FindObjectsSortMode.None));
        }
        if (slingshotPlanner == null)
            slingshotPlanner = FindFirstObjectByType<MarsSlingshotPlanner>();
    }

    private void Update() {
        // Listen for the toggle hotkey. Update() runs every frame.
        if (!hotkeyEnabled) return;
        Keyboard kb = Keyboard.current;
        if (kb != null && kb[toggleKey].wasPressedThisFrame) Toggle();
    }

    private void LateUpdate() {
        // LateUpdate runs after physics, so the ship's position and velocity
        // are settled for this frame before we read them.

        // If drawing is turned off, just hide the line and bail.
        if (!drawTrajectory) {
            line.enabled = false;
            return;
        }

        // No ship in the scene yet? Hide the line.
        Spacecraft ship = Spacecraft.GetInstance();
        if (ship == null) {
            line.enabled = false;
            return;
        }
        // We need a Rigidbody2D to read velocity. Without one, no prediction.
        Rigidbody2D rb = ship.GetComponent<Rigidbody2D>();
        if (rb == null) {
            line.enabled = false;
            return;
        }

        // Number of integration steps. Always at least 2 so the line has length.
        int steps = Mathf.Max(2, Mathf.CeilToInt(predictionSeconds / Mathf.Max(0.001f, stepSeconds)));

        // Working copies of the ship's state, we mutate these forward, the real ship is untouched.
        Vector2 pos = rb.position;
        Vector2 vel = rb.linearVelocity;
        float dt = stepSeconds;

        // Buffer for every predicted position. First slot is "now".
        Vector3[] points = new Vector3[steps + 1];
        points[0] = pos;
        int actualCount = 1;

        for (int i = 1; i <= steps; i++) {
            // Semi-implicit Euler: v += a*dt; p += v*dt. Stable enough for a visualization.
            // Step 1: figure out the gravity pulling on us at this point.
            Vector2 accel = SumGravity(pos);
            // Step 2: gravity changes our velocity over the time-step.
            vel += accel * dt;
            // Step 3: the new velocity moves us forward.
            pos += vel * dt;

            // Bail out if the trajectory would dive into a planet, so the line doesn't spiral inside it.
            if (CrashedIntoPlanet(pos)) {
                points[i] = pos;
                actualCount = i + 1;
                break;
            }

            points[i] = pos;
            actualCount = i + 1;
        }

        // Push the predicted points into the LineRenderer for drawing.
        line.enabled = true;
        line.startWidth = lineWidth;
        line.endWidth = lineWidth;
        bool viable = slingshotPlanner != null && slingshotPlanner.IsSlingshotViable;
        line.startColor = viable ? slingshotStartColor : startColor;
        line.endColor = viable ? slingshotEndColor : endColor;
        line.positionCount = actualCount;
        for (int i = 0; i < actualCount; i++) line.SetPosition(i, points[i]);
    }

    // Adds up the gravity acceleration from every active source at this position.
    // Each PlanetGravitySource knows how to compute its own pull at a given point.
    private Vector2 SumGravity(Vector2 worldPos) {
        Vector2 a = Vector2.zero;
        for (int i = 0; i < gravitySources.Count; i++) {
            PlanetGravitySource src = gravitySources[i];
            if (src == null || !src.IsEnabled) continue;
            a += src.GetAccelerationAt(worldPos);
        }
        return a;
    }

    // Returns true if the predicted position is inside any planet's "danger zone".
    // Used to cut the line short instead of letting it spiral into a planet.
    private bool CrashedIntoPlanet(Vector2 worldPos) {
        for (int i = 0; i < gravitySources.Count; i++) {
            PlanetGravitySource src = gravitySources[i];
            if (src == null || !src.IsEnabled) continue;
            // Squared-distance compare avoids a square root - same result, faster.
            if (((Vector2)src.transform.position - worldPos).sqrMagnitude < crashCutoffDistance * crashCutoffDistance) return true;
        }
        return false;
    }
}
