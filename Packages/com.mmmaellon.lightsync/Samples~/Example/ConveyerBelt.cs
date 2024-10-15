
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

public class ConveyerBelt : UdonSharpBehaviour
{
    public override void OnPlayerTriggerStay(VRCPlayerApi player)
    {
        if (Utilities.IsValid(player) && player.isLocal)
        {
            var speed = 0.5f;
            var conveyerDirection = transform.rotation * Vector3.forward;
            player.SetVelocity(conveyerDirection * speed);
        }
    }
}
