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
        
        TextMeshProUGUI tmp;
        switch (newConnectingDirection) {
            case ShipBuildingGrid.direction.above:
                transform.localRotation = Quaternion.Euler(0, 0, 180);
                tmp = GetComponentInChildren<TextMeshProUGUI>();
                if(tmp != null) tmp.gameObject.GetComponent<RectTransform>().localRotation = Quaternion.Euler(0, 0, 180);
                break;
            case ShipBuildingGrid.direction.below:
                transform.localRotation = Quaternion.Euler(0, 0, 0);
                tmp = GetComponentInChildren<TextMeshProUGUI>();
                if(tmp != null) tmp.gameObject.GetComponent<RectTransform>().localRotation = Quaternion.Euler(0, 0, 0);
                break;
            case ShipBuildingGrid.direction.left:
                transform.localRotation = Quaternion.Euler(0, 0, -90);
                tmp = GetComponentInChildren<TextMeshProUGUI>();
                if(tmp != null) tmp.gameObject.GetComponent<RectTransform>().localRotation = Quaternion.Euler(0, 0, 90);
                break;
            case ShipBuildingGrid.direction.right:
                transform.localRotation = Quaternion.Euler(0, 0, 90);
                tmp = GetComponentInChildren<TextMeshProUGUI>();
                if(tmp != null) tmp.gameObject.GetComponent<RectTransform>().localRotation = Quaternion.Euler(0, 0, -90);
                break;
        }
    }
}
