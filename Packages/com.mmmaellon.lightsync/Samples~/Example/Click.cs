
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

public class Click : UdonSharpBehaviour
{
    public UdonBehaviour targetBehaviour;
    public string targetEvent;
    public override void Interact()
    {
        targetBehaviour.SendCustomEvent(targetEvent);
    }
}
