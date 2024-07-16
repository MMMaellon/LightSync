
using MMMaellon;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

public class Teleporter : UdonSharpBehaviour
{
    public SmartObjectSync[] smarts;
    public MMMaellon.LightSync.LightSync[] lights;
    bool up = false;
    public override void Interact()
    {
        up = !up;
        if (up)
        {
            foreach (var smart in smarts)
            {
                smart.TeleportToLocalSpace(smart.spawnPos + (Vector3.up * 0.25f), Quaternion.identity, Vector3.zero, Vector3.zero);
            }
            foreach (var light in lights)
            {
                light.TeleportToLocalSpace(light.spawnPos + (Vector3.up * 0.25f), light.spawnRot, true);
            }
        }
        else
        {
            foreach (var smart in smarts)
            {
                smart.TeleportToLocalSpace(smart.spawnPos, Quaternion.identity, Vector3.zero, Vector3.zero);
            }
            foreach (var light in lights)
            {
                light.TeleportToLocalSpace(light.spawnPos, light.spawnRot, true);
            }
        }
    }
}
