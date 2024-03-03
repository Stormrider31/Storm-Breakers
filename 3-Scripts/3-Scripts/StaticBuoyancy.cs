using UnityEngine;

namespace StormBreakers
{
    public class StaticBuoyancy : MonoBehaviour
    {
        // This component make an object follow the waves like it's floating but without using physic

        // the saved initial position of the game object
        private Vector3 undeformedPosition;

        void Start()
        {
            // saving the initial position
            undeformedPosition = transform.position;
        }

        void Update()
        {
            // getting the ground depth
            float groundDepth = 200f;
            if (Ocean.useTerrain)
            {
                groundDepth = - Ocean.terrain.SampleHeight(undeformedPosition) - Ocean.terrain.transform.position.y;
            }

            // computing the ocean waves
            Vector3 deformation = Ocean.OceanDeformation(Time.time, undeformedPosition, out _ , groundDepth);

            // setting the new position
            transform.position = undeformedPosition + deformation;
        }
    }
}