using System;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(CircleCollider2D))]
public class PlanetGravitySource : MonoBehaviour
{
    [Header("Gravity Zone")]
    [SerializeField] private float gravityRadius = 200f;

    [Header("Gravity Strength")]
    [SerializeField] private float nearPlanetGravity = 9.81f;

    [Tooltip("Minimum distance clamp to prevent extreme forces")]
    [SerializeField] private float nearDistance = 5f;

    [Tooltip("Use inverse square gravity falloff")]
    [SerializeField] private bool useInverseSquare = false;

    [Header("Affected Objects")]
    [Tooltip("Only objects on these layers will be affected by gravity")]
    [SerializeField] private LayerMask affectedLayers;

    private readonly HashSet<Rigidbody2D> bodiesInZone = new HashSet<Rigidbody2D>();
    private CircleCollider2D gravityTrigger;
    private int spacecraftLayerIndex;
    
    public class GravityEventArgs : System.EventArgs {
        public bool entering;
        public GravityEventArgs(bool entering) => this.entering = entering;
    }
    public event EventHandler<GravityEventArgs> OnEnterGravityRange;

    private void Awake()
    {
        gravityTrigger = GetComponent<CircleCollider2D>();
        gravityTrigger.isTrigger = true;
        gravityTrigger.radius = gravityRadius;
        spacecraftLayerIndex = LayerMask.NameToLayer("SpaceCraft");
    }

    private void FixedUpdate()
    {
        if (bodiesInZone.Count == 0) return;

        Vector2 planetCenter = transform.position;
        bool spacecraftGravityApplied = false;

        foreach (Rigidbody2D rb in bodiesInZone)
        {
            if (rb == null)
            {
                continue;
            }

            // For spacecraft parts, apply gravity only once to the main body
            if (rb.gameObject.layer == spacecraftLayerIndex)
            {
                if (spacecraftGravityApplied) continue;
                spacecraftGravityApplied = true;

                Spacecraft spacecraft = Spacecraft.GetInstance();
                if (spacecraft == null) continue;
                Rigidbody2D mainRb = spacecraft.GetComponent<Rigidbody2D>();
                if (mainRb == null) continue;

                ApplyGravity(mainRb, planetCenter);
                continue;
            }

            ApplyGravity(rb, planetCenter);
        }
    }

    private void ApplyGravity(Rigidbody2D rb, Vector2 planetCenter)
    {
        Vector2 direction = planetCenter - rb.position;
        float distance = direction.magnitude;

        if (distance < 0.001f) return;

        direction.Normalize();
        float acceleration = CalculateGravity(distance);
        rb.AddForce(direction * acceleration * rb.mass, ForceMode2D.Force);
    }

    private float CalculateGravity(float distance)
    {
        float clampedDistance = Mathf.Max(distance, nearDistance);

        if (useInverseSquare)
        {
            float ratio = (nearDistance * nearDistance) /
                          (clampedDistance * clampedDistance);

            return nearPlanetGravity * ratio;
        }

        float t = Mathf.InverseLerp(nearDistance, gravityRadius, clampedDistance);

        float smooth = 1f - (t * t * (3f - 2f * t));

        return nearPlanetGravity * smooth;
    }

    /// <summary>Acceleration this gravity source applies at worldPos. Zero outside the gravity radius.</summary>
    public Vector2 GetAccelerationAt(Vector2 worldPos)
    {
        Vector2 toPlanet = (Vector2)transform.position - worldPos;
        float distance = toPlanet.magnitude;
        if (distance < 0.001f || distance > gravityRadius) return Vector2.zero;
        return (toPlanet / distance) * CalculateGravity(distance);
    }

    public bool IsEnabled => enabled && gameObject.activeInHierarchy;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (((1 << other.gameObject.layer) & affectedLayers) == 0)
        {
            return;
        }

        Rigidbody2D rb = other.attachedRigidbody;

        if (rb == null)
        {
            return;
        }
        
        OnEnterGravityRange?.Invoke(this, new GravityEventArgs(true));

        bodiesInZone.Add(rb);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (((1 << other.gameObject.layer) & affectedLayers) == 0)
        {
            return;
        }

        Rigidbody2D rb = other.attachedRigidbody;

        if (rb == null)
        {
            return;
        }
        
        OnEnterGravityRange?.Invoke(this, new GravityEventArgs(false));

        bodiesInZone.Remove(rb);
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, gravityRadius);
    }
#endif
}
