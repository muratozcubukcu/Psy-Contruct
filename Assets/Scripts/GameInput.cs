using UnityEngine;
using UnityEngine.InputSystem;
using System;
using UnityEditor;
using UnityEngine.SceneManagement;

// =============================================================================
// GameInput
// -----------------------------------------------------------------------------
// What it does:
//   Central input handler. Reads keyboard/mouse input and fires events that
//   other scripts (engines, repair, scene buttons) listen to.
//   Engines are controlled with the digit keys 1-9: each key fires the engine
//   with the matching ID. Keys 1-4 are bound through the InputSystem action
//   asset; keys 5-9 are polled directly here.
// =============================================================================

//Class that handles input and triggers events based on it.

public class GameInput : MonoBehaviour {
    public static GameInput Instance { get; private set; }
    public event EventHandler<EngineEventArgs> OnEnginePerformedAction;
    public event EventHandler<EngineEventArgs> OnEngineCanceledAction;

    public event EventHandler OnRepairShipPerformedAction;
    public event EventHandler OnRepairShipCanceledAction;
    
    public event EventHandler OnLeftMouseClickPerformedAction;

    public event EventHandler OnDeletePartPerformedAction;
    
    public event EventHandler OnRotatePartPerformedAction;

    public event EventHandler OnSetFlightScenePerformedAction;
    
    private InputSystem_Actions inputActions;
    
    public class EngineEventArgs : EventArgs {
        public bool activated;
        public int engineNum;
        
        public EngineEventArgs(bool activated, int engineNum) {
            this.activated = activated;
            this.engineNum = engineNum;
        }
    }
    
    public void Awake() {
        if (Instance != null && Instance != this) {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(this);

        inputActions = new InputSystem_Actions();

        if (SceneManager.GetActiveScene().name == "FlightScene") inputActions.Spacecraft.Enable();
        else inputActions.SpacecraftBuilding.Enable();

        inputActions.General.Enable();
    }

    public void Start() {
        inputActions.Spacecraft.EngineOne.performed += EngineOne_performed;
        inputActions.Spacecraft.EngineOne.canceled += EngineOne_canceled;
        inputActions.Spacecraft.EngineTwo.performed += EngineTwo_performed;
        inputActions.Spacecraft.EngineTwo.canceled += EngineTwo_canceled;
        inputActions.Spacecraft.EngineThree.performed += EngineThree_performed;
        inputActions.Spacecraft.EngineThree.canceled += EngineThree_canceled;
        inputActions.Spacecraft.EngineFour.performed += EngineFour_performed;
        inputActions.Spacecraft.EngineFour.canceled += EngineFour_canceled;

        inputActions.Spacecraft.RepairShip.performed += RepairShip_performed;
        inputActions.Spacecraft.RepairShip.canceled += RepairShip_canceled;

        inputActions.SpacecraftBuilding.DeletePart.performed += DeletePart_performed;
        inputActions.SpacecraftBuilding.LeftMouseClick.performed += LeftMouseClick_performed;
        inputActions.SpacecraftBuilding.RotatePart.performed += RotatePart_performed;
        
        inputActions.General.ReturnToMenu.performed += ReturnToMenu_performed;
    }
    
    // Numeric keys 5-9 polled directly; 1-4 wired via InputSystem_Actions asset.
    // We track each key's previous pressed-state so we only fire events on edges
    // (key down / key up), not every frame the key is held.
    private readonly bool[] extraNumericKeyState = new bool[5];
    private static readonly Key[] ExtraNumericKeys = {
        Key.Digit5, Key.Digit6, Key.Digit7, Key.Digit8, Key.Digit9
    };

    private void Update() {
        // gameActive = the game is unpaused AND we're in flight (not the build scene).
        bool gameActive = Time.timeScale != 0f && Spacecraft.IsFlightMode;

        // Need a real keyboard to read keys. Returns null in headless/automation.
        Keyboard kb = Keyboard.current;
        if (kb == null) return;

        if (gameActive) {
            // ----- Numeric path (keys 5-9) -----
            // Keys 1-4 come through the InputSystem callbacks; 5-9 we poll
            // manually here because they aren't bound in the action asset.
            for (int i = 0; i < ExtraNumericKeys.Length; i++) {
                int engineNum = i + 5;
                bool isPressed = kb[ExtraNumericKeys[i]].isPressed;
                // Skip if no change since last frame - we only care about edges.
                if (isPressed == extraNumericKeyState[i]) continue;
                extraNumericKeyState[i] = isPressed;
                if (isPressed) OnEnginePerformedAction?.Invoke(this, new EngineEventArgs(true, engineNum));
                else OnEngineCanceledAction?.Invoke(this, new EngineEventArgs(false, engineNum));
            }
        } else {
            // Game paused or not in flight - make sure nothing stays "stuck on".
            ReleaseExtraNumericKeys();
        }
    }

    // Forces every numeric-key tracked state to "released" and fires the
    // canceled events. Used when pausing so engines don't stay firing forever.
    private void ReleaseExtraNumericKeys() {
        for (int i = 0; i < extraNumericKeyState.Length; i++) {
            if (!extraNumericKeyState[i]) continue;
            extraNumericKeyState[i] = false;
            OnEngineCanceledAction?.Invoke(this, new EngineEventArgs(false, i + 5));
        }
    }

    private void EngineOne_performed(UnityEngine.InputSystem.InputAction.CallbackContext obj) {
        if (Time.timeScale == 0f) return;
        OnEnginePerformedAction?.Invoke(this, new EngineEventArgs(true, 1)); //"?.Invoke" basically checks if theres any listeners (methods). If there are listeners, calls all of 'em.
    }
    
    private void EngineOne_canceled(UnityEngine.InputSystem.InputAction.CallbackContext obj) {
        OnEngineCanceledAction?.Invoke(this, new EngineEventArgs(false, 1)); 
    }

    private void EngineTwo_performed(UnityEngine.InputSystem.InputAction.CallbackContext obj) {
        if (Time.timeScale == 0f) return;
        OnEnginePerformedAction?.Invoke(this, new EngineEventArgs(true, 2));
    }

    private void EngineTwo_canceled(UnityEngine.InputSystem.InputAction.CallbackContext obj) {
        OnEngineCanceledAction?.Invoke(this, new EngineEventArgs(false, 2));
    }

    private void EngineThree_performed(UnityEngine.InputSystem.InputAction.CallbackContext obj) {
        if (Time.timeScale == 0f) return;
        OnEnginePerformedAction?.Invoke(this, new EngineEventArgs(true, 3));
    }

    private void EngineThree_canceled(UnityEngine.InputSystem.InputAction.CallbackContext obj) {
        OnEngineCanceledAction?.Invoke(this, new EngineEventArgs(false, 3));
    }

    private void EngineFour_performed(UnityEngine.InputSystem.InputAction.CallbackContext obj) {
        if (Time.timeScale == 0f) return;
        OnEnginePerformedAction?.Invoke(this, new EngineEventArgs(true, 4)); 
    }
     
    private void EngineFour_canceled(UnityEngine.InputSystem.InputAction.CallbackContext obj) {
        OnEngineCanceledAction?.Invoke(this, new EngineEventArgs(false, 4)); 
    }

    private void RepairShip_performed(UnityEngine.InputSystem.InputAction.CallbackContext obj) {
        OnRepairShipPerformedAction?.Invoke(this, EventArgs.Empty);
    }
    
    private void RepairShip_canceled(UnityEngine.InputSystem.InputAction.CallbackContext obj) {
        OnRepairShipCanceledAction?.Invoke(this, EventArgs.Empty);
    }
    

    private void DeletePart_performed(UnityEngine.InputSystem.InputAction.CallbackContext obj) {
        OnDeletePartPerformedAction?.Invoke(this, EventArgs.Empty);
    }
    
    private void LeftMouseClick_performed(UnityEngine.InputSystem.InputAction.CallbackContext obj) {
        OnLeftMouseClickPerformedAction?.Invoke(this, EventArgs.Empty); 
    }
    
    private void RotatePart_performed(UnityEngine.InputSystem.InputAction.CallbackContext obj) {
        OnRotatePartPerformedAction?.Invoke(this, EventArgs.Empty); 
    }
    

    private void ReturnToMenu_performed(UnityEngine.InputSystem.InputAction.CallbackContext obj) {
        if (SceneManager.GetActiveScene().name == "FlightScene") {
            return;
        }
        SetMainMenuScene();
    }

    public void SetBuildScene() {
        SceneManager.LoadScene("BuildScene");

        inputActions.Spacecraft.Disable();
        inputActions.SpacecraftBuilding.Enable();
    }

    public void SetFlightScene() {
        SceneManager.LoadScene("FlightScene");
        
        inputActions.SpacecraftBuilding.Disable();
        inputActions.Spacecraft.Enable();

        OnSetFlightScenePerformedAction?.Invoke(this, EventArgs.Empty);
    }

    public void SetFlightFactsScene() {
        
        BuildRequirements requirements = BuildRequirements.Instance;
        
        if (!requirements.IsReadyForFlight(out string message)) {
            Debug.Log(message); // Example: "Missing parts: SolarPanel, SatelliteDish"
            return; // Stop here -> do NOT load FlightScene
        }
        if (ShipBuildingGrid.Instance != null && ShipBuildingGrid.Instance.HighlightDisconnectedParts()) {
            DisconnectedPartsWarningManager.Instance.DisplayWarning();
            Debug.Log("Warning: Some ship parts are not connected to the spacecraft core.");
            return; // Stop here -> do NOT load FlightScene
        }
        
        // Passed requirements -> go to the Opening cutscene, which chains to
        // the Send-off cutscene, which then loads FlightScene.
        SceneManager.LoadScene("OpeningCutscene");
        
        Settings.Instance.toggleTutorial(false);
        inputActions.Spacecraft.Disable();
        inputActions.SpacecraftBuilding.Disable();
    }

    public void SetCreditsScene() {
        SceneManager.LoadScene("CreditsScene");

        inputActions.Spacecraft.Disable();
        inputActions.SpacecraftBuilding.Disable();
    }

    public void SetMissionDetailsScene() {
        SceneManager.LoadScene("MissionDetailsScene");

        inputActions.Spacecraft.Disable();
        inputActions.SpacecraftBuilding.Disable();
    }

    public void SetSettingsScene() {
        SceneManager.LoadScene("SettingsScene");

        inputActions.Spacecraft.Disable();
        inputActions.SpacecraftBuilding.Disable();
    }

    public void SetMissionFactsScene() {
        SceneManager.LoadScene("MissionFactsScene");

        inputActions.Spacecraft.Disable();
        inputActions.SpacecraftBuilding.Disable();
    }

    public void SetMainMenuScene() {
        SceneManager.LoadScene("MainMenuScene");
        
        Spacecraft spacecraft = Spacecraft.GetInstance();
        if (spacecraft != null) Destroy(spacecraft.gameObject);

        inputActions.Spacecraft.Disable();
        inputActions.SpacecraftBuilding.Disable();
    }

    public void SetGameOverScene(bool victory) {
        GameOverUI.isVictory = victory;
        SceneManager.LoadScene("GameOverScene");

        inputActions.Spacecraft.Disable();
        inputActions.SpacecraftBuilding.Disable();
    }

    private void OnDisable() {
        // Disable input actions when the component is disabled
        CleanupInputActions();
    }
    
    private void OnDestroy() {
        // Cleanup input actions when the object is destroyed
        CleanupInputActions();
    }
    
    private void CleanupInputActions() {
        //Properly disable and cleanup input actions
        if (inputActions != null) {
            // Unsubscribe from all events first
            inputActions.Spacecraft.EngineOne.performed -= EngineOne_performed;
            inputActions.Spacecraft.EngineOne.canceled -= EngineOne_canceled;
            inputActions.Spacecraft.EngineTwo.performed -= EngineTwo_performed;
            inputActions.Spacecraft.EngineTwo.canceled -= EngineTwo_canceled;
            inputActions.Spacecraft.EngineThree.performed -= EngineThree_performed;
            inputActions.Spacecraft.EngineThree.canceled -= EngineThree_canceled;
            inputActions.Spacecraft.EngineFour.performed -= EngineFour_performed;
            inputActions.Spacecraft.EngineFour.canceled -= EngineFour_canceled;

            inputActions.SpacecraftBuilding.LeftMouseClick.performed -= LeftMouseClick_performed;
            
            // Always disable the action maps (safe to call even if already disabled)
            inputActions.Spacecraft.Disable();
            
            // Disable the entire input system and dispose
            inputActions.Disable();
            inputActions.Dispose();
            inputActions = null;
        }
    }
}
