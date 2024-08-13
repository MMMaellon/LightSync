
using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;
using VRC.Udon;

public class NetworkCloggedDetector : UdonSharpBehaviour
{
    public Text text;
    float clogStart = -1001f;
    bool clogged = false;
    public void Update()
    {

        if (clogged)
        {
            if (!Networking.IsClogged)
            {
                clogged = false;
                if (text)
                {
                    text.text = "Status: Not clogged. Total clogged Time: " + (Time.timeSinceLevelLoad - clogStart);
                }
            }
            else
            {
                if (text)
                {
                    text.text = "Status: ⚠ Clogged ⚠ Total clogged Time: " + (Time.timeSinceLevelLoad - clogStart);
                }
            }
        }
        else
        {
            if (Networking.IsClogged)
            {
                clogStart = Time.timeSinceLevelLoad;
                clogged = true;
            }
        }
    }
}
