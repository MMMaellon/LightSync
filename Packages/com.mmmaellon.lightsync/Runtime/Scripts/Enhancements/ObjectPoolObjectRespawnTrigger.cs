
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

namespace MMMaellon.LightSync
{
    public class ObjectPoolObjectRespawnTrigger : UdonSharpBehaviour
    {
        ObjectPoolObject obj;
        public void OnTriggerEnter(Collider other)
        {
            if (other)
            {
                obj = other.GetComponent<ObjectPoolObject>();
                if (obj && obj.sync.IsOwner())
                {
                    obj.Hide();
                }
            }
        }
    }
}
