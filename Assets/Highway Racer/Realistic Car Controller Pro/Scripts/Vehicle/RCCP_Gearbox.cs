//----------------------------------------------
//        Realistic Car Controller Pro
//
// Copyright � 2014 - 2024 BoneCracker Games
// https://www.bonecrackergames.com
// Ekrem Bugra Ozdoganlar
//
//----------------------------------------------

using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Multiplies the received power from the engine --> clutch by x ratio, and transmits it to the differential. Higher ratios = faster accelerations, lower top speeds, lower ratios = slower accelerations, higher top speeds.
/// </summary>
[AddComponentMenu("BoneCracker Games/Realistic Car Controller Pro/Drivetrain/RCCP Gearbox")]
public class RCCP_Gearbox : RCCP_Component {

    /// <summary>
    /// Overrides gears with given values. All calculations will be ignored.
    /// </summary>
    public bool overrideGear = false;

    /// <summary>
    /// Gear ratios. Faster accelerations on higher values, but lower top speeds.
    /// </summary>
    [Min(0.1f)] public float[] gearRatios = new float[] { 4.35f, 2.5f, 1.66f, 1.23f, 1.0f, .85f };

    /// <summary>
    /// Target gear RPMs.
    /// </summary>
    public float[] GearRPMs {

        get {

            gearRPMs = new float[gearRatios.Length];

            for (int i = 0; i < gearRPMs.Length; i++) {

                if (GetComponentInParent<RCCP_CarController>(true).GetComponentInChildren<RCCP_Engine>(true))
                    gearRPMs[i] = GetComponentInParent<RCCP_CarController>(true).GetComponentInChildren<RCCP_Engine>(true).maxEngineRPM / gearRatios[i];

            }

            return gearRPMs;

        }

    }

    /// <summary>
    /// All gear rpms.
    /// </summary>
    public float[] gearRPMs;

    /// <summary>
    /// Current gear.
    /// </summary>
    [Min(0)] public int currentGear = 0;

    /// <summary>
    /// 0 means N, 1 means any gear is in use now.
    /// </summary>
    [Min(0f)] public float gearInput = 0f;

    /// <summary>
    /// Neutral gear engaged now?
    /// </summary>
    public bool forceToNGear = false;

    /// <summary>
    /// Reverse gear engaged now?
    /// </summary>
    public bool forceToRGear = false;

    [System.Serializable]
    public class CurrentGearState {

        public enum GearState { Park, InReverseGear, Neutral, InForwardGear }
        public GearState gearState = GearState.InForwardGear;

    }

    public CurrentGearState currentGearState;
    public CurrentGearState.GearState defaultGearState = CurrentGearState.GearState.InForwardGear;

    ///// <summary>
    ///// Reverse gear engaged now?
    ///// </summary>
    //public bool reverseGearEngaged = false;

    ///// <summary>
    ///// Neutral gear engaged now?
    ///// </summary>
    //public bool neutralGearEngaged = false;

    ///// <summary>
    ///// Park gear engaged now?
    ///// </summary>
    //public bool parkGearEngaged = false;

    ///// <summary>
    ///// Drive gear engaged now?
    ///// </summary>
    //public bool driveGearEngaged = false;

    public float[] targetSpeeds;

    /// <summary>
    /// Shifting time.
    /// </summary>
    [Min(0f)] public float shiftingTime = .2f;

    /// <summary>
    /// Shifting now?
    /// </summary>
    public bool shiftingNow = false;

    /// <summary>
    /// Don't shift timer if too close to previous one.
    /// </summary>
    public bool dontShiftTimer = true;

    /// <summary>
    /// Timer for don't shift.
    /// </summary>
    [Min(0f)] public float lastTimeShifted = 0f;

    /// <summary>
    /// Automatic transmission.
    /// </summary>
    [System.Obsolete("automaticTransmission in RCCP_Gearbox is obsolete, please use transmissionType instead.")] public bool automaticTransmission = true;

    /// <summary>
    /// Transmission types.
    /// </summary>
    public enum TransmissionType { Manual, Automatic, Automatic_DNRP }
    public TransmissionType transmissionType = TransmissionType.Automatic;

    /// <summary>
    /// Semi automatic gear.
    /// </summary>
    public enum SemiAutomaticDNRPGear { D, N, R, P }
    public SemiAutomaticDNRPGear automaticGearSelector = SemiAutomaticDNRPGear.D;

    /// <summary>
    /// Automatic transmission will shift up late on higher values.
    /// </summary>
    [Range(.1f, .9f)] public float shiftThreshold = .8f;

    /// <summary>
    /// Target engine rpm to shift up.
    /// </summary>
    [Min(0f)] public float shiftUpRPM = 5500f;

    /// <summary>
    /// Target engine rpm to shift down.
    /// </summary>
    [Min(0f)] public float shiftDownRPM = 2750f;

    /// <summary>
    /// Received torque from the component. It should be the clutch in this case.
    /// </summary>
    public float receivedTorqueAsNM = 0f;

    /// <summary>
    /// Produced and delivered torque to the component. It should be the differential in this case.
    /// </summary>
    public float producedTorqueAsNM = 0f;

    /// <summary>
    /// Output with custom class.
    /// </summary>
    public RCCP_Event_Output outputEvent = new RCCP_Event_Output();
    private RCCP_Output output = new RCCP_Output();

    private void Update() {

        //  Setting timer for last shifting.
        if (lastTimeShifted > 0)
            lastTimeShifted -= Time.deltaTime;

        //  Clamping timer.
        lastTimeShifted = Mathf.Clamp(lastTimeShifted, 0f, 10f);

    }

    private void FixedUpdate() {

        //  Early out if overriding gear is enabled. This means, an external class is adjusting gears.
        if (!overrideGear) {

            //  If transmission type is full automatic.
            if (transmissionType == TransmissionType.Automatic)
                AutomaticTransmission();

            //  If transmission type is automatic with DNRP gear selection.
            if (transmissionType == TransmissionType.Automatic_DNRP)
                AutomaticTransmissionDNRP();

            //  Forcing to N gear.
            if (forceToNGear)
                currentGearState.gearState = CurrentGearState.GearState.Neutral;

            //  Forcing to R gear.
            if (forceToRGear)
                currentGearState.gearState = CurrentGearState.GearState.InReverseGear;

            //  Forcing to N gear while shifting.
            if (shiftingNow)
                currentGearState.gearState = CurrentGearState.GearState.Neutral;

            //  If gear is not neutral or park, set gear input to 1. Otherwise to 0.
            if (currentGearState.gearState == CurrentGearState.GearState.Park || currentGearState.gearState == CurrentGearState.GearState.Neutral)
                gearInput = 0f;
            else
                gearInput = 1f;

        }

        //  Output.
        Output();

    }

    /// <summary>
    /// Calculates estimated speeds and rpms to shift up / down.
    /// </summary>
    private void AutomaticTransmission() {

        //  Early out if overriding gear is enabled. This means, an external class is adjusting gears.
        if (overrideGear)
            return;

        //  Getting engine rpm.
        float engineRPM = CarController.engineRPM;

        //  Creating float array for target speeds.
        float[] targetSpeeds = FindTargetSpeed();

        //  Creating low and high limits multiplied with threshold value.
        float lowLimit, highLimit;

        //  If current gear is not first gear, there is a low limit.
        if (currentGear > 0)
            lowLimit = targetSpeeds[currentGear - 1];

        //  High limit.
        highLimit = targetSpeeds[currentGear];

        bool canShiftUpNow = false;

        //  If reverse gear is not engaged, engine rpm is above shiftup rpm, and wheel & vehicle speed is above the high limit, shift up.
        if (currentGear < gearRatios.Length && currentGearState.gearState != CurrentGearState.GearState.InReverseGear && engineRPM >= shiftUpRPM && CarController.wheelRPM2Speed >= highLimit && CarController.speed >= highLimit)
            canShiftUpNow = true;

        //  Can't shift up while shifting already.
        if (shiftingNow)
            canShiftUpNow = false;

        bool canShiftDownNow = false;

        //  If reverse gear is not engaged, engine rpm is below shiftdown rpm, and wheel & vehicle speed is below the low limit, shift down.
        if (currentGear > 0 && currentGearState.gearState != CurrentGearState.GearState.InReverseGear && engineRPM <= shiftDownRPM) {

            if (FindEligibleGear() != currentGear)
                canShiftDownNow = true;
            else
                canShiftDownNow = false;

        }

        //  Can't shift down while shifting already.
        if (shiftingNow)
            canShiftDownNow = false;

        //  Setting last time shifted timer.
        if (!dontShiftTimer)
            lastTimeShifted = 0f;

        //  If last time shifted time is long enough...
        if (!shiftingNow && lastTimeShifted <= .02f) {

            //  Shift down.
            if (canShiftDownNow)
                ShiftToGear(FindEligibleGear());

            //  Shift up.
            if (canShiftUpNow)
                ShiftUp();

        }

    }

    /// <summary>
    /// Calculates estimated speeds and rpms to shift up / down.
    /// </summary>
    private void AutomaticTransmissionDNRP() {

        //  Early out if overriding gear is enabled. This means, an external class is adjusting gears.
        if (overrideGear)
            return;

        switch (automaticGearSelector) {

            case SemiAutomaticDNRPGear.D:

                currentGearState.gearState = CurrentGearState.GearState.InForwardGear;
                break;

            case SemiAutomaticDNRPGear.N:

                currentGearState.gearState = CurrentGearState.GearState.Neutral;
                break;

            case SemiAutomaticDNRPGear.R:

                currentGearState.gearState = CurrentGearState.GearState.InReverseGear;
                break;

            case SemiAutomaticDNRPGear.P:

                currentGearState.gearState = CurrentGearState.GearState.Park;
                break;

        }

        //  Getting engine rpm.
        float engineRPM = CarController.engineRPM;

        //  Creating float array for target speeds.
        float[] targetSpeeds = FindTargetSpeed();

        //  Creating low and high limits multiplied with threshold value.
        float lowLimit, highLimit;

        //  If current gear is not first gear, there is a low limit.
        if (currentGear > 0)
            lowLimit = targetSpeeds[currentGear - 1];

        //  High limit.
        highLimit = targetSpeeds[currentGear];

        bool canShiftUpNow = false;

        //  If reverse gear is not engaged, engine rpm is above shiftup rpm, and wheel & vehicle speed is above the high limit, shift up.
        if (currentGear < gearRatios.Length && currentGearState.gearState != CurrentGearState.GearState.InReverseGear && engineRPM >= shiftUpRPM && CarController.wheelRPM2Speed >= highLimit && CarController.speed >= highLimit)
            canShiftUpNow = true;

        //  Can't shift up while shifting already.
        if (shiftingNow)
            canShiftUpNow = false;

        bool canShiftDownNow = false;

        //  If reverse gear is not engaged, engine rpm is below shiftdown rpm, and wheel & vehicle speed is below the low limit, shift down.
        if (currentGear > 0 && currentGearState.gearState != CurrentGearState.GearState.InReverseGear && engineRPM <= shiftDownRPM) {

            if (FindEligibleGear() != currentGear)
                canShiftDownNow = true;
            else
                canShiftDownNow = false;

        }

        //  Can't shift down while shifting already.
        if (shiftingNow)
            canShiftDownNow = false;

        if (!dontShiftTimer)
            lastTimeShifted = 0f;

        //  If last time shifted time is long enough...
        if (!shiftingNow && lastTimeShifted <= .02f) {

            if (canShiftDownNow)
                ShiftToGear(FindEligibleGear());

            if (canShiftUpNow)
                ShiftUp();

        }

    }

    /// <summary>
    /// Received torque from the component.
    /// </summary>
    /// <param name="output"></param>
    public void ReceiveOutput(RCCP_Output output) {

        receivedTorqueAsNM = output.NM;

    }

    /// <summary>
    /// Finds eligible gear depends on the speed.
    /// </summary>
    /// <returns></returns>
    private float[] FindTargetSpeed() {

        //  Creating float array for target speeds.
        targetSpeeds = new float[gearRatios.Length];

        float partition = CarController.maximumSpeed / gearRatios.Length;

        //  Assigning target speeds.
        for (int i = targetSpeeds.Length - 1; i >= 0; i--)
            targetSpeeds[i] = partition * (i + 1) * shiftThreshold;

        return targetSpeeds;

    }

    /// <summary>
    /// Finds eligible gear depends on the speed.
    /// </summary>
    /// <returns></returns>
    private int FindEligibleGear() {

        float[] targetSpeeds = FindTargetSpeed();
        int eligibleGear = 0;

        for (int i = 0; i < targetSpeeds.Length; i++) {

            if (CarController.speed < targetSpeeds[i]) {

                eligibleGear = i;
                break;

            }

        }

        return eligibleGear;

    }

    /// <summary>
    /// Shift up.
    /// </summary>
    public void ShiftUp() {

        //  Can't shift now while shifting already.
        if (shiftingNow)
            return;

        //  Return if transmission type is DNRP and gear is not D.
        if (transmissionType == TransmissionType.Automatic_DNRP && automaticGearSelector != SemiAutomaticDNRPGear.D)
            return;

        //  If current gear is R, shift to first gear.
        if (currentGearState.gearState == CurrentGearState.GearState.InReverseGear) {

            StartCoroutine(ShiftTo(0));

        } else {

            if (currentGear < gearRatios.Length - 1)
                StartCoroutine(ShiftTo(currentGear + 1));

        }

    }

    /// <summary>
    /// Shift down.
    /// </summary>
    public void ShiftDown() {

        //  Can't shift now while shifting already.
        if (shiftingNow)
            return;

        //  Return if transmission type is DNRP and gear is not D.
        if (transmissionType == TransmissionType.Automatic_DNRP && automaticGearSelector != SemiAutomaticDNRPGear.D)
            return;

        //  If current gear is not the first gear, shift down. Otherwise put it to R.
        if (currentGear > 0)
            StartCoroutine(ShiftTo(currentGear - 1));
        else
            ShiftReverse();

    }

    /// <summary>
    /// Shift reverse.
    /// </summary>
    public void ShiftReverse() {

        //  Can't shift now while shifting already.
        if (shiftingNow)
            return;

        //  Return if transmission type is DNRP and gear is not R.
        if (transmissionType == TransmissionType.Automatic_DNRP && automaticGearSelector != SemiAutomaticDNRPGear.R)
            return;

        //  Return if speed is above 20km/h.
        if (CarController.speed > 20f)
            return;

        //  Put it to R.
        StartCoroutine(ShiftTo(-1));

    }

    /// <summary>
    /// Shift to specific gear.
    /// </summary>
    /// <param name="gear"></param>
    public void ShiftToGear(int gear) {

        //  Can't shift now while shifting already.
        if (shiftingNow)
            return;

        //  Put it to specific gear.
        StartCoroutine(ShiftTo(gear));

    }

    /// <summary>
    /// Shift to specific gear.
    /// </summary>
    /// <param name="gear"></param>
    public void ShiftToN() {

        //  Can't shift now while shifting already.
        if (shiftingNow)
            return;

        //  If current gear is neutral, disable N gear, Otherwise enable N gear.
        if (currentGearState.gearState == CurrentGearState.GearState.Neutral)
            currentGearState.gearState = CurrentGearState.GearState.InForwardGear;
        else
            currentGearState.gearState = CurrentGearState.GearState.Neutral;

    }

    /// <summary>
    /// Shift to specific gear with delay.
    /// </summary>
    /// <param name="gear"></param>
    /// <returns></returns>
    private IEnumerator ShiftTo(int gear) {

        //  Shifting now.
        shiftingNow = true;

        //  Wait.
        yield return new WaitForSeconds(shiftingTime);

        //  Last time shifted.
        lastTimeShifted = 1f;

        //  If target gear is -1, put it to R. Otherwise to forward gear.
        if (gear == -1)
            currentGearState.gearState = CurrentGearState.GearState.InReverseGear;
        else
            currentGearState.gearState = CurrentGearState.GearState.InForwardGear;

        //  Clamping gear.
        gear = Mathf.Clamp(gear, 0, gearRatios.Length - 1);

        //  Setting the current gear.
        currentGear = gear;

        //  Setting shiftingNow to false after the delay.
        shiftingNow = false;

    }

    /// <summary>
    /// Output.
    /// </summary>
    private void Output() {

        if (output == null)
            output = new RCCP_Output();

        producedTorqueAsNM = receivedTorqueAsNM * gearRatios[currentGear] * gearInput;

        if (currentGearState.gearState == CurrentGearState.GearState.InReverseGear)
            producedTorqueAsNM *= -1;

        output.NM = producedTorqueAsNM / outputEvent.GetPersistentEventCount();
        outputEvent.Invoke(output);

    }

    /// <summary>
    /// Inits the gears.
    /// </summary>
    public void InitGears(int totalGears) {

        //  Creating float array.
        gearRatios = new float[totalGears];

        //  Creating other arrays for specific.
        float[] gearRatio = new float[gearRatios.Length];
        int[] maxSpeedForGear = new int[gearRatios.Length];
        int[] targetSpeedForGear = new int[gearRatios.Length];

        //  Assigning array with preset values.
        if (gearRatios.Length == 1)
            gearRatio = new float[] { 1.0f };

        if (gearRatios.Length == 2)
            gearRatio = new float[] { 2.0f, 1.0f };

        if (gearRatios.Length == 3)
            gearRatio = new float[] { 2.0f, 1.5f, 1.0f };

        if (gearRatios.Length == 4)
            gearRatio = new float[] { 2.86f, 1.62f, 1.0f, .72f };

        if (gearRatios.Length == 5)
            gearRatio = new float[] { 4.23f, 2.52f, 1.66f, 1.22f, 1.0f, };

        if (gearRatios.Length == 6)
            gearRatio = new float[] { 4.35f, 2.5f, 1.66f, 1.23f, 1.0f, .85f };

        if (gearRatios.Length == 7)
            gearRatio = new float[] { 4.5f, 2.5f, 1.66f, 1.23f, 1.0f, .9f, .8f };

        if (gearRatios.Length == 8)
            gearRatio = new float[] { 4.6f, 2.5f, 1.86f, 1.43f, 1.23f, 1.05f, .9f, .72f };

        gearRatios = gearRatio;

    }

    /// <summary>
    /// Override the gear.
    /// </summary>
    /// <param name="targetGear"></param>
    /// <param name="RGear"></param>
    public void OverrideGear(int targetGear, bool RGear, bool NGear) {

        currentGear = targetGear;

        if (RGear)
            currentGearState.gearState = CurrentGearState.GearState.InReverseGear;
        else if (NGear)
            currentGearState.gearState = CurrentGearState.GearState.Neutral;
        else
            currentGearState.gearState = CurrentGearState.GearState.InForwardGear;

    }

    public void Reload() {

        //  Make sure shifting now, and neutral gear engaged is set to false when enabling the vehicle.
        shiftingNow = false;
        currentGearState.gearState = defaultGearState;
        lastTimeShifted = 0f;
        currentGear = 0;
        gearInput = 0f;
        receivedTorqueAsNM = 0f;
        producedTorqueAsNM = 0f;

    }

}
