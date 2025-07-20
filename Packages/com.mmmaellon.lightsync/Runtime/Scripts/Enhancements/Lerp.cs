using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

namespace MMMaellon.LightSync
{
    [AddComponentMenu("LightSync/Lerp (lightsync)")]
    public class Lerp : LightSyncState
    {
        [Tooltip("How long to wait after the lerp is done to drop the object, If set to a negative value, object is held in place indefinitely.")]
        public float secondsUntilEnablingPhysics = 0;
        [UdonSynced]
        public float lerpDuration = 0.5f;
        [System.NonSerialized]
        public float progress;
        bool kinematicOnEnter;
        bool alreadyAtEnd;
        public override void OnEnterState()
        {
            if (sync.localTransformFlag)
            {
                RecordLocalTransforms();
            }
            else
            {
                RecordWorldTransforms();
            }
            kinematicOnEnter = sync.rigid.isKinematic;
            sync.rigid.isKinematic = sync.kinematicFlag;
            alreadyAtEnd = false;
        }

        public override void OnExitState()
        {
            //reset kinematic to whatever it was before we lerped.
            sync.rigid.isKinematic = kinematicOnEnter;
        }

        Vector3 tempPos;
        Quaternion tempRot;
        public override bool OnLerp(float elapsedTime, float autoSmoothedLerp)
        {
            if (!alreadyAtEnd || !sync.rigid.isKinematic)
            {
                progress = lerpDuration <= 0 ? 1 : Mathf.Clamp01(elapsedTime / lerpDuration);
                tempPos = sync.HermiteInterpolatePosition(recordedPos, recordedVel, sync.pos, sync.vel, progress, lerpDuration);
                tempRot = Quaternion.Slerp(recordedRot, sync.rot, progress);
                sync.ApplyTransforms(tempPos, tempRot);
                if (progress >= 1)
                {
                    alreadyAtEnd = true;
                }
            }
            if (secondsUntilEnablingPhysics >= 0 && elapsedTime - lerpDuration >= secondsUntilEnablingPhysics)
            {
                if (sync.IsOwner())
                {
                    sync.ApplyVelocities();
                    ExitState();
                }
                return false;
            }
            return true;
        }

        Vector3 targetPos;
        public void LerpTo(bool useLocalTransforms, bool forceKinematic, Vector3 position, Quaternion rotation, Vector3 velocity, Vector3 angularVelocity)
        {
            EnterState();
            sync.localTransformFlag = useLocalTransforms;
            sync.kinematicFlag = forceKinematic;
            sync.pos = position;
            sync.rot = rotation;
            sync.vel = velocity;
            sync.spin = angularVelocity;
        }

        public void LerpToSpawn()
        {
            LerpTo(!sync.useWorldSpaceTransforms, false, sync.spawnPos, sync.spawnRot, Vector3.zero, Vector3.zero);
        }


        //Helper functions
        Vector3 recordedPos;
        Quaternion recordedRot;
        Vector3 recordedVel;
        Vector3 recordedSpin;
        public void RecordWorldTransforms()
        {
            recordedPos = sync.rigid.position;
            recordedRot = sync.rigid.rotation;
            recordedVel = sync.rigid.velocity;
            recordedSpin = sync.rigid.angularVelocity;
        }

        public void RecordLocalTransforms()
        {
            recordedPos = transform.localPosition;
            recordedRot = transform.localRotation;
            recordedVel = Quaternion.Inverse(sync.rigid.rotation) * sync.rigid.velocity;
            recordedSpin = Quaternion.Inverse(sync.rigid.rotation) * sync.rigid.angularVelocity;
        }

        //Copied from LightSyncLooper
        public float GetAutoSmoothedInterpolation(float realElapsedTime)
        {
            if (sync.teleportFlag)
            {
                sync.teleportFlag = false;
                return 1;
            }
            // return lerpPeriod <= 0 ? 1 : (Time.realtimeSinceStartup - startTime) / lerpPeriod;
            return lerpDuration <= 0 ? 1 : realElapsedTime / lerpDuration;
        }
    }
}
