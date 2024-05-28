
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

public class HideIfNotMaster : UdonSharpBehaviour
{
    void Start()
    {
        if (!Networking.LocalPlayer.isMaster)
        {
            gameObject.SetActive(false);
        }
    }
}
