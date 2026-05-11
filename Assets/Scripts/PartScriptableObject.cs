using UnityEngine;

[CreateAssetMenu(fileName = "PartScriptableObject", menuName = "Scriptable Objects/PartScriptableObject")]
public class PartScriptableObject : ScriptableObject {
    public GameObject part;

    public int partID;

    public float mass;

    public bool isStackable;
    
    public bool isRotatable;

    //"This parts should only be connectable to the rest of the spacecraft from..."
    //public string[] connectingDirections;

    [Header("Tooltip Info")]
    [TextArea(2, 4)]
    public string description;          // e.g. "Provides thrust to propel the spacecraft forward."
    [TextArea(1, 3)]
    public string connectsTo;
}
