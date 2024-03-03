using UnityEngine;
using UnityEngine.VFX;

namespace StormBreakers
{
    public class SphereWaterInteraction : MonoBehaviour
    {
        // this class generate  buoyancy and drag forces of a sphere on the ocean
        // This component also create particles based on the relative speed of the mesh and water
        // This component also generate procedural audio based on the particle generation
        // Is similar to WaterInteraction but optimized to run with a sphere shape

        [Header("Physic porperties")]
        [Tooltip("You can choose whether to generate forces. The rigid body useGravity will be set to the same. If set to false the force will still be drawed in the gizmos.")]
        public bool generateWaterForce = true;
        [Tooltip("The radius in meter of the sphere.")]
        public float radius = 1f;
        [Tooltip("The position of the gravity center in meter. When positive the gravity center is in local green axis side.")]
        public float gravityCenterHeight = 0f;
        [Tooltip("The relative density of the rigid body (Ratio of the mass and the water mass for the same volume). Above 1 it sinks.")]
        [Range(0f, 1f)] public float density = 0.3f;
        [Tooltip("The drag coefficient in every axis. https://en.wikipedia.org/wiki/Drag_coefficient")]
        [Range(0f, 2f)] public float dragCoeff = 0.5f;
        [Tooltip("The multiplier of the moment of inertia in every axis. The rigid body component compute the inertia tensor based on the hypothesis the mesh is full, so for a hollow object in that direction you should increase the value by setting this multiplier superior to 1. https://en.wikipedia.org/wiki/List_of_moments_of_inertia")]
        [Range(0f, 3f)] public float inertiaTensorFactor = 1.5f;

        //internal physic data
        private Vector3 undeformedPosition; //the undeformed position that matchs position after ocean deformation
        private Vector3 previousPosition; // used to update undeformed position reguardless ofthe wave
        private float waterHeight; // is set in Update and got in fixedUpdate
        private Vector3 waterNormal; // is set in Update and got in fixedUpdate
        private Vector3 waterVelocity; // is set in Update and got in fixedUpdate
        private Rigidbody rb; // the rigid body attached to this gameObject
        private float area; // [m²] the projected surface of the sphere


        [Space(10)]
        [Header("Particles porperties (more in VFX component)")]
        [Tooltip("You can choose whether to generate the particles or not.")]
        public bool generateParticles = true;
        [Tooltip("The minimal realtive speed of the water onto the triangle to generate particles.")]
        [Range(0f, 5f)] public float minImpactSpeed = 1.5f;
        [Tooltip("The number of burst particles generated per frame")]
        [Range(0f, 10f)] public float burstCount = 2f;
        [Tooltip("The number of sea spray particles generated per surface area, per relative velocity and per wind velocity.")]
        [Range(0f, 0.5f)] public float sprayCountFactor = 0.01f;
        [Tooltip("The maximum number of sea spray particles generated per fixed delta time frame.")]
        [Range(0f, 25f)] public float sprayMaxCount = 5f;
        [Tooltip("The sea spray lifetime per impact speed and wind strength.")]
        [Range(0f, 0.15f)] public float sprayLifetimeFactor = 0.03f;

        // internal particles data
        private VisualEffect splashVFX;
        private VFXEventAttribute eventAttribute;
        private int positionID;
        private int velocityID;
        private int lifetimeID;
        private int triangleVelocityID;
        private int waterVelocityID;
        private int triangleNormalID;
        private int triangleSizeID;
        private int targetPositionID;
        private int eventBurstID;
        private int eventSprayID;
        private int spawnCountID;

        [Space(10)]
        [Header("Audio properties")]
        [Tooltip("You can choose whether to generate audio or not.")]
        public bool generateAudio = true;
        [Tooltip("The minimal relative speed at which the audio can get louder. Increase this value to avoid noise when the object is moving slowly.")]
        [Range(0f, 5f)] public float audioMinImpactSpeed = 1f;
        [Tooltip("The audio volume per dynamic pressure acting on the mesh.")]
        [Range(0f, 0.1f)] public float volumeFactor = 0.02f;
        [Tooltip("The maximal volume of the audio, can exceed 1 because the pitch effect reduce the audio intensity")]
        [Range(0f, 25f)] public float maxVolume = 5f;
        [Tooltip("The rate at which the audio loses volume after a blast.")]
        [Range(0f, 5f)] public float volumeFadeoffRate = 1f;
        [Tooltip("The audio pitch per dynamic pressure acting on the mesh. Might need to adjust volume level when modifying this input. ")]
        [Range(0f, 100f)] public float pitchFactor = 20f;
        [Tooltip("The minimal value of the pitch. low pitch makes less powerfull noise so increasing it can help simulate larger object but with worng pitch.")]
        [Range(0f, 1f)] public float minPitch = 0.2f;
        [Tooltip("The rate at which the audio got back to high pitch after a blast.")]
        [Range(0f, 0.5f)] public float pitchFadeoffRate = 0.1f;


        // internal audio data
        private float volume = 0f; // the computed volume
        private float pitch = 1f; // the computed pitch
        private float lastFilteredDataLeft = 0f; // used to save previous value for the low pass filter
        private float lastFilteredDataRight = 0f; // used to save previous value for the low pass filter
        float dynamicPressure = 0f; // the value used to compted audio volume and pitch

        [Space(10)]
        [Header("Debugging")]
        [Tooltip("Whether to draw forces with lines in gizmos mode.")]
        public bool drawForce = true;
        [Tooltip("The size of the vector drawn in gizmos mode to visualize the forces.")]
        public float vectorSize = 0.001f;

        // Start is called before the first frame update
        void Start()
        {
            // -------- initializing the physics data ---------------
            //setting the physical properties
            InitializePhysic();

            // setting the undeformed position as being the initial position to allow the algorithm to start it's search of the point vertically aligned (CAREFULL IF TOO BIG OF A DEFORMATION)
            undeformedPosition = transform.position;
            previousPosition = transform.position;

            // -------- initializing the VFX -------
            // getting the component
            splashVFX = GetComponent<VisualEffect>();

            // checking the component
            if (splashVFX != null && splashVFX.HasMatrix4x4("_LIDR") && splashVFX.HasMatrix4x4("_NKVW") && splashVFX.HasVector4("_totalLigthColor") && splashVFX.HasVector4("_waterColor") && splashVFX.HasVector3("_wind"))
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
        private void InitializePhysic()
        {
            // getting the rigidBody
            rb = GetComponent<Rigidbody>();
            if (rb == null)
            {
                // rigid body is mandatory
                rb = this.gameObject.AddComponent<Rigidbody>();

                Debug.Log("Rigid body added to this game obeject.");
            }

            // settign the use of the gravity, if no forces generation is requested, the gravity need to be deactivated otherwise the object will make a free fall through the water
            rb.useGravity = generateWaterForce;

            // calculating the projected surface (pi.r²)
            area = Mathf.PI*radius*radius;

            // calculating the mass (m = rho*4/3*pi*r^3)
            float mass = density*1000f*4f*Mathf.PI/3f*radius*radius*radius;
            rb.mass = mass;

            // calculating the center of gravity shift and setting it to the rigid body
            rb.centerOfMass = Vector3.up*gravityCenterHeight;

            // calculating the rotational inertia as being a sphere with a center of mass shifted in y direction
            rb.inertiaTensor = new Vector3(
                inertiaTensorFactor*2f/5f*mass*radius*radius + mass*radius*radius, // 2/5.m.R² + m.d² 
                inertiaTensorFactor*2f/5f*mass*radius*radius,                      // 2/5.m.R²            
                inertiaTensorFactor*2f/5f*mass*radius*radius + mass*radius*radius  // 2/5.m.R² + m.d²   
                );
            rb.inertiaTensorRotation = Quaternion.identity;
        }

        public void UpdateVFXProperties() // is called by ocean controller when there are modification
        {
            if (splashVFX != null)
            {
                splashVFX.SetMatrix4x4("_LIDR", Ocean.LIDR);
                splashVFX.SetMatrix4x4("_NKVW", Ocean.NKVW);
                splashVFX.SetVector4("_totalLigthColor", Ocean.totalLight);
                splashVFX.SetVector4("_waterColor", Ocean.waterColor);

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

        private void Update()
        {
            // ocean computation is done in update to avoid doing it several time per frame

            // getting the ground depth
            float groundDepth = 200f;
            if (Ocean.useTerrain)
            {
                groundDepth = - Ocean.terrain.SampleHeight(undeformedPosition) - Ocean.terrain.transform.position.y;
            }

            // updating the undeformed position and getting the height of the water
            waterHeight = Ocean.GetHeight(Time.time, transform.position, ref undeformedPosition, out Vector3 deformation, groundDepth);

            // calculating the depth as beign the water level minus the vertical position of the triangle, is positive inside water 
            float depth = waterHeight - transform.position.y + radius;

            // calculating the water normal and velocity only when touching the water
            if (depth > 0f)
            {
                waterNormal = Ocean.GetNormal(Time.time, undeformedPosition, deformation, groundDepth, radius);
                waterVelocity = Ocean.GetVelocity(Time.time, undeformedPosition, deformation, out _, depth, groundDepth);
            }
            // outside water the default values are set
            else
            {
                waterNormal = Vector3.up;
                waterVelocity = Vector3.zero;
            }

            // ---------------- computing the audio --------------------------------------
            if (generateAudio)
            {
                // applying the volume only when it get louder 
                // non optimized : volume = Mathf.Max(volume, dynamicPressure*volumeFactor);
                float newVolume = dynamicPressure*volumeFactor;
                volume = volume<newVolume ? newVolume : volume;

                // volume fade off fucntion of the volume itself
                // non optimized : volume -= Time.deltaTime*volumeFadeoffRate*Mathf.Max(1f, volume);
                volume -= Time.deltaTime*volumeFadeoffRate;// * (volume<1f ? 1f : volume);

                // making sure the volume is correclty clamped
                if (volume<0f) { volume = 0f; }
                else if (volume > maxVolume) { volume = maxVolume; }

                // applying the pitch when it get lower to the previous value
                // non optimized : pitch = Mathf.Min(pitch, pitchFactor/dynamicPressure);
                float newPitch = pitchFactor/dynamicPressure;
                pitch = pitch>newPitch ? newPitch : pitch;

                // making the pitch fade off
                pitch += Time.deltaTime*pitchFadeoffRate;

                // ensuring the pitch is correctly clamped
                if (pitch>1f) { pitch = 1f; }
                else if (pitch < minPitch) { pitch = minPitch; }
            }
        }

        private void FixedUpdate()
        {
            // initializing the sum  that will be used for the audio
            dynamicPressure = 0f;

            // getting the depth
            float depth = waterHeight - transform.position.y + radius;

            // trying to update undeformed position reguardless of the wave in an atemmpt to make the GetHeight function more acurate.
            undeformedPosition += transform.position - previousPosition;
            previousPosition = transform.position;

            // calculating the wind position that is shared with force computation and particle generation
            float wind = Ocean.GetWindSpeed(transform.position);
            Vector3 windVector = Ocean.GetWindVelocity(transform.position);

            //----------- force computation------------------
            //forces happen when inside water
            if (depth > 0f)
            {
                //clampling the depth to 2 times the radius (diameter) because when deeper the forces don't evolve anymore
                if (depth > 2f*radius) { depth = 2f*radius; }

                // calculating the buoyancy force by making the approximation that the volume of water displaced is proportional to depth
                // rho.g.4/3.pi.R^3*depth/2R = rho.g.4/6.pi.R^2*depth
                Vector3 buoyancyForce = 20943.95f*radius*radius*depth*waterNormal;

                // calculating the velocity relative to the water
                Vector3 relativeVelocity = waterVelocity - rb.velocity;

                // calculating the drag force by making the approximation that the imerged surface is proportional to the depth
                // 1/2.rho.V².S.Cx
                Vector3 dragForce = dragCoeff*500f*relativeVelocity*relativeVelocity.magnitude*area*depth/(2f*radius);

                // the force apply in the middle of the sphere
                if (generateWaterForce) { rb.AddForceAtPosition(dragForce + buoyancyForce, transform.position); }

                // drawing force in debug mode
                if (drawForce)
                {
                    Debug.DrawLine(transform.position, transform.position + vectorSize*dragForce, Color.blue);
                    Debug.DrawLine(transform.position, transform.position + vectorSize*buoyancyForce, Color.red);
                }

                //-------------- particles & audio generation-----------------------

                // calculating the magnitude of relative speed
                float relativeSpeed = relativeVelocity.magnitude;

                // deducing the direction facing the flow
                Vector3 flowVector = relativeVelocity/relativeSpeed;

                // particles are generated only when no too deep
                // and when the chrono says so
                if (depth < 2f*radius)
                {
                    if (generateParticles && (0.5f*relativeVelocity.y + relativeSpeed) > minImpactSpeed)
                    {
                        // ------------ sea foam burst --------------------

                        // setting the foam burst attribute
                        eventAttribute.SetFloat(spawnCountID, burstCount);
                        eventAttribute.SetVector3(positionID, transform.position - radius*flowVector - Vector3.up*depth);
                        eventAttribute.SetVector3(triangleVelocityID, rb.velocity);
                        eventAttribute.SetVector3(waterVelocityID, waterVelocity);
                        eventAttribute.SetVector3(triangleNormalID, flowVector);
                        eventAttribute.SetFloat(triangleSizeID, 0.5f*radius);
                        eventAttribute.SetVector3(targetPositionID, undeformedPosition);

                        // playing the burst VFX once 
                        splashVFX.SendEvent(eventBurstID, eventAttribute);

                        // ------------- sea spray -----------------

                        // setting the spray attribute
                        eventAttribute.SetFloat(spawnCountID, Mathf.Min(sprayMaxCount, sprayCountFactor*area*relativeSpeed*wind));
                        eventAttribute.SetFloat(lifetimeID, sprayLifetimeFactor*relativeSpeed*wind);
                        eventAttribute.SetVector3(positionID, transform.position);
                        eventAttribute.SetVector3(velocityID, windVector);

                        // playing the spray VFX once 
                        splashVFX.SendEvent(eventSprayID, eventAttribute);
                    }

                    // updating the sum that will define the audio volume when the velocity is enough
                    if (generateAudio && relativeSpeed > audioMinImpactSpeed)
                    {
                        dynamicPressure += area*relativeSpeed;
                    }

                }
            }
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
            Gizmos.DrawWireSphere(transform.TransformPoint(rb.centerOfMass), Mathf.Pow(rb.mass, 1f/3f)*0.005f); //transform.position + 
            Gizmos.color = Color.grey;
            Gizmos.DrawWireSphere(transform.position, radius);
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
#endif
    }
}