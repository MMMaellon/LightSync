
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

public class Spinner : UdonSharpBehaviour
{
    public Rigidbody rigid;
    float lastUpdate;
    public void FixedUpdate()
    {
        if (!Networking.LocalPlayer.isMaster)
        {
            transform.localPosition = Vector3.down * 0.02f;
            return;
        }
        if (lastUpdate + 1.0f > Time.timeSinceLevelLoad)
        {
            transform.localPosition = Vector3.down * 0.02f;
            return;
        }
        lastUpdate = Time.timeSinceLevelLoad;
        rigid.angularVelocity = Vector3.forward * 30f;
        transform.localPosition = Vector3.up * Random.Range(-0.01f, 0.01f);
    }
}
