using VRC.SDKBase;
using VRC.Udon.Common;
using System.Collections.Generic;
using UnityEngine;
using System;



#if !COMPILER_UDONSHARP && UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UdonSharpEditor;


namespace MMMaellon.LightSync
{
    [CustomEditor(typeof(AttachToPlayer)), CanEditMultipleObjects]

    public class AttachToPlayerEditor : Editor
    {
        SerializedProperty[] bones;
        SerializedProperty dataProperty;
        static bool showBones = true;
        static bool showAdvanced = false;

        public void OnEnable()
        {
            // Fetch the objects from the MyScript script to display in the inspector
            dataProperty = serializedObject.FindProperty("data");
            AttachToPlayer attach = (AttachToPlayer)target;
            bones = boneNames.Select(name => serializedObject.FindProperty(name)).ToArray();
        }
        public override void OnInspectorGUI()
        {
            if (UdonSharpGUI.DrawDefaultUdonSharpBehaviourHeader(targets)) return;
            base.OnInspectorGUI();
            AttachToPlayer attach = (AttachToPlayer)target;
            if (!attach)
            {
                return;
            }

            serializedObject.Update();
            EditorGUI.BeginChangeCheck();
            if (attach.attachToAvatarBones)
            {
                showBones = EditorGUILayout.BeginFoldoutHeaderGroup(showBones, "Allowed Bones");
                if (showBones)
                {
                    EditorGUI.indentLevel++;
                    foreach (var bone in bones)
                    {
                        EditorGUILayout.PropertyField(bone);
                    }
                    EditorGUI.indentLevel--;
                }
                EditorGUILayout.EndFoldoutHeaderGroup();
            }
            EditorGUILayout.Space();
            showAdvanced = EditorGUILayout.BeginFoldoutHeaderGroup(showAdvanced, "Advanced");
            if (showAdvanced)
            {
                EditorGUI.indentLevel++;
                GUI.enabled = false;
                EditorGUILayout.PropertyField(dataProperty);
                GUI.enabled = false;
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
            serializedObject.ApplyModifiedProperties();
            if (EditorGUI.EndChangeCheck())
            {
                SyncAllowedBones();
            }
            EditorGUILayout.Space();
            serializedObject.ApplyModifiedProperties();
        }

        public void SyncAllowedBones()
        {
            foreach (AttachToPlayer attach in targets.Cast<AttachToPlayer>())
            {
                SyncAllowedBones(attach);
            }
        }

        public void SyncAllowedBones(AttachToPlayer attach)
        {
            SerializedObject serializedAttach = new SerializedObject(attach);
            List<int> allowedBones = new List<int>();
            for (int i = 0; i < bones.Length; i++)
            {
                if (serializedAttach.FindProperty(boneNames[i]).boolValue)
                {
                    if (Enum.TryParse(boneNames[i], out HumanBodyBones bone))
                    {
                        allowedBones.Add((int)bone);
                    }
                }
            }
            attach.allowedBones = allowedBones.ToArray();
        }

        static readonly string[] boneNames = {
            "Hips",
            "LeftUpperLeg",
            "RightUpperLeg",
            "LeftLowerLeg",
            "RightLowerLeg",
            "LeftFoot",
            "RightFoot",
            "Spine",
            "Chest",
            "UpperChest",
            "Neck",
            "Head",
            "LeftShoulder",
            "RightShoulder",
            "LeftUpperArm",
            "RightUpperArm",
            "LeftLowerArm",
            "RightLowerArm",
            "LeftHand",
            "RightHand",
            "LeftToes",
            "RightToes",
            "LeftEye",
            "RightEye",
            "Jaw",
            "LeftThumbProximal",
            "LeftThumbIntermediate",
            "LeftThumbDistal",
            "LeftIndexProximal",
            "LeftIndexIntermediate",
            "LeftIndexDistal",
            "LeftMiddleProximal",
            "LeftMiddleIntermediate",
            "LeftMiddleDistal",
            "LeftRingProximal",
            "LeftRingIntermediate",
            "LeftRingDistal",
            "LeftLittleProximal",
            "LeftLittleIntermediate",
            "LeftLittleDistal",
            "RightThumbProximal",
            "RightThumbIntermediate",
            "RightThumbDistal",
            "RightIndexProximal",
            "RightIndexIntermediate",
            "RightIndexDistal",
            "RightMiddleProximal",
            "RightMiddleIntermediate",
            "RightMiddleDistal",
            "RightRingProximal",
            "RightRingIntermediate",
            "RightRingDistal",
            "RightLittleProximal",
            "RightLittleIntermediate",
            "RightLittleDistal",
            "LastBone"
        };
    }
}

#endif

namespace MMMaellon.LightSync
{
    [AddComponentMenu("LightSync/Attach To Player (lightsync)")]
    public class AttachToPlayer : LightSyncEnhancementWithData
    {
        [Tooltip("Objects further than this from a valid target will not get attached")]
        public float maxDistance = 0.5f;
        [Tooltip("Objects further than the max distance but within the snap distance of the max distance will get snapped towards the target until they're within the max distance")]
        public float snapDistance = 0.0f;
        [Space]
        public int playerSampleSize = 3;
        public bool advancedDistanceCheck = true;
        public Transform attachCenterOverride;
        [Space]
        public bool attachOnDrop = true;
        public bool attachOnPickupUseDown = false;
        public bool attachOnRightStickDown = false;
        [Space]
        public bool attachToSelf = true;
        public bool attachToOthers = false;
        public bool attachToAvatarBones = true;
        [HideInInspector]
        public int[] allowedBones = { 0 };
        [HideInInspector]
        public bool Hips = true;
        [HideInInspector]
        public bool LeftUpperLeg;
        [HideInInspector]
        public bool RightUpperLeg;
        [HideInInspector]
        public bool LeftLowerLeg;
        [HideInInspector]
        public bool RightLowerLeg;
        [HideInInspector]
        public bool LeftFoot;
        [HideInInspector]
        public bool RightFoot;
        [HideInInspector]
        public bool Spine;
        [HideInInspector]
        public bool Chest;
        [HideInInspector]
        public bool UpperChest;
        [HideInInspector]
        public bool Neck;
        [HideInInspector]
        public bool Head;
        [HideInInspector]
        public bool LeftShoulder;
        [HideInInspector]
        public bool RightShoulder;
        [HideInInspector]
        public bool LeftUpperArm;
        [HideInInspector]
        public bool RightUpperArm;
        [HideInInspector]
        public bool LeftLowerArm;
        [HideInInspector]
        public bool RightLowerArm;
        [HideInInspector]
        public bool LeftHand;
        [HideInInspector]
        public bool RightHand;
        [HideInInspector]
        public bool LeftToes;
        [HideInInspector]
        public bool RightToes;
        [HideInInspector]
        public bool LeftEye;
        [HideInInspector]
        public bool RightEye;
        [HideInInspector]
        public bool Jaw;
        [HideInInspector]
        public bool LeftThumbProximal;
        [HideInInspector]
        public bool LeftThumbIntermediate;
        [HideInInspector]
        public bool LeftThumbDistal;
        [HideInInspector]
        public bool LeftIndexProximal;
        [HideInInspector]
        public bool LeftIndexIntermediate;
        [HideInInspector]
        public bool LeftIndexDistal;
        [HideInInspector]
        public bool LeftMiddleProximal;
        [HideInInspector]
        public bool LeftMiddleIntermediate;
        [HideInInspector]
        public bool LeftMiddleDistal;
        [HideInInspector]
        public bool LeftRingProximal;
        [HideInInspector]
        public bool LeftRingIntermediate;
        [HideInInspector]
        public bool LeftRingDistal;
        [HideInInspector]
        public bool LeftLittleProximal;
        [HideInInspector]
        public bool LeftLittleIntermediate;
        [HideInInspector]
        public bool LeftLittleDistal;
        [HideInInspector]
        public bool RightThumbProximal;
        [HideInInspector]
        public bool RightThumbIntermediate;
        [HideInInspector]
        public bool RightThumbDistal;
        [HideInInspector]
        public bool RightIndexProximal;
        [HideInInspector]
        public bool RightIndexIntermediate;
        [HideInInspector]
        public bool RightIndexDistal;
        [HideInInspector]
        public bool RightMiddleProximal;
        [HideInInspector]
        public bool RightMiddleIntermediate;
        [HideInInspector]
        public bool RightMiddleDistal;
        [HideInInspector]
        public bool RightRingProximal;
        [HideInInspector]
        public bool RightRingIntermediate;
        [HideInInspector]
        public bool RightRingDistal;
        [HideInInspector]
        public bool RightLittleProximal;
        [HideInInspector]
        public bool RightLittleIntermediate;
        [HideInInspector]
        public bool RightLittleDistal;
        [HideInInspector]
        public bool LastBone;

        [HideInInspector]
        public AttachToPlayerData data;

        VRCPlayerApi[] attachTargets = new VRCPlayerApi[4];
        VRCPlayerApi attachTarget;
        public void Attach()
        {
            sync.TakeOwnershipIfNotOwner();
            FindNearestPlayers(ref attachTargets);
            if (attachTargets == null || attachTargets.Length <= 0 || !Utilities.IsValid(attachTargets[0]))
            {
                return;
            }
            if (attachToAvatarBones)
            {
                FindNearestBone(attachTargets, out attachTarget, out data.bone);
                data.playerId = attachTarget.playerId;
            }
            else
            {
                data.bone = -1001;
                data.playerId = attachTargets[0].playerId;
            }
            RecordPositions();
            if (data.playerId == localPlayer.playerId)
            {
                PerformAttachment();
            }
            else
            {
                data.RequestSync();
            }
        }

        VRCPlayerApi[] _allPlayers = new VRCPlayerApi[82];
        float _currentPlayerDistance;
        public void FindNearestPlayers(ref VRCPlayerApi[] players)
        {
            if (players == null || players.Length <= 0)
            {
                return;
            }
            if (!attachToOthers)
            {
                if (attachToSelf)
                {
                    players = new VRCPlayerApi[1];
                    players[0] = localPlayer;
                    return;
                }
                else
                {
                    return;
                }
            }

            float[] distances = new float[players.Length];
            _allPlayers = VRCPlayerApi.GetPlayers(_allPlayers);
            foreach (VRCPlayerApi player in _allPlayers)
            {
                if (!Utilities.IsValid(player) || (player.isLocal && !attachToSelf))
                {
                    continue;
                }
                if (advancedDistanceCheck)
                {
                    if (attachCenterOverride)
                    {
                        _currentPlayerDistance = Vector3.Distance(attachCenterOverride.position, FindNearestPoint(player.GetPosition(), player.GetPosition() + Vector3.up * player.GetAvatarEyeHeightAsMeters(), attachCenterOverride.position));
                    }
                    else
                    {
                        _currentPlayerDistance = Vector3.Distance(sync.rigid.position, FindNearestPoint(player.GetPosition(), player.GetPosition() + Vector3.up * player.GetAvatarEyeHeightAsMeters(), sync.rigid.position));
                    }
                }
                else
                {
                    if (attachCenterOverride)
                    {
                        _currentPlayerDistance = Vector3.Distance(attachCenterOverride.position, player.GetPosition());
                    }
                    else
                    {
                        _currentPlayerDistance = Vector3.Distance(sync.rigid.position, player.GetPosition());
                    }
                }
                for (int i = 0; i < distances.Length; i++)
                {
                    if (distances[i] <= 0 || distances[i] > _currentPlayerDistance)
                    {
                        if (i + 1 < distances.Length)
                        {
                            distances[i + 1] = distances[i];
                            players[i + 1] = players[i];
                        }
                        distances[i] = _currentPlayerDistance;
                        players[i] = player;
                    }
                }
            }
        }

        public void FindNearestBone(VRCPlayerApi[] players, out VRCPlayerApi player, out int bone)
        {
            bone = 0;
            player = null;
            float nearestDistance = -1001f;
            float currentDistance;
            foreach (VRCPlayerApi p in players)
            {
                foreach (int b in allowedBones)
                {
                    if (advancedDistanceCheck)
                    {
                        if (attachCenterOverride)
                        {
                            currentDistance = Vector3.Distance(attachCenterOverride.position, FindNearestPoint(p.GetBonePosition((HumanBodyBones)b), FindBoneEnd((HumanBodyBones)b, p), attachCenterOverride.position));
                        }
                        else
                        {
                            currentDistance = Vector3.Distance(sync.rigid.position, FindNearestPoint(p.GetBonePosition((HumanBodyBones)b), FindBoneEnd((HumanBodyBones)b, p), sync.rigid.position));
                        }
                    }
                    else
                    {
                        if (attachCenterOverride)
                        {
                            currentDistance = Vector3.Distance(attachCenterOverride.position, p.GetBonePosition((HumanBodyBones)b));
                        }
                        else
                        {
                            currentDistance = Vector3.Distance(sync.rigid.position, p.GetBonePosition((HumanBodyBones)b));
                        }
                    }

                    if (currentDistance > maxDistance + snapDistance)
                    {
                        continue;
                    }

                    if (nearestDistance < 0 || nearestDistance > currentDistance)
                    {
                        nearestDistance = currentDistance;
                        player = p;
                        bone = b;
                    }
                }
            }
        }

        public Vector3 FindBoneEnd(HumanBodyBones humanBodyBone, VRCPlayerApi player)
        {
            Vector3 bonePos = player.GetBonePosition(humanBodyBone);
            switch (humanBodyBone)
            {
                case HumanBodyBones.LeftUpperLeg:
                    {
                        return player.GetBonePosition(HumanBodyBones.LeftLowerLeg);
                    }
                case HumanBodyBones.RightUpperLeg:
                    {
                        return player.GetBonePosition(HumanBodyBones.RightLowerLeg);
                    }
                case HumanBodyBones.LeftLowerLeg:
                    {
                        return player.GetBonePosition(HumanBodyBones.LeftFoot);
                    }
                case HumanBodyBones.RightLowerLeg:
                    {
                        return player.GetBonePosition(HumanBodyBones.RightFoot);
                    }
                case HumanBodyBones.LeftUpperArm:
                    {
                        return player.GetBonePosition(HumanBodyBones.LeftLowerArm);
                    }
                case HumanBodyBones.RightUpperArm:
                    {
                        return player.GetBonePosition(HumanBodyBones.RightLowerArm);
                    }
                case HumanBodyBones.LeftLowerArm:
                    {
                        return player.GetBonePosition(HumanBodyBones.LeftHand);
                    }
                case HumanBodyBones.RightLowerArm:
                    {
                        return player.GetBonePosition(HumanBodyBones.RightHand);
                    }
            }
            return bonePos;
        }

        Vector3 _centeredVector;
        Vector3 _projectedTarget;
        public Vector3 FindNearestPoint(Vector3 start, Vector3 end, Vector3 target)
        {
            _centeredVector = end - start;
            target -= start;
            _projectedTarget = Vector3.Project(target, _centeredVector);
            if (Vector3.Dot(_projectedTarget, target) <= 0)
            {
                return start;
            }
            else if (target.magnitude > _projectedTarget.magnitude)
            {
                return end;
            }
            return start + _projectedTarget;
        }

        public void RecordPositions()
        {
            attachTarget = VRCPlayerApi.GetPlayerById(data.playerId);
            if (!Utilities.IsValid(attachTarget) || !attachTarget.IsValid())
            {
                return;
            }
            Vector3 parentPos;
            Vector3 parentEndPos;
            Quaternion parentRot;

            if (!attachToAvatarBones || data.bone < 0 || data.bone >= (int)HumanBodyBones.LastBone)
            {
                data.bone = -1001;
                parentPos = attachTarget.GetPosition();
                parentRot = attachTarget.GetRotation();
                parentEndPos = attachTarget.GetPosition() + (Vector3.up * attachTarget.GetAvatarEyeHeightAsMeters());
            }
            else
            {
                parentPos = attachTarget.GetBonePosition((HumanBodyBones)data.bone);
                parentRot = attachTarget.GetBoneRotation((HumanBodyBones)data.bone);
                parentEndPos = FindBoneEnd((HumanBodyBones)data.bone, attachTarget);
            }
            var invParentRot = Quaternion.Inverse(parentRot);
            var snapOffset = Vector3.zero;
            if (snapDistance > 0)
            {
                if (advancedDistanceCheck)
                {
                    snapOffset = sync.rigid.position - FindNearestPoint(parentPos, parentEndPos, sync.rigid.position);
                }
                else
                {
                    snapOffset = sync.rigid.position - parentPos;
                }
                if (snapOffset.magnitude > maxDistance)
                {
                    snapOffset = snapOffset.normalized * (maxDistance - snapOffset.magnitude);
                }
            }
            data.position = invParentRot * (sync.rigid.position + snapOffset - parentPos);
            data.rotation = invParentRot * sync.rigid.rotation;
        }

        VRCPlayerApi localPlayer;
        public void Start()
        {
            localPlayer = Networking.LocalPlayer;
        }

        public override void OnDataDeserialization()
        {
            PerformAttachment();
        }

        void PerformAttachment()
        {
            if (data.playerId == localPlayer.playerId)
            {
                sync.TakeOwnershipIfNotOwner();
                if (data.bone >= 0 && data.bone < (int)HumanBodyBones.LastBone)
                {
                    sync.state = (sbyte)(LightSync.STATE_BONE - data.bone);
                }
                else
                {
                    sync.state = LightSync.STATE_LOCAL_TO_OWNER;
                }
                sync.pos = data.position;
                sync.rot = data.rotation;
                sync.Sync();
            }
        }

        public override string GetDataTypeName()
        {
            return "MMMaellon.LightSync.AttachToPlayerData";
        }

        public override void OnDataObjectCreation(LightSyncEnhancementData enhancementData)
        {
            data = (AttachToPlayerData)enhancementData;
        }

        public override void OnDrop()
        {
            if (attachOnDrop)
            {
                Attach();
            }
        }

        public override void OnPickupUseDown()
        {
            if (attachOnPickupUseDown)
            {
                Attach();
            }
        }

        public override void InputLookVertical(float value, UdonInputEventArgs args)
        {
            if (attachOnRightStickDown && sync.pickup.IsHeld && value < -0.95f && Networking.LocalPlayer.IsUserInVR())
            {
                Attach();
            }
        }

    }
}
