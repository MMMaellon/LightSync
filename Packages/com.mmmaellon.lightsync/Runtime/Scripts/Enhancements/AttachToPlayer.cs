using UnityEngine;
using VRC.SDKBase;


#if !COMPILER_UDONSHARP && UNITY_EDITOR
using UnityEditor;
using UdonSharpEditor;


namespace MMMaellon.LightSync
{
    [CustomEditor(typeof(AttachToPlayer)), CanEditMultipleObjects]

    public class AttachToPlayerEditor : Editor
    {
        SerializedProperty _allowedBones;
        SerializedProperty allowedBones;

        public void OnEnable()
        {
            // Fetch the objects from the MyScript script to display in the inspector
            _allowedBones = serializedObject.FindProperty("_allowedBones");
            allowedBones = serializedObject.FindProperty("allowedBones");
            SyncAllowedBones();
        }
        public override void OnInspectorGUI()
        {
            if (!target || UdonSharpGUI.DrawDefaultUdonSharpBehaviourHeader(target)) return;
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
                EditorGUILayout.PropertyField(_allowedBones, true);
            }
            if (EditorGUI.EndChangeCheck())
            {
                SyncAllowedBones();
            }
            EditorGUILayout.Space();
        }

        public void SyncAllowedBones()
        {
            allowedBones.ClearArray();
            AttachToPlayer attach = (AttachToPlayer)target;
            if (attach && attach.attachToAvatarBones)
            {
                for (int i = 0; i < _allowedBones.arraySize; i++)
                {
                    allowedBones.InsertArrayElementAtIndex(i);
                    allowedBones.GetArrayElementAtIndex(i).intValue = _allowedBones.GetArrayElementAtIndex(i).enumValueIndex;
                }
            }
            serializedObject.ApplyModifiedProperties();
        }
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
        public bool advancedDistanceCheck = true;
        public int playerSampleSize = 3;
        public Transform attachCenterOverride;
        public bool attachOnDrop = true;
        public bool attachOnPickupUseDown = false;
        public bool attachOnRightStickDown = false;
        public bool attachToSelf = true;
        public bool attachToOthers = false;
        public bool attachToAvatarBones = true;
        [HideInInspector]
        public int[] allowedBones = { 0 };
#if !COMPILER_UDONSHARP && UNITY_EDITOR
        //For displaying in the editor only
        [HideInInspector]
        public HumanBodyBones[] _allowedBones = { 0 };
#else
        //For displaying in the editor only
        [HideInInspector]
        public int[] _allowedBones = { 0 };
#endif

        AttachToPlayerData data;

        VRCPlayerApi[] attachTargets;
        VRCPlayerApi attachTarget;
        public void Attach()
        {
            if (!sync.IsOwner())
            {
                Networking.SetOwner(Networking.LocalPlayer, gameObject);
            }
            FindNearestPlayers(ref attachTargets);
            if (attachTargets == null || attachTargets.Length <= 0 || !Utilities.IsValid(attachTargets[0]) || !attachTargets[0].IsValid())
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
            data.RequestSerialization();
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
            VRCPlayerApi.GetPlayers(_allPlayers);
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
            float currentDistance = -1001f;
            foreach (VRCPlayerApi p in players)
            {
                foreach (int b in allowedBones)
                {
                    if (advancedDistanceCheck)
                    {
                        if (attachCenterOverride)
                        {
                            currentDistance = Vector3.Distance(attachCenterOverride.position, FindNearestPoint(player.GetBonePosition((HumanBodyBones)b), FindBoneEnd((HumanBodyBones)b, p), attachCenterOverride.position));
                        }
                        else
                        {
                            currentDistance = Vector3.Distance(sync.rigid.position, FindNearestPoint(player.GetBonePosition((HumanBodyBones)b), FindBoneEnd((HumanBodyBones)b, p), sync.rigid.position));
                        }
                    }
                    else
                    {
                        if (attachCenterOverride)
                        {
                            currentDistance = Vector3.Distance(attachCenterOverride.position, player.GetBonePosition((HumanBodyBones)b));
                        }
                        else
                        {
                            currentDistance = Vector3.Distance(sync.rigid.position, player.GetBonePosition((HumanBodyBones)b));
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
            if (data.playerId == localPlayer.playerId)
            {
                sync._print("body attachment deserialization");
                Networking.SetOwner(localPlayer, gameObject);
                if (data.bone >= 0 && data.bone < (int)HumanBodyBones.LastBone)
                {
                    sync.state = LightSyncData.STATE_BONE - data.bone;
                }
                else
                {
                    sync.state = LightSyncData.STATE_LOCAL_TO_OWNER;
                }
                sync.data.pos = data.position;
                sync.data.rot = data.rotation;
                sync.data.RequestSerialization();
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

    }
}
