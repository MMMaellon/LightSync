﻿using UnityEngine;
namespace MMMaellon.LightSync
{
    [AddComponentMenu("")]//prevents it from showing up in the add component menu
    public class LightSyncLooperFixedUpdate : LightSyncLooper
    {
        public void FixedUpdate()
        {
            Loop();
        }
        // public override float GetAutoSmoothedInterpolation(float elapsedTime)
        // {
        //     return lerpPeriod <= 0 ? 1 : elapsedTime / lerpPeriod;
        // }
    }
}
