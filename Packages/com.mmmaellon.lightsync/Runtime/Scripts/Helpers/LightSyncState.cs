
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


        public abstract void OnEnterState();

        public abstract void OnExitState();

        // Summary:
        //     The owner executes this command when they are about to send data to the other players
        //     You should override this function to set all the synced variables that other players need
        public abstract void OnSendingData();

        // Summary:
        //     Controls the how the lerp progresses
        //     Ends the lerp if it's equal to or greater than 1
        public virtual float GetInterpolation()
        {
            return sync.GetInterpolation();
        }

        // Summary:
        //     Non-owners execute this command when they receive data from the owner and begin interpolating towards the synced data
        public abstract void OnLerpStart();

        // Summary:
        //     All players execute this command during the interpolation period. The interpolation period for owners is one frame
        //     the 'interpolation' parameter is a value between 0.0 and 1.0 representing how far along the interpolation period we are
        public abstract void OnLerp();

        // Summary:
        //     All players execute once this at the end of the interpolation period
        //     Return true to extend the interpolation period by another frame
        //     Return false to end the interpolation period and call the physics dispatch
        public abstract bool OnLerpEnd();

        // Summary:
        //     All players execute once this after OnLerpEnd on a physics frame if OnLerpEnd returned false
        public abstract bool OnPhysicsDispatch();
    }
}
