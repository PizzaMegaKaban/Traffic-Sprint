﻿//----------------------------------------------
//           	   Highway Racer
//
// Copyright © 2014 - 2023 BoneCracker Games
// http://www.bonecrackergames.com
//
//----------------------------------------------

using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System;
using UnityEngine.SceneManagement;

/// <summary>
/// Management of the main menu events. Creates and spawns vehicles, switches them, enables/disables menus.
/// </summary>
[AddComponentMenu("BoneCracker Games/Highway Racer/Main Menu/HR Main Menu Handler")]
public class HR_MainMenuHandler : MonoBehaviour {

    #region SINGLETON PATTERN
    private static HR_MainMenuHandler instance;
    public static HR_MainMenuHandler Instance {
        get {
            if (instance == null) {
                instance = FindObjectOfType<HR_MainMenuHandler>();
            }

            return instance;
        }
    }
    #endregion

    [Header("Spawn Location Of The Cars")]
    public Transform carSpawnLocation;      //  Spawn location.

    private GameObject[] createdCars;       //	All created cars will be stored.
    private GameObject[] racingCars;        //  Racing versions of all created cars.
    
    public RCCP_CarController currentCar;      //	Current selected car.
    public RCCP_CarController currentRacingCar; // Current selected car racing version.

    internal int carIndex = 0;      //	Current car index.

    [Header("UI Menus")]
    public GameObject optionsMenu;
    public GameObject carSelectionMenu;
    public GameObject modCarPanel;
    public GameObject modsSelectionMenu;
    public GameObject sceneSelectionMenu;
    public GameObject creditsMenu;

    [Header("UI Loading Section")]
    public GameObject loadingScreen;
    // public Slider loadingBar;
    private AsyncOperation async;

    [Header("Buttons")]
    public GameObject buyCarButton;
    public GameObject selectCarButton;

    [Header("Text")]
    public TextMeshProUGUI currency;

    [Header("Best Score Texts")]
    public Text vehicleNameText;        //	Current vehicle name text.
    //public Text bestScoreOneWay;        //	Best score one way text.
    //public Text bestScoreTwoWay;        //	Best score two ways text.
    //public Text bestScoreTimeLeft;      //	Best score time left text.
    //public Text bestScoreBomb;      //	Best score bomb text.

    internal AudioSource mainMenuSoundtrack;

#if PHOTON_UNITY_NETWORKING && BCG_HR_PHOTON
    private GameObject multiplayerMenu;
#endif

    private void Awake() {

        // Setting time scale, volume, unpause, and target frame rate.
        Time.timeScale = 1f;
        AudioListener.volume = PlayerPrefs.GetFloat("MasterVolume", 1f);
        AudioListener.pause = false;

#if PHOTON_UNITY_NETWORKING && BCG_HR_PHOTON

        HR_PhotonHandler[] photonHandlers = FindObjectsOfType<HR_PhotonHandler>(true);

        for (int i = 0; i < photonHandlers.Length; i++) {

            if (photonHandlers[i].photonView == null)
                Destroy(photonHandlers[i].gameObject);
            else
                photonHandlers[i].SetInstance();

        }

                if (FindObjectOfType<HR_PhotonObserver>(true))
            Destroy(FindObjectOfType<HR_PhotonObserver>(true).gameObject);

#endif


        //	Creating soundtracks for the main menu.
        if (HR_HighwayRacerProperties.Instance.mainMenuClips != null && HR_HighwayRacerProperties.Instance.mainMenuClips.Length > 0) {

            mainMenuSoundtrack = HR_CreateAudioSource.NewAudioSource(gameObject, "Main Menu Soundtrack", 0f, 0f, PlayerPrefs.GetFloat("MusicVolume", .35f), HR_HighwayRacerProperties.Instance.mainMenuClips[UnityEngine.Random.Range(0, HR_HighwayRacerProperties.Instance.mainMenuClips.Length)], true, true, false);
            mainMenuSoundtrack.ignoreListenerPause = true;

        }

        //	Getting the money.
        if (!PlayerPrefs.HasKey("Currency"))
            PlayerPrefs.SetInt("Currency", HR_HighwayRacerProperties.Instance.initialMoney);

        //	Getting last selected car index.
        carIndex = PlayerPrefs.GetInt("SelectedPlayerCarIndex", 0);

        CreateCars();   //	Creating all selectable cars at once.
        SpawnCar();     //	Spawning only target car (carIndex).
        EnableMenu(carSelectionMenu);       //  Enabling car selection menu.

#if PHOTON_UNITY_NETWORKING && BCG_HR_PHOTON

        HR_PhotonLobbyManager multiplayerManager = FindObjectOfType<HR_PhotonLobbyManager>(true);

        if (multiplayerManager)
            multiplayerMenu = multiplayerManager.gameObject;

#endif

    }

    private void Update() {

        //	Displaying currency.
        currency.text = HR_API.GetCurrency().ToString("F0");

        //	If loading, set value of the loading slider.
        //if (async != null && !async.isDone)
        //    loadingBar.value = async.progress;

    }

    /// <summary>
    /// Creating all spawnable cars at once.
    /// </summary>
    private void CreateCars() {

        //	Creating a new array.
        createdCars = new GameObject[HR_PlayerCars.Instance.cars.Length];
        racingCars = new GameObject[HR_PlayerCars.Instance.cars.Length];

        //	Setting array elements.
        for (int i = 0; i < createdCars.Length; i++) {
            var x = HR_PlayerCars.Instance.cars[i].playerCar;
            var y = HR_PlayerCars.Instance.cars[i].racingCar;

            createdCars[i] = (RCCP.SpawnRCC(HR_PlayerCars.Instance.cars[i].playerCar.GetComponent<RCCP_CarController>(), carSpawnLocation.position, carSpawnLocation.rotation, false, false, false)).gameObject;
            createdCars[i].SetActive(false);

            if (createdCars[i].GetComponent<RCCP_CarController>().Lights != null)
                createdCars[i].GetComponent<RCCP_CarController>().Lights.lowBeamHeadlights = true;

            racingCars[i] = (RCCP.SpawnRCC(HR_PlayerCars.Instance.cars[i].racingCar.GetComponent<RCCP_CarController>(), carSpawnLocation.position, carSpawnLocation.rotation, false, false, false)).gameObject;
            racingCars[i].SetActive(false);

            if (racingCars[i].GetComponent<RCCP_CarController>().Lights != null)
                racingCars[i].GetComponent<RCCP_CarController>().Lights.lowBeamHeadlights = true;
        }

    }

    /// <summary>
    /// Spawns target car (carIndex).
    /// </summary>
    private void SpawnCar() {

        //	If price of the car is 0, or unlocked, save it as owned car.
        if (HR_PlayerCars.Instance.cars[carIndex].price <= 0 || HR_PlayerCars.Instance.cars[carIndex].unlocked)
            HR_API.UnlockVehice(carIndex);

        //	If current spawned car is owned, enable buy button, disable select button. Do opposite otherwise.
        if (HR_API.OwnedVehicle(carIndex)) {

            //  Displaying price null.
            if (buyCarButton.GetComponentInChildren<Text>())
                buyCarButton.GetComponentInChildren<Text>().text = "";

            // Enabling select button, disabling buy button.
            buyCarButton.SetActive(false);
            selectCarButton.SetActive(true);
            modCarPanel.SetActive(true);

        } else {

            //  Displaying price.
            if (buyCarButton.GetComponentInChildren<Text>())
                buyCarButton.GetComponentInChildren<Text>().text = "BUY FOR\n" + HR_PlayerCars.Instance.cars[carIndex].price.ToString("F0");

            //  Enabling buy button, disabling select button.
            selectCarButton.SetActive(false);
            buyCarButton.SetActive(true);
            modCarPanel.SetActive(false);

        }

        //	Disabling all cars at once. And then enabling only target car (carIndex). And make sure spawned cars are always at spawn point.
        for (int i = 0; i < createdCars.Length; i++) {

            if (createdCars[i].activeInHierarchy)
                createdCars[i].SetActive(false);

        }

        //	Enabling only target car (carIndex).
        createdCars[carIndex].SetActive(true);
        RCCP.RegisterPlayerVehicle(createdCars[carIndex].GetComponent<RCCP_CarController>(), false, false);

        //	Setting current car.
        currentCar = createdCars[carIndex].GetComponent<RCCP_CarController>();
        currentRacingCar = racingCars[carIndex].GetComponent<RCCP_CarController>();

        //	Displaying car name text.
        if (vehicleNameText)
            vehicleNameText.text = HR_PlayerCars.Instance.cars[carIndex].vehicleName;

        FindObjectOfType<HR_CustomizationUIManager>(true).CloseCustomizationPanels();

    }

    /// <summary>
    /// Purchases current car.
    /// </summary>
    public void BuyCar() {

        // If we own the car, don't consume currency.
        if (HR_API.OwnedVehicle(carIndex)) {

            Debug.LogError("Car is already owned!");
            return;

        }

        //	If currency is enough, save it and consume currency. Otherwise display the informer.
        if (HR_API.GetCurrency() >= HR_PlayerCars.Instance.cars[carIndex].price) {

            HR_API.ConsumeCurrency(HR_PlayerCars.Instance.cars[carIndex].price);

        } else {

            HR_UIInfoDisplayer.Instance.ShowInfo("Not Enough Coins", "You have to earn " + (HR_PlayerCars.Instance.cars[carIndex].price - HR_API.GetCurrency()).ToString() + " more coins to buy this vehicle", HR_UIInfoDisplayer.InfoType.NotEnoughMoney);
            return;

        }

        //	Saving the car.
        HR_API.UnlockVehice(carIndex);

        //	And spawning again to check modders of the car.
        SpawnCar();

    }

    /// <summary>
    /// Selects the current car with carIndex.
    /// </summary>
    public void SelectCar() {

        PlayerPrefs.SetInt("SelectedPlayerCarIndex", carIndex);

    }

    /// <summary>
    /// Switch to next car.
    /// </summary>
    public void PositiveCarIndex() {

        carIndex++;

        if (carIndex >= createdCars.Length)
            carIndex = 0;

        SpawnCar();

    }

    /// <summary>
    /// Switch to previous car.
    /// </summary>
    public void NegativeCarIndex() {

        carIndex--;

        if (carIndex < 0)
            carIndex = createdCars.Length - 1;

        SpawnCar();

    }

    /// <summary>
    /// Enables target menu and disables all other menus.
    /// </summary>
    /// <param name="activeMenu"></param>
    public void EnableMenu(GameObject activeMenu) {

        optionsMenu.SetActive(false);
        carSelectionMenu.SetActive(false);
        modsSelectionMenu.SetActive(false);
        sceneSelectionMenu.SetActive(false);
        creditsMenu.SetActive(false);
        loadingScreen.SetActive(false);

        activeMenu.SetActive(true);

        //if (activeMenu == modsSelectionMenu)
        //    BestScores();

    }

    /// <summary>
    /// Selects the scene with int.
    /// </summary>
    /// <param name="levelIndex"></param>
    public void SelectScene(string levelName) {

        PlayerPrefs.SetString("SelectedScene", levelName);

    }

    /// <summary>
    /// Selects the mode with int.
    /// </summary>
    /// <param name="_modeIndex"></param>
    public void SelectMode(int _modeIndex) {

        //	Saving the selected mode, and enabling scene selection menu.
        PlayerPrefs.SetInt("SelectedModeIndex", _modeIndex);

    }

    /// <summary>
    /// Selects the scene with int.
    /// </summary>
    /// <param name="levelIndex"></param>
    public void StartRace() {

        SelectCar();
        EnableMenu(loadingScreen);
        async = SceneManager.LoadSceneAsync(PlayerPrefs.GetString("SelectedScene", ""));
    }

    /// <summary>
    /// Displays best scores of all four modes.
    /// </summary>
    //private void BestScores() {

    //    int[] scores = HR_API.GetHighScores();

    //    bestScoreOneWay.text = "BEST SCORE\n" + scores[0];
    //    bestScoreTwoWay.text = "BEST SCORE\n" + scores[1];
    //    bestScoreTimeLeft.text = "BEST SCORE\n" + scores[2];
    //    bestScoreBomb.text = "BEST SCORE\n" + scores[3];

    //}

#if PHOTON_UNITY_NETWORKING && BCG_HR_PHOTON

    public void SetMultiplayer(bool state) {

        PlayerPrefs.SetInt("Multiplayer", state ? 1 : 0);

        if (state) {

            if (multiplayerMenu)
                EnableMenu(multiplayerMenu);

        }

    }

#endif

    /// <summary>
    /// Quits the game.
    /// </summary>
    public void QuitGame() {

        Application.Quit();

    }

    public void AddMoney() {

        HR_API.AddCurrency(10000);

    }

    public void UnlockCars() {

        for (int i = 0; i < HR_PlayerCars.Instance.cars.Length; i++)
            HR_API.UnlockVehice(i);

        SpawnCar();

    }

    public void DeleteSave() {

        PlayerPrefs.DeleteAll();
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);

    }

}
