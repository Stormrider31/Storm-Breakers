using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.Jobs;
using UnityEngine.VFX;
using Unity.Burst;

namespace StormBreakers
{
    public class WaterInteraction : MonoBehaviour
    {
        // This component make realistic buoyancy and drag force on a rigid body based on a mesh
        // The mesh must be either from a mesh collider component or from a hull builder component
        // This component also create particles based on the relative speed of the mesh and water
        // This component also generate procedural audio based on the particle generation


        [Header("Physic porperties")]
        [Tooltip("You can choose whether to generate forces. The rigid body useGravity will be set to the same. If set to false the force will still be drawed in the gizmos.")]
        public bool generateWaterForces = true;
        [Tooltip("Using jobs can increase FPS when the simualted mesh has a lot of triangles.")]
        public bool useJobs = true;
        [Tooltip("Tip this when the water is flat or when the object is away from focus, this will prevent the waves to be computed, saving a great deal of resource. ")]
        public bool flatWater = false;
        [Tooltip("Tip this when there are almost no waves and to orient the buoyancy force vertically, this will prevent the object to move on its own because of the mesh disymetry making a net buoyancy force non vertical.")]
        public bool orientBuoyancyForceVertically = false;
        [Tooltip("Tip this if you want the mesh to drift with the wind, can add a little overahead. Does not act as actual sail !")]
        public bool generateWindForce = false;
        [Tooltip("Tip this is you want that the system simulate the wake wave in the computation of the forces (no rendering of it), this help speedboat cornering at high speed and leaning while surfing. ")]
        public bool simulateWakeWave = true;
        [Tooltip("The relative density of the rigid body (Ratio of the mass and the water mass for the same volume). Above 1 it sinks.")]
        [Range(0f, 1f)] public float relativeDensity = 0.3f;
        [Tooltip("The drag coefficient in the local axis. https://en.wikipedia.org/wiki/Drag_coefficient")]
        public Vector3 dragCoefficients = new Vector3(0.2f, 1f, 0.5f);
        [Tooltip("The multipliers of the moment of inertia in local axis. The rigid body component compute the inertia tensor based on the hypothesis the mesh is full, so for a hollow object in that direction you should increase the value by setting this multiplier superior to 1. https://en.wikipedia.org/wiki/List_of_moments_of_inertia")]
        public Vector3 inertiaFactors = new Vector3(2f, 1f, 1.2f);
        [Tooltip("The position of the gravity center in local axis realtive to the one automatically computed by the rigid body component. Use this to tweak the behaviour of the object in the water.")]
        public Vector3 gravityCenterShift = Vector3.zero;

        // internal physic data
        private Mesh simulatedMesh; // the mesh used to compute the force, audio and particles
        private int numberOfTriangles; // the number of triangle in the simulated mesh
        private Vector3[] triangleCenter; // the local center of each triangle
        private Vector3[] previousWorldTriangleCenter;
        private float[] triangleSize; // the approximate size of the triangles
        private float[] triangleArea; // the area of the triangles, square of the size
        private Vector3[] triangleNormal; // local normal of the triangles
        private Vector3[] draggedNormal; // local normal but with the drag coeff applied so it can directly multplied by the dynamic pressure to make the drag force.
        private Vector3[] undeformedPosition; // used to store the previous water undeformed position of each triangle
        private Rigidbody rb; // the rigidbody attached to this game object
        private float[] waterHeight; // is set in Update and got in fixedUpdate
        private Vector3[] waterNormal; // is set in Update and got in fixedUpdate
        private Vector3[] waterVelocity; // is set in Update and get in fixedUpdate
        private Vector3[] waterTorque; // is set in Update and get in fixedUpdate

        [Space(10)]
        [Header("Particles properties (more in VFX component)")]
        [Tooltip("You can choose whether to generate the particles or not.")]
        public bool generateParticles = true;
        [Tooltip("Culls the particle and audio generation for triangles with local normal under a value. Increase this value to prevent particle to be generated trough the bottom of a flat hull.")]
        [Range(0f, 1f)] public float bottomCulling = 0f;
        [Tooltip("Culls the particle and audio generation for triangles with local normal above a value. Increase this value to prevent particle to be generated on the deck of a hull.")]
        [Range(0f, 1f)] public float topsideCulling = 0f;
        [Tooltip("The minimal relative speed of the water onto the triangle to generate particles.")]
        [Range(0f, 5f)] public float minImpactSpeed = 1f;
        [Tooltip("The number of burst particles generated per frame per triangle. Increasing it with the alpha clip can imporve the simualtion but also increase the number of particles rendered in the scene.")]
        [Range(0f, 5f)] public float burstCount = 1f;
        [Tooltip("The number of sea spray particles generated per impact velocity and wind. Set zero if you don't want sea spray particles.")]
        [Range(0f, 0.5f)] public float sprayCountFactor = 0.1f;
        [Tooltip("The maximum number of particles generated for a triangles, this because the number generated depends of the impact velocity which can reach high value.")]
        [Range(0f, 25f)] public float sprayMaxCount = 5f;
        [Tooltip("The sea spray lifetime per impact speed and wind strength.")]
        [Range(0f, 0.15f)] public float sprayLifetimeFactor = 0.03f;

        // internal particles data
        private VisualEffect splashVFX; // VFX component
        private VFXEventAttribute eventAttribute; // used to sent attributes
        private int positionID; // VFX attribute ID
        private int velocityID; // VFX attribute ID
        private int lifetimeID; // VFX attribute ID
        private int triangleVelocityID; // VFX attribute ID
        private int waterVelocityID; // VFX attribute ID
        private int triangleNormalID; // VFX attribute ID
        private int triangleSizeID; // VFX attribute ID
        private int targetPositionID; // VFX attribute ID
        private int eventBurstID; // VFX attribute ID
        private int eventSprayID; // VFX attribute ID
        private int spawnCountID; // VFX attribute ID


        [Space(10)]
        [Header("Audio properties")]
        [Tooltip("You can choose whether to generate audio or not.")]
        public bool generateAudio = true;
        [Tooltip("The minimal water impact speed at which a triangle adds dynamic pressure for the use of audio computation. Increase this value to avoid audio when the mesh is moving slowly.")]
        [Range(0f, 5f)] public float audioMinImpactSpeed = 1f;
        [Tooltip("The audio volume per dynamic pressure acting on the mesh, depends on the size of the obejct simulated. Lower it when saturation happens.")]
        public float volumeFactor = 0.1f;
        [Tooltip("The maximal volume of the audio, can exceed 1 because the pitch effect reduce the audio intensity")]
        [Range(0f,25f)] public float maxVolume = 5f;
        [Tooltip("The rate at which the audio loose volume after a blast.")]
        [Range(0f, 5f)] public float volumeFadeoffRate = 1f;
        [Tooltip("The audio pitch per dynamic pressure acting on the mesh. Might need to ajust volume level when modifying this input. ")]
        [Range(0f, 50f)] public float pitchFactor = 10f;
        [Tooltip("The minimal value of the pitch. low pitch makes less powerfull noise so increasing it can help simulate larger object but with worng pitch.")]
        [Range(0f, 1f)]public float minPitch = 0.2f;
        [Tooltip("The rate at which the audio got back to high pitch after a blast.")]
        [Range(0f, 0.5f)] public float pitchFadeoffRate = 0.1f;


        // internal audio data
        private float volume = 0f; // the computed volume
        private float pitch = 1f; // the computed pitch
        private float lastFilteredDataLeft = 0f; // used to save previous value for the low pass filter
        private float lastFilteredDataRight = 0f; // used to save previous value for the low pass filter
        private float totalDynamicPressure = 0f; // the value used to compted audio volume and pitch

        [Space(10)]
        [Header("Debugging")]
        [Tooltip("The maximal pressure apllied due to the speed. Since it evolve with the square of speed, this can reach higher values that the physic can handles for small object moving fast. Lower it when the object bounce too fast off the water.")]
        public float maximalPressure = 50000f;
        [Tooltip("Indicate the number of time the maximal dynamic pressure is reached, if it happens too often this mean the physics might be impacted and you should consider rise the maximal pressure if there isn't trouble with it.")]
        public int pressureExcessOccurrences = 0;
        [Tooltip("The maximal additional depth when the wake wave is computed, should be in the vicinity of the hull height.")]
        public float maximalWakeWaveHeight = 1f;
        [Tooltip("Indicate the number of time the maximal wake wave height is reached, if it happens too often this mean the physics might be impacted and you should consider rise the maximal pressure if there isn't trouble with it.")]
        public int wakeWaveOccurrences = 0;
        [Tooltip("Whether to draw forces with lines in gizmos mode.")]
        public bool drawForce = true;
        [Tooltip("The size of the vector drawn in gizmos mode to visualize the forces.")]
        public float vectorSize = 0.001f;

        private void Start()
        {
            // -------- initializing the physics data ---------------
            InitializePhysic();

            // constructing the array that will be used to parse data from update to FixedUpdate
            waterHeight = new float[numberOfTriangles];
            waterNormal = new Vector3[numberOfTriangles];
            waterVelocity = new Vector3[numberOfTriangles];
            waterTorque = new Vector3[numberOfTriangles];

            // -------- initializing the VFX -------
            // getting the component
            splashVFX = GetComponent<VisualEffect>();

            // checking the component
            if (splashVFX != null && splashVFX.HasMatrix4x4("_LIDR") && splashVFX.HasMatrix4x4("_NKVW") && splashVFX.HasVector4("_totalLigthColor") && splashVFX.HasVector4("_waterColor")  && splashVFX.HasVector3("_wind"))
            {
                // initializing the event attribute
                eventAttribute = splashVFX.CreateVFXEventAttribute();

                // getting the attribute ID
                positionID = Shader.PropertyToID("position");
                velocityID = Shader.PropertyToID("velocity");
                lifetimeID = Shader.PropertyToID("lifetime");
                triangleVelocityID = Shader.PropertyToID("triangleVelocity");
                waterVelocityID = Shader.PropertyToID("waterVelocity");
                triangleNormalID = Shader.PropertyToID("triangleNormal");
                triangleSizeID = Shader.PropertyToID("triangleSize");
                eventBurstID = Shader.PropertyToID("burst");
                eventSprayID =  Shader.PropertyToID("spray");
                spawnCountID = Shader.PropertyToID("spawnCount");
                targetPositionID = Shader.PropertyToID("targetPosition");

                // setting the properties
                UpdateVFXProperties();
            }
            // if the VFX component is wrong then the particle generation is deactivated
            else
            {
                // informing the missuse
                if (generateParticles) { Debug.LogWarning("Please attach splashVFX.vfx component to this object if you want to generate particles or disable generate particles."); }

                // deactivating
                generateParticles = false;
            }

            // --------- initializing audio ---------------
            // checking the component
            AudioSource audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                generateAudio = false;
            }
            else
            {
                audioSource.enabled = generateAudio;
            }

            // ----------- initialize debuging ------------
            // resetting the number of occurence if user change it
            pressureExcessOccurrences = 0;
        }

        void InitializePhysic()
        {
            // -------- getting components and mesh ---------------

            // if a hull generator is attached and will use its function to set up the simualted mesh, colllider and mesh filter if any
            HullGenerator hullGenerator = GetComponent<HullGenerator>();
            if (hullGenerator != null)
            {
                // setting the simulated mesh
                simulatedMesh = hullGenerator.GenerateHull();

                // uploading the mesh to the mesh filter if any
                MeshFilter meshFilter = GetComponent<MeshFilter>();
                if (meshFilter != null)
                {
                    meshFilter.mesh = simulatedMesh;
                }

                // the mesh collider is also uptated if any and asked
                MeshCollider meshCollider = GetComponent<MeshCollider>();
                if (meshCollider != null && hullGenerator.useThisAsMeshCollider)
                {
                    meshCollider.sharedMesh = simulatedMesh;
                }
                // warning the abscence of a mesh collider
                else { Debug.Log("There is no mesh collider attached, this object won't interact with other rigid bodies."); }

            }
            // else if there is no hull builder component, it looks into the mesh collider
            else
            {
                MeshCollider meshCollider = GetComponent<MeshCollider>();
                if (meshCollider != null || !meshCollider.enabled)
                {
                    // getting the mesh
                    simulatedMesh = meshCollider.sharedMesh;
                }
                // if not then there is a big issue.
                else { Debug.LogError("Please add a hull generator component or mesh collider to this game object."); }
            }

            // last check to be sure
            if (simulatedMesh == null) { Debug.LogError("No simulated mesh found. Please verify the existence of a hull generator component or mesh collider."); }

            // getting and checking the rigidbody
            rb = GetComponent<Rigidbody>();
            if (rb == null)
            {
                // rigid body is mandatory to get the speed
                rb = this.gameObject.AddComponent<Rigidbody>();

                Debug.Log("Rigid body added to this game object.");
            }

            // ------------ setting the rigid body ----------------------

            // settign the use of the gravity, if no forces generation is requested, the gravity need to be deactivated otherwise the object will make a free fall through the water
            rb.useGravity = generateWaterForces;

            // settign the mass and inertia tensor
            rb.SetDensity(1000f*relativeDensity);

            // tweaking the intertia tensor
            rb.inertiaTensor =  Vector3.Scale(inertiaFactors, rb.inertiaTensor);

            // saving those value because when the center of mass is shifted, it resets the mass and inertia
            float mass = rb.mass;
            Vector3 inertiaTensor = rb.inertiaTensor;

            // set the gravity center position
            rb.ResetCenterOfMass();
            rb.centerOfMass +=  gravityCenterShift;

            // setting back the inertia and mass that have been lost with rb.ResetCenterOfMass()
            rb.mass = mass;
            rb.inertiaTensor = inertiaTensor;

            // ----------------- setting per triangle data -----------------

            // initialize the number of triangle that need to be calculated because mesh.triangles is the list of all vertices, 3 per triangles
            numberOfTriangles = simulatedMesh.triangles.Length/3;

            // initialize the size of the per-triangle arrays
            triangleCenter = new Vector3[numberOfTriangles];
            previousWorldTriangleCenter = new Vector3[numberOfTriangles];
            triangleSize = new float[numberOfTriangles];
            triangleArea = new float[numberOfTriangles];
            triangleNormal = new Vector3[numberOfTriangles];
            draggedNormal = new Vector3[numberOfTriangles];
            undeformedPosition =  new Vector3[numberOfTriangles];

            // calculating the per-triangle values
            for (int t = 0; t<numberOfTriangles; t++)
            {
                // getting each vertex of the triangle
                Vector3 vertex1 = simulatedMesh.vertices[simulatedMesh.triangles[3*t]];
                Vector3 vertex2 = simulatedMesh.vertices[simulatedMesh.triangles[3*t + 1]];
                Vector3 vertex3 = simulatedMesh.vertices[simulatedMesh.triangles[3*t + 2]];

                // scaling the vertex with the transform scale for the computation of the area and size
                Vector3 scaledVertex1 = Vector3.Scale(transform.localScale, vertex1);
                Vector3 scaledVertex2 = Vector3.Scale(transform.localScale, vertex2);
                Vector3 scaledVertex3 = Vector3.Scale(transform.localScale, vertex3);

                // calculating the unscaled center of the triangle being the average of each unscaled vertex
                triangleCenter[t] = (vertex1 + vertex2 + vertex3)/3f;

                // TESTING
                previousWorldTriangleCenter[t] = transform.TransformPoint(triangleCenter[t]);

                // setting the center's undeformed water position as its world position to initiate the algorithm of Ocean.GetHeight
                undeformedPosition[t] = transform.TransformPoint(triangleCenter[t]);

                // calculating the cross product that will serve to calculate the scaled area of the traingle and its normal
                Vector3 crossProduct = Vector3.Cross(scaledVertex2 - scaledVertex1, scaledVertex3 - scaledVertex1);

                // calulating the area of the triangle being half the magnitude of the cross product
                float area = 0.5f*crossProduct.magnitude;
                triangleArea[t] = area;

                // calculating the size of the triangle as being the square root of the surface (approx)
                triangleSize[t] = Mathf.Sqrt(area);

                // calculating the normal vector as beign the opposite of the normalized cross product (so the force push inward)
                triangleNormal[t] = -crossProduct.normalized;

                // calculating the normal with the drag coeff already multiplied
                draggedNormal[t] = Vector3.Scale(triangleNormal[t], dragCoefficients);
            }

            // ----------------- setting job system ---------------------
            
            // checking the use of job system in accordance with the use of terrain
            if (Ocean.useTerrain && useJobs)
            {
                Debug.Log("Cannot use job systems while using shalow water effect (terrain in Ocean Controller). Regular loop will be used instead.");
                useJobs = false;
            }
        }

        public void UpdateVFXProperties() // is called by ocean controller when there are modification
        {
            if (splashVFX != null)
            {
                splashVFX.SetMatrix4x4("_LIDR", Ocean.LIDR);
                splashVFX.SetMatrix4x4("_NKVW", Ocean.NKVW);
                splashVFX.SetVector4("_totalLigthColor", Ocean.totalLight);
                splashVFX.SetVector4("_waterColor", Ocean.waterColor);
                splashVFX.SetVector3("_wind", new Vector3(Ocean.Wind.speed*Ocean.Wind.cosDirection, Ocean.Wind.inverseHeight, Ocean.Wind.speed*Ocean.Wind.sinDirection));

                // when using a terrain the VFX properties are filled with the static data that should have been set
                if (Ocean.useTerrain)
                {
                    // making sure the terrain has been set
                    if (Ocean.terrain != null)
                    {
                        // setting the data
                        splashVFX.SetTexture("_terrainHeightmap", Ocean.terrain.terrainData.heightmapTexture);
                        splashVFX.SetVector3("_terrainPosition", Ocean.terrain.transform.position);
                        splashVFX.SetVector3("_terrainScale", Ocean.terrain.terrainData.size);
                    }
                    else { Debug.LogWarning("No terrain detected !"); }
                }
                // else if no terrain is used, setting some default value that ensure no depth effect
                else
                {
                    // default value of a deep flat sea bed
                    splashVFX.SetTexture("_terrainHeightmap", new RenderTexture(1,1,0));
                    splashVFX.SetVector3("_terrainPosition", new Vector3(0f,-200f, 0f));
                    splashVFX.SetVector3("_terrainScale", new Vector3(1f,1f,1f));
                }
            }
        }

        private void Update()
        {
            // ------------- computing the ocean -----------------------
            // computing the ocean in update to avoid computing it several time per frame because of the way FixedUpdate is called.

            // if it is not asked to compute the waves, default value of flat water are set
            if (flatWater)
            {
                // runing through each trangles
                for (int t = 0; t<numberOfTriangles; t++)
                {
                    // setting defautls value of flat water, keeping underformed position updated in case the wave are computed again
                    undeformedPosition[t] = transform.TransformPoint(triangleCenter[t]);
                    waterHeight[t] = 0f;
                    waterVelocity[t] = Vector3.zero;
                    waterTorque[t] = Vector3.zero;
                    waterNormal[t] = Vector3.up;
                }
            }
            // else the waves are computed
            else
            {
                if (useJobs) // using the C# jobs
                {
                    // constructing the native array that will be passed to the job
                    NativeArray<Vector3> passedWorldTriangleCenter = new NativeArray<Vector3>(numberOfTriangles, Allocator.TempJob);
                    NativeArray<Vector3> passedUndeformedPosition = new NativeArray<Vector3>(numberOfTriangles, Allocator.TempJob);
                    NativeArray<float> passedWaterHeight = new NativeArray<float>(numberOfTriangles, Allocator.TempJob);
                    NativeArray<Vector3> passedWaterNormal = new NativeArray<Vector3>(numberOfTriangles, Allocator.TempJob);
                    NativeArray<Vector3> passedWaterVelocity = new NativeArray<Vector3>(numberOfTriangles, Allocator.TempJob);
                    NativeArray<Vector3> passedWaterTorque = new NativeArray<Vector3>(numberOfTriangles, Allocator.TempJob);

                    // initializing the native array that will be read by the job
                    for (int t = 0; t<numberOfTriangles; t++)
                    {
                        passedWorldTriangleCenter[t] = transform.TransformPoint(triangleCenter[t]);
                        passedUndeformedPosition[t] = undeformedPosition[t];
                    }

                    // creating the job data structure
                    JobComputeOceanData jobData = new JobComputeOceanData
                    {
                        // populating the job structure
                        time = Time.time,
                        computeNormal = false,
                        worldTriangleCenter = passedWorldTriangleCenter,
                        undeformedPosition = passedUndeformedPosition,
                        waterHeight = passedWaterHeight,
                        waterNormal = passedWaterNormal,
                        waterVelocity = passedWaterVelocity,
                        waterTorque = passedWaterTorque
                    };

                    // Scheduling the job 
                    JobHandle jobHandle = jobData.Schedule(numberOfTriangles, 1);

                    // Ensure the job has completed.
                    // !! ("It is not recommended to Complete a job immediately, since that reduces the chance of having other jobs run in parallel with this one. You optimally want to schedule a job early in a frame and then wait for it later in the frame.")
                    jobHandle.Complete();

                    // collecting the results
                    for (int t = 0; t<numberOfTriangles; t++)
                    {
                        undeformedPosition[t] = jobData.undeformedPosition[t];
                        waterHeight[t] = jobData.waterHeight[t];
                        waterNormal[t] = jobData.waterNormal[t];
                        waterVelocity[t] = jobData.waterVelocity[t];
                        waterTorque[t] = jobData.waterTorque[t];
                    }

                    // free the memory allocated by the arrays
                    passedWorldTriangleCenter.Dispose();
                    passedUndeformedPosition.Dispose();
                    passedWaterHeight.Dispose();
                    passedWaterNormal.Dispose();
                    passedWaterVelocity.Dispose();
                    passedWaterTorque.Dispose();
                }
                else // using a regular loop
                {
                    // runing through each trangles
                    for (int t = 0; t<numberOfTriangles; t++)
                    {
                        // computing the world space position of the triangle
                        Vector3 worldTriangleCenter = transform.TransformPoint(triangleCenter[t]);

                        // getting the ground depth
                        float groundDepth = 200f;
                        if (Ocean.useTerrain)
                        {
                            groundDepth = - Ocean.terrain.SampleHeight(undeformedPosition[t]) - Ocean.terrain.transform.position.y;
                        }

                        // updating the undeformed position and getting the height of the water
                        waterHeight[t] = Ocean.GetHeight(Time.time, worldTriangleCenter, ref undeformedPosition[t], out Vector3 deformation, groundDepth) ;

                        // calculating the depth as being the water level minus the vertical position of the triangle, is positive inside water 
                        float depth = waterHeight[t] - worldTriangleCenter.y;

                        // getting velocity of the water and the breaking torque
                        waterVelocity[t] = Ocean.GetVelocity(Time.time, undeformedPosition[t], deformation, out waterTorque[t], depth, groundDepth);

                        // currently there are no need to get the water normal
                        waterNormal[t] = Vector3.up;
                    }
                }
            }

            // ---------------- computing the audio --------------------------------------
            if (generateAudio)
            {
                // applying the pitch when it get lower to the previous value
                // non optimized : pitch = Mathf.Min(pitch, pitchFactor/dynamicPressure);
                float newPitch = pitchFactor/totalDynamicPressure;
                pitch = pitch>newPitch ? newPitch : pitch;

                // making the pitch fade off
                pitch += Time.deltaTime*pitchFadeoffRate;

                // ensuring the pitch is correctly clamped
                if (pitch>1f) { pitch = 1f; }
                else if (pitch < minPitch) { pitch = minPitch; }

                // applying the volume only when it get louder 
                // non optimized : volume = Mathf.Max(volume, dynamicPressure*volumeFactor);
                float newVolume = totalDynamicPressure*volumeFactor;
                volume = volume<newVolume ? newVolume : volume;

                // volume fade off fucntion of the volume itself
                // non optimized : volume -= Time.deltaTime*volumeFadeoffRate*Mathf.Max(1f, volume);
                volume -= Time.deltaTime*volumeFadeoffRate;// * (volume<1f ? 1f : volume);

                // making sure the volume is correclty clamped
                if (volume<0f) { volume = 0f; }
                else if (volume > maxVolume) { volume = maxVolume; }

                // clamping volume function of the pitch TEST
                //float maxVolume = maxVolumeFactor/pitch;
                //if (volume > maxVolume) { volume = maxVolume; }

                // reinitializing the sum  that will be used for the audio in the next fixed updates
                totalDynamicPressure = 0f;
            }
        }

        private void FixedUpdate()
        {
            // ------------- computing the forces, particles and audio ----------------------       

            // initializing the current total dynamic pressure, this will overwrite the value used for the audio only when bigger to keep quick burst audio that happens within the fixed frames serie
            float curentTotalDynamicPressure = 0f;

            // running for each triangle
            for (int t = 0; t<numberOfTriangles; t++)
            {
                // getting the wolrd space center of the triangle
                Vector3 worldTriangleCenter = transform.TransformPoint(triangleCenter[t]);

                // trying to update undeformed position reguardless of the wave in an atemmpt to make the GetHeight function more acurate.
                undeformedPosition[t] += worldTriangleCenter - previousWorldTriangleCenter[t];
                previousWorldTriangleCenter[t] = worldTriangleCenter;

                // getting the depth, it is asumed that in the several frame of fixed update, the water don't move but the triangles do
                float depth = waterHeight[t] - worldTriangleCenter.y;


                // calculating the wind at the triangle world position that is shared with force computation and particle generation
                float wind = Ocean.GetWindSpeed(worldTriangleCenter);
                Vector3 windVector = Ocean.GetWindVelocity(worldTriangleCenter);

                // force and particles can happens a bit outside water
                if (depth > -0.5f*triangleSize[t])
                {
                    // setting the normal in world space
                    Vector3 worldTriangleNormal = transform.TransformDirection(triangleNormal[t]);

                    // computing the world space normal with the drag coeff included
                    Vector3 draggedWorldNormal = transform.TransformDirection(draggedNormal[t]);

                    // the relative velocity is the difference between the velocity given by the rigidbody and the water velocity
                    Vector3 triangleVelocity = rb.GetPointVelocity(worldTriangleCenter);
                    Vector3 relativeVelocity = waterVelocity[t] - triangleVelocity;

                    //computing the normal component of the velocity relative to the triangle
                    float normalVelocity = Vector3.Dot(relativeVelocity, worldTriangleNormal);

                    // if it is asked to compute the wake wave, the depth will be ajusted to simulate this effect
                    if (simulateWakeWave)
                    {
                        // computing the normal dragged velocity, the rest of the physic don't use this as its unstable for the dynamic pressure
                        float draggedNormalVelocity = Vector3.Dot(relativeVelocity, draggedWorldNormal);

                        // computing the additional depth, the formula comes from the height of a wave that move at the normal velocity
                        float additionalDepth = Mathf.Sign(draggedNormalVelocity)*draggedNormalVelocity*draggedNormalVelocity*0.06f;

                        // clamping the value
                        if (additionalDepth > maximalWakeWaveHeight)
                        {
                            additionalDepth = maximalWakeWaveHeight;

#if UNITY_EDITOR        // counting the occurences
                            wakeWaveOccurrences++;
#endif
                        }

                        depth += additionalDepth;
                    }

                    //----------- force computation------------------

                    // force happens only when inside water
                    if (depth > 0f)
                    {
                        // static pressure is rho.g.h
                        float staticPressure = depth*10000f;

                        // the buoyancy resultant is then the presssure multiplied by the area dn oriented in triangle normal
                        Vector3 buoyancyForce = staticPressure*triangleArea[t]*worldTriangleNormal;

                        // if asked the force is directed upward to avoid a net force that would'nt be excatly upward
                        if (orientBuoyancyForceVertically)
                        {
                            buoyancyForce = Vector3.Dot(buoyancyForce, Vector3.up)*Vector3.up;
                        }

                        // dynamic pressure is 1/2*rho*V², currentlty oriented in the normal velocity direction
                        float dynamicPressure;
                        if (normalVelocity > 0f) { dynamicPressure =  500f*normalVelocity*normalVelocity; }
                        else { dynamicPressure = -500f*normalVelocity*normalVelocity; }

                        // clamping the dynamic pressure that can reach high value due to the square of the speed
                        if (dynamicPressure > maximalPressure)
                        {
                            dynamicPressure = maximalPressure;

#if UNITY_EDITOR
                            // debuging 
                            pressureExcessOccurrences++;
#endif
                        }

                        // clamping the dynamic pressure to the static pressure so the summ of pressure cannot be negative
                        if (dynamicPressure < -staticPressure) { dynamicPressure = -staticPressure; }

                        // the drag force is the presure multiplied by the area with the drag coeff aplied that orient the vector
                        Vector3 dragForce = triangleArea[t]*dynamicPressure*draggedWorldNormal;

                        // finally the force can be applied
                        if (generateWaterForces)
                        {
                            rb.AddForceAtPosition(buoyancyForce + dragForce, worldTriangleCenter);
                        }

                        // drawing forces
                        if (drawForce)
                        {
                            Debug.DrawLine(worldTriangleCenter, worldTriangleCenter + dragForce*vectorSize, Color.blue, 0f, false);
                            Debug.DrawLine(worldTriangleCenter, worldTriangleCenter + buoyancyForce*vectorSize, Color.red, 0f, false);


                        }
                    }
                    // outside water
                    else if (generateWindForce)
                    {
                        // calculating the normal velocity of the wind
                        float normalWindVelocity = Vector3.Dot(windVector - triangleVelocity, worldTriangleNormal);

                        // calculating the wind force 1/2*rho*V2*Cx
                        Vector3 windForce = 0.5f*Mathf.Sign(normalWindVelocity)*normalWindVelocity*normalWindVelocity*draggedWorldNormal;

                        // the force can be applied
                        rb.AddForceAtPosition(windForce, worldTriangleCenter);
                    }

                    //-------------- particles & audio generation-----------------------

                    // particles and audio are generated only when the triangle is no too deep (when depth exceed its size it is supposed that the triangle above will generate particles) 
                    // only when the relative speed is going toward the triangle
                    // when the triangle is not one from the bottom or topside cull
                    if (triangleNormal[t].y > -1f+topsideCulling &&
                        triangleNormal[t].y < 1f-bottomCulling &&
                        depth <  0.5f*triangleSize[t] &&
                        (0.75f*relativeVelocity.y + normalVelocity) > minImpactSpeed
                         )
                    {
                        if (generateParticles)
                        {
                            // ------------ sea foam burst --------------------

                            // setting the foam burst attribute
                            eventAttribute.SetFloat(spawnCountID, burstCount);
                            eventAttribute.SetVector3(positionID, worldTriangleCenter);
                            eventAttribute.SetVector3(triangleVelocityID, triangleVelocity);
                            eventAttribute.SetVector3(waterVelocityID, waterVelocity[t]);
                            eventAttribute.SetVector3(triangleNormalID, worldTriangleNormal);
                            eventAttribute.SetFloat(triangleSizeID, triangleSize[t]);
                            eventAttribute.SetVector3(targetPositionID, undeformedPosition[t]);

                            // playing the burst VFX once 
                            splashVFX.SendEvent(eventBurstID, eventAttribute);

                            // ------------- sea spray -----------------

                            // setting the spray attribute
                            eventAttribute.SetFloat(spawnCountID, Mathf.Min(sprayMaxCount, sprayCountFactor*wind*normalVelocity));
                            eventAttribute.SetFloat(lifetimeID, sprayLifetimeFactor*normalVelocity*wind);
                            eventAttribute.SetVector3(positionID, worldTriangleCenter);
                            eventAttribute.SetVector3(velocityID, windVector);

                            // playing the spray VFX once 
                            splashVFX.SendEvent(eventSprayID, eventAttribute);
                        }

                        // updating the sum that will define the audio volume when the velocity is enough
                        if (generateAudio && normalVelocity > audioMinImpactSpeed)
                        {
                            curentTotalDynamicPressure += triangleArea[t]*normalVelocity;
                        }

                    }
                }

                // adding the breaking torque function of the triangle size
                // it's regardless of the depth as the torque is already depth dependent 
                rb.AddTorque(triangleArea[t]*1000f*waterTorque[t]);

                // drawing the torque vector in magenta
                if (drawForce)
                {
                    Debug.DrawLine(worldTriangleCenter, worldTriangleCenter + vectorSize*1000f*triangleArea[t]*waterTorque[t], Color.magenta);
                }
            }

            // setting the total dynamic pressure when larger to the previous value
            totalDynamicPressure = curentTotalDynamicPressure>totalDynamicPressure ? curentTotalDynamicPressure : totalDynamicPressure;
        }

        void OnAudioFilterRead(float[] data, int channels)
        {
            // ---------- procedural audio ------------------
            if (generateAudio)
            {
                // looping through each sample
                for (int s = 0; s < data.Length; s += channels)
                {
                    // ---- left chanel (index = s + 0) ----            
                    // applying the low pass filter
                    float filteredDataleft = pitch*data[s] + (1f-pitch)*lastFilteredDataLeft;

                    // saving the last filtered data
                    lastFilteredDataLeft = filteredDataleft;

                    // setting the audio data with the volume
                    data[s] = volume*filteredDataleft;

                    // ---- right chanel (index = s + 1) ----
                    // applying the low pass filter
                    float filteredDataRight = pitch*data[s+1] + (1f-pitch)*lastFilteredDataRight;

                    // saving the last filtered data
                    lastFilteredDataRight = filteredDataRight;

                    // setting the audio data with the volume
                    data[s+1] = volume*filteredDataRight;
                }
            }
        }

        //[BurstCompile] // !! Burst cannot have access to non-readonly static fields, need to implement SharedStatic<T> https://docs.unity3d.com/Packages/com.unity.burst@1.7/manual/docs/AdvancedUsages.html#shared-static in Ocean.cs
        public struct JobComputeOceanData : IJobParallelFor
        {
            // this job only compute ocean waves which need to be processed again to compute and apply the forces

            public float time; // the game time at which to read the ocean
            public bool computeNormal; // wether to compute water normal
            [ReadOnly] public NativeArray<Vector3> worldTriangleCenter; // read, is an input of the job
            public NativeArray<Vector3> undeformedPosition; // read/write is equal to the array of the same name in MeshFloater, is a result and input of the job
            public NativeArray<float> waterHeight; // write, is a result of the job
            public NativeArray<Vector3> waterNormal; // write, is a result of the job
            public NativeArray<Vector3> waterVelocity; // write, is a result of the job
            public NativeArray<Vector3> waterTorque; // write, is a result of the job

            public void Execute(int t)
            {
                // updating the undeformed position and getting the height of the water
                Vector3 currentUndeformedPosition = undeformedPosition[t];
                waterHeight[t] = Ocean.GetHeight(time, worldTriangleCenter[t], ref currentUndeformedPosition, out Vector3 deformation);

                // the undeformed position is updated and will be passed to the next frame
                undeformedPosition[t] = currentUndeformedPosition;

                // calculating the depth as beign the water level minus the vertical position of the triangle, is positive inside water
                float depth = waterHeight[t] - worldTriangleCenter[t].y; ;

                // getting velocity of the water
                waterVelocity[t] = Ocean.GetVelocity(time, undeformedPosition[t], deformation, out Vector3 breakingTorque, depth);

                // collecting the torque generated by the breakers
                waterTorque[t] = breakingTorque;

                // the normal is computed only when required because it's expansive
                if (computeNormal)
                {
                    waterNormal[t] = Ocean.GetNormal(time, undeformedPosition[t], deformation);
                }
                else // the default value is set
                {
                    waterNormal[t] = Vector3.up;
                }
            }
        }

        // editor scripts
#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            // drawing the position of the center of gravity

            // getting the rigidbody first
            rb = GetComponent<Rigidbody>();

            // checking the component
            if (rb == null) { Debug.LogWarning("Water Interaction need a rigid body atached."); return; }

            // drawing
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.TransformPoint(rb.centerOfMass), Mathf.Pow(rb.mass, 1f/3f)*0.005f);
        }

        // On Validate is bugged and need this fix 
        private void OnValidate() => UnityEditor.EditorApplication.delayCall += OnValidateDebuged;
        private void OnValidateDebuged()
        {
            UnityEditor.EditorApplication.delayCall -= OnValidateDebuged;
            if (this == null) return;

            // updata data (mostly the position of gravity center) only when touching the inspector
            InitializePhysic();
        }

        private void OnDisable()
        {
            // resseting the occurences
            pressureExcessOccurrences = 0;
            wakeWaveOccurrences = 0;
        }
#endif

    }
}



