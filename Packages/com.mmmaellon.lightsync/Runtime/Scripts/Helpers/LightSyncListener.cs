
using JetBrains.Annotations;
using UdonSharp;
using VRC.SDKBase;
using VRC.Udon;

namespace MMMaellon.LightSync
{
    public abstract class LightSyncListener : UdonSharpBehaviour
    {
        public abstract void OnChangeState(LightSync sync, int prevState, int currentState);
        public abstract void OnChangeOwner(LightSync sync, VRCPlayerApi prevOwner, VRCPlayerApi currentOwner);
        public virtual void OnLerpEnd(LightSync sync){
        }

        /// <summary>
        /// Variable name that will be set before the OnChangeState and OnChangeOwner events is sent to the listener.
        /// This variable will store the light sync component calling the event
        /// </summary>
        [PublicAPI]
        public const string syncVariableName = "currentSync";
        /// <summary>
        /// Variable name that will be set before the OnChangeState and OnChangeOwner events is sent to the listener.
        /// This variable will store the light sync component calling the event
        /// </summary>
        [PublicAPI]
        public const string prevStateVariableName = "prevState";
        /// <summary>
        /// Variable name that will be set before the OnChangeState and OnChangeOwner events is sent to the listener.
        /// This variable will store the light sync component calling the event
        /// </summary>
        [PublicAPI]
        public const string currentStateVariableName = "currentState";
        /// <summary>
        /// Variable name that will be set before the OnChangeState and OnChangeOwner events is sent to the listener.
        /// This variable will store the light sync component calling the event
        /// </summary>
        [PublicAPI]
        public const string prevOwnerVariableName = "prevOwner";
        /// <summary>
        /// Variable name that will be set before the OnChangeState and OnChangeOwner events is sent to the listener.
        /// This variable will store the light sync component calling the event
        /// </summary>
        [PublicAPI]
        public const string currentOwnerVariableName = "currentOwner";
        /// <summary>
        /// The name of the event that gets called when the light sync changes state
        /// </summary>
        [PublicAPI]
        public const string changeStateEventName = "OnChangeState";
        /// <summary>
        /// The name of the event that gets called when the light sync finishes its lerp period. This usually is where the light sync is synced up with where the owner is.
        /// </summary>
        [PublicAPI]
        public const string lerpStopEventName = "OnLerpEnd";
        /// <summary>
        /// The name of the event that gets called when the light sync changes owner
        /// </summary>
        [PublicAPI]
        public const string changeOwnerEventName = "OnChangeOwner";
    }
}
