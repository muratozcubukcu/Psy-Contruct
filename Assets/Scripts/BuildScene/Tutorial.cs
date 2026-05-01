using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class Tutorial : MonoBehaviour
{

    public static Tutorial instance;

    private TextMeshProUGUI text;
    private RectTransform textbox;
    private Image image;

    private bool ShipAdded = false;
    private bool EngineAdded = false;
    private bool GammaRayAdded = false;
    private bool MagnetometerAdded = false;
    private bool MultispectralImagerAdded = false;
    private bool NeutronSpectrometerAdded = false;
    private bool SatelliteDishAdded = false;
    private bool SolarPanelAdded = false;
    private bool FuelTankAdded = false;
    void Awake()
    {
        instance = this;
    }

    void Start()
    {
        text = GetComponentInChildren<TextMeshProUGUI>();
        textbox = GetComponentInChildren<RectTransform>();
        image = GetComponent<Image>();
        if (!Settings.Instance.tutorialEnabled)
        {
            image.enabled = false;
            text.enabled = false;
            enabled = false;
        }
        updateState();
    }

    public void partAdded(string addedPart)
    {
        switch(addedPart) {
            case "ShipPart":
                ShipAdded = true;
                break;
            case "BottomEnginePart": case "LeftEnginePart": case "RightEnginePart": case "TopEnginePart":
                EngineAdded = true;
                break;
            case "GammaRayPart":
                GammaRayAdded = true;
                break;
            case "MagnetometerPart":
                MagnetometerAdded = true;
                break;
            case "MultispectralImagerPart":
                MultispectralImagerAdded = true;
                break;
            case "NeutronSpectrometerPart":
                NeutronSpectrometerAdded = true;
                break;
            case "SatelliteDishPart":
                SatelliteDishAdded = true;
                break;
            case "SolarPanelPart":
                SolarPanelAdded = true;
                break;
            case "FuelTankPart":
                FuelTankAdded = true;
                break;
        }
        updateState();
    }

    private void updateState()
    {
        if (!ShipAdded)
        {
            text.text = "Try adding a Ship part!\nThese are the basic building blocks of your spacecraft!";
        }
        else if (!EngineAdded)
        {
            text.text = "Your ship will need engines to fly. They are controlled with the number key displayed\non them.";
        }
        else if (!FuelTankAdded)
        {
            text.text = "Engines take fuel to use. Add a fuel tank to increase your fuel reserve.";
        }
        else if (!SolarPanelAdded)
        {
            text.text = "Engines also consume energy gathered by solar panels. Solar panels will build energy while facing toward the sun!";
        }
        else if (!SatelliteDishAdded)
        {
            text.text = "A sattelite dish will allow you to contact earth for help with repairs when faced toward it! Use it with the spacebar!";
        }
        else if (!(GammaRayAdded && MagnetometerAdded && NeutronSpectrometerAdded && MultispectralImagerAdded))
        {
            text.text = "The four remaining parts are sensors which will allow you to achieve your mission! Make sure to include them!";
        }
        else
        {
            text.text = "When your ship is ready, press the button in the lower right to begin the mission!";
        }
    }
}
