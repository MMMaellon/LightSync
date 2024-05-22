
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

namespace MMMaellon.LightSync
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None), RequireComponent(typeof(LightSync))]
    public abstract class LightSyncState : UdonSharpBehaviour
    {
#if !COMPILER_UDONSHARP && UNITY_EDITOR
        public virtual void Reset()
        {
            sync = GetComponent<LightSync>();
            sync.SetupStates();
        }
#endif
        [HideInInspector]
        public int stateID;
        [HideInInspector]
        public LightSync sync;
        [HideInInspector]
        public LightSyncData data;

        public virtual void EnterState()
        {
            if (Utilities.IsValid(sync))
            {
                if (!sync.IsOwner())
                {
                    Networking.SetOwner(Networking.LocalPlayer, sync.gameObject);
                }
                // sync.state = (stateID + LightSync.STATE_CUSTOM);
            }
        }
        public virtual void ExitState()
        {
            if (sync)
            {
                if (!sync.IsOwner())
                {
                    Networking.SetOwner(Networking.LocalPlayer, sync.gameObject);
                }
                // sync.state = LightSync.STATE_FALLING;
            }
        }

        public bool IsActiveState()
        {
            return sync.data.state == stateID;
        }

        /*
             STATE LIFECYCLE
             Whenever anything happens to a lightsync object it does these things in order:
                1) Exits current state. If state forced any temporary changes to the object, they'd be undone here
                2) Enter new state. If there are any temporary changes, they get performed here.
                3) The looper is dispatched and begins calling OnLerp every physics frame until stopped
                4) On the very first OnLerp call, data.RequestSerialization is called
        */
        public abstract void OnExitState();

        public abstract void OnEnterState();

        // Summary:
        //     All players execute this command during the interpolation period. The interpolation period for owners is one frame
        //     the 'interpolation' parameter is a value between 0.0 and 1.0 representing how far along the interpolation period we are
        public abstract bool OnLerp(float elapsedTime, float autoSmoothedLerp);
    }
}
