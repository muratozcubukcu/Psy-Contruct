using TMPro;
using UnityEngine;

public class RotatablePart : MonoBehaviour {
    public ShipBuildingGrid.direction connectingDirection;

    public ShipBuildingGrid.direction RotatePart() {
        transform.localRotation *= Quaternion.Euler(0, 0, 90);
        TextMeshProUGUI tmp = GetComponentInChildren<TextMeshProUGUI>();
        if(tmp != null) tmp.gameObject.GetComponent<RectTransform>().rotation *= Quaternion.Euler(0, 0, -90);
        
        connectingDirection = connectingDirection == ShipBuildingGrid.direction.right ? 
                                                     ShipBuildingGrid.direction.above : connectingDirection + 1;

        return connectingDirection;
    }
    
    public void SetRotation(ShipBuildingGrid.direction newConnectingDirection) {
        connectingDirection = newConnectingDirection;
        TextMeshProUGUI tmp = GetComponentInChildren<TextMeshProUGUI>();
        Quaternion rotation = new Quaternion();
        
        switch (newConnectingDirection) {
            case ShipBuildingGrid.direction.above:
                rotation = Quaternion.Euler(0, 0, 180);
                break;
            case ShipBuildingGrid.direction.below:
                rotation = Quaternion.Euler(0, 0, 0);
                break;
            case ShipBuildingGrid.direction.left:
                rotation = Quaternion.Euler(0, 0, -90);
                break;
            case ShipBuildingGrid.direction.right:
                rotation = Quaternion.Euler(0, 0, 90);
                break;
        }
        
        transform.localRotation = rotation;
        if(tmp != null) tmp.gameObject.GetComponent<RectTransform>().localRotation = rotation;
    }
}
