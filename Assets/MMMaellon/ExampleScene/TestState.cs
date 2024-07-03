
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

public class TestState : MMMaellon.LightSync.LightSyncState
{
    public override void OnEnterState()
    {

    }

    public override void OnExitState()
    {

    }

    public override bool OnLerp(float elapsedTime, float autoSmoothedLerp)
    {
        return false;
    }


    void Start()
    {

    }
}
