
using UdonSharp;
using UnityEngine;
using VRC.SDK3.Components;
using VRC.SDKBase;
using VRC.Udon;

namespace MMMaellon.LightSync
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    [RequireComponent(typeof(LightSync))]
    [RequireComponent(typeof(VRCPickup))]
    public class MultiPickupHandle : LightSyncEnhancement
    {
        public MultiPickupObject parentObject;

        public float maxForce = 1f;
        public float maxTorque = 1f;

        [UdonSynced]
        public Vector3 offsetPos;
        [UdonSynced]
        public Quaternion offsetRot;

        public void Setup(MultiPickupObject obj)
        {
            parentObject = obj;
            sync.rigid.isKinematic = true;
            transform.SetParent(parentObject.transform, true);
            RecordOffsets();
        }

        public void RecordOffsets()
        {
            offsetPos = parentObject.transform.InverseTransformPoint(transform.position);
            offsetRot = Quaternion.Inverse(parentObject.transform.rotation) * transform.rotation;
        }

        public Vector3 CalcTargetPos()
        {
            return parentObject.transform.TransformPoint(offsetPos);
        }

        public Quaternion CalcTargetRot()
        {
            return parentObject.transform.rotation * offsetRot;
        }

        // public override void OnPickup()
        // {
        //     RecordOffsets();
        // }

        public void Detach()
        {
            transform.SetParent(null, true);
        }

        public void Attach()
        {
#if UNITY_EDITOR
            if (!parentObject)
            {
                return;
            }
#endif
            transform.SetParent(parentObject.transform, true);
            transform.localPosition = offsetPos;
            transform.localRotation = offsetRot;
            sync.SyncIfOwner();
        }

        Vector3 targetPos;
        Quaternion targetRot;
        Vector3 posForce;
        Vector3 posTorque;
        Vector3 rotForce;
        Vector3 rotTorque;
        Vector3 distanceVector;
        Vector3 targetDistanceVector;
        float angle;

        public void ApplyForces(float delta)
        {
            targetPos = CalcTargetPos();
            targetRot = CalcTargetRot();

            distanceVector = transform.position - parentObject.rigid.worldCenterOfMass;
            targetDistanceVector = targetPos - parentObject.rigid.worldCenterOfMass;


            if (maxForce != 0)
            {
                posForce = distanceVector.normalized * (distanceVector.magnitude - targetDistanceVector.magnitude) / delta;
                angle = Vector3.Angle(targetDistanceVector, distanceVector);
                posTorque = Vector3.Cross(targetDistanceVector, distanceVector).normalized * angle;

                if (maxTorque != 0)
                {

                }
            }
            else if (maxTorque != 0)
            {
                targetRot = transform.rotation * Quaternion.Inverse(parentObject.transform.rotation);
                targetRot.ToAngleAxis(out angle, out rotForce);
                rotTorque = rotForce.normalized * Mathf.Clamp(angle, 0, maxTorque);
                rotForce = Vector3.Cross(targetDistanceVector, rotTorque);
            }

            if (maxForce != 0 || maxTorque != 0)
            {
                var proj = Vector3.Project(parentObject.rigid.velocity, posForce);
                var invProj = parentObject.rigid.velocity - proj;
                var adjustment = Vector3.Cross(invProj, distanceVector) * Mathf.PI;

                parentObject.rigid.AddTorque((rotForce + adjustment - parentObject.rigid.angularVelocity) / delta, ForceMode.Acceleration);
                parentObject.rigid.AddForce(Vector3.ClampMagnitude(posForce - proj, maxForce) / delta, ForceMode.Acceleration);
            }
        }
    }
}
