using UnityEngine;

/// <summary>Plans and draws a slingshot path around Mars to Psyche.</summary>
public class MarsSlingshotPlanner : MonoBehaviour {

    [Tooltip("Optional override. Falls back to Mars.Instance.transform if null.")]
    [SerializeField] private Transform marsOverride;

    [Tooltip("Optional override. Falls back to PsycheAsteroid.Instance.transform if null.")]
    [SerializeField] private Transform psycheOverride;

    [Tooltip("Optional override. Falls back to Spacecraft.GetInstance().transform if null.")]
    [SerializeField] private Transform spacecraftOverride;

    [Tooltip("Closest distance to Mars during the slingshot (periapsis). Used as a fallback if useMarsColliderRadius is off OR Mars has no CircleCollider2D.")]
    [SerializeField] private float periapsisRadius = 5f;

    [Tooltip("If true, the slingshot radius is automatically set to Mars's CircleCollider2D radius + radiusMargin so the arc always passes around Mars on the outside.")]
    [SerializeField] private bool useMarsColliderRadius = true;

    [Tooltip("Extra clearance added on top of Mars's collider radius when useMarsColliderRadius is true.")]
    [SerializeField] private float radiusMargin = 2f;

    [Tooltip("True = clockwise pass around Mars. False = counterclockwise. Pick whichever requires less course correction for the current approach.")]
    [SerializeField] private bool clockwisePass = true;

    [Tooltip("Auto-pick rotation direction based on which side of the Mars→Psyche line the ship is on.")]
    [SerializeField] private bool autoPickRotation = true;

    [Header("Visualization")]
    [Tooltip("Draw the planned path as a LineRenderer at runtime.")]
    [SerializeField] private bool drawRuntimePath = true;

    [Tooltip("Draw the editor gizmos (Mars circle + path) when this object is selected in the scene view.")]
    [SerializeField] private bool drawEditorGizmos = true;

    [Tooltip("Number of points sampled along the slingshot arc.")]
    [SerializeField] private int arcSegments = 32;

    [Tooltip("Color for the ship → Mars approach segment.")]
    [SerializeField] private Color approachColor = new Color(1f, 0.4f, 0.4f, 1f);

    [Tooltip("Color for the slingshot arc around Mars.")]
    [SerializeField] private Color arcColor = new Color(1f, 0.85f, 0.2f, 1f);

    [Tooltip("Color for the exit segment heading to Psyche.")]
    [SerializeField] private Color exitColor = new Color(0.4f, 1f, 0.5f, 1f);

    [Tooltip("Width of the runtime path line.")]
    [SerializeField] private float lineWidth = 0.4f;

    private LineRenderer pathLine;

    public Transform MarsTransform => MarsTf;
    public Transform PsycheTransform => PsycheTf;
    public Transform ShipTransform => ShipTf;
    public float SlingshotRadius => isFrozen ? frozenRadius : EffectiveRadius;
    public float RotationSign => isFrozen ? frozenSign : ResolveRotationSign();
    public bool IsFrozen => isFrozen;

    public bool DrawRuntimePath {
        get => drawRuntimePath;
        set => drawRuntimePath = value;
    }

    public bool DrawEditorGizmos {
        get => drawEditorGizmos;
        set => drawEditorGizmos = value;
    }

    // Snapshot fields used while frozen.
    private bool isFrozen;
    private Vector3[] frozenPath;
    private Vector2 frozenEntry, frozenExit, frozenExitDir;
    private float frozenRadius, frozenSign;
    private bool frozenSolutionValid;

    /// <summary>Snapshot the current path so subsequent queries return stable values.</summary>
    public void FreezePath() {
        frozenSolutionValid = IsSolutionValid;
        if (frozenSolutionValid) {
            frozenEntry = SlingshotEntryPoint;
            frozenExit = SlingshotExitPoint;
            frozenExitDir = SlingshotExitDirection;
            frozenRadius = EffectiveRadius;
            frozenSign = ResolveRotationSign();
            frozenPath = GetPathPoints();
        }
        isFrozen = true;
    }

    public void UnfreezePath() {
        isFrozen = false;
        frozenPath = null;
    }

    private Transform MarsTf => marsOverride != null ? marsOverride
        : (Mars.Instance != null ? Mars.Instance.transform : null);

    private Transform PsycheTf => psycheOverride != null ? psycheOverride
        : (PsycheAsteroid.Instance != null ? PsycheAsteroid.Instance.transform : null);

    private Transform ShipTf => spacecraftOverride != null ? spacecraftOverride
        : (Spacecraft.GetInstance() != null ? Spacecraft.GetInstance().transform : null);

    /// <summary>Slingshot radius: Mars collider + margin, or the manual periapsis fallback.</summary>
    private float EffectiveRadius {
        get {
            if (!useMarsColliderRadius || MarsTf == null) return periapsisRadius;

            CircleCollider2D col = MarsTf.GetComponentInChildren<CircleCollider2D>();
            if (col == null) return periapsisRadius;

            float worldScale = Mathf.Max(MarsTf.lossyScale.x, MarsTf.lossyScale.y);
            return col.radius * worldScale + radiusMargin;
        }
    }

    /// <summary>Unit vector from the ship toward Mars.</summary>
    public Vector2 DirectionFromShipToMars {
        get {
            if (ShipTf == null || MarsTf == null) return Vector2.zero;
            Vector2 delta = (Vector2)MarsTf.position - (Vector2)ShipTf.position;
            return delta.sqrMagnitude > 0f ? delta.normalized : Vector2.zero;
        }
    }

    /// <summary>Unit vector from Mars toward Psyche.</summary>
    public Vector2 DirectionFromMarsToPsyche {
        get {
            if (MarsTf == null || PsycheTf == null) return Vector2.zero;
            Vector2 delta = (Vector2)PsycheTf.position - (Vector2)MarsTf.position;
            return delta.sqrMagnitude > 0f ? delta.normalized : Vector2.zero;
        }
    }

    /// <summary>Tangent point on the Mars circle from which the ship can fly straight to Psyche.</summary>
    public Vector2 SlingshotExitPoint {
        get {
            if (isFrozen) return frozenExit;
            if (MarsTf == null || PsycheTf == null) return Vector2.zero;
            Vector2 marsPos = MarsTf.position;
            Vector2 toPsyche = DirectionFromMarsToPsyche;

            // Two tangent lines exist from any external point P to a circle (center M, radius r).
            // We pick the one matching our rotation direction by flipping the perpendicular sign.
            // perp is toPsyche rotated 90° in the chosen direction.
            float sign = ResolveRotationSign();
            Vector2 perp = new Vector2(toPsyche.y * sign, -toPsyche.x * sign);

            // Geometry: triangle Mars-Tangent-Psyche has a right angle at the tangent point
            // (radius is perpendicular to the tangent line). Hypotenuse |MP| = d, opposite-side
            // leg |MT| = r, so cos(theta) = r / d where theta is the angle at M between
            // (Mars→Psyche) and (Mars→Tangent). sin(theta) follows from the Pythagorean identity.
            float r = EffectiveRadius;
            float d = ((Vector2)PsycheTf.position - marsPos).magnitude;
            if (d <= r) return marsPos; // Psyche inside the circle: no tangent exists.

            float cosTheta = r / d;
            float sinTheta = Mathf.Sqrt(Mathf.Max(0f, 1f - cosTheta * cosTheta));

            // Place the tangent point at angle ±theta around M, measured from (Mars→Psyche).
            // Decompose into "along toPsyche" (cos component) and "perpendicular" (sin component).
            return marsPos + r * (toPsyche * cosTheta + perp * sinTheta);
        }
    }

    /// <summary>Unit vector from the exit point toward Psyche.</summary>
    public Vector2 SlingshotExitDirection {
        get {
            if (isFrozen) return frozenExitDir;
            if (PsycheTf == null) return Vector2.zero;
            Vector2 exit = SlingshotExitPoint;
            Vector2 toPsyche = (Vector2)PsycheTf.position - exit;
            return toPsyche.sqrMagnitude > 0f ? toPsyche.normalized : Vector2.zero;
        }
    }

    /// <summary>Exit direction as an angle in degrees, CCW from +X.</summary>
    public float SlingshotExitAngleDegrees {
        get {
            Vector2 dir = SlingshotExitDirection;
            return Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        }
    }

    /// <summary>True when Psyche sits outside the slingshot circle and a path can be drawn.</summary>
    public bool IsSolutionValid {
        get {
            if (isFrozen) return frozenSolutionValid;
            if (MarsTf == null || PsycheTf == null || ShipTf == null) return false;
            float d = ((Vector2)PsycheTf.position - (Vector2)MarsTf.position).magnitude;
            return d > EffectiveRadius;
        }
    }

    /// <summary>Tangent point on the Mars circle where the ship's approach line touches.</summary>
    public Vector2 SlingshotEntryPoint {
        get {
            if (isFrozen) return frozenEntry;
            if (MarsTf == null || ShipTf == null) return Vector2.zero;
            Vector2 marsPos = MarsTf.position;
            Vector2 toShip = (Vector2)ShipTf.position - marsPos;
            float d = toShip.magnitude;
            float r = EffectiveRadius;
            if (d <= r) return marsPos; // Ship inside the circle: no valid tangent.

            // Same tangent-from-external-point formula as the exit, but with the ship as the
            // external point. The perpendicular sign flips relative to the exit so that the
            // entry tangent and the exit tangent end up on the same rotation side of Mars
            // (so the arc between them goes the chosen way around).
            Vector2 toShipDir = toShip / d;
            float sign = ResolveRotationSign();
            Vector2 perp = new Vector2(-toShipDir.y * sign, toShipDir.x * sign);

            // cos(theta) = r/d (right triangle Mars-Tangent-Ship), sin(theta) by identity.
            float cosTheta = r / d;
            float sinTheta = Mathf.Sqrt(Mathf.Max(0f, 1f - cosTheta * cosTheta));

            return marsPos + r * (toShipDir * cosTheta + perp * sinTheta);
        }
    }

    /// <summary>Path polyline: ship -> entry -> arc -> exit -> Psyche.</summary>
    public Vector3[] GetPathPoints() {
        if (isFrozen) return frozenPath ?? System.Array.Empty<Vector3>();
        if (!IsSolutionValid) return System.Array.Empty<Vector3>();

        Vector2 entry = SlingshotEntryPoint;
        Vector2 exit = SlingshotExitPoint;
        Vector2 marsPos = MarsTf.position;
        float r = EffectiveRadius;

        // Convert the two tangent points to angles measured from Mars's center.
        // atan2(dy, dx) returns the polar angle in [-pi, pi].
        float entryAngle = Mathf.Atan2(entry.y - marsPos.y, entry.x - marsPos.x);
        float exitAngle = Mathf.Atan2(exit.y - marsPos.y, exit.x - marsPos.x);

        // Sweep is the signed angular distance entry → exit. Naive subtraction can give
        // a negative result for CCW (sign>0) or positive for CW (sign<0), which would draw
        // the arc the wrong way. Wrap by ±2π so the sweep direction matches our rotation sign.
        float sign = ResolveRotationSign();
        float sweep = exitAngle - entryAngle;
        if (sign > 0f && sweep < 0f) sweep += 2f * Mathf.PI;
        if (sign < 0f && sweep > 0f) sweep -= 2f * Mathf.PI;

        int segs = Mathf.Max(2, arcSegments);
        Vector3[] points = new Vector3[segs + 3];
        points[0] = ShipTf != null ? ShipTf.position : (Vector3)entry;

        // Parametric circle: P(a) = M + r * (cos(a), sin(a)). Step the angle linearly from
        // entryAngle to entryAngle+sweep over segs+1 samples to get the polyline arc.
        for (int i = 0; i <= segs; i++) {
            float t = i / (float)segs;
            float a = entryAngle + sweep * t;
            points[1 + i] = marsPos + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
        }

        points[segs + 2] = PsycheTf != null ? PsycheTf.position : (Vector3)exit;
        return points;
    }

    private void Awake() {
        if (drawRuntimePath) EnsureLineRenderer();
    }

    private void LateUpdate() {
        if (!drawRuntimePath) {
            if (pathLine != null) pathLine.enabled = false;
            return;
        }

        EnsureLineRenderer();

        if (!IsSolutionValid) {
            pathLine.enabled = false;
            return;
        }

        Vector3[] pts = GetPathPoints();
        pathLine.enabled = true;
        pathLine.positionCount = pts.Length;
        pathLine.SetPositions(pts);

        // Approach -> arc -> exit gradient.
        var gradient = new Gradient();
        gradient.SetKeys(
            new[] {
                new GradientColorKey(approachColor, 0f),
                new GradientColorKey(arcColor, 1f / Mathf.Max(1, pts.Length - 1)),
                new GradientColorKey(arcColor, (pts.Length - 2f) / Mathf.Max(1, pts.Length - 1)),
                new GradientColorKey(exitColor, 1f),
            },
            new[] {
                new GradientAlphaKey(approachColor.a, 0f),
                new GradientAlphaKey(arcColor.a, 1f / Mathf.Max(1, pts.Length - 1)),
                new GradientAlphaKey(arcColor.a, (pts.Length - 2f) / Mathf.Max(1, pts.Length - 1)),
                new GradientAlphaKey(exitColor.a, 1f),
            }
        );
        pathLine.colorGradient = gradient;
    }

    private void EnsureLineRenderer() {
        if (pathLine == null) {
            pathLine = GetComponent<LineRenderer>();
            if (pathLine == null) pathLine = gameObject.AddComponent<LineRenderer>();
            pathLine.useWorldSpace = true;
            pathLine.material = new Material(Shader.Find("Sprites/Default"));
            pathLine.numCornerVertices = 8;
            pathLine.numCapVertices = 8;
            pathLine.sortingOrder = 100;
        }
        pathLine.startWidth = lineWidth;
        pathLine.endWidth = lineWidth;
    }

    private float ResolveRotationSign() {
        if (!autoPickRotation) return clockwisePass ? -1f : 1f;
        if (ShipTf == null) return -1f;

        // 2D cross product (z-component of the 3D cross): a × b = a.x*b.y - a.y*b.x.
        // Sign tells us which side of (Mars→Psyche) the ship is on:
        //   positive = ship is counterclockwise of the line → swing CCW around Mars,
        //   negative = ship is clockwise of the line → swing CW.
        // Picking the side the ship is already on minimizes the sweep angle.
        Vector2 toShip = (Vector2)ShipTf.position - (Vector2)MarsTf.position;
        Vector2 toPsyche = DirectionFromMarsToPsyche;
        float cross = toPsyche.x * toShip.y - toPsyche.y * toShip.x;
        return cross >= 0f ? 1f : -1f;
    }

    private void OnDrawGizmosSelected() {
        if (!drawEditorGizmos) return;
        if (MarsTf == null || PsycheTf == null) return;

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(MarsTf.position, EffectiveRadius);

        if (!IsSolutionValid) return;

        Vector3[] pts = GetPathPoints();
        for (int i = 0; i < pts.Length - 1; i++) {
            Gizmos.color = i == 0 ? approachColor
                         : i >= pts.Length - 2 ? exitColor
                         : arcColor;
            Gizmos.DrawLine(pts[i], pts[i + 1]);
        }
    }
}
