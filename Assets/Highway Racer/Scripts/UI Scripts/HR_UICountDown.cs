using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

[AddComponentMenu("BoneCracker Games/Highway Racer/UI/HR UI Countdown")]
public class HR_UICountDown : MonoBehaviour {

    private void OnEnable() {

        HR_GamePlayHandler.OnCountDownStarted += HR_GamePlayHandler_OnCountDownStarted;

    }

    private void HR_GamePlayHandler_OnCountDownStarted() {

        GetComponent<Animator>().SetTrigger("Count");

    }

    private void OnDisable() {

        HR_GamePlayHandler.OnCountDownStarted -= HR_GamePlayHandler_OnCountDownStarted;

    }

}
