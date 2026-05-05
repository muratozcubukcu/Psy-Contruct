using System;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

public class OrbitAssist : MonoBehaviour {
    public static event EventHandler OnEnteredOrbit;

    [SerializeField] private Rigidbody2D rb;
    [SerializeField] private float orbitRadius;
    [SerializeField] private float transitionSpeed;
    [SerializeField] private float orbitSpeed;
    [SerializeField] private bool clockwiseOrbit;
    [SerializeField] private bool transitioningToOrbit;
    [SerializeField] private bool faceMovementDirectionWhileInOrbit;
    
    private Transform psycheAsteroid;
    private float angle = 0f;
    private bool inOrbit = false;
    private Quaternion targetRotation;
    private int rotationOffset;
    private int nonOrbitAssistVelocityDamper = 10;
    
    private void Start() {
        GameInput.Instance.OnEnginePerformedAction += GameInput_OnEngineAction;
        GameInput.Instance.OnEngineCanceledAction += GameInput_OnEngineAction;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }
    
    void Update() {
        //if (transitioningToOrbit && !inOrbit) TransitionToOrbit();
        if (inOrbit) {
            Orbit();
            return;
        }
        
        if (transitioningToOrbit && (psycheAsteroid.position - transform.position).magnitude <= orbitRadius) {
            clockwiseOrbit = ClockwiseOrbit();
            psycheAsteroid.GetComponentInChildren<PlanetGravitySource>().enabled = false;
            Vector2 toShip = transform.position - psycheAsteroid.position;
            angle = Mathf.Atan2(toShip.y, toShip.x);

            inOrbit = true;
            OnEnteredOrbit?.Invoke(this, EventArgs.Empty);
        }
    }

    void TransitionToOrbit() {
        // Figure out where we are relatvie to objectToOrbit as an angle
        Vector2 toShip = transform.position - psycheAsteroid.position;
        float targetAngle = Mathf.Atan2(toShip.y, toShip.x);

        // Smoothly move angle toward the ship's current angle
        angle = Mathf.LerpAngle(angle, targetAngle * Mathf.Rad2Deg, transitionSpeed * Time.deltaTime) * Mathf.Deg2Rad;

        // Calculate psycheAsteroid orbit position at ship's current angle
        float x = Mathf.Cos(targetAngle) * orbitRadius;
        float y = Mathf.Sin(targetAngle) * orbitRadius;
        Vector3 orbitPos = psycheAsteroid.position + new Vector3(x, y, 0);

        // Gradually move toward orbit position
        transform.position = Vector3.MoveTowards(transform.position, orbitPos, transitionSpeed * Time.deltaTime);

        // Face movement direction
        Vector2 moveDir = orbitPos - transform.position;
        if (moveDir != Vector2.zero) { //Checks for divide by zero error
            float rot = Mathf.Atan2(moveDir.y, moveDir.x) * Mathf.Rad2Deg;
            targetRotation = Quaternion.Euler(0, 0, rot - 90);
            transform.rotation = Quaternion.Lerp(transform.rotation, targetRotation, transitionSpeed * Time.deltaTime);
        }

        // Check if we've reached the orbit radius
        if (Vector2.Distance(transform.position, orbitPos) < 0.05f) {
            angle = targetAngle;
            inOrbit = true;
            OnEnteredOrbit?.Invoke(this, EventArgs.Empty);
            rb.linearDamping = nonOrbitAssistVelocityDamper;
            clockwiseOrbit = ClockwiseOrbit();
            rotationOffset = clockwiseOrbit ? 180 : -90;
        }
    }

    void Orbit() {
        float direction = clockwiseOrbit ? -1f : 1f;
        angle += direction * orbitSpeed * Time.deltaTime;

        float x = Mathf.Cos(angle) * orbitRadius;
        float y = Mathf.Sin(angle) * orbitRadius;
        transform.position = psycheAsteroid.position + new Vector3(x, y);

        if (!faceMovementDirectionWhileInOrbit) return;
        
        float nextAngle = angle + direction * orbitSpeed * Time.deltaTime;
        float nextX = Mathf.Cos(nextAngle) * orbitRadius;
        float nextY = Mathf.Sin(nextAngle) * orbitRadius;
        Vector2 moveDir = new Vector2(nextX - x, nextY - y);
        float rot = Mathf.Atan2(moveDir.y, moveDir.x) * Mathf.Rad2Deg;
        
        targetRotation = Quaternion.Euler(0, 0, rot + rotationOffset);
        transform.rotation = Quaternion.Lerp(transform.rotation, targetRotation, transitionSpeed * Time.deltaTime);
    }

    private bool ClockwiseOrbit() {
        Vector3 spacecraftToPsyche = (psycheAsteroid.position - transform.position).normalized;
        Vector3 movementDir = rb.linearVelocity.normalized;

        //Checks if spacecraft is above psyche
        if (spacecraftToPsyche.y < 0f) return movementDir.x > spacecraftToPsyche.x;
        return movementDir.x < spacecraftToPsyche.x;
    }

    public void GetPsycheAsteroid() {
        psycheAsteroid = PsycheAsteroid.Instance.transform;
        
        PlanetGravitySource psycheGravity = psycheAsteroid.GetComponentInChildren<PlanetGravitySource>();
        psycheGravity.OnEnterGravityRange += PlanetGravitySource_OnGravityCrossBorder;
    }
    private void GameInput_OnEngineAction(object sender, GameInput.EngineEventArgs e) {
        if (!inOrbit) return;

        rb.linearDamping = e.activated ? 0 : nonOrbitAssistVelocityDamper;
    }
    
    private void PlanetGravitySource_OnGravityCrossBorder(object sender, PlanetGravitySource.GravityEventArgs e) {
        if (e.entering && EnteringOrbitSmoothly()) transitioningToOrbit = true;
    }

    private bool EnteringOrbitSmoothly() {
        if (GetApproachingAngle() < 15f) return false;
        return true;
    }

    private float GetApproachingAngle() {
        Vector3 spacecraftToPsyche = (psycheAsteroid.position - transform.position).normalized;
        Vector3 movementDir = rb.linearVelocity.normalized;

        return Vector3.Angle(spacecraftToPsyche, movementDir);
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode) {
        if (scene.name != "FlightScene") {
            transitioningToOrbit = false;
            inOrbit = false;
        }
    }
}
