using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
/// <summary>
/// Class that allows for retrieving ship parts for cloning and getting data about existing parts.
/// </summary>
public class SpacecraftPartDatabase : MonoBehaviour {
    [SerializeField] private PartScriptableObject[] allParts;

    public static SpacecraftPartDatabase Instance;
    public bool hasSavedGridState = false;
    public int[,] savedGridState;
    public Dictionary<(int, int), GameObject> savedPlacedParts;
    public Dictionary<GameObject, GameObject> savedPartStackedOn;
    public Dictionary<SpriteRenderer, Color> savedOriginalSpriteColors;

    public void Awake() {
        if (Instance != null && Instance != this) {
            Destroy(gameObject);
            return;
        }
        
        Instance = this;
        DontDestroyOnLoad(Instance);
    }
    
    public int GetPartID(GameObject part) {
        if (part == null) return -1;
        if (part.name.Contains("(Clone)")) return GetPartID(GetPartGameObject(part.name));
        
        foreach (PartScriptableObject partSO in allParts) {
            if (partSO.part.name == part.name) return partSO.partID;
        }
        
        return -1;
    }
    
    public GameObject GetPartGameObject(int id) {
        foreach (PartScriptableObject partSO in allParts) {
            if (partSO.partID == id) return partSO.part;
        }

        return null;
    }

    public GameObject GetPartGameObject(string objectOrObjectCloneName) {
        if (objectOrObjectCloneName.Contains("(Clone)")) {
            string nameWithoutClone = "";

            foreach (char c in objectOrObjectCloneName) {
                if (c == '(') break;

                nameWithoutClone += c;
            }

            objectOrObjectCloneName = nameWithoutClone;
        }
        
        foreach (PartScriptableObject partSO in allParts) {
            if (partSO.part.name == objectOrObjectCloneName) return partSO.part;
        }

        return null;
    }

    //public List<string> GetSnapableDirections(GameObject part) => GetSnapableDirections(GetPartID(part));

    public float GetMass(GameObject part) {
        if (part == null) return -1;
        foreach (PartScriptableObject partSO in allParts) {
            if (partSO.part.name == part.name) return partSO.mass;
        }
        
        return -1;
    }

    public PartScriptableObject[] GetAllParts() => allParts;

    // public List<string> GetSnapableDirections(int id) {
    //     foreach (PartScriptableObject partSO in allParts) {
    //         if (partSO.partID == id) {
    //             return partSO.connectingDirections.ToList();
    //         }
    //     }
    //
    //     return null;
    // }
    
    public bool PartIsStackable(int id) {
        foreach (PartScriptableObject partSO in allParts) {
            if (partSO.partID == id) return partSO.isStackable;
        }

        return false;
    }

    public bool PartIsStackable(GameObject part) => PartIsStackable(GetPartID(part));
    
    public bool PartIsRotatable(int id) {
        foreach (PartScriptableObject partSO in allParts) {
            if (partSO.partID == id) return partSO.isRotatable;
        }

        return false;
    }

    public bool PartIsRotatable(GameObject part) => PartIsRotatable(GetPartID(part));

    public PartScriptableObject GetPartSO(int id) {
        foreach (PartScriptableObject partSO in allParts) {
            if (partSO.partID == id) return partSO;
        }
        
        return null;
    }
    
    public PartScriptableObject GetPartSO(GameObject part) => GetPartSO(GetPartID(part));
}
