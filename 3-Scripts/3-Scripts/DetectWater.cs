using UnityEngine;

namespace StormBreakers
{
    public class DetectWater : MonoBehaviour
    {
        // this component detect if there is water at the game object's position and how deep it is

        // public data
        public bool IsUnderwater { get; private set; } = false;
        [Tooltip("Is positive inside water")] public float Depth { get; private set; } = 0f;

        public float WaterHeight { get; private set; } = 0f;

        // internal data
        private Vector3 undeformedPosition;

        void Start()
        {
            // setting the first undeformed position as the position 
            undeformedPosition = transform.position;
        }

        void Update()
        {
            // getting the ground depth
            float groundDepth = 200f;
            if (Ocean.useTerrain)
            {
                groundDepth = -Ocean.terrain.SampleHeight(undeformedPosition) - Ocean.terrain.transform.position.y;
            }
            
            // calculating roughly the ocean height while updating the undeformed position
            WaterHeight = Ocean.GetHeight(Time.time, transform.position, ref undeformedPosition, out _, groundDepth, false);

            // deducing the public data
            Depth = WaterHeight - transform.position.y;
            IsUnderwater = Depth >= 0f;
        }
    }
}