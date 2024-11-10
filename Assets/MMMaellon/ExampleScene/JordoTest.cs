
using MMMaellon;
using MMMaellon.LightSync;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
public class JordoTest : UdonSharpBehaviour
{
    public LightSync junk;
    public LightSync[] platforms;
    public SmartObjectSync smartJunk;
    public SmartObjectSync[] smartPlatforms;
    void Start()
    {
        if (Networking.LocalPlayer.IsOwner(gameObject))
        {
            ResetSim();
        }
    }

    public void ResetSim()
    {
        junk.Respawn();
        smartJunk.Respawn();
        foreach (var platform in platforms)
        {
            platform.Respawn();
        }
        foreach (var smartPlatform in smartPlatforms)
        {
            smartPlatform.Respawn();
        }
        SendCustomEventDelayedSeconds(nameof(StartSim), 1f);
    }

    public void StartSim()
    {
        if (Networking.IsClogged)
        {
            SendCustomEventDelayedSeconds(nameof(StartSim), 1f);
            return;
        }
        junk.Respawn();
        smartJunk.Respawn();
        junk.rigid.rotation = Random.rotation;
        junk.rigid.velocity = Vector3.forward * 10f + Random.insideUnitSphere;
        smartJunk.rigid.rotation = junk.rigid.rotation;
        smartJunk.rigid.velocity = junk.rigid.velocity;
        junk.Sync();
        smartJunk.Serialize();
        SendCustomEventDelayedSeconds(nameof(ResetSim), 8f);
    }
}
