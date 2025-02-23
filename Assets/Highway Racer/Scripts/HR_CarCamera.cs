﻿//----------------------------------------------
//           	   Highway Racer
//
// Copyright © 2014 - 2023 BoneCracker Games
// http://www.bonecrackergames.com
//
//----------------------------------------------

using UnityEngine;
using System.Collections;
using TMPro;

/// <summary>
/// Car camera with three modes.
/// </summary>
[AddComponentMenu("BoneCracker Games/Highway Racer/Camera/HR Car Camera")]
public class HR_CarCamera : MonoBehaviour {

    public CameraMode cameraMode = CameraMode.Top;       //  Camera modes.
    public enum CameraMode { Top, TPS, FPS }

    internal int cameraSwitchCount = 0; //  Current camera mode as int.

    private RCCP_HoodCamera hoodCam;     //  Hood camera transform.

    public bool tiltCameraOnFPSMode = true;

    private float targetFieldOfView = 50f;      //  Field of the camera will be adapted to this value.
    public float topFOV = 48f;      //  FOV for top mode.
    public float tpsFOV = 55f;      //  FOV for tps mode.
    public float fpsFOV = 65f;      //  FOV for fps mode.

    // The target we are following.
    public Transform playerCar;
    private Rigidbody playerRigid;

    public bool gameover = false;

    private Camera cam;     //  Actual camera.
    private Vector3 targetPosition = new Vector3(0, 0, 50);     //  Target position.
    private Vector3 pastFollowerPosition, pastTargetPosition;       //  Past position.

    // The distance in the x-z plane to the target
    public float distance = 12f;

    // The height we want the camera to be above the target
    public float height = 8.5f;

    //  X Rotation of the camera.
    public float rotation = 30f;

    private float currentT;
    private float oldT;

    private float speed = 0f;

    private GameObject mirrors;

    private void Start() {

        //  Getting camera component.
        cam = GetComponentInChildren<Camera>();

        //  Setting very first position and rotation of the camera (before the intro animation).
        transform.position = new Vector3(2f, 1f, 55f);
        transform.rotation = Quaternion.Euler(new Vector3(0f, -40f, 0f));

    }

    private void OnEnable() {

        //  Listening events when player spawns, dies, and change cameras.
        HR_GamePlayHandler.OnPlayerSpawned += OnPlayerSpawned;
        HR_GamePlayHandler.OnPlayerDied += OnPlayerCrashed;
        RCCP_InputManager.OnChangedCamera += RCC_InputManager_OnChangeCamera;

    }

    /// <summary>
    /// When player changes the camera mode.
    /// </summary>
    private void RCC_InputManager_OnChangeCamera() {

        ChangeCamera();

    }

    /// <summary>
    /// When player spawns, set player variable and get hood and tps camera locations.
    /// </summary>
    /// <param name="player"></param>
    private void OnPlayerSpawned(HR_PlayerHandler player) {

        playerCar = player.transform;
        playerRigid = player.Rigid;
        hoodCam = player.GetComponentInChildren<RCCP_HoodCamera>();

        if (GameObject.Find("Mirrors"))
            mirrors = GameObject.Find("Mirrors").gameObject;

    }

    /// <summary>
    /// When player dies.
    /// </summary>
    /// <param name="player"></param>
    private void OnPlayerCrashed(HR_PlayerHandler player, int[] scores) {

        gameover = true;

    }

    private void LateUpdate() {

        //  If no player car found yet, return.
        if (!playerCar)
            return;

        //  Animating the camera at spawn sequence.
        if (!playerCar || !playerRigid || Time.timeSinceLevelLoad < 1.5f) {

            transform.position += Quaternion.identity * Vector3.forward * (Time.deltaTime * 3f);

        } else if (playerCar && playerRigid) {

            //  Setting FOV of the camera.
            cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, targetFieldOfView, Time.deltaTime * 3f);

            //  Running corresponding method with current camera mode.
            if (!gameover) {

                switch (cameraMode) {

                    case CameraMode.Top:

                        TOP();

                        if (mirrors)
                            mirrors.SetActive(false);

                        break;

                    case CameraMode.TPS:

                        TPS();

                        if (mirrors)
                            mirrors.SetActive(false);

                        break;

                    case CameraMode.FPS:

                        if (hoodCam) {

                            FPS();

                            if (mirrors)
                                mirrors.SetActive(true);

                        } else {

                            cameraSwitchCount++;
                            ChangeCamera();

                        }

                        break;

                }

            } else {

                transform.LookAt(playerCar.transform.position);
                targetFieldOfView = Mathf.Lerp(targetFieldOfView, Mathf.Lerp(50f, 10f, Mathf.InverseLerp(0f, 30f, Vector3.Distance(transform.position, playerCar.transform.position))), Time.deltaTime * 3f);

                if (Vector3.Distance(transform.position, playerCar.transform.position) > 30f) {

                    transform.position = playerCar.transform.position;
                    transform.position += Vector3.forward * (20f * Mathf.Sign(playerCar.InverseTransformDirection(playerRigid.velocity).z));
                    transform.position += Vector3.up * 2f;
                    transform.position += Vector3.right * Random.Range(-2f, 2f);

                }

            }

            //  Setting proper camera mode with int.
            switch (cameraSwitchCount) {

                case 0:
                    cameraMode = CameraMode.Top;
                    break;
                case 1:
                    cameraMode = CameraMode.TPS;
                    break;
                case 2:
                    cameraMode = CameraMode.FPS;
                    break;

            }

        }

        pastFollowerPosition = transform.position;
        pastTargetPosition = targetPosition;

        currentT = (transform.position.z - oldT);
        oldT = transform.position.z;

    }

    /// <summary>
    /// Changes the camera mode.
    /// </summary>
    public void ChangeCamera() {

        cameraSwitchCount++;

        if (cameraSwitchCount >= 3)
            cameraSwitchCount = 0;

    }

    /// <summary>
    /// Top camera mode.
    /// </summary>
    private void TOP() {

        transform.rotation = Quaternion.Lerp(transform.rotation, Quaternion.Euler(rotation, 0f, 0f), Time.deltaTime * 2f);

        targetPosition = new Vector3(0f, playerCar.position.y, playerCar.position.z);
        targetPosition -= transform.rotation * Vector3.forward * distance;
        targetPosition = new Vector3(targetPosition.x, height, targetPosition.z);

        if (Time.timeSinceLevelLoad < 3f)
            transform.position = SmoothApproach(pastFollowerPosition, pastTargetPosition, targetPosition, (speed / 2f) * Mathf.Clamp(Time.timeSinceLevelLoad - 1.5f, 0f, 10f));
        else
            transform.position = targetPosition;

        targetFieldOfView = topFOV;

    }

    /// <summary>
    /// TPS camera mode.
    /// </summary>
    private void TPS() {

        transform.rotation = Quaternion.Lerp(transform.rotation, Quaternion.Euler(rotation / 3f, 0f, 0f), Time.deltaTime * 2f);
        targetPosition = new Vector3(playerCar.position.x, height / 3f, playerCar.position.z - (distance / 1.75f));
        transform.position = targetPosition;
        transform.LookAt(playerCar.transform.position + (playerCar.forward * 3f));
        targetFieldOfView = tpsFOV;
         
    }

    /// <summary>
    /// FPS camera mode.
    /// </summary>
    private void FPS() {

        transform.rotation = Quaternion.Lerp(transform.rotation, Quaternion.identity, Time.deltaTime * 2f);

        if (tiltCameraOnFPSMode)
            transform.localEulerAngles = new Vector3(transform.localEulerAngles.x, transform.InverseTransformDirection(playerRigid.velocity).x / 2f, -transform.InverseTransformDirection(playerRigid.velocity).x / 2f);

        targetPosition = hoodCam.transform.position;
        transform.position = targetPosition;
        targetFieldOfView = fpsFOV;

    }

    /// <summary>
    /// Used for smooth position lerping.
    /// </summary>
    /// <param name="pastPosition"></param>
    /// <param name="pastTargetPosition"></param>
    /// <param name="targetPosition"></param>
    /// <param name="delta"></param>
    /// <returns></returns>
    private Vector3 SmoothApproach(Vector3 pastPosition, Vector3 pastTargetPosition, Vector3 targetPosition, float delta) {

        if (Time.timeScale == 0 || float.IsNaN(delta) || float.IsInfinity(delta) || delta == 0 || pastPosition == Vector3.zero || pastTargetPosition == Vector3.zero || targetPosition == Vector3.zero)
            return transform.position;

        float t = (Time.deltaTime * delta) + .00001f;
        Vector3 v = (targetPosition - pastTargetPosition) / t;
        Vector3 f = pastPosition - pastTargetPosition + v;
        Vector3 l = targetPosition - v + f * Mathf.Exp(-t);

        if (l != Vector3.negativeInfinity && l != Vector3.positiveInfinity && l != Vector3.zero)
            return l;
        else
            return transform.position;

    }

    private void FixedUpdate() {

        //  If no player rigid found yet, return.
        if (!playerRigid)
            return;

        //  Getting speed of the car.
        speed = Mathf.Lerp(speed, (playerRigid.velocity.magnitude * 3.6f), Time.deltaTime * 1.5f);

    }

    private void OnDisable() {

        HR_GamePlayHandler.OnPlayerSpawned -= OnPlayerSpawned;
        HR_GamePlayHandler.OnPlayerDied -= OnPlayerCrashed;
        RCCP_InputManager.OnChangedCamera -= RCC_InputManager_OnChangeCamera;

    }

}