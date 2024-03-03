using UnityEngine;

namespace StormBreakers
{
    public class Foil : MonoBehaviour
    {
        // This component makes the forces of a profile in the water or in the air. Use this component for any fins, keel, or even sails and hydrofoils (physics might be a bit unprecise tough)
        // Is supposed to be child of a rigidbody on which it will apply the forces.

        [Tooltip("Tip this when the water is flat or when the object is away from focus, this will prevent the waves to be computed and saving some resource.")]
        public bool flatWater = false;

        [Tooltip("The lift coefficient. The higher, the more force it will generate at low angle of attack.")]
        [Range(0f, 50f)] public float liftCoeff = 10f;

        // internal data
        private int numberOfTriangles; // the number of triangle the mesh contains
        private Vector3[] triangleCenter; // the centers of each triangle in local space
        private float[] triangleArea; // the area of the triangle
        private Vector3[] triangleNormal; // the normals of the triangles in local space
        private Vector3[] undeformedPosition; // used to store the previous undeformed position of each triangle
        private Vector3[] previousWorldTriangleCenter; // used to update undeformed position reguardldess of the waves
        private Rigidbody rb; // the rigidbody attached to the parent of this game object
        private Mesh colliderMesh; // the mesh that will be used to computes the drag force

        private float[] waterHeight; // is set in Update and got in fixedUpdate
        private Vector3[] fluidVelocity; // is set in Update and got in fixedUpdate

        [Space(10)]
        [Header("Debugging")]
        [Tooltip("The maximal pressure apllied due to the speed. Since it evolve with the square of speed, this can reach higher values that the physic can handles for small object moving fast. Lower it when the object bounce too fast off the water.")]
        public float maximalPressure = 10000f;
        [Tooltip("Indicate the number of time the maximal dynamic pressure is reached, if it happens too often this mean the physics might be impacted and you should consider rise the maximal pressure if there isn't trouble with it.")]
        public int pressureExcessOccurrences = 0;
        [Tooltip("Whether to draw forces with lines in gizmos mode.")]
        public bool drawForce = true;
        [Tooltip("The size of the vector drawn in gizmos mode to visualize the forces.")]
        public float vectorSize = 0.001f;

        void Start()
        {
            // getting the mesh and rigidbody
            colliderMesh = GetComponent<MeshFilter>().mesh;
            rb = transform.parent.GetComponent<Rigidbody>();

            // checking the component, disabling if no component found
            if (colliderMesh == null) { Debug.LogWarning("Please attach a mesh filter to this object."); this.enabled = false; return; }
            if (rb == null) { Debug.LogWarning("Please make this object child of a rigid body."); this.enabled = false; return; }

            // getting the number of triangle that need to be calculated. 3 vertex per triangles
            numberOfTriangles = colliderMesh.triangles.Length/3;

            // initializing the size of the per-triangle arrays
            triangleCenter = new Vector3[numberOfTriangles];
            //triangleSize = new float[numberOfTriangles];
            triangleArea = new float[numberOfTriangles];
            triangleNormal = new Vector3[numberOfTriangles];
            undeformedPosition =  new Vector3[numberOfTriangles];
            previousWorldTriangleCenter =  new Vector3[numberOfTriangles];

            // calculating the per-triangle values by looping trough each triangle
            for (int t = 0; t<numberOfTriangles; t++)
            {
                // getting each vertex of the triangle
                Vector3 vertex1 = colliderMesh.vertices[colliderMesh.triangles[3*t]];
                Vector3 vertex2 = colliderMesh.vertices[colliderMesh.triangles[3*t + 1]];
                Vector3 vertex3 = colliderMesh.vertices[colliderMesh.triangles[3*t + 2]];

                // saving the scaled vertex local position for later
                Vector3 scaledVertex1 = Vector3.Scale(transform.localScale, vertex1);
                Vector3 scaledVertex2 = Vector3.Scale(transform.localScale, vertex2);
                Vector3 scaledVertex3 = Vector3.Scale(transform.localScale, vertex3);

                // calculating the unscaled center of the triangle being the average of each unscaled vertex
                triangleCenter[t] = (vertex1 + vertex2 + vertex3)/3f;

                // setting the center undeformed water position as its world position to initiate the algorithm of Ocean.GetHeight
                undeformedPosition[t] = transform.TransformPoint(triangleCenter[t]);
                previousWorldTriangleCenter[t] = undeformedPosition[t];

                // calculating the cross product that will serve to calculate the scaled area of the triangle and its normal
                Vector3 crossProduct = Vector3.Cross(scaledVertex2 - scaledVertex1, scaledVertex3 - scaledVertex1);

                // calulating the area of the triangle being half the magnitude of the cross product
                triangleArea[t] = 0.5f*crossProduct.magnitude;

                // calculating the normal vector as beign the opposite of the normalized cross product as a convention
                triangleNormal[t] = -crossProduct.normalized;
            }

            // constructing the array that will be used to parse data from update to FixedUpdate
            waterHeight = new float[numberOfTriangles];
            fluidVelocity = new Vector3[numberOfTriangles];
        }

        private void Update()
        {
            // computing the ocean in update to avoid computing it several time per frame because of the way FixedUpdate is called even if the fixed rate is low.

            // if it is not asked to compute the waves, default value of flat water are set
            if (flatWater)
            {
                // runing through each triangles
                for (int t = 0; t<numberOfTriangles; t++)
                {
                    // computing the world space position of the triangle
                    Vector3 worldTriangleCenter = transform.TransformPoint(triangleCenter[t]);
                    undeformedPosition[t] = worldTriangleCenter;

                    //computing the fluid velocity depending on whether inside water or not
                    if (worldTriangleCenter.y < 0f)
                    {
                        // setting velocity of the water
                        fluidVelocity[t] = Vector3.zero;
                    }
                    else
                    {
                        // getting the velocity of the air
                        fluidVelocity[t] = Ocean.GetWindVelocity(worldTriangleCenter, true);
                    }

                    // in any case the water height is 0
                    waterHeight[t] = 0f;
                }
            }
            // else the waves are computed
            else
            {
                // running trough all triangle to calculate each's triangle forces
                for (int t = 0; t<numberOfTriangles; t++)
                {
                    // getting the world space position of the triangle
                    Vector3 worldTriangleCenter = transform.TransformPoint(triangleCenter[t]);

                    // getting the ground depth
                    float groundDepth = 200f;
                    if (Ocean.useTerrain)
                    {
                        groundDepth = - Ocean.terrain.SampleHeight(undeformedPosition[t]) - Ocean.terrain.transform.position.y;
                    }

                    // updating the undeformed position and getting the height of the water
                    waterHeight[t] = Ocean.GetHeight(Time.time, worldTriangleCenter, ref undeformedPosition[t], out Vector3 deformation, groundDepth);

                    // calculating the depth as beign the water level minus the vertical position of the triangle, is positive inside water
                    float depth = waterHeight[t] - worldTriangleCenter.y;

                    //computing the fluid velocity depending on whether inside water or not
                    if (depth > 0f)
                    {
                        // getting velocity of the water
                        fluidVelocity[t] = Ocean.GetVelocity(Time.time, undeformedPosition[t], deformation, out _, depth, groundDepth);
                    }
                    else
                    {
                        // getting the velocity of the air
                        fluidVelocity[t] = Ocean.GetWindVelocity(worldTriangleCenter, true);
                    }
                }
            }
        }


        private void FixedUpdate()
        {
            // running trough all triangle to calculate each's triangle forces
            for (int t = 0; t<numberOfTriangles; t++)
            {
                // getting the world space triangle center
                Vector3 worldTriangleCenter = transform.TransformPoint(triangleCenter[t]);

                // trying to update undeformed position reguardless of the wave in an atemmpt to make the GetHeight function more acurate.
                undeformedPosition[t] += worldTriangleCenter - previousWorldTriangleCenter[t];
                previousWorldTriangleCenter[t] = worldTriangleCenter;

                // getting hte world space normal
                Vector3 worldTriangleNormal = transform.TransformDirection(triangleNormal[t]);

                // the relative velocity is the difference between the velocity given by the rigidbody and the medium velocity
                Vector3 relativeVelocity = fluidVelocity[t] - rb.GetPointVelocity(worldTriangleCenter);

                // getting its magnitude, 
                float relativeSpeed = relativeVelocity.magnitude;

                // no force are generated under too low speed to avoid a 0 division
                if (relativeSpeed > 0.1f)
                {
                    // computing the approximated angle of attack with the dot product of the normaized velocity vector and normal
                    float angleOfAttack = Vector3.Dot(relativeVelocity/relativeSpeed, worldTriangleNormal);

                    // computing the lift coefficient and clamping to -1,1 (the coef of a flat plane against a normal stream)
                    float coeff = angleOfAttack*liftCoeff;
                    if (coeff > 1f) { coeff = 1f; }
                    if (coeff <-1f) { coeff =-1f; }

                    // the pressurea acting on the triangle is 1/2.rho.V².Cl depending on the fluid acting on it
                    float dynamicPressure;
                    if (waterHeight[t] > worldTriangleCenter.y) // in the water
                    {
                        dynamicPressure = 500f*relativeSpeed*relativeSpeed*coeff;
                    }
                    else // in the air
                    {
                        dynamicPressure = 0.5f*relativeSpeed*relativeSpeed*coeff;
                    }

                    // clamping the dynamic pressure that can reach high value due to the square of the speed
                    if (dynamicPressure > maximalPressure)
                    {
                        dynamicPressure = maximalPressure;

#if UNITY_EDITOR // counting the excess to see if it happens too often
                        pressureExcessOccurrences++;
#endif
                    }
                    if (dynamicPressure <-maximalPressure)
                    {
                        dynamicPressure =-maximalPressure;

#if UNITY_EDITOR // counting the excess to see if it happens too often
                        pressureExcessOccurrences++;
#endif
                    }

                    // the force is the pressure time the area and directed in triangle normal
                    Vector3 force = dynamicPressure*triangleArea[t]*worldTriangleNormal;

                    // applying the force
                    rb.AddForceAtPosition(force, worldTriangleCenter);

                    // drawing the force when in gizmos mode
                    if (drawForce)
                    {
                        Debug.DrawLine(worldTriangleCenter, worldTriangleCenter + force*vectorSize, Color.blue, 0f, false);
                    }
                }

            }
        }
    }
}