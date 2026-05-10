using System;
using System.Collections.Generic;
using UnityEngine;

// =============================================================================
// MarsSlingshotPlanner
// -----------------------------------------------------------------------------
// What it does:
//   Plans and draws a slingshot path that swings the ship around Mars and
//   ends pointed at Psyche. The path appears as a colored line on the minimap.
//
// How it works:
//   When the ship gets close enough to Mars (enters the gravity range), we
//   freeze a single curve in place using real orbit math (a "conic section").
//   The curve goes: ship -> swing around Mars -> Psyche. We don't recompute
//   the curve every frame.
//   If the math is too unstable to find a clean curve, we fall back to a
//   simple "go around the circle" shape so something is always drawn.
//
// How to use it:
//   1. Drop this component on a GameObject in the flight scene.
//   2. Either fill in the Mars/Psyche/Spacecraft transform overrides in the
//      inspector, or rely on the auto-lookup (Mars.Instance, etc.).
//   3. Set 'periapsisRadius' (closest approach to Mars) or tick
//      'useMarsColliderRadius' to derive it from Mars's collider.
//   4. Other systems call GetPathPoints() to read the current path as an
//      array of world positions.
//   5. Call FreezePath() to lock the current shape and ignore further updates;
//      UnfreezePath() to resume the normal entry/exit logic.
// =============================================================================

/// <summary>Snapshot-on-entry slingshot planner: captures a Keplerian conic when ship enters Mars's range and holds it until exit.</summary>
public class MarsSlingshotPlanner : MonoBehaviour {

    public static event System.Action OnSlingshotEntered;
    public static event System.Action OnSlingshotExited;

    // Closest distance the curve is allowed to come to Mars. Used as a fallback
    // when 'useMarsColliderRadius' is off or no collider is found.
    [SerializeField] private float periapsisRadius = 5f;
    // If true, we read Mars's CircleCollider2D and use that radius (plus a
    // safety margin) so the curve never clips into the planet sprite.
    [SerializeField] private bool useMarsColliderRadius = true;
    // Extra distance added on top of the collider radius - keeps the curve
    // from grazing the planet's edge.
    [SerializeField] private float radiusMargin = 10f;

    [Header("Snapshot Trigger")]
    // CircleCollider2D whose radius defines the entry distance for snapshotting.
    // the slingshot scoring fires at the same boundary as the minimap reveal.
    [SerializeField] private CircleCollider2D entryCollider;
    
    // The exit distance is the entry distance times this number. Having
    // exit > entry creates "hysteresis" so the path doesn't flicker on/off
    // when the ship hovers right at the boundary.
    [Range(1f, 3f)]
    [SerializeField] private float exitRangeMultiplier = 1.05f;

    [Header("Sampling")]
    // How many segments make up the curve. More = smoother, but more work.
    [SerializeField] private int conicSegments = 96;

    [Header("Visualization")]
    // Show/hide the in-game line. Other code can flip DrawRuntimePath at runtime.
    [SerializeField] private bool drawRuntimePath = true;
    // Show/hide the editor-only gizmo preview when this object is selected.
    [SerializeField] private bool drawEditorGizmos = true;
    // Color of the first part of the line (approach to Mars).
    [SerializeField] private Color approachColor = new Color(1f, 0.4f, 0.4f, 1f);
    // Color of the middle of the line (the slingshot arc itself).
    [SerializeField] private Color arcColor = new Color(1f, 0.85f, 0.2f, 1f);
    // Color of the last part of the line (heading toward Psyche).
    [SerializeField] private Color exitColor = new Color(0.4f, 1f, 0.5f, 1f);

    [Tooltip("How many degrees off the ideal orbit direction the ship can be and still count as viable.")]
    [SerializeField] private float headingToleranceDegrees = 15f;

    // The LineRenderer that draws the curve in the game world. Created lazily.
    private LineRenderer pathLine;

    // Public read-only access to the resolved transforms (for other systems).
    public Transform MarsTransform => MarsTf;
    public Transform PsycheTransform => PsycheTf;
    public Transform ShipTransform => ShipTf;

    /// <summary>External on/off switch for the in-world line. UI/settings can flip this.</summary>
    public bool DrawRuntimePath {
        get => drawRuntimePath;
        set => drawRuntimePath = value;
    }

    /// <summary>External on/off switch for the editor gizmos (the cyan/green/orange wire spheres).</summary>
    public bool DrawEditorGizmos {
        get => drawEditorGizmos;
        set => drawEditorGizmos = value;
    }

    // The cached curve points. Filled in when the ship enters range, cleared
    // when it leaves. Returned by GetPathPoints().
    private Vector3[] snapshotPath;
    // True while the ship is currently inside Mars's gravity range.
    private bool inMarsRange;

    // If something external called FreezePath(), we ignore range checks and
    // just keep returning the frozen array no matter what.
    private bool externallyFrozen;
    private Vector3[] externallyFrozenPath;

    private bool lastPathWasConic = false;

    private Conic? snapshotConic = null;

    /// <summary>True while FreezePath() has locked the path in place.</summary>
    public bool IsFrozen => externallyFrozen;

    /// <summary>Lock the current path so it stops updating. Useful for cutscenes
    /// or "commit to maneuver" UI.</summary>
    public void FreezePath() {
        externallyFrozenPath = GetPathPoints();
        externallyFrozen = true;
    }

    /// <summary>Resume normal range-based snapshotting after FreezePath().</summary>
    public void UnfreezePath() {
        externallyFrozen = false;
        externallyFrozenPath = null;
    }

    /// <summary>True if we currently have a usable path (at least 2 points).</summary>
    public bool IsSolutionValid => snapshotPath != null && snapshotPath.Length >= 2;

    public bool InSlingshotRange => inMarsRange;

    public float DistanceFromPath(Vector2 worldPos) {
        if (snapshotPath == null || snapshotPath.Length < 2) return -1f;

        float bestSqr = float.MaxValue;
        for (int i = 0; i < snapshotPath.Length - 1; i++) {
            Vector2 a = snapshotPath[i];
            Vector2 b = snapshotPath[i + 1];
            float dSqr = NearestPointDistanceSqr(worldPos, a, b);
            if (dSqr < bestSqr) bestSqr = dSqr;
        }
        return Mathf.Sqrt(bestSqr);
    }

    private static float NearestPointDistanceSqr(Vector2 p, Vector2 a, Vector2 b) {
        Vector2 ab = b - a;
        float lenSqr = ab.sqrMagnitude;
        if (lenSqr < 1e-6f) return (p - a).sqrMagnitude;
        float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / lenSqr);
        Vector2 proj = a + ab * t;
        return (p - proj).sqrMagnitude;
    }

    public bool IsSlingshotViable {
        get {
            if (MarsTf == null || ShipTf == null || PsycheTf == null) return false;

            Vector2 ship = ShipPos;
            Vector2 mars = MarsTf.position;
            Vector2 psy = PsychePos;

            Conic conic;
            if (inMarsRange && lastPathWasConic && snapshotConic != null) {
                conic = snapshotConic.Value;
            } else if (TrySolveConic(ship, mars, psy, out conic)) {
                // Outside the range still predict if the player is going the right direction
                // so it isnt a supreise for them when they enter the range.
            } else {
                return false;
            }

            return IsHeadingAligned(conic, ship, mars);
        }
    }

    // Singleton lookups
    private Transform MarsTf => Mars.Instance != null ? Mars.Instance.transform : null;
    private Transform PsycheTf => PsycheAsteroid.Instance != null ? PsycheAsteroid.Instance.transform : null;
    private Transform ShipTf {
        get {
            Spacecraft s = Spacecraft.GetInstance();
            return s != null ? s.transform : null;
        }
    }

    // Picks the actual closest-approach radius the math will use.
    // If allowed, reads it from Mars's collider so it scales with the planet sprite.
    private float EffectiveRadius {
        get {
            if (!useMarsColliderRadius || MarsTf == null) return periapsisRadius;
            CircleCollider2D col = MarsTf.GetComponentInChildren<CircleCollider2D>();
            if (col == null) return periapsisRadius;
            float scale = Mathf.Max(MarsTf.lossyScale.x, MarsTf.lossyScale.y);
            return col.radius * scale + radiusMargin;
        }
    }

    // Reads the ship's current position. Prefers the Rigidbody2D position
    // (more accurate during physics) over the transform position.
    private Vector2 ShipPos {
        get {
            Transform t = ShipTf;
            if (t == null) return Vector2.zero;
            Rigidbody2D rb = t.GetComponent<Rigidbody2D>();
            return rb != null ? rb.position : (Vector2)t.position;
        }
    }

    // Same for Psyche - rigidbody if available, otherwise transform.
    private Vector2 PsychePos {
        get {
            Transform t = PsycheTf;
            if (t == null) return Vector2.zero;
            Rigidbody2D rb = t.GetComponent<Rigidbody2D>();
            return rb != null ? rb.position : (Vector2)t.position;
        }
    }

    private float entryRange;

    /// <summary>
    /// Main public method. Returns the current slingshot path as world-space points.
    /// Use this from other systems (LineRenderer, AI) to read the path.
    /// Returns an empty array when the ship is out of range or any required
    /// transform is missing. The first point is the ship, the last is Psyche.
    /// </summary>
    public Vector3[] GetPathPoints() {
        // If FreezePath() was called, just hand back the frozen copy.
        if (externallyFrozen) return externallyFrozenPath ?? System.Array.Empty<Vector3>();
        // Sanity check: we need all three transforms to compute anything.
        if (MarsTf == null || ShipTf == null || PsycheTf == null) return System.Array.Empty<Vector3>();

        // Snapshot the current world positions of the three actors.
        Vector2 ship = ShipPos;
        Vector2 psy = PsychePos;
        Vector2 mars = MarsTf.position;

        // Distance from ship to Mars right now.
        float dShip = (ship - mars).magnitude;
        // Distance at which we should compute a new path (entering range).
        float entryR = entryRange;
        // Distance at which we should clear the path (leaving range).
        float exitR = entryR * exitRangeMultiplier;

        if (!inMarsRange && dShip <= entryR) {
            // Just entered Mars's range - build the curve and remember it.
            snapshotPath = BuildSlingshotPlan(ship, mars, psy);
            inMarsRange = true;
            OnSlingshotEntered?.Invoke();
        } else if (inMarsRange && dShip >= exitR) {
            snapshotPath = null;
            inMarsRange = false;
            lastPathWasConic = false;
            snapshotConic = null;
            OnSlingshotExited?.Invoke();
        }
        // While inside the range we keep returning the same cached path.

        if (snapshotPath == null || snapshotPath.Length < 2) return System.Array.Empty<Vector3>();
        return snapshotPath;
    }

    // Tries the precise orbital math first, falls back to a simple geometric
    // arc if the math fails (degenerate case, ship inside Mars, etc.).
    private Vector3[] BuildSlingshotPlan(Vector2 ship, Vector2 mars, Vector2 psy) {
        if (TrySolveConic(ship, mars, psy, out Conic c)) {
            lastPathWasConic = true;
            snapshotConic = c;
            return SampleConic(c, ship, mars, psy);
        }
        lastPathWasConic = false;
        snapshotConic = null;
        return BuildGeometricArc(ship, mars, psy);
    }

    private bool IsHeadingAligned(Conic c, Vector2 ship, Vector2 mars) {
        Vector2 toShip = ship - mars;
        float theta_s = Mathf.Atan2(toShip.y, toShip.x);
        float nu_s = WrapPi(theta_s - c.omega);

        // Radial and tangential unit vectors at the ship's position on the orbit.
        Vector2 radial = new Vector2(Mathf.Cos(theta_s), Mathf.Sin(theta_s));
        Vector2 tangential = new Vector2(-Mathf.Sin(theta_s), Mathf.Cos(theta_s));

        // Velocity direction on the conic at this point (no speed needed, just direction).
        float vr = c.e * Mathf.Sin(nu_s);
        float vt = 1f + c.e * Mathf.Cos(nu_s);

        // Geometric rotation pick avoids the feedback loop where heading is compared against
        // an orbit whose direction was itself picked from heading.
        float rotSign = PickRotationSign(ship, mars, PsychePos);
        if (rotSign < 0f) tangential = -tangential;

        Vector2 orbitDir = (radial * vr + tangential * vt).normalized;
        if (orbitDir.sqrMagnitude < 1e-6f) return false;

        // Compare against actual movement, not facing. The trajectory line is
        // drawn from linearVelocity, so the green path must agree with the
        // visible path, rotating the ship in place must not flip the color.
        Vector2 vel = ShipVelocity;
        if (vel.sqrMagnitude < 1e-4f) return false;
        Vector2 heading = vel.normalized;

        float angle = Mathf.Acos(Mathf.Clamp(Vector2.Dot(orbitDir, heading), -1f, 1f)) * Mathf.Rad2Deg;
        float trueAngle = Math.Abs(angle - 180);

        return trueAngle <= headingToleranceDegrees;
    }

    // A "conic section" - the family of curves that includes ellipses,
    // parabolas, and hyperbolas. Real orbits are conics centered on the planet.
    //   p     = how big the orbit is
    //   e     = how stretched it is (0 = circle, ~1 = elongated)
    //   omega = rotation of the orbit in world space
    private struct Conic {
        public float p, e, omega;
    }

    // Pure-geometry rotation pick: which side of Mars is Psyche on relative
    // to the ship? Used as a fallback when motion-based picking can't decide.
    private static float PickRotationSign(Vector2 ship, Vector2 mars, Vector2 psy) {
        Vector2 marsToShip = ship - mars;
        Vector2 marsToPsy = psy - mars;
        // Cross product sign tells us "left of" vs "right of".
        float cross = marsToPsy.x * marsToShip.y - marsToPsy.y * marsToShip.x;
        return cross >= 0f ? +1f : -1f;
    }

    /// <summary>
    /// Orbital rotation sign from the ship's effective heading about Mars (r × heading).
    /// Heading is velocity when the ship is moving, otherwise the ship's facing direction
    /// (transform.up) so the path matches player intent on slow entries.
    /// Falls back to the geometric Psyche-based sign only as a last resort.
    /// </summary>
    private float PickRotationSignFromMotion(Vector2 ship, Vector2 mars, Vector2 psy) {
        // Start from the player's heading (velocity or facing).
        Vector2 heading = EffectiveHeading;
        float sign;
        if (heading.sqrMagnitude > 1e-6f) {
            // Vector from Mars to the ship.
            Vector2 r = ship - mars;
            // Angular momentum sign: positive = counter-clockwise around Mars.
            float angMomZ = r.x * heading.y - r.y * heading.x;
            if (Mathf.Abs(angMomZ) > 1e-4f) {
                sign = angMomZ >= 0f ? +1f : -1f;
                return invertSlingshotSide ? -sign : sign;
            }
        }
        // Heading was too small to be meaningful - fall back to geometry.
        sign = PickRotationSign(ship, mars, psy);
        return invertSlingshotSide ? -sign : sign;
    }

    /// <summary>How to decide which side of Mars the curve wraps around.</summary>
    public enum HeadingSource { FacingOnly, VelocityOnly, FacingPreferred, VelocityPreferred }

    [Header("Heading Source")]
    [Tooltip("Which signal drives which side of Mars the path wraps. FacingOnly = always use transform.up.")]
    // Which input we use to figure out which way the ship is going.
    [SerializeField] private HeadingSource headingSource = HeadingSource.FacingPreferred;
    [Tooltip("Velocity² threshold for the *Preferred* modes; below this the secondary signal is used instead.")]
    // When the ship is moving slower than this, we trust the alternate signal.
    [SerializeField] private float velocityHeadingThresholdSqr = 4f;
    [Tooltip("Flip which side of Mars the path wraps around. Toggle if the visualization is mirrored.")]
    // Quick fix if the curve always picks the wrong side - just flip it.
    [SerializeField] private bool invertSlingshotSide = false;

    // Reads the ship's current velocity from its rigidbody (if any).
    private Vector2 ShipVelocity {
        get {
            Transform t = ShipTf;
            if (t == null) return Vector2.zero;
            Rigidbody2D rb = t.GetComponent<Rigidbody2D>();
            return rb != null ? rb.linearVelocity : Vector2.zero;
        }
    }

    // Returns the heading vector we'll use for picking the curve side.
    // Different modes prioritize "where the ship is pointing" vs "where it
    // is actually moving" - useful when the ship is drifting backwards.
    private Vector2 EffectiveHeading {
        get {
            Transform t = ShipTf;
            Vector2 facing = t != null ? (Vector2)t.up : Vector2.zero;
            Vector2 v = ShipVelocity;
            switch (headingSource) {
                case HeadingSource.FacingOnly:
                    // Always use which way the ship's nose is pointing.
                    return facing;
                case HeadingSource.VelocityOnly:
                    // Always use the actual movement direction.
                    return v;
                case HeadingSource.VelocityPreferred:
                    // Prefer velocity; use facing only if barely moving.
                    return v.sqrMagnitude > velocityHeadingThresholdSqr ? v.normalized : facing;
                case HeadingSource.FacingPreferred:
                default:
                    // Prefer facing; fall back to velocity if there's no facing.
                    return facing.sqrMagnitude > 1e-6f ? facing
                        : (v.sqrMagnitude > velocityHeadingThresholdSqr ? v.normalized : v);
            }
        }
    }

    // Given a candidate orbit, which way does it travel from ship to Psyche?
    // Used to pick between the two valid conic solutions.
    private static float OrbitalDirectionSign(float omega, float theta_s, float theta_p) {
        float nu_s = WrapPi(theta_s - omega);
        float nu_p = WrapPi(theta_p - omega);
        float diff = nu_p - nu_s;
        return diff >= 0f ? +1f : -1f;
    }

    // Solves for an orbit (conic section) that passes through both the ship
    // and Psyche, with closest-approach distance equal to EffectiveRadius.
    // Returns true with a usable curve, or false if no good curve exists.
    private bool TrySolveConic(Vector2 ship, Vector2 mars, Vector2 psy, out Conic conic) {
        conic = default;
        // Vectors from Mars to the ship and to Psyche.
        Vector2 toShip = ship - mars;
        Vector2 toPsy = psy - mars;
        float r_s = toShip.magnitude;
        float r_psy = toPsy.magnitude;
        float r_peri = EffectiveRadius;
        // If either body is inside the closest-approach radius, the math doesn't apply.
        if (r_s <= r_peri || r_psy <= r_peri) return false;

        // Setup: ratios of perigee distance to body distances.
        float A_s = r_peri / r_s;
        float A_p = r_peri / r_psy;
        // Angles of ship and Psyche around Mars.
        float theta_s = Mathf.Atan2(toShip.y, toShip.x);
        float theta_p = Mathf.Atan2(toPsy.y, toPsy.x);

        // The math reduces to solving for an angle. B and C are the working
        // variables that come out of plugging both (r, theta) pairs into the
        // conic equation r = p / (1 + e*cos(theta - omega)).
        float B = (A_s - 1f) * Mathf.Cos(theta_p) - (A_p - 1f) * Mathf.Cos(theta_s);
        float C = (A_s - 1f) * Mathf.Sin(theta_p) - (A_p - 1f) * Mathf.Sin(theta_s);
        float R = Mathf.Sqrt(B * B + C * C);
        // If R is tiny, the geometry is degenerate (ship and Psyche basically the same direction).
        if (R < 1e-6f) return false;

        // The acos() below has no real solution if |ratio| > 1. That means
        // there's no orbit with this perigee through both points - bail out.
        float ratio = (A_s - A_p) / R;
        if (Mathf.Abs(ratio) > 1f + 1e-4f) return false;
        ratio = Mathf.Clamp(ratio, -1f, 1f);

        float phi = Mathf.Atan2(C, B);
        float ac = Mathf.Acos(ratio);

        // Two possible orbits satisfy the geometry. We try both.
        Conic candA = BuildConicFromOmega(phi + ac, theta_s, A_s, r_peri);
        Conic candB = BuildConicFromOmega(phi - ac, theta_s, A_s, r_peri);

        // For a slingshot, periapsis (closest approach) must be BETWEEN ship
        // and Psyche along the orbit, not behind one of them.
        bool aValid = candA.e > 0f && PeriapsisBetween(candA.omega, theta_s, theta_p);
        bool bValid = candB.e > 0f && PeriapsisBetween(candB.omega, theta_s, theta_p);

        // No good options - give up and let the caller use the fallback arc.
        if (!aValid && !bValid) return false;
        // Only one option - use it.
        if (aValid && !bValid) { conic = candA; return true; }
        if (bValid && !aValid) { conic = candB; return true; }

        // Both options are geometrically valid - pick the one whose travel
        // direction matches what the player intends (their heading).
        float rotSign = PickRotationSignFromMotion(ship, mars, psy);
        float dirA = OrbitalDirectionSign(candA.omega, theta_s, theta_p);
        float dirB = OrbitalDirectionSign(candB.omega, theta_s, theta_p);
        if (Mathf.Sign(dirA) == Mathf.Sign(rotSign)) { conic = candA; return true; }
        if (Mathf.Sign(dirB) == Mathf.Sign(rotSign)) { conic = candB; return true; }
        // Last resort.
        conic = candA;
        return true;
    }

    // Builds a complete Conic from the rotation angle 'omega' plus one
    // measured (r, theta) point on it. Returns e = -1 to flag a bad solve.
    private static Conic BuildConicFromOmega(float omega, float theta_s, float A_s, float r_peri) {
        float nu_s = theta_s - omega;
        float denom = Mathf.Cos(nu_s) - A_s;
        if (Mathf.Abs(denom) < 1e-6f) return new Conic { e = -1f };
        float e = (A_s - 1f) / denom;
        return new Conic { p = r_peri * (1f + e), e = e, omega = omega };
    }

    // True if Mars's "closest approach point" (periapsis) is between the
    // ship and Psyche when traveling along the orbit.
    private static bool PeriapsisBetween(float omega, float theta_s, float theta_p) {
        float nu_s = WrapPi(theta_s - omega);
        float nu_p = WrapPi(theta_p - omega);
        // Opposite signs = the two angles are on opposite sides of periapsis.
        return nu_s * nu_p < 0f;
    }

    // Forces an angle into the range (-pi, pi]. Plain modulo math doesn't
    // handle the wraparound cleanly, hence the loops.
    private static float WrapPi(float a) {
        while (a > Mathf.PI) a -= 2f * Mathf.PI;
        while (a < -Mathf.PI) a += 2f * Mathf.PI;
        return a;
    }

    // Walks along the conic from ship to Psyche in equal-angle steps and
    // converts each (r, theta) into a world-space point. The resulting array
    // is the actual curve that gets drawn.
    private Vector3[] SampleConic(Conic c, Vector2 ship, Vector2 mars, Vector2 psy) {
        Vector2 toShip = ship - mars;
        Vector2 toPsy = psy - mars;
        float theta_s = Mathf.Atan2(toShip.y, toShip.x);
        float theta_p = Mathf.Atan2(toPsy.y, toPsy.x);
        // True anomalies (angle measured from periapsis) at start and end.
        float nu_s = WrapPi(theta_s - c.omega);
        float nu_p = WrapPi(theta_p - c.omega);

        int segs = Mathf.Max(8, conicSegments);
        var pts = new Vector3[segs + 1];
        // Pin the first point to the ship's exact position so the line starts
        // there cleanly with no visible snap.
        pts[0] = ship;
        int filled = 1;
        for (int i = 1; i <= segs; i++) {
            float t = i / (float)segs;
            float nu = Mathf.Lerp(nu_s, nu_p, t);
            // Polar form of a conic: r = p / (1 + e*cos(nu)).
            float r = c.p / (1f + c.e * Mathf.Cos(nu));
            // Bail out if the math went weird (negative or huge radius).
            if (r <= 0f || r > 1e6f) {
                System.Array.Resize(ref pts, filled);
                return pts;
            }
            float theta = c.omega + nu;
            // Convert (r, theta) back to a world position around Mars.
            pts[i] = mars + new Vector2(Mathf.Cos(theta), Mathf.Sin(theta)) * r;
            filled = i + 1;
        }
        // Pin the last point to Psyche's exact position.
        pts[pts.Length - 1] = psy;
        return pts;
    }

    // Fallback "dumb" path used when the conic math fails. We draw straight
    // lines from ship and Psyche to tangent points on a circle around Mars,
    // then trace along that circle between the two tangents. Not real orbital
    // physics, but it always produces a valid curving path.
    private Vector3[] BuildGeometricArc(Vector2 ship, Vector2 mars, Vector2 psy) {
        float r = EffectiveRadius;
        float dShip = (ship - mars).magnitude;
        float dPsy = (psy - mars).magnitude;
        // If either point is inside the circle there's no tangent to draw.
        if (dShip <= r || dPsy <= r) return new Vector3[] { ship, psy };

        // Pick which side of Mars to wrap around.
        float sign = PickRotationSignFromMotion(ship, mars, psy);
        Vector2 toShipDir = (ship - mars) / dShip;
        Vector2 toPsyDir = (psy - mars) / dPsy;
        // Perpendicular vectors used to find tangent points.
        Vector2 perpShip = new Vector2(-toShipDir.y * sign, toShipDir.x * sign);
        Vector2 perpPsy = new Vector2(toPsyDir.y * sign, -toPsyDir.x * sign);
        // Standard right-triangle math for tangent lines from an outside point.
        float cosTs = r / dShip, sinTs = Mathf.Sqrt(Mathf.Max(0f, 1f - cosTs * cosTs));
        float cosTp = r / dPsy, sinTp = Mathf.Sqrt(Mathf.Max(0f, 1f - cosTp * cosTp));
        // Tangent point on Mars's circle from the ship's side.
        Vector2 entry = mars + r * (toShipDir * cosTs + perpShip * sinTs);
        // Tangent point on Mars's circle from Psyche's side.
        Vector2 exit = mars + r * (toPsyDir * cosTp + perpPsy * sinTp);

        // Walk the circle from entry to exit in the chosen direction.
        float entryAngle = Mathf.Atan2(entry.y - mars.y, entry.x - mars.x);
        float exitAngle = Mathf.Atan2(exit.y - mars.y, exit.x - mars.x);
        float sweep = exitAngle - entryAngle;
        // Make sure we go the short way around in the right direction.
        if (sign > 0f && sweep < 0f) sweep += 2f * Mathf.PI;
        if (sign < 0f && sweep > 0f) sweep -= 2f * Mathf.PI;

        int segs = Mathf.Max(16, conicSegments);
        var pts = new List<Vector3>(segs + 4);
        // Build the path: ship -> tangent in -> arc segments -> tangent out -> Psyche.
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
        // Make sure there's a LineRenderer ready if we want to draw the path.
        if (drawRuntimePath) EnsureLineRenderer();
    }

    private void Start() {
        entryRange = entryCollider.radius;
    }

    private void LateUpdate() {
        // Hide the line if drawing is turned off.
        if (!drawRuntimePath) {
            if (pathLine != null) pathLine.enabled = false;
            return;
        }

        // Make sure the line renderer exists and is configured.
        EnsureLineRenderer();

        if (ShipTf == null) return;

        // Pull the current path from our own public method.
        Vector3[] pts = GetPathPoints();
        if (pts.Length < 2) {
            // Not in range, or no valid path - hide the line.
            pathLine.enabled = false;
            return;
        }

        // Push the points into the line renderer.
        pathLine.enabled = true;
        pathLine.positionCount = pts.Length;
        pathLine.SetPositions(pts);

        // Build a 3-stop color gradient: approach -> arc -> exit.
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

    // Lazy setup: grabs or creates a LineRenderer with the right settings.
    // Called from Awake and from LateUpdate (in case the component was added at runtime).
    private void EnsureLineRenderer() {
        if (pathLine == null) {
            pathLine = GetComponent<LineRenderer>();
            if (pathLine == null) pathLine = gameObject.AddComponent<LineRenderer>();
            pathLine.useWorldSpace = true;
            // Sprites/Default supports the per-vertex gradient color.
            pathLine.material = new Material(Shader.Find("Sprites/Default"));
            // Smooth out joints and end caps so the curve looks polished.
            pathLine.numCornerVertices = 8;
            pathLine.numCapVertices = 8;
            // High sortingOrder so the line draws over planets, not under them.
            pathLine.sortingOrder = 100;
        }
    }

    // Editor-only preview. Shows the closest-approach circle, the entry and
    // exit ranges, and the path itself - but only when this object is selected.
    private void OnDrawGizmosSelected() {
        if (!drawEditorGizmos) return;
        if (MarsTf == null) return;

        // Cyan ring = the closest the curve will come to Mars.
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(MarsTf.position, EffectiveRadius);

        // Range gizmos only make sense at runtime (positions are static otherwise).
        if (Application.isPlaying) {
            float entryR = entryRange;
            // Green = where the snapshot fires.
            Gizmos.color = new Color(0.4f, 1f, 0.5f, 0.4f);
            Gizmos.DrawWireSphere(MarsTf.position, entryR);
            // Orange = where the snapshot clears.
            Gizmos.color = new Color(1f, 0.6f, 0.4f, 0.4f);
            Gizmos.DrawWireSphere(MarsTf.position, entryR * exitRangeMultiplier);

            // Draw the path itself with the same gradient feel as the runtime line.
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
