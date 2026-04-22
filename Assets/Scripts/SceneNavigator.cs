using UnityEngine;

public class SceneNavigator : MonoBehaviour
{
    public void SetBuildScene() => GameInput.Instance.SetBuildScene();
    public void SetMainMenuScene() => GameInput.Instance.SetMainMenuScene();
    public void SetFlightScene() => GameInput.Instance.SetFlightScene();
    public void SetFlightFactsScene() => GameInput.Instance.SetFlightFactsScene();
    public void SetMissionFactsScene() => GameInput.Instance.SetMissionFactsScene();
    public void SetMissionDetailsScene() => GameInput.Instance.SetMissionDetailsScene();
    public void SetCreditsScene() => GameInput.Instance.SetCreditsScene();
    public void SetSettingsScene() => GameInput.Instance.SetSettingsScene();

    public void Restart() {
        Spacecraft existing = Spacecraft.GetInstance();
        if (existing != null) Destroy(existing.gameObject);

        SpacecraftPartDatabase partDB = SpacecraftPartDatabase.Instance;
        if (partDB != null) {
            partDB.hasSavedGridState = false;
            partDB.savedGridState = null;
            partDB.savedPlacedParts = null;
        }

        GameInput.Instance.SetBuildScene();
    }
}
