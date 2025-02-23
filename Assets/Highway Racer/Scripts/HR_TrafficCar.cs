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
#if PHOTON_UNITY_NETWORKING
using Photon;
using Photon.Pun;
#endif

/// <summary>
/// Traffic car controller.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
[AddComponentMenu("BoneCracker Games/Highway Racer/Traffic/HR Traffic Car")]
public class HR_TrafficCar : MonoBehaviour {

    // Getting an Instance of HR_GamePlayHandler.
    #region HR_GamePlayHandler Instance

    private HR_GamePlayHandler HR_GamePlayHandlerInstance;
    private HR_GamePlayHandler HR_GamePlayHandler {
        get {
            if (HR_GamePlayHandlerInstance == null) {
                HR_GamePlayHandlerInstance = HR_GamePlayHandler.Instance;
            }
            return HR_GamePlayHandlerInstance;
        }
    }

    #endregion

    // Getting an Instance of HR_TrafficPooling.
    #region HR_TrafficPooling Instance

    private HR_TrafficPooling HR_TrafficPoolingInstance;
    private HR_TrafficPooling HR_TrafficPooling {
        get {
            if (HR_TrafficPoolingInstance == null) {
                HR_TrafficPoolingInstance = HR_TrafficPooling.Instance;
            }
            return HR_TrafficPoolingInstance;
        }
    }

    #endregion

    private Rigidbody rigid;        //  Rigidbody.
    public BoxCollider bodyCollider;        //  Collider.
    [HideInInspector] public BoxCollider triggerCollider;       //  Trigger.

    private bool crashed = false;       //  Crashed?

    public ChangingLines changingLines = ChangingLines.Straight;
    public enum ChangingLines { Straight, Right, Left }
    internal int currentLine = 0;       //  Current line.

    public float maximumSpeed = 10f;        //  Maximum speed of the car.
    private float _maximumSpeed = 10f;
    private float desiredSpeed;     //  Desired speed (adapted) of the car.
    private float distance = 0f;        //  Distance to next car.
    private Quaternion steeringAngle = Quaternion.identity;     //  Steering angle.

    public Transform[] wheelModels;     //  Wheel models.
    private float wheelRotation = 0f;       //  Wheel rotation.

    [Header("Just Lights. All of them will work on ''NOT Important'' Render Mode.")]
    public Light[] headLights;
    public Light[] brakeLights;
    public Light[] signalLights;

    private bool headlightsOn = false;
    private bool brakingOn = false;

    private SignalsOn signalsOn = SignalsOn.Off;
    private enum SignalsOn { Off, Right, Left, All }
    private float signalTimer = 0f;
    private float spawnProtection = 0f;

    [Space(10)]

    public AudioClip engineSound;
    private AudioSource engineSoundSource;

#if PHOTON_UNITY_NETWORKING && BCG_HR_PHOTON
    private bool networked = false;
#endif

    private void Awake() {

        // Rigidbody.
        rigid = GetComponent<Rigidbody>();
        rigid.drag = 1f;
        rigid.angularDrag = 4f;
        rigid.maxAngularVelocity = 2.5f;
        rigid.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ | RigidbodyConstraints.FreezePositionY;

        //  Getting all lights.
        Light[] allLights = GetComponentsInChildren<Light>();

        //  Setting lights as vertex lights and make sure they are not affecting to any surface.
        foreach (Light l in allLights)
            l.renderMode = LightRenderMode.ForceVertex;

        //  Forward distance.
        distance = 50f;

        //  Collider.
        if (!bodyCollider) {

            Debug.LogWarning(transform.name + "is missing collider in HR_TrafficCar script. Select your vehicle collider. Assigning collider automatically now, but it may select the wrong collider now...");
            bodyCollider = GetComponentInChildren<BoxCollider>();

        }

        //  Creating trigger for detecting front vehicles.
        GameObject triggerColliderGO = new GameObject("TriggerVolume");
        triggerColliderGO.transform.position = bodyCollider.bounds.center;
        triggerColliderGO.transform.rotation = bodyCollider.transform.rotation;
        triggerColliderGO.transform.SetParent(transform, true);
        triggerColliderGO.transform.localScale = bodyCollider.transform.localScale;
        triggerColliderGO.AddComponent<BoxCollider>();
        triggerColliderGO.GetComponent<BoxCollider>().isTrigger = true;
        triggerColliderGO.GetComponent<BoxCollider>().size = bodyCollider.size;
        triggerColliderGO.GetComponent<BoxCollider>().center = bodyCollider.center;

        triggerCollider = triggerColliderGO.GetComponent<BoxCollider>();
        triggerCollider.size = new Vector3(bodyCollider.size.x * 1.5f, bodyCollider.size.y, bodyCollider.size.z + (bodyCollider.size.z * 3f));
        triggerCollider.center = new Vector3(bodyCollider.center.x, 0f, bodyCollider.center.z + (triggerCollider.size.z / 2f) - (bodyCollider.size.z / 2f));

        // Enabling lights if scene is a night or rainy scene.
        if (HR_GamePlayHandler.dayOrNight == HR_GamePlayHandler.DayOrNight.Night)
            headlightsOn = true;
        else
            headlightsOn = false;

        //  Creating engine sound.
        engineSoundSource = HR_CreateAudioSource.NewAudioSource(gameObject, "Engine Sound", 2f, 5f, 1f, engineSound, true, true, false);
        engineSoundSource.pitch = 1.5f;

        _maximumSpeed = maximumSpeed;

        //  Setting layer of the child gameobjects.
        foreach (Transform t in transform)
            t.gameObject.layer = (int)Mathf.Log(HR_HighwayRacerProperties.Instance.trafficCarsLayer.value, 2);

        //  Setting layer of the trigger volume.
        triggerCollider.gameObject.layer = LayerMask.NameToLayer("TrafficCarVolume");

    }

    private void Start() {

#if PHOTON_UNITY_NETWORKING && BCG_HR_PHOTON

        if (GetComponent<PhotonView>() && !GetComponent<PhotonView>().IsMine)
            networked = true;

                    if (networked)
            return;

#endif

        //  Speeding up the vehicle at each 4 seconds.
        InvokeRepeating("SpeedUp", 4f, 4f);
        //  Changing lines randomly.
        InvokeRepeating("ChangeLines", Random.Range(15, 45), Random.Range(15, 45));

    }

    private void Update() {

        //  Spawn protection used for preventing crashes too soon.
        spawnProtection += Time.deltaTime;

        Lights();
        Wheels();

    }

    /// <summary>
    /// Lights of the car.
    /// </summary>
    private void Lights() {

        signalTimer += Time.deltaTime;

        for (int i = 0; i < signalLights.Length; i++) {

            if (signalsOn == SignalsOn.Off)
                signalLights[i].intensity = 0f;

            if (signalsOn == SignalsOn.Left) {

                if (signalTimer >= .5f) {

                    if (signalLights[i].transform.localPosition.x < 0f)
                        signalLights[i].intensity = 0f;

                } else {

                    if (signalLights[i].transform.localPosition.x < 0f)
                        signalLights[i].intensity = 1f;
                }

                if (signalTimer >= 1f)
                    signalTimer = 0f;

            }

            if (signalsOn == SignalsOn.Right) {

                if (signalTimer >= .5f) {

                    if (signalLights[i].transform.localPosition.x > 0f)
                        signalLights[i].intensity = 0f;

                } else {

                    if (signalLights[i].transform.localPosition.x > 0f)
                        signalLights[i].intensity = 1f;

                }

                if (signalTimer >= 1f)
                    signalTimer = 0f;
            }

            if (signalsOn == SignalsOn.All) {

                if (signalTimer >= .5f)
                    signalLights[i].intensity = 0f;
                else
                    signalLights[i].intensity = 1f;

                if (signalTimer >= 1f)
                    signalTimer = 0f;
            }

        }

        for (int i = 0; i < headLights.Length; i++) {

            if (!headlightsOn)
                headLights[i].intensity = 0f;
            else
                headLights[i].intensity = 1f;

        }

        for (int i = 0; i < brakeLights.Length; i++) {

            if (brakingOn) {

                brakeLights[i].intensity = 1f;

            } else {

                if (!headlightsOn)
                    brakeLights[i].intensity = 0f;
                else
                    brakeLights[i].intensity = .6f;

            }

        }

    }

    /// <summary>
    /// Wheels rotation.
    /// </summary>
    private void Wheels() {

        for (int i = 0; i < wheelModels.Length; i++) {

            wheelRotation += desiredSpeed * 20 * Time.deltaTime;
            wheelModels[i].transform.localRotation = Quaternion.Euler(wheelRotation, 0f, 0f);

        }

    }

    private void FixedUpdate() {

#if PHOTON_UNITY_NETWORKING && BCG_HR_PHOTON

        if (networked)
            return;

#endif

        //  Adjusting desired speed according to distance to the next car. If crashed, set it to 0.
        if (!crashed)
            desiredSpeed = Mathf.Clamp(maximumSpeed - Mathf.Lerp(maximumSpeed, 0f, (distance - 10f) / 50f), 0f, maximumSpeed);
        else
            desiredSpeed = Mathf.Lerp(desiredSpeed, 0f, Time.fixedDeltaTime);

        //  Braking distance.
        if (distance < 50)
            brakingOn = true;
        else
            brakingOn = false;

        // If mode is not two ways, adjust steering angle.
        if (!crashed && HR_GamePlayHandler.mode != HR_GamePlayHandler.Mode.TwoWay)
            transform.rotation = Quaternion.Lerp(transform.rotation, steeringAngle, Time.fixedDeltaTime * 3f);

        // Setting linear and angular velocity of the car.
        rigid.velocity = Vector3.Slerp(rigid.velocity, transform.forward * desiredSpeed, Time.fixedDeltaTime * 3f);
        rigid.angularVelocity = Vector3.Slerp(rigid.angularVelocity, Vector3.zero, Time.fixedDeltaTime * 10f);

        //  If game mode is not two ways, change the lines.
        if (!crashed && HR_GamePlayHandler.mode != HR_GamePlayHandler.Mode.TwoWay) {

            currentLine = Mathf.Clamp(currentLine, 0, 3);

            switch (changingLines) {

                case ChangingLines.Straight:
                    steeringAngle = Quaternion.identity;
                    break;

                case ChangingLines.Left:

                    if (currentLine == 0) {

                        changingLines = ChangingLines.Straight;
                        break;

                    }

                    if (transform.position.x <= HR_TrafficPooling.lines[currentLine - 1].position.x + .5f) {

                        currentLine--;
                        signalsOn = SignalsOn.Off;
                        changingLines = ChangingLines.Straight;

                    } else {

                        steeringAngle = Quaternion.identity * Quaternion.Euler(0f, -5f, 0f);
                        signalsOn = SignalsOn.Left;

                    }
                    break;

                case ChangingLines.Right:

                    if (currentLine == 3) {

                        changingLines = ChangingLines.Straight;
                        break;

                    }

                    if (transform.position.x >= HR_TrafficPooling.lines[currentLine + 1].position.x - .5f) {

                        currentLine++;
                        signalsOn = SignalsOn.Off;
                        changingLines = ChangingLines.Straight;

                    } else {

                        steeringAngle = Quaternion.identity * Quaternion.Euler(0f, 5f, 0f);
                        signalsOn = SignalsOn.Right;

                    }

                    break;

            }

        }

    }

    private void OnTriggerStay(Collider col) {

#if PHOTON_UNITY_NETWORKING && BCG_HR_PHOTON

        if (networked)
            return;

#endif

        if ((1 << col.gameObject.layer) != HR_HighwayRacerProperties.Instance.trafficCarsLayer.value || col.isTrigger)
            return;

        //  Calculating distance to the next car.
        distance = Vector3.Distance(transform.position, col.transform.position);

    }

    private void OnCollisionEnter(Collision col) {

        if (crashed || spawnProtection < .5f)
            return;

        crashed = true;
        signalsOn = SignalsOn.All;

    }

    public void OnReAligned() {

        crashed = false;
        spawnProtection = 0f;
        rigid.velocity = Vector3.zero;
        rigid.angularVelocity = Vector3.zero;
        signalsOn = SignalsOn.Off;
        changingLines = ChangingLines.Straight;
        maximumSpeed = Random.Range(_maximumSpeed, _maximumSpeed * 1.5f);
        distance = 50f;

    }

    /// <summary>
    /// Speeds up the car.
    /// </summary>
    private void SpeedUp() {

        distance = 50f;

    }

    /// <summary>
    /// Switches the lines.
    /// </summary>
    private void ChangeLines() {

        if (changingLines == ChangingLines.Left || changingLines == ChangingLines.Right)
            return;

        int randomNumber = Random.Range(0, 2);

        changingLines = randomNumber == 0 ? ChangingLines.Left : ChangingLines.Right;

    }

    private void Reset() {

        Rigidbody rigidbody = GetComponent<Rigidbody>();

        if (!rigidbody)
            rigidbody = gameObject.AddComponent<Rigidbody>();

        rigidbody.interpolation = RigidbodyInterpolation.Interpolate;
        rigidbody.mass = 1500f;
        rigidbody.drag = 1f;
        rigidbody.angularDrag = 4f;
        rigidbody.maxAngularVelocity = 2.5f;
        rigidbody.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ | RigidbodyConstraints.FreezePositionY;

    }

}
