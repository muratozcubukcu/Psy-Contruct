using UnityEngine;
using System;
using System.Collections;

/// <summary>
/// Camera script that locks onto and follows the spacecraft in FlightScene.
/// This is attached to the camera in the flight scene.
/// </summary>
public class FlightCamera : MonoBehaviour {
    
    private const float DEFAULT_CAM_WIDTH = 3.55f;
    private const float DEFAULT_CAM_HEIGHT = 2f;
    public static event EventHandler<AsteroidPassingEventArgs> OnAsteroidPassing;
    
    public class AsteroidPassingEventArgs : EventArgs {
        public GameObject asteroid;
        public bool isEntering;
    
        public AsteroidPassingEventArgs(GameObject asteroid, bool isEntering) {
            this.asteroid = asteroid;
            this.isEntering = isEntering;
        }
    }

    [SerializeField] private float transitionToPsycheSpeed;
    
    [Tooltip("Offset from the target position")]
    [SerializeField] private Vector3 offset;
    [SerializeField] private float distanceMultiplier;
    [SerializeField] private float velocityResponsiveness;
    
    private Transform target;
    private Transform psycheAsteroid;
    private Transform spacecraft;
    private Rigidbody2D shipRB;
    private bool transitionToPsyche = false;
    private Vector3 currentOffset;
    private Vector3 maxOffset;
    private Vector3 minOffset;
    
    private void Awake() {
        spacecraft = Spacecraft.GetInstance().transform;
        psycheAsteroid = PsycheAsteroid.Instance.transform;
        target = spacecraft;
        shipRB = spacecraft.GetComponent<Rigidbody2D>();
        
        SetColliderSize();
    }
    
    private void Start() {
        OrbitAssist.OnEnteredOrbit += OrbitAssist_OnEnteredOrbit;
    }
    
    private void SetColliderSize() {
        BoxCollider2D boxCollider = GetComponent<BoxCollider2D>();
        

        boxCollider.size = new Vector2(GetCameraWidth() + .75f, GetCameraHeight() + .75f);
        maxOffset = new Vector3(GetCameraWidth() / 2 - 2.75f, GetCameraHeight() / 2 - 2.75f);
        minOffset = -maxOffset;
    }
    
    private void LateUpdate() {
        if (transitionToPsyche) {
            TransitionToPsyche();
            return;
        }

        if (!target) return;
        transform.position = target.position + offset + SpacecraftVelocityOffset();
    }
    
    private Vector3 SpacecraftVelocityOffset() {
        if (target == psycheAsteroid) return Vector3.zero;

        Vector3 targetOffset = shipRB.linearVelocity * distanceMultiplier;
        targetOffset.x = Math.Min(maxOffset.x, targetOffset.x);
        targetOffset.x = Math.Max(minOffset.x, targetOffset.x);
        targetOffset.y = Math.Min(maxOffset.y, targetOffset.y);
        targetOffset.y = Math.Max(minOffset.y, targetOffset.y);

        currentOffset = Vector3.Lerp(currentOffset, targetOffset, velocityResponsiveness * Time.deltaTime);

        return currentOffset;
    }

    private void TransitionToPsyche() {
        Vector3 targetPos = target.position + offset;
        
        transform.position = Vector3.Lerp(transform.position, targetPos, transitionToPsycheSpeed * Time.deltaTime);
        
        if (Vector3.Distance(transform.position, targetPos) <= .05f) transitionToPsyche = false;
    }

    private void OnTriggerEnter2D(Collider2D objectCollider) {
        GameObject otherObject = objectCollider.gameObject;
        
        //Only runs if otherObject is an asteroid
        if (otherObject.GetComponent<AsteroidFlight>() == null) return; 
        
        OnAsteroidPassing?.Invoke(this, new AsteroidPassingEventArgs(otherObject, true));
    }
    
    private void OnTriggerExit2D(Collider2D objectCollider) {
        GameObject otherObject = objectCollider.gameObject;
        
        //Only runs if otherObject is an asteroid
        if (otherObject.GetComponent<AsteroidFlight>() == null) return; 
        
        OnAsteroidPassing?.Invoke(this, new AsteroidPassingEventArgs(otherObject, false));
    }
    
    private void OrbitAssist_OnEnteredOrbit(object sender, System.EventArgs e) {
        target = psycheAsteroid;
        transitionToPsyche = true;
    }

    public float GetCameraWidth() => GetComponent<Camera>().orthographicSize * DEFAULT_CAM_WIDTH;
    
    public float GetCameraHeight() => GetComponent<Camera>().orthographicSize * DEFAULT_CAM_HEIGHT;
}