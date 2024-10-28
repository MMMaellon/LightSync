
using UdonSharp;
using UnityEngine;
using VRC.SDK3.Components;
using VRC.SDKBase;
using VRC.Udon;

public class SpawnTrigger : UdonSharpBehaviour
{
    public VRCObjectPool pool;
    public override void Interact()
    {
        Networking.SetOwner(Networking.LocalPlayer, pool.gameObject);
        pool.TryToSpawn();
    }
}
