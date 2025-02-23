﻿//----------------------------------------------
//           	   Highway Racer
//
// Copyright © 2014 - 2023 BoneCracker Games
// http://www.bonecrackergames.com
//
//----------------------------------------------

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

[AddComponentMenu("BoneCracker Games/Highway Racer/UI/HR UI Dynamic Score Displayer")]
public class HR_UIDynamicScoreDisplayer : MonoBehaviour {

    #region SINGLETON PATTERN
    private static HR_UIDynamicScoreDisplayer instance;
    public static HR_UIDynamicScoreDisplayer Instance {
        get {
            if (instance == null) {
                instance = FindObjectOfType<HR_UIDynamicScoreDisplayer>();
            }

            return instance;
        }
    }
    #endregion

    private Text scoreText;
    private Text[] scoreTexts;
    private int index = 0;

    private float lifeTime = 1f;
    private float timer = 0f;
    private Vector3 defPos;

    public enum Side { Left, Right, Center }

    private AudioSource nearMissSound;

    private void Start() {

        scoreText = GetComponentInChildren<Text>();
        scoreText.gameObject.SetActive(false);

        scoreTexts = new Text[10];

        for (int i = 0; i < 10; i++) {

            GameObject instantiatedText = Instantiate(scoreText.gameObject, transform);
            scoreTexts[i] = instantiatedText.GetComponent<Text>();
            scoreTexts[i].color = new Color(scoreTexts[i].color.r, scoreTexts[i].color.g, scoreTexts[i].color.b, 0f);
            scoreTexts[i].gameObject.SetActive(true);

        }

        timer = 0f;
        defPos = scoreTexts[0].transform.position;

    }

    private void OnEnable() {

        HR_PlayerHandler.OnNearMiss += HR_PlayerHandler_OnNearMiss;

    }

    private void HR_PlayerHandler_OnNearMiss(HR_PlayerHandler player, int score, Side side) {

        switch (side) {

            case Side.Left:
                DisplayScore(score, -75f);
                break;

            case Side.Right:
                DisplayScore(score, 75f);
                break;

            case Side.Center:
                DisplayScore(score, 0f);
                break;

        }

    }

    public void DisplayScore(int score, float offset) {

        if (index < scoreTexts.Length - 1)
            index++;
        else
            index = 0;

        scoreTexts[index].text = "+" + score.ToString();
        scoreTexts[index].transform.position = new Vector3(defPos.x + offset, defPos.y, defPos.z);

        timer = lifeTime;
        nearMissSound = HR_CreateAudioSource.NewAudioSource(gameObject, HR_HighwayRacerProperties.Instance.nearMissAudioClip.name, 0f, 0f, 1f, HR_HighwayRacerProperties.Instance.nearMissAudioClip, false, true, true);
        nearMissSound.ignoreListenerPause = true;

    }

    private void Update() {

        if (timer > 0)
            timer -= Time.deltaTime;

        timer = Mathf.Clamp(timer, 0f, lifeTime);

        for (int i = 0; i < scoreTexts.Length; i++) {
            //			scoreTexts [i].transform.Translate (Vector3.up * Time.deltaTime * 75f, Space.World);
            scoreTexts[i].color = Color.Lerp(scoreTexts[i].color, new Color(scoreTexts[i].color.r, scoreTexts[i].color.g, scoreTexts[i].color.b, 0f), Time.deltaTime * 10f);
        }

        if (timer > 0) {

            scoreTexts[index].color = new Color(scoreTexts[index].color.r, scoreTexts[index].color.g, scoreTexts[index].color.b, 1f);

        }

    }

    private void OnDisable() {

        HR_PlayerHandler.OnNearMiss -= HR_PlayerHandler_OnNearMiss;

    }

}
