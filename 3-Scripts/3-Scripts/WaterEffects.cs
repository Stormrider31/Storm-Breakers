using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.Jobs;
using UnityEngine.VFX;

namespace StormBreakers
{
    public class WaterEffects : MonoBehaviour
    {
        // This component creates particles based on the relative speed of the mesh and water
        // This component generates procedural audio based on the particle generation


        [Header("Physic porperties")]
        [Tooltip("Tick this when the object doesn't move to optimize the number of triangle computed.")]
        public bool isStatic = false;
        [Tooltip("Tip this when the water is flat or when the object is away from focus, this will prevent the waves to be computed, saving a great deal of resource. ")]
        public bool flatWater = false;

        // internal physic data
        private Mesh simulatedMesh; // the mesh used to compute the force, audio and particles
        private int numberOfTriangles; // the number of triangle in the simulated mesh
        private bool[] simulatedTriangle; // wether the triangle will be processed
        private Vector3[] triangleCenter; // the local center of each triangle
        private Vector3[] previousWorldTriangleCenter;
        private float[] triangleSize; // the approximate size of the triangles
        private float[] triangleArea; // the area of the triangles, square of the size
        private Vector3[] triangleNormal; // local normal of the triangles
        private Vector3[] undeformedPosition; // used to store the previous water undeformed position of each triangle
        private float[] waterHeight; // is set in Update and got in fixedUpdate
        private Vector3[] waterNormal; // is set in Update and got in fixedUpdate
        private Vector3[] waterVelocity; // is set in Update and get in fixedUpdate

        [Space(10)]
        [Header("Particles properties (more in VFX component)")]
        [Tooltip("You can choose whether to generate the particles or not.")]
        public bool generateParticles = true;
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
        [Range(0f, 25f)] public float maxVolume = 5f;
        [Tooltip("The rate at which the audio loose volume after a blast.")]
        [Range(0f, 5f)] public float volumeFadeoffRate = 1f;
        [Tooltip("The audio pitch per dynamic pressure acting on the mesh. Might need to ajust volume level when modifying this input. ")]
        [Range(0f, 50f)] public float pitchFactor = 10f;
        [Tooltip("The minimal value of the pitch. low pitch makes less powerfull noise so increasing it can help simulate larger object but with worng pitch.")]
        [Range(0f, 1f)] public float minPitch = 0.2f;
        [Tooltip("The rate at which the audio got back to high pitch after a blast.")]
        [Range(0f, 0.5f)] public float pitchFadeoffRate = 0.1f;

        // internal audio data
        private float volume = 0f; // the computed volume
        private float pitch = 1f; // the computed pitch
        private float lastFilteredDataLeft = 0f; // used to save previous value for the low pass filter
        private float lastFilteredDataRight = 0f; // used to save previous value for the low pass filter
        private float totalDynamicPressure = 0f; // the value used to compted audio volume and pitch

        private void Start()
        {
            // -------- initializing the physics data ---------------
            InitializePhysic();

            // defining each simulated triangle
            DefineSimulatedTriangle();

            // constructing the array that will be used to parse data from update to FixedUpdate
            waterHeight = new float[numberOfTriangles];
            waterNormal = new Vector3[numberOfTriangles];
            waterVelocity = new Vector3[numberOfTriangles];

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
        }

        void InitializePhysic()
        {
            // -------- getting components and mesh ---------------


            // getting the simulated mesh
            // uploading the mesh to the mesh filter if any
            MeshFilter meshFilter = GetComponent<MeshFilter>();
            if (meshFilter != null)
            {
                simulatedMesh = meshFilter.mesh;
            }
            else { Debug.LogError("Please attach a mesh filter to this game object"); }
                      
            // last check to be sure
            if (simulatedMesh == null) { Debug.LogError("No simulated mesh found. Please verify the existence of a mesh filter."); }

            // ----------------- setting per triangle data -----------------

            // initialize the number of triangle that need to be calculated because mesh.triangles is the list of all vertices, 3 per triangles
            numberOfTriangles = simulatedMesh.triangles.Length/3;

            // initialize the size of the per-triangle arrays
            triangleCenter = new Vector3[numberOfTriangles];
            simulatedTriangle = new bool[numberOfTriangles];
            previousWorldTriangleCenter = new Vector3[numberOfTriangles];
            triangleSize = new float[numberOfTriangles];
            triangleArea = new float[numberOfTriangles];
            triangleNormal = new Vector3[numberOfTriangles];
            undeformedPosition =  new Vector3[numberOfTriangles];

            // calculating the per-triangle values
            for (int t = 0; t<numberOfTriangles; t++)
            {
                // initializing whether the triangle is simulated
                simulatedTriangle[t] = true;

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

                // initializing the previous position of the triangle to get the velocity
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

                // in case the wave were modified, redefining each simualted triangle
                DefineSimulatedTriangle();

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
                    splashVFX.SetTexture("_terrainHeightmap", new RenderTexture(1, 1, 0));
                    splashVFX.SetVector3("_terrainPosition", new Vector3(0f, -200f, 0f));
                    splashVFX.SetVector3("_terrainScale", new Vector3(1f, 1f, 1f));
                }
            }
        }

        private void DefineSimulatedTriangle() // define whether each triangle will be simulated according to their height and the maximal waves
        {
            // getting the maximal wave height
            float maxWaveHeight = 0f;

            // running through each wave system to add wave height
            for (int w = 0; w < 4; w++)
            {
                // increasing the max wave height with the current wave height
                maxWaveHeight += Ocean.wavelength[w]*0.085f*Mathf.Min(1f, Ocean.intensity[w]);
            }

            // running through each triangles
            for (int t = 0; t<numberOfTriangles; t++)
            {
                // getting the world posiiton of the triangle
                Vector3 worlTrianglePosition = transform.TransformPoint(triangleCenter[t]);

                 // defining whether the triangle is simulated : when its height is under the highest wave in absolute including a bit of its size
                simulatedTriangle[t] = !isStatic | Mathf.Abs(worlTrianglePosition.y) - 0.5f*triangleSize[t] < maxWaveHeight;
            }
        }
        private void Update()
        {
            // ------------- computing the ocean -----------------------
            // computing the ocean in update to avoid computing it several time per frame because of the way FixedUpdate is called.

            // if it is not asked to compute the waves, default value of flat water are set
            if (flatWater)
            {
                // updating only if the object is considered as non static
                if (!isStatic)
                {
                    // runing through each trangles
                    for (int t = 0; t<numberOfTriangles; t++)
                    {
                        // setting defautls value of flat water, keeping underformed position updated in case the wave are computed again
                        undeformedPosition[t] = transform.TransformPoint(triangleCenter[t]);
                        waterHeight[t] = 0f;
                        waterVelocity[t] = Vector3.zero;
                        waterNormal[t] = Vector3.up;
                    }
                }
            }
            // else the waves are computed
            else
            {
                // runing through each trangles
                for (int t = 0; t<numberOfTriangles; t++)
                {
                    // checking wether the triangle is simulated
                    if (simulatedTriangle[t])
                    {
                        // computing the world space position of the triangle
                        Vector3 worldTriangleCenter = transform.TransformPoint(triangleCenter[t]);

                        // getting the ground depth
                        float groundDepth = 200f;
                        if (Ocean.useTerrain)
                        {
                            groundDepth = -Ocean.terrain.SampleHeight(undeformedPosition[t]) - Ocean.terrain.transform.position.y;
                        }

                        // updating the undeformed position and getting the height of the water
                        waterHeight[t] = Ocean.GetHeight(Time.time, worldTriangleCenter, ref undeformedPosition[t], out Vector3 deformation, groundDepth);

                        // calculating the depth as being the water level minus the vertical position of the triangle, is positive inside water 
                        float depth = waterHeight[t] - worldTriangleCenter.y;

                        // getting velocity of the water and the breaking torque
                        waterVelocity[t] = Ocean.GetVelocity(Time.time, undeformedPosition[t], deformation, out _, depth, groundDepth);

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
                // checking wether the triangle is simulated
                if (simulatedTriangle[t])
                {
                    // getting the wolrd space center of the triangle
                    Vector3 worldTriangleCenter = transform.TransformPoint(triangleCenter[t]);

                    // trying to update undeformed position reguardless of the wave in an atemmpt to make the GetHeight function more acurate.
                    undeformedPosition[t] += worldTriangleCenter - previousWorldTriangleCenter[t];

                    // getting the velocity before setting previous world tirangle center
                    Vector3 triangleVelocity = (worldTriangleCenter - previousWorldTriangleCenter[t])/Time.fixedDeltaTime;

                    // setting previous world tirangle center for next loop
                    previousWorldTriangleCenter[t] = worldTriangleCenter;

                    // getting the depth, it is asumed that in the several frame of fixed update, the water don't move but the triangles do
                    float depth = waterHeight[t] - worldTriangleCenter.y;

                    // calculating the wind at the triangle world position that is shared with force computation and particle generation
                    float wind = Ocean.GetWindSpeed(worldTriangleCenter);
                    Vector3 windVector = Ocean.GetWindVelocity(worldTriangleCenter);

                    //particles can happens a bit outside water
                    if (depth > -0.5f*triangleSize[t])
                    {
                        // setting the normal in world space
                        Vector3 worldTriangleNormal = transform.TransformDirection(triangleNormal[t]);

                        // the relative velocity is the difference between the velocity given by the rigidbody and the water velocity
                        Vector3 relativeVelocity = waterVelocity[t] - triangleVelocity;

                        //computing the normal component of the velocity relative to the triangle
                        float normalVelocity = Vector3.Dot(relativeVelocity, worldTriangleNormal);

                        //-------------- particles & audio generation-----------------------

                        // particles and audio are generated only when the triangle is no too deep (when depth exceed its size it is supposed that the triangle above will generate particles) 
                        // only when the relative speed is going toward the triangle
                        // when the triangle is not one from the bottom or topside cull
                        if (depth <  0.5f*triangleSize[t] &&
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
    }
}