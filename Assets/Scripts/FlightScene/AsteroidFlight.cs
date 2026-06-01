using UnityEngine;

public class AsteroidFlight : MonoBehaviour {
    [SerializeField] public int asteroidSize;
    [SerializeField] private float maxSpeed = 8f;
    [SerializeField] private float speedReductionPerSize = 1.4f;
    [SerializeField] private float speedVariance = 1.2f;
    [SerializeField] private float minimumScaledSpeed = 0.5f;

    public Vector3 direction;
    public float speed;

    private void Awake() {
        speed = GetRandomSpeedForSize();
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
        if(speed == -1f) speed = GetRandomSpeedForSize();

        this.speed = speed;
        this.direction = direction;
        GetComponent<Rigidbody2D>().linearVelocity = direction * speed;
    }

    private float GetRandomSpeedForSize() {
        int size = Mathf.Max(0, asteroidSize);
        float sizePenalty = size * speedReductionPerSize;
        float scaledMaxSpeed = Mathf.Max(minimumScaledSpeed, maxSpeed - sizePenalty);
        float scaledMinSpeed = Mathf.Max(minimumScaledSpeed, scaledMaxSpeed - speedVariance);

        return UnityEngine.Random.Range(scaledMinSpeed, scaledMaxSpeed);
    }
    
}
