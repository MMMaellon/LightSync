
using UnityEngine;
using VRC.SDKBase;
#if !COMPILER_UDONSHARP && UNITY_EDITOR
using System.Linq;
using UnityEditor;
using VRC.SDKBase.Editor.BuildPipeline;
#endif

namespace MMMaellon.LightSync
{
    [RequireComponent(typeof(Rigidbody))]
    public class MultiPickupObject : LightSyncListener
    {
        public MultiPickupHandle[] handles;
        public Rigidbody rigid;

        public override void OnChangeOwner(LightSync sync, VRCPlayerApi prevOwner, VRCPlayerApi currentOwner)
        {

        }

        MultiPickupHandle currHandle;
        public override void OnChangeState(LightSync sync, int prevState, int currentState)
        {
            currHandle = sync.GetComponent<MultiPickupHandle>();
            if (currHandle)
            {
                if (currentState != LightSync.STATE_PHYSICS)
                {
                    currHandle.Detach();
                    loop = true;
                    SendCustomEventDelayedFrames(nameof(Loop), 2);
                }
                else if (prevState != currentState)
                {
                    currHandle.Attach();
                }
            }
        }

        public override void OnLerpEnd(LightSync sync)
        {

        }

        bool loop = false;
        int lastLoop = -1001;
        public void Loop(float delta)
        {
            if (!loop || lastLoop == Time.frameCount)
            {
                return;
            }

            loop = false;
            foreach (var handle in handles)
            {
                if (handle.sync.state != LightSync.STATE_PHYSICS)
                {
                    handle.ApplyForces(delta);
                    loop = true;
                }
            }
            lastLoop = Time.frameCount;
            // SendCustomEventDelayedFrames(nameof(Loop), 1);
        }

        public void FixedUpdate()
        {
            Loop(Time.fixedDeltaTime);
        }

#if UNITY_EDITOR && !COMPILER_UDONSHARP
        public void Reset()
        {
            Setup();
        }
        public void Setup()
        {
            rigid = GetComponent<Rigidbody>();
            handles = handles.Where(x => x != null).ToArray();
            foreach (var handle in handles)
            {
                handle.Setup(this);
                var newListeners = handle.sync.eventListeners.Union(new Component[] { this });
                if (newListeners.Count() != handle.sync.eventListeners.Length)
                {
                    handle.sync.eventListeners = newListeners.ToArray();
                    handle.sync.SetupListeners();
                }
            }
        }
#endif

    }
}

#if UNITY_EDITOR && !COMPILER_UDONSHARP
namespace MMMaellon.LightSync
{

    public class MultiPickupObjectBuildHandler : IVRCSDKBuildRequestedCallback
    {
        public int callbackOrder => 1;

        [InitializeOnLoadMethod]
        public static void Initialize()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        public static void OnPlayModeStateChanged(PlayModeStateChange change)
        {
            if (change != PlayModeStateChange.ExitingEditMode)
            {
                return;
            }
            AutoSetup();
        }

        public bool OnBuildRequested(VRCSDKRequestedBuildType requestedBuildType)
        {
            AutoSetup();
            return true;
        }

        public static void AutoSetup()
        {
            foreach (MultiPickupObject obj in GameObject.FindObjectsOfType<MultiPickupObject>(true))
            {
                obj.Setup();
            }
        }
    }
}

#endif
