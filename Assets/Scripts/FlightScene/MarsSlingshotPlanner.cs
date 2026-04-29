using System.Collections.Generic;
using UnityEngine;

/// <summary>Snapshot-on-entry slingshot planner: captures a Keplerian conic when ship enters Mars's range and holds it until exit.</summary>
public class MarsSlingshotPlanner : MonoBehaviour {

    [SerializeField] private Transform marsOverride;
    [SerializeField] private Transform psycheOverride;
    [SerializeField] private Transform spacecraftOverride;

    [SerializeField] private float periapsisRadius = 5f;
    [SerializeField] private bool useMarsColliderRadius = true;
    [SerializeField] private float radiusMargin = 10f;

    [Header("Snapshot Trigger")]
    [SerializeField] private float entryRangeOverride = 0f;
    [Range(1f, 3f)]
    [SerializeField] private float exitRangeMultiplier = 1.25f;

    [Header("Sampling")]
    [SerializeField] private int conicSegments = 96;

    [Header("Visualization")]
    [SerializeField] private bool drawRuntimePath = true;
    [SerializeField] private bool drawEditorGizmos = true;
    [SerializeField] private Color approachColor = new Color(1f, 0.4f, 0.4f, 1f);
    [SerializeField] private Color arcColor = new Color(1f, 0.85f, 0.2f, 1f);
    [SerializeField] private Color exitColor = new Color(0.4f, 1f, 0.5f, 1f);
    [SerializeField] private float lineWidth = 0.4f;

    [Header("Gravity Sources (optional)")]
    [SerializeField] private PlanetGravitySource marsGravity;

    private LineRenderer pathLine;

    public Transform MarsTransform => MarsTf;
    public Transform PsycheTransform => PsycheTf;
    public Transform ShipTransform => ShipTf;

    public bool DrawRuntimePath {
        get => drawRuntimePath;
        set => drawRuntimePath = value;
    }

    public bool DrawEditorGizmos {
        get => drawEditorGizmos;
        set => drawEditorGizmos = value;
    }

    private Vector3[] snapshotPath;
    private bool inMarsRange;

    private bool externallyFrozen;
    private Vector3[] externallyFrozenPath;

    public bool IsFrozen => externallyFrozen;

    public void FreezePath() {
        externallyFrozenPath = GetPathPoints();
        externallyFrozen = true;
    }

    public void UnfreezePath() {
        externallyFrozen = false;
        externallyFrozenPath = null;
    }

    public bool IsSolutionValid => snapshotPath != null && snapshotPath.Length >= 2;

    private Transform MarsTf => marsOverride != null ? marsOverride
        : (Mars.Instance != null ? Mars.Instance.transform : null);

    private Transform PsycheTf => psycheOverride != null ? psycheOverride
        : (PsycheAsteroid.Instance != null ? PsycheAsteroid.Instance.transform : null);

    private Transform ShipTf => spacecraftOverride != null ? spacecraftOverride
        : (Spacecraft.GetInstance() != null ? Spacecraft.GetInstance().transform : null);

    private float EffectiveRadius {
        get {
            if (!useMarsColliderRadius || MarsTf == null) return periapsisRadius;
            CircleCollider2D col = MarsTf.GetComponentInChildren<CircleCollider2D>();
            if (col == null) return periapsisRadius;
            float scale = Mathf.Max(MarsTf.lossyScale.x, MarsTf.lossyScale.y);
            return col.radius * scale + radiusMargin;
        }
    }

    private Vector2 ShipPos {
        get {
            Transform t = ShipTf;
            if (t == null) return Vector2.zero;
            Rigidbody2D rb = t.GetComponent<Rigidbody2D>();
            return rb != null ? rb.position : (Vector2)t.position;
        }
    }

    private Vector2 PsychePos {
        get {
            Transform t = PsycheTf;
            if (t == null) return Vector2.zero;
            Rigidbody2D rb = t.GetComponent<Rigidbody2D>();
            return rb != null ? rb.position : (Vector2)t.position;
        }
    }

    private float EntryRange {
        get {
            if (entryRangeOverride > 0f) return entryRangeOverride;
            if (marsGravity != null) return marsGravity.GetGravityRadius();
            return 60f;
        }
    }

    public Vector3[] GetPathPoints() {
        if (externallyFrozen) return externallyFrozenPath ?? System.Array.Empty<Vector3>();
        if (MarsTf == null || ShipTf == null || PsycheTf == null) return System.Array.Empty<Vector3>();

        Vector2 ship = ShipPos;
        Vector2 psy = PsychePos;
        Vector2 mars = MarsTf.position;

        float dShip = (ship - mars).magnitude;
        float entryR = EntryRange;
        float exitR = entryR * exitRangeMultiplier;

        if (!inMarsRange && dShip <= entryR) {
            snapshotPath = BuildSlingshotPlan(ship, mars, psy);
            inMarsRange = true;
        } else if (inMarsRange && dShip >= exitR) {
            snapshotPath = null;
            inMarsRange = false;
        }

        if (snapshotPath == null || snapshotPath.Length < 2) return System.Array.Empty<Vector3>();
        return snapshotPath;
    }

    private Vector3[] BuildSlingshotPlan(Vector2 ship, Vector2 mars, Vector2 psy) {
        if (TrySolveConic(ship, mars, psy, out Conic c)) {
            return SampleConic(c, ship, mars, psy);
        }
        return BuildGeometricArc(ship, mars, psy);
    }

    private struct Conic {
        public float p, e, omega;
    }

    private static float PickRotationSign(Vector2 ship, Vector2 mars, Vector2 psy) {
        Vector2 marsToShip = ship - mars;
        Vector2 marsToPsy = psy - mars;
        float cross = marsToPsy.x * marsToShip.y - marsToPsy.y * marsToShip.x;
        return cross >= 0f ? +1f : -1f;
    }

    private bool TrySolveConic(Vector2 ship, Vector2 mars, Vector2 psy, out Conic conic) {
        conic = default;
        Vector2 toShip = ship - mars;
        Vector2 toPsy = psy - mars;
        float r_s = toShip.magnitude;
        float r_psy = toPsy.magnitude;
        float r_peri = EffectiveRadius;
        if (r_s <= r_peri || r_psy <= r_peri) return false;

        float A_s = r_peri / r_s;
        float A_p = r_peri / r_psy;
        float theta_s = Mathf.Atan2(toShip.y, toShip.x);
        float theta_p = Mathf.Atan2(toPsy.y, toPsy.x);

        float B = (A_s - 1f) * Mathf.Cos(theta_p) - (A_p - 1f) * Mathf.Cos(theta_s);
        float C = (A_s - 1f) * Mathf.Sin(theta_p) - (A_p - 1f) * Mathf.Sin(theta_s);
        float R = Mathf.Sqrt(B * B + C * C);
        if (R < 1e-6f) return false;

        float ratio = (A_s - A_p) / R;
        if (Mathf.Abs(ratio) > 1f + 1e-4f) return false;
        ratio = Mathf.Clamp(ratio, -1f, 1f);

        float phi = Mathf.Atan2(C, B);
        float ac = Mathf.Acos(ratio);

        Conic candA = BuildConicFromOmega(phi + ac, theta_s, A_s, r_peri);
        Conic candB = BuildConicFromOmega(phi - ac, theta_s, A_s, r_peri);

        bool aValid = candA.e > 0f && PeriapsisBetween(candA.omega, theta_s, theta_p);
        bool bValid = candB.e > 0f && PeriapsisBetween(candB.omega, theta_s, theta_p);

        if (!aValid && !bValid) return false;
        if (aValid && !bValid) { conic = candA; return true; }
        if (bValid && !aValid) { conic = candB; return true; }

        float rotSign = PickRotationSign(ship, mars, psy);
        float sideA = SignOfPeriSide(candA.omega, theta_p);
        float sideB = SignOfPeriSide(candB.omega, theta_p);
        if (Mathf.Sign(sideA) == Mathf.Sign(rotSign)) { conic = candA; return true; }
        if (Mathf.Sign(sideB) == Mathf.Sign(rotSign)) { conic = candB; return true; }
        conic = candA;
        return true;
    }

    private static Conic BuildConicFromOmega(float omega, float theta_s, float A_s, float r_peri) {
        float nu_s = theta_s - omega;
        float denom = Mathf.Cos(nu_s) - A_s;
        if (Mathf.Abs(denom) < 1e-6f) return new Conic { e = -1f };
        float e = (A_s - 1f) / denom;
        return new Conic { p = r_peri * (1f + e), e = e, omega = omega };
    }

    private static bool PeriapsisBetween(float omega, float theta_s, float theta_p) {
        float nu_s = WrapPi(theta_s - omega);
        float nu_p = WrapPi(theta_p - omega);
        return nu_s * nu_p < 0f;
    }

    private static float SignOfPeriSide(float omega, float theta_psyche) {
        Vector2 periDir = new Vector2(Mathf.Cos(omega), Mathf.Sin(omega));
        Vector2 psyDir = new Vector2(Mathf.Cos(theta_psyche), Mathf.Sin(theta_psyche));
        return psyDir.x * periDir.y - psyDir.y * periDir.x;
    }

    private static float WrapPi(float a) {
        while (a > Mathf.PI) a -= 2f * Mathf.PI;
        while (a < -Mathf.PI) a += 2f * Mathf.PI;
        return a;
    }

    private Vector3[] SampleConic(Conic c, Vector2 ship, Vector2 mars, Vector2 psy) {
        Vector2 toShip = ship - mars;
        Vector2 toPsy = psy - mars;
        float theta_s = Mathf.Atan2(toShip.y, toShip.x);
        float theta_p = Mathf.Atan2(toPsy.y, toPsy.x);
        float nu_s = WrapPi(theta_s - c.omega);
        float nu_p = WrapPi(theta_p - c.omega);

        int segs = Mathf.Max(8, conicSegments);
        var pts = new Vector3[segs + 1];
        pts[0] = ship;
        int filled = 1;
        for (int i = 1; i <= segs; i++) {
            float t = i / (float)segs;
            float nu = Mathf.Lerp(nu_s, nu_p, t);
            float r = c.p / (1f + c.e * Mathf.Cos(nu));
            if (r <= 0f || r > 1e6f) {
                System.Array.Resize(ref pts, filled);
                return pts;
            }
            float theta = c.omega + nu;
            pts[i] = mars + new Vector2(Mathf.Cos(theta), Mathf.Sin(theta)) * r;
            filled = i + 1;
        }
        pts[pts.Length - 1] = psy;
        return pts;
    }

    private Vector3[] BuildGeometricArc(Vector2 ship, Vector2 mars, Vector2 psy) {
        float r = EffectiveRadius;
        float dShip = (ship - mars).magnitude;
        float dPsy = (psy - mars).magnitude;
        if (dShip <= r || dPsy <= r) return new Vector3[] { ship, psy };

        float sign = PickRotationSign(ship, mars, psy);
        Vector2 toShipDir = (ship - mars) / dShip;
        Vector2 toPsyDir = (psy - mars) / dPsy;
        Vector2 perpShip = new Vector2(-toShipDir.y * sign, toShipDir.x * sign);
        Vector2 perpPsy = new Vector2(toPsyDir.y * sign, -toPsyDir.x * sign);
        float cosTs = r / dShip, sinTs = Mathf.Sqrt(Mathf.Max(0f, 1f - cosTs * cosTs));
        float cosTp = r / dPsy, sinTp = Mathf.Sqrt(Mathf.Max(0f, 1f - cosTp * cosTp));
        Vector2 entry = mars + r * (toShipDir * cosTs + perpShip * sinTs);
        Vector2 exit = mars + r * (toPsyDir * cosTp + perpPsy * sinTp);

        float entryAngle = Mathf.Atan2(entry.y - mars.y, entry.x - mars.x);
        float exitAngle = Mathf.Atan2(exit.y - mars.y, exit.x - mars.x);
        float sweep = exitAngle - entryAngle;
        if (sign > 0f && sweep < 0f) sweep += 2f * Mathf.PI;
        if (sign < 0f && sweep > 0f) sweep -= 2f * Mathf.PI;

        int segs = Mathf.Max(16, conicSegments);
        var pts = new List<Vector3>(segs + 4);
        pts.Add(ship);
        pts.Add(entry);
        for (int i = 1; i <= segs; i++) {
            float t = i / (float)segs;
            float a = entryAngle + sweep * t;
            pts.Add(mars + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r);
        }
        pts.Add(psy);
        return pts.ToArray();
    }

    private void Awake() {
        if (drawRuntimePath) EnsureLineRenderer();
    }

    private void Start() {
        if (marsGravity == null && MarsTf != null) {
            marsGravity = MarsTf.GetComponent<PlanetGravitySource>()
                       ?? MarsTf.GetComponentInChildren<PlanetGravitySource>();
        }
    }

    private void LateUpdate() {
        if (!drawRuntimePath) {
            if (pathLine != null) pathLine.enabled = false;
            return;
        }

        EnsureLineRenderer();

        Vector3[] pts = GetPathPoints();
        if (pts.Length < 2) {
            pathLine.enabled = false;
            return;
        }

        pathLine.enabled = true;
        pathLine.positionCount = pts.Length;
        pathLine.SetPositions(pts);

        var gradient = new Gradient();
        gradient.SetKeys(
            new[] {
                new GradientColorKey(approachColor, 0f),
                new GradientColorKey(arcColor, 0.5f),
                new GradientColorKey(exitColor, 1f),
            },
            new[] {
                new GradientAlphaKey(approachColor.a, 0f),
                new GradientAlphaKey(arcColor.a, 0.5f),
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

    private void OnDrawGizmosSelected() {
        if (!drawEditorGizmos) return;
        if (MarsTf == null) return;

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(MarsTf.position, EffectiveRadius);

        if (Application.isPlaying) {
            float entryR = EntryRange;
            Gizmos.color = new Color(0.4f, 1f, 0.5f, 0.4f);
            Gizmos.DrawWireSphere(MarsTf.position, entryR);
            Gizmos.color = new Color(1f, 0.6f, 0.4f, 0.4f);
            Gizmos.DrawWireSphere(MarsTf.position, entryR * exitRangeMultiplier);

            Vector3[] pts = GetPathPoints();
            for (int i = 0; i < pts.Length - 1; i++) {
                float t = i / (float)Mathf.Max(1, pts.Length - 1);
                Gizmos.color = t < 0.5f
                    ? Color.Lerp(approachColor, arcColor, t * 2f)
                    : Color.Lerp(arcColor, exitColor, (t - 0.5f) * 2f);
                Gizmos.DrawLine(pts[i], pts[i + 1]);
            }
        }
    }
}
