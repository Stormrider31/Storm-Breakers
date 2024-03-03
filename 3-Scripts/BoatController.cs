using UnityEngine;
using UnityEngine.VFX;

namespace StormBreakers
{
    public class BoatController : MonoBehaviour
    {
        // This simple boat controller makes a propeller and rudder force according to the input given by the player and when the controller is inside the water
        // propeller force is in X axis, rudder force is in Z axis
        // This component must be attached to a game oject child of an object with a rigid body attached
        // This component can aslo generate wake VFX when the controller is inside the water
        // (part of the VFX tweaks is managed in the VFX inspector itself)
        // This compenent can also manage an audio source according to the player input
        // this component can also make an automatic pilot that can be usefull for NPC boat

        [Header("Physics properties")]
        [Tooltip("Propeller forward and backward force in Newtons.")]
        public float propellerForce = 100f;
        [Tooltip("Rudder lateral force in Newtons.")]
        public float rudderForce = 100f;
        [Tooltip("The depth in meter at which the controller make full rudder and propeller forces. Should be close to the propeller and rudder actual size, increase this value for powerfull watercraft.")]
        public float effectiveDepth = 1f;

        // physic internal datas
        private Rigidbody rb; // the rigidbody attached to the parent
        private Vector3 undeformedPosition; // used to compute the ocean deformation
        private float depth;
        private Vector3 waterVelocity;
        private float propellerUsage = 0f;
        private float rudderUsage = 0f;
        private float depthForceFactor; // the inverse of effective depth to prevent the use of a divide
        private float effectiveness = 0f;

        [Space(10)]
        [Header("Automatic pilot properties")]
        [Tooltip("Wether to activate automatic pilot for this controller")]
        public bool automaticPilot = false;
        [Tooltip("The percentage of the propeller force to permanently use.")]
        [Range(0f, 1f)] public float throttle = 1f;
        [Tooltip("The gain of the regualtor that maintain the course.")]
        [Range(0f, 10f)] public float rudderRegulatorGain = 2f;
        [HideInInspector] public Vector3 course;


        [Space(10)]
        [Header("Particles properties (more in the VFX component)")]
        [Tooltip("The distance between each wake particles creation.")]
        [Range(0.01f, 2.5f)] public float swirlDistanceRate = 0.5f;
        [Tooltip("The lifetime of the particles when the propeller is at full force.")]
        [Range(0.1f, 25f)] public float swirlLifetime = 5f;
        [Tooltip("Whether to generate the foam burst particles.")]
        public bool generateBurstParticles = true;
        [Tooltip("The local position where the burst particles are generated.")]
        public Vector3 burstGenerationPosition = Vector3.zero;
        [Tooltip("The local backward speed of the burst particle generated.")]
        [Range(-1f, 5f)] public float burstBackwardSpeed = 0f;
        [Tooltip("The local vertical speed of the burst particle generated.")]
        [Range(0f, 25f)] public float burstUpwardSpeed = 5f;

        // VFX internal datas
        private VisualEffect wakeVFX; // the VFX that makes swirls into the water
        private VFXEventAttribute eventAttribute; // the data sents to the VFX
        private float particlesCreationDeltaTime; // the delta time between each particles creation, is the inverse of creatio rate
        private int positionID; // The ID of position attribute
        private int velocityID; // The ID of velocity attribute
        private int undeformedPositionID; // The ID of an atribute that position the particle accordind to the waves
        private int alphaID; // The ID of alpha attribute
        private int lifetimeID; // the ID of the lifetime attribute
        private int swirlEventId;
        private int burstEventID;
        private float chrono = 0f; // a time counter that serves to know when to create a particle

        [Space(10)]
        [Header("Audio properties")]
        [Tooltip("The volume of the audio source when the engine is idle.")]
        [Range(0f, 1f)] public float idleVolume = 0.5f;
        [Tooltip("The volume of the audio source when the engine is full throttle.")]
        [Range(0f, 1f)] public float fullThrottleVolume = 1f;
        [Tooltip("The pitch of the audio source when the engine is idle.")]
        [Range(0f, 3f)] public float idlePitch = 0.5f;
        [Tooltip("The pitch of the audio source when the engine is full throttle and inside water.")]
        [Range(0f, 3f)] public float fullThrottlePitch = 1f;
        [Tooltip("The additional pitch of the audio source when the engine is full throttle and outside water.")]
        [Range(0f, 3f)] public float unloadedAdditionalPitch = 1f;

        // audio internal datas
        private AudioSource audioSource; // the engine audio source if any

        [Space(10)]
        [Header("Debugging")]
        [Tooltip("Whether to draw forces with lines in gizmos mode.")]
        public bool drawForce = true;
        [Tooltip("The size of the vector drawn in gizmos mode to visualize the forces.")]
        public float vectorSize = 0.001f;

        void Start()
        {
            // -------- initializing physic ------------

            // getting the needed component
            rb = transform.parent.GetComponent<Rigidbody>();

            // if no rigid body found, then the component is deactivated
            if (rb == null) { Debug.LogWarning("Please attach this component to a game object child of a rigid body"); this.enabled = false; return; }

            // initializing the undeformed position with current world position
            undeformedPosition = transform.position;

            // the depth factor is the inverse of depth effectiveness
            if (effectiveDepth > 0f)
            {
                depthForceFactor = 1f/effectiveDepth;
            }
            // except when is 0
            else
            {
                // setting an arbitrary high value.
                depthForceFactor = 1000f;
            }

            // ---------- initializing the autopilot -----------

            // computing the course vector
            SetCurrentCourse();

            // --------- initializing VFX --------------------

            // getting the needed component
            wakeVFX = GetComponent<VisualEffect>();

            // checking the VFX existence
            if (wakeVFX != null)
            {
                // checking if the VFX template is correct
                if (wakeVFX.HasMatrix4x4("_LIDR") && wakeVFX.HasMatrix4x4("_NKVW") && wakeVFX.HasVector4("_totalLigthColor")  && wakeVFX.HasVector3("_wind"))
                {
                    // saving the ID of the attribute
                    positionID = Shader.PropertyToID("position");
                    velocityID = Shader.PropertyToID("velocity");
                    undeformedPositionID = Shader.PropertyToID("targetPosition");
                    alphaID = Shader.PropertyToID("alpha");
                    lifetimeID = Shader.PropertyToID("lifetime");
                    swirlEventId = Shader.PropertyToID("swirl");
                    burstEventID = Shader.PropertyToID("burst");

                    // the inital chrono is set to half the particle lifetime as it is supposed not to have any mouvement.
                    particlesCreationDeltaTime = swirlLifetime*0.5f;

                    // initializing the VFX event attribute
                    eventAttribute = wakeVFX.CreateVFXEventAttribute();

                    // setting VFX properties
                    UpdateVFXProperties();

                    // initializing chrono
                    chrono = 0f;
                }
                else
                {
                    // warning that it's the wrong template
                    Debug.LogWarning("Wrong VFX template, please use wakeVFX.vfx");

                    // detaching virtually the component
                    wakeVFX = null;
                }
            }

            // --------- initializing the audio ------------------

            // getting the component
            audioSource = GetComponent<AudioSource>();
        }
        public void UpdateVFXProperties() // is to be called when there are modification
        {
            if (wakeVFX != null)
            {
                wakeVFX.SetMatrix4x4("_LIDR", Ocean.LIDR);
                wakeVFX.SetMatrix4x4("_NKVW", Ocean.NKVW);
                wakeVFX.SetVector4("_totalLigthColor", Ocean.totalLight);
                wakeVFX.SetVector3("_wind", new Vector3(Ocean.Wind.speed*Ocean.Wind.cosDirection, Ocean.Wind.inverseHeight, Ocean.Wind.speed*Ocean.Wind.sinDirection));

                // when using a terrain the VFX properties are filled with the static data that should have been set
                if (Ocean.useTerrain)
                {
                    // making sure the terrain has been set
                    if (Ocean.terrain != null)
                    {
                        // setting the data
                        wakeVFX.SetTexture("_terrainHeightmap", Ocean.terrain.terrainData.heightmapTexture);
                        wakeVFX.SetVector3("_terrainPosition", Ocean.terrain.transform.position);
                        wakeVFX.SetVector3("_terrainScale", Ocean.terrain.terrainData.size);
                    }
                    else { Debug.LogWarning("No terrain detected !"); }
                }
                // else if no terrain is used, setting some default value that ensure no depth effect
                else
                {
                    // default value of a deep flat sea bed
                    wakeVFX.SetTexture("_terrainHeightmap", new RenderTexture(1, 1, 0));
                    wakeVFX.SetVector3("_terrainPosition", new Vector3(0f, -200f, 0f));
                    wakeVFX.SetVector3("_terrainScale", new Vector3(1f, 1f, 1f));
                }
            }
        }
        public void SetCurrentCourse()
        {
            course = Vector3.ProjectOnPlane(transform.right, Vector3.up).normalized;
        }
        void Update()
        {
            // ----------- physics --------------
            #region
            // getting the ground depth
            float groundDepth = 200f;
            if (Ocean.useTerrain)
            {
                groundDepth = -Ocean.terrain.SampleHeight(undeformedPosition) - Ocean.terrain.transform.position.y;
            }

            // getting roughly the water height
            float oceanHeight = Ocean.GetHeight(Time.time, transform.position, ref undeformedPosition, out Vector3 deformation, groundDepth, false);

            // calculating the depth of the controller
            depth = oceanHeight - transform.position.y;

            // computing expensive GetVelocity only when required
            if (depth > 0f)
            {
                // getting the water velocity
                waterVelocity = Ocean.GetVelocity(Time.time, undeformedPosition, deformation, out _);
            }
            else
            {
                waterVelocity = Vector3.zero;

                // clamping to 0
                depth = 0f;
            }
            #endregion
            // ------------ particles --------------
            #region
            // if the component exist and depth positive
            if (wakeVFX != null && depth > 0f)
            {
                // ----------- swirls ----------------
                #region
                // computing the swrils lifetime function of propeller activity
                float lifetime = propellerUsage*swirlLifetime;
                // taking absolute
                if (lifetime < 0f) { lifetime *= -1f; }

                // updating the chrono
                chrono += Time.deltaTime;

                // generating swrils only if lifetime is superior to 0
                if (lifetime > 0f)
                {
                    // the particles generation is function of the velocity, the faster it goes the more particles are to be created to avoid having them separated
                    Vector3 relativeVelocity = waterVelocity - rb.GetPointVelocity(transform.position);
                    float relativeSpeed = relativeVelocity.magnitude;

                    // to avoid division by 0 when the controller is not moving, the speed is clamped to the value that would generate particles every quarter lifetime
                    relativeSpeed = Mathf.Max(swirlDistanceRate/lifetime*4f, relativeSpeed);

                    // updating the chrono limit
                    particlesCreationDeltaTime = swirlDistanceRate/relativeSpeed;

                    // creating particles every time the chorno reach the limit
                    if (chrono > particlesCreationDeltaTime)
                    {
                        // reinitializing chrono
                        chrono = 0f;

                        // setting the undeformed position attribute of the particle
                        eventAttribute.SetVector3(undeformedPositionID, undeformedPosition);

                        // setting the alpha of the particle, is 1 when full throttle
                        eventAttribute.SetFloat(alphaID, propellerUsage);

                        // settting the lifetime
                        eventAttribute.SetFloat(lifetimeID, lifetime);

                        // creating a particle
                        wakeVFX.SendEvent(swirlEventId, eventAttribute);
                    }
                }
                #endregion
                // ------------ burst -------------
                #region
                if (generateBurstParticles && propellerUsage > 0f)
                {
                    // setting the undeformed position attribute of the particle
                    eventAttribute.SetVector3(undeformedPositionID, undeformedPosition);

                    // setting the position of the particles
                    eventAttribute.SetVector3(positionID, transform.position + transform.TransformDirection(burstGenerationPosition));

                    // setting the speed of the particles
                    eventAttribute.SetVector3(velocityID, effectiveness*propellerUsage*(-transform.right*burstBackwardSpeed + transform.up*burstUpwardSpeed));

                    // creating a particle
                    wakeVFX.SendEvent(burstEventID, eventAttribute);
                }
                #endregion
            }
            #endregion
            // ------- audio management ----------
            #region
            // checking if there is an audio source
            if (audioSource != null)
            {
                // calculating how close from the surface and clamping (depth is already positive)
                float nearSurface = 1f - depth*depthForceFactor;
                if (nearSurface < 0f) { nearSurface = 0f; }

                // setting the audio volume and pitch according to the absolute of propeller usage
                float absPropellerUsage = Mathf.Abs(propellerUsage);
                audioSource.volume = Mathf.Lerp(idleVolume, fullThrottleVolume, absPropellerUsage);
                audioSource.pitch =  Mathf.Lerp(idlePitch, fullThrottlePitch, absPropellerUsage) + absPropellerUsage*nearSurface*unloadedAdditionalPitch;
            }
            #endregion
        }

        void FixedUpdate()
        {
            // inputs
            if (automaticPilot) // autopilot
            {
                // the propeller is continually activated
                propellerUsage = throttle;

                // the rudder force is computed with the dot product of the local blue axis with the course vector
                rudderUsage = -Vector3.Dot(course, transform.forward)*rudderRegulatorGain;

                // calmping the rudder usage to 1
                if (rudderUsage > 1f) { rudderUsage = 1f; }
                else if (rudderUsage < -1f) { rudderUsage = -1f; }
            }
            else // player input
            {
                propellerUsage = Input.GetAxis("Vertical");
                rudderUsage = Input.GetAxis("Horizontal");
            }

            // computing the force effectiveness based on depth and clamping to 0;1
            effectiveness = depth*depthForceFactor;
            if (effectiveness > 1f) { effectiveness = 1f; }
            if (effectiveness < 0f) { effectiveness = 0f; }

            // applying the sum of the forces
            rb.AddForceAtPosition(effectiveness*propellerUsage*transform.right*propellerForce + effectiveness*rudderUsage*transform.forward*rudderForce, transform.position);

        }

        private void OnValidate()
        {
            // computing the course vector
            course = Vector3.ProjectOnPlane(transform.right, Vector3.up).normalized;
        }
        private void OnDrawGizmos()
        {
            if (drawForce)
            {
                // forces are draw in red
                Gizmos.color = Color.red;

                // simplifying the code
                Vector3 from = transform.position;

                // drawing current force in play mode
                if (Application.isPlaying)
                {
                    // drawing the propeler force vector
                    Vector3 toward = transform.right*propellerUsage*propellerForce*vectorSize;
                    Gizmos.DrawLine(from, from + toward);

                    // drawing the rudder force
                    toward = transform.forward*rudderUsage*rudderForce*vectorSize;
                    Gizmos.DrawLine(from, from + toward);
                }
                // drawing potential forces in editor
                else
                {
                    // drawing the propeler force vector
                    Vector3 toward = transform.right*propellerForce*vectorSize;
                    Gizmos.DrawLine(from, from + toward);

                    // drawing the rudder force
                    toward = transform.forward*rudderForce*vectorSize;
                    Gizmos.DrawLine(from, from + toward);
                    Gizmos.DrawLine(from, from - toward);
                }


                // drawing the burst generation point
                if (generateBurstParticles)
                {
                    Gizmos.color = Color.white;
                    Gizmos.DrawWireSphere(from + transform.TransformDirection(burstGenerationPosition), 0.2f);
                }

                // draw the course when automatic pilot is on
                if (automaticPilot)
                {
                    Gizmos.color = Color.green;
                    Gizmos.DrawLine(from, from + course*200f);
                }
            }
        }
    }
}