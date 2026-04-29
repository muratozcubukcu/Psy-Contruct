using UnityEngine;

[CreateAssetMenu(fileName = "PartScriptableObject", menuName = "Scriptable Objects/PartScriptableObject")]
public class PartScriptableObject : ScriptableObject {
    public GameObject part;

    public int partID;

    public float mass;

    public bool isStackable;
    
    //"This parts should only be connectable to the rest of the spacecraft from..."
    public string[] connectingDirections;
}
