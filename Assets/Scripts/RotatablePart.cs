using TMPro;
using UnityEngine;

public class RotatablePart : MonoBehaviour {
    public ShipBuildingGrid.direction connectingDirection;

    // When set, the "right" connecting direction (part placed on the LEFT side of the ship)
    // renders as a horizontal mirror of the default art instead of a 180° rotation.
    // Use for parts whose art has left-right asymmetry but top-bottom symmetry (e.g., SolarPanel).
    public bool mirrorOnOppositeDirection = false;

    public ShipBuildingGrid.direction RotatePart() {
        connectingDirection = connectingDirection == ShipBuildingGrid.direction.right ?
                                                     ShipBuildingGrid.direction.above : connectingDirection + 1;
        SetRotation(connectingDirection);
        return connectingDirection;
    }

    public void SetRotation(ShipBuildingGrid.direction newConnectingDirection) {
        connectingDirection = newConnectingDirection;

        TextMeshProUGUI tmp = GetComponentInChildren<TextMeshProUGUI>();
        RectTransform tmpRect = tmp != null ? tmp.gameObject.GetComponent<RectTransform>() : null;

        // Always clear flip state before reapplying — directions other than `right` should not be flipped.
        SetSpriteFlip(false, false);

        switch (newConnectingDirection) {
            case ShipBuildingGrid.direction.above:
                transform.localRotation = Quaternion.Euler(0, 0, 180);
                if (tmpRect != null) tmpRect.localRotation = Quaternion.Euler(0, 0, 180);
                break;
            case ShipBuildingGrid.direction.below:
                transform.localRotation = Quaternion.Euler(0, 0, 0);
                if (tmpRect != null) tmpRect.localRotation = Quaternion.Euler(0, 0, 0);
                break;
            case ShipBuildingGrid.direction.left:
                transform.localRotation = Quaternion.Euler(0, 0, -90);
                if (tmpRect != null) tmpRect.localRotation = Quaternion.Euler(0, 0, 90);
                break;
            case ShipBuildingGrid.direction.right:
                transform.localRotation = Quaternion.Euler(0, 0, 90);
                if (tmpRect != null) tmpRect.localRotation = Quaternion.Euler(0, 0, -90);
                // The child visual already has a +90° local rotation, so visual-world rotation here is 180°.
                // Setting flipY (rather than flipX) cancels the unwanted vertical-axis component of the
                // 180° rotation, leaving a pure horizontal mirror in world space.
                if (mirrorOnOppositeDirection) SetSpriteFlip(false, true);
                break;
        }
    }

    private void SetSpriteFlip(bool flipX, bool flipY) {
        foreach (SpriteRenderer sr in GetComponentsInChildren<SpriteRenderer>()) {
            sr.flipX = flipX;
            sr.flipY = flipY;
        }
    }

    public bool TryAutoSetRotation((int, int) coords) {
        ShipBuildingGrid shipGrid = ShipBuildingGrid.Instance;
        if (!shipGrid.TryFindRotatableConnectingDirection(gameObject, coords, out ShipBuildingGrid.direction dir)) {
            return false;
        }

        SetRotation(dir);
        return true;
    }
}
