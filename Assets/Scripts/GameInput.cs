using UnityEngine;
using UnityEngine.InputSystem;
using System;
using UnityEditor;
using UnityEngine.SceneManagement;

//Class that handles input and triggers events based on it.

public class GameInput : MonoBehaviour {
    public static GameInput Instance { get; private set; }
    public event EventHandler<EngineEventArgs> OnEnginePerformedAction;
    public event EventHandler<EngineEventArgs> OnEngineCanceledAction;

    public event EventHandler<ThrustEventArgs> OnThrustRolesChanged;

    public ThrustRole CurrentThrustRoles { get; private set; }

    public class ThrustEventArgs : EventArgs {
        public ThrustRole activeRoles;
        public bool AnyActive => activeRoles != ThrustRole.None;
        public ThrustEventArgs(ThrustRole r) { activeRoles = r; }
    }

    public event EventHandler OnRepairShipPerformedAction;
    public event EventHandler OnRepairShipCanceledAction;
    
    public event EventHandler OnLeftMouseClickPerformedAction;

    public event EventHandler OnDeletePartPerformedAction;

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
        
        inputActions.General.ReturnToMenu.performed += ReturnToMenu_performed;
    }
    
    public Settings.ControlScheme ActiveScheme => Settings.Instance != null
        ? Settings.Instance.controlScheme
        : Settings.ControlScheme.Wasd;

    private Settings.ControlScheme lastSchemeSeen;

    // Numeric keys 5-9 polled directly; 1-4 wired via InputSystem_Actions asset.
    private readonly bool[] extraNumericKeyState = new bool[5];
    private static readonly Key[] ExtraNumericKeys = {
        Key.Digit5, Key.Digit6, Key.Digit7, Key.Digit8, Key.Digit9
    };

    private void Update() {
        if (ActiveScheme != lastSchemeSeen) {
            ReleaseAllEngines();
            ReleaseExtraNumericKeys();
            lastSchemeSeen = ActiveScheme;
        }

        bool gameActive = Time.timeScale != 0f && Spacecraft.IsFlightMode;
        bool wasdActive = ActiveScheme == Settings.ControlScheme.Wasd;

        if (!gameActive || !wasdActive) {
            if (CurrentThrustRoles != ThrustRole.None) {
                CurrentThrustRoles = ThrustRole.None;
                OnThrustRolesChanged?.Invoke(this, new ThrustEventArgs(ThrustRole.None));
            }
        }

        Keyboard kb = Keyboard.current;
        if (kb == null) return;

        if (gameActive && wasdActive) {
            ThrustRole next = ThrustRole.None;
            if (kb.wKey.isPressed) next |= ThrustRole.Forward;
            if (kb.sKey.isPressed) next |= ThrustRole.Reverse;
            if (kb.aKey.isPressed) next |= ThrustRole.StrafeLeft;
            if (kb.dKey.isPressed) next |= ThrustRole.StrafeRight;
            if (kb.qKey.isPressed) next |= ThrustRole.TurnLeft;
            if (kb.eKey.isPressed) next |= ThrustRole.TurnRight;

            if (next != CurrentThrustRoles) {
                CurrentThrustRoles = next;
                OnThrustRolesChanged?.Invoke(this, new ThrustEventArgs(next));
            }
            ReleaseExtraNumericKeys();
        } else if (gameActive && IsNumericSchemeActive) {
            for (int i = 0; i < ExtraNumericKeys.Length; i++) {
                int engineNum = i + 5;
                bool isPressed = kb[ExtraNumericKeys[i]].isPressed;
                if (isPressed == extraNumericKeyState[i]) continue;
                extraNumericKeyState[i] = isPressed;
                if (isPressed) OnEnginePerformedAction?.Invoke(this, new EngineEventArgs(true, engineNum));
                else OnEngineCanceledAction?.Invoke(this, new EngineEventArgs(false, engineNum));
            }
        } else {
            ReleaseExtraNumericKeys();
        }
    }

    private void ReleaseExtraNumericKeys() {
        for (int i = 0; i < extraNumericKeyState.Length; i++) {
            if (!extraNumericKeyState[i]) continue;
            extraNumericKeyState[i] = false;
            OnEngineCanceledAction?.Invoke(this, new EngineEventArgs(false, i + 5));
        }
    }

    private bool IsNumericSchemeActive => ActiveScheme == Settings.ControlScheme.Numeric;

    private void ReleaseAllEngines() {
        if (CurrentThrustRoles != ThrustRole.None) {
            CurrentThrustRoles = ThrustRole.None;
            OnThrustRolesChanged?.Invoke(this, new ThrustEventArgs(ThrustRole.None));
        }
        for (int i = 1; i <= Mathf.Max(1, Engine.totalEngineCount); i++) {
            OnEngineCanceledAction?.Invoke(this, new EngineEventArgs(false, i));
        }
    }

    private void EngineOne_performed(UnityEngine.InputSystem.InputAction.CallbackContext obj) {
        if (Time.timeScale == 0f) return;
        if (!IsNumericSchemeActive) return;
        OnEnginePerformedAction?.Invoke(this, new EngineEventArgs(true, 1)); //"?.Invoke" basically checks if theres any listeners (methods). If there are listeners, calls all of 'em.
    }
    
    private void EngineOne_canceled(UnityEngine.InputSystem.InputAction.CallbackContext obj) {
        OnEngineCanceledAction?.Invoke(this, new EngineEventArgs(false, 1)); 
    }

    private void EngineTwo_performed(UnityEngine.InputSystem.InputAction.CallbackContext obj) {
        if (Time.timeScale == 0f) return;
        if (!IsNumericSchemeActive) return;
        OnEnginePerformedAction?.Invoke(this, new EngineEventArgs(true, 2));
    }

    private void EngineTwo_canceled(UnityEngine.InputSystem.InputAction.CallbackContext obj) {
        OnEngineCanceledAction?.Invoke(this, new EngineEventArgs(false, 2));
    }

    private void EngineThree_performed(UnityEngine.InputSystem.InputAction.CallbackContext obj) {
        if (Time.timeScale == 0f) return;
        if (!IsNumericSchemeActive) return;
        OnEnginePerformedAction?.Invoke(this, new EngineEventArgs(true, 3));
    }

    private void EngineThree_canceled(UnityEngine.InputSystem.InputAction.CallbackContext obj) {
        OnEngineCanceledAction?.Invoke(this, new EngineEventArgs(false, 3));
    }

    private void EngineFour_performed(UnityEngine.InputSystem.InputAction.CallbackContext obj) {
        if (Time.timeScale == 0f) return;
        if (!IsNumericSchemeActive) return;
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
        
        // Passed requirements -> go to FlightFactsScene
        SceneManager.LoadScene("FlightFactsScene");
        
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
