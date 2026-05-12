using System;
using UnityEngine;
using Random = System.Random;

public class AsteroidFlight : MonoBehaviour {
    [SerializeField] public int asteroidSize;
    public Vector3 direction;
    public float speed;

    private void Awake() {
        speed = UnityEngine.Random.Range(.5f, 8f);
        GetDirection();
        GetComponent<Rigidbody2D>().angularVelocity = UnityEngine.Random.Range(15f, 100f);
        GetComponent<Rigidbody2D>().linearVelocity = direction * speed;
    }

    private void GetDirection() {
        float directionRandomness = .75f;
        Spacecraft spacecraft = Spacecraft.GetInstance();
        Vector2 spacecraftVel = spacecraft.GetComponent<Rigidbody2D>().linearVelocity;
        
        float distanceToSpacecraft = (spacecraft.transform.position - transform.position).magnitude;
        float timeToReachSpacecraft = distanceToSpacecraft / speed;
        
        Vector3 predictedPos = spacecraft.transform.position + (Vector3)(spacecraftVel * timeToReachSpacecraft);
        
        Vector3 idealDirection = (predictedPos - transform.position).normalized;
        float xDir = idealDirection.x + UnityEngine.Random.Range(-directionRandomness, directionRandomness);
        float yDir = idealDirection.y + UnityEngine.Random.Range(-directionRandomness, directionRandomness);
        direction = new Vector3(xDir, yDir).normalized;
    }

    public void ChangeMotion(Vector3 direction, float speed = -1f) {
        if(speed == -1f) speed = UnityEngine.Random.Range(.5f, 8f);

        this.speed = speed;
        this.direction = direction;
        GetComponent<Rigidbody2D>().linearVelocity = direction * speed;
    }
    
}
