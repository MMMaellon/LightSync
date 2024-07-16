
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

namespace MMMaellon.LightSync
{
    [AddComponentMenu("LightSync/ObjectPool Spawner (lightsync)")]
    public class ObjectPoolSpawner : UdonSharpBehaviour
    {
        public ObjectPool pool;

        public Transform spawnCenter;
        public float spawnPositionVariation = 0.0f;
        public float spawnRotationVariation = 0.0f;

        public void Spawn()
        {
            if (!spawnCenter)
            {
                pool.SpawnRandom();
            }
            else
            {
                pool.SpawnRandom(spawnCenter.position + spawnPositionVariation * Random.insideUnitSphere, spawnCenter.rotation * Quaternion.Euler(Random.Range(-spawnRotationVariation, spawnRotationVariation), Random.Range(-spawnRotationVariation, spawnRotationVariation), Random.Range(-spawnRotationVariation, spawnRotationVariation)));
            }
        }

        public override void Interact()
        {
            Spawn();
        }

        public void Reset()
        {
            spawnCenter = transform;
        }
    }
}
