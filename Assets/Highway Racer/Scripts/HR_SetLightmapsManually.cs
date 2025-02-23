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

public class HR_SetLightmapsManually {

    public static void AlignLightmaps(GameObject referenceMainGameObject, GameObject targetMainGameObject) {

        Renderer[] referenceRenderers;
        Renderer[] targetRenderers;

        referenceRenderers = referenceMainGameObject.GetComponentsInChildren<Renderer>();
        targetRenderers = targetMainGameObject.GetComponentsInChildren<Renderer>();

        for (int i = 0; i < targetRenderers.Length; i++) {

            targetRenderers[i].lightmapIndex = referenceRenderers[i].lightmapIndex;
            targetRenderers[i].lightmapScaleOffset = referenceRenderers[i].lightmapScaleOffset;

        }

    }

}
