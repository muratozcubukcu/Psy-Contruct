using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>Draws the spacecraft's predicted trajectory by simulating forward under gravity.</summary>
[RequireComponent(typeof(LineRenderer))]
public class SpacecraftTrajectoryPredictor : MonoBehaviour {

    [Header("Prediction")]
    [Tooltip("How far ahead in seconds to simulate.")]
    [SerializeField] private float predictionSeconds = 6f;

    [Tooltip("Simulation step in seconds. Smaller = more accurate but more cost.")]
    [SerializeField] private float stepSeconds = 0.05f;

    [Tooltip("Stop simulating if the predicted ship gets within this distance of any gravity source center (avoids the line spiraling into a planet).")]
    [SerializeField] private float crashCutoffDistance = 1.5f;

    [Header("Display")]
    [SerializeField] private bool drawTrajectory = true;
    [SerializeField] private float lineWidth = 0.15f;
    [SerializeField] private Color startColor = new Color(0.4f, 0.9f, 1f, 1f);
    [SerializeField] private Color endColor   = new Color(0.4f, 0.9f, 1f, 0.1f);

    [Header("Toggle Hotkey")]
    [Tooltip("Press this key to flip the trajectory on/off at runtime.")]
    [SerializeField] private Key toggleKey = Key.T;

    [Tooltip("Disable to ignore the hotkey.")]
    [SerializeField] private bool hotkeyEnabled = true;

    [Header("Sources")]
    [Tooltip("If empty, the predictor finds all PlanetGravitySource objects in the scene at Start.")]
    [SerializeField] private List<PlanetGravitySource> gravitySources = new();

    public bool DrawTrajectory {
        get => drawTrajectory;
        set => drawTrajectory = value;
    }

    /// <summary>Flip the trajectory display state. Wire to a UI button or keybind.</summary>
    public void Toggle() => drawTrajectory = !drawTrajectory;

    private LineRenderer line;

    private void Awake() {
        line = GetComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.numCornerVertices = 4;
        line.numCapVertices = 4;
        if (line.sharedMaterial == null) {
            line.material = new Material(Shader.Find("Sprites/Default"));
        }
    }

    private void Start() {
        if (gravitySources == null || gravitySources.Count == 0) {
            gravitySources = new List<PlanetGravitySource>(FindObjectsByType<PlanetGravitySource>(FindObjectsSortMode.None));
        }
    }

    private void Update() {
        if (!hotkeyEnabled) return;
        Keyboard kb = Keyboard.current;
        if (kb != null && kb[toggleKey].wasPressedThisFrame) Toggle();
    }

    private void LateUpdate() {
        if (!drawTrajectory) {
            line.enabled = false;
            return;
        }

        Spacecraft ship = Spacecraft.GetInstance();
        if (ship == null) {
            line.enabled = false;
            return;
        }
        Rigidbody2D rb = ship.GetComponent<Rigidbody2D>();
        if (rb == null) {
            line.enabled = false;
            return;
        }

        // Number of integration steps. Always at least 2 so the line has length.
        int steps = Mathf.Max(2, Mathf.CeilToInt(predictionSeconds / Mathf.Max(0.001f, stepSeconds)));

        // Working copies of the ship's state — we mutate these forward, the real ship is untouched.
        Vector2 pos = rb.position;
        Vector2 vel = rb.linearVelocity;
        float dt = stepSeconds;

        Vector3[] points = new Vector3[steps + 1];
        points[0] = pos;
        int actualCount = 1;

        for (int i = 1; i <= steps; i++) {
            // Semi-implicit Euler: v += a*dt; p += v*dt. Stable enough for a visualization.
            Vector2 accel = SumGravity(pos);
            vel += accel * dt;
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

        line.enabled = true;
        line.startWidth = lineWidth;
        line.endWidth = lineWidth;
        line.startColor = startColor;
        line.endColor = endColor;
        line.positionCount = actualCount;
        for (int i = 0; i < actualCount; i++) line.SetPosition(i, points[i]);
    }

    private Vector2 SumGravity(Vector2 worldPos) {
        Vector2 a = Vector2.zero;
        for (int i = 0; i < gravitySources.Count; i++) {
            PlanetGravitySource src = gravitySources[i];
            if (src == null || !src.IsEnabled) continue;
            a += src.GetAccelerationAt(worldPos);
        }
        return a;
    }

    private bool CrashedIntoPlanet(Vector2 worldPos) {
        for (int i = 0; i < gravitySources.Count; i++) {
            PlanetGravitySource src = gravitySources[i];
            if (src == null || !src.IsEnabled) continue;
            if (((Vector2)src.transform.position - worldPos).sqrMagnitude < crashCutoffDistance * crashCutoffDistance) return true;
        }
        return false;
    }
}
