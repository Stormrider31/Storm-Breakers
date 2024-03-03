using UnityEngine;

namespace StormBreakers
{
    public class WindController : MonoBehaviour
    {
        // This component set the wind and generate procedural audio according to the main camera position 

        [Header("Wind properties")]
        [Tooltip("The direction of the wind in degree relative to the world red axis (opposite).")]
        [Range(-180f, 180f)] public float windDirection = 0f;
        public enum WindUnit { knot, kmh, mph, mps }
        [Tooltip("The unit of the wind strenght set in the inspector.")]
        public WindUnit windUnit = WindUnit.kmh;
        [Tooltip("The strength of the wind in wind unit.")]
        [Range(0f, 200f)] public float windSpeed = 30f;
        [Tooltip("The height at which the wind is full strength. At sea level the wind strength is halfed.")]
        [Range(0f, 50f)] public float windHeight = 20f;

        [Space(10)]
        [Header("Audio properties")]
        public bool generateAudio = true;
        [Tooltip("Wether to compute the camera's parent in the wind computation.")]
        public bool includeCameraVelocity = false;
        [Tooltip("Defines the pitch of the audio source function of the wind strength at camera's location.")]
        [Range(0f, 2.5f)] public float pitchFactor = 0.5f;
        [Tooltip("Defines the volume of the audio source function of the wind strength at camera's location.")]
        [Range(0f, 0.5f)] public float volumeFactor = 0.1f;
        [Tooltip("Defines the pan variation when wind comes from the side of the camera.")]
        [Range(0f, 2.5f)] public float panFactor = 0.5f;
        [Tooltip("The percentage of the wind increase during gusts. Affect only audio.")]
        [Range(0f, 2.5f)] public float turbulenceAmplitude = 0.5f;
        [Tooltip("The frequency of the turbulence per seconds.")]
        [Range(0f, 2.5f)] public float turbulenceFrequency = 0.5f;

        // internal data
        private AudioSource audioSource; // the audio component
        private DetectWater detectWater; // the detect water component
        private Transform cameraTransform; // the camera position used to compute its velocity, can be its parent position
        private Vector3 previousCameraPosition = Vector3.zero; // the previous position used to computed the camera velocity
        private Vector3 cameraVelocity = Vector3.zero;

        private void Start()
        {
            UpdateWindProperties();

            // getting the audio component
            audioSource = GetComponent<AudioSource>();

            // checking the component
            if (audioSource == null) { generateAudio = false; }

            // setting the audio source according to the input
            else { audioSource.enabled = generateAudio; }

            // getting the compenent that detect when the camera is underwater
            if (Camera.main == null) { Debug.LogError("Please set the tag 'main camera' to a camera in the scene."); }
            detectWater = Camera.main.GetComponent<DetectWater>();

            // checking the component
            if (detectWater == null) { detectWater = Camera.main.gameObject.AddComponent<DetectWater>(); }

            // getting hte camera object to follow
            if (includeCameraVelocity)
            {
                // if the camera has a parent, then the parent is the position we follow to avoid fast camera orbit super speed. Otherwise we take the camera.
                if (Camera.main.transform.parent == null)
                {
                    cameraTransform = Camera.main.transform;
                }
                else
                {
                    cameraTransform = Camera.main.transform.parent;
                }

                // initializing the previous position
                previousCameraPosition = cameraTransform.position;
            }
        }

        public void UpdateWindProperties() // set the static properties of Ocean static class
        {
            // converting in meter per second the wind input according to its unit
            if (windUnit == WindUnit.kmh) { Ocean.Wind.speed = windSpeed/3.6f; }
            if (windUnit == WindUnit.knot) { Ocean.Wind.speed = windSpeed*0.5144444f; }
            if (windUnit == WindUnit.mph) { Ocean.Wind.speed = windSpeed*0.44704f; }
            if (windUnit == WindUnit.mps) { Ocean.Wind.speed = windSpeed; }

            // the static that store the height is inversed to remove the use of several divide at runtime (multiplication are faster)
            Ocean.Wind.inverseHeight =  1f/windHeight;

            // setting the direction in degree
            Ocean.Wind.direction = windDirection;

            // the static store cos and sin of the direction to remove the use of thoses expansive function at runtime
            Ocean.Wind.cosDirection = -Mathf.Cos(Mathf.Deg2Rad*windDirection);
            Ocean.Wind.sinDirection = -Mathf.Sin(Mathf.Deg2Rad*windDirection);

            // turbulences
            Ocean.Wind.turbulenceAmplitude = turbulenceAmplitude;
            Ocean.Wind.turbulenceFrequency = turbulenceFrequency;
        }

        void Update()
        {
            // in update only the audio is managed
            if (generateAudio)
            {
                // defining the wind velocity that depends if the camera is underwater or not
                Vector3 windVelocity = Vector3.zero;

                // when the camera is underwater there is no wind in the ears, else the wind is computed
                if (!detectWater.IsUnderwater)
                {
                    windVelocity = Ocean.GetWindVelocity(Camera.main.transform.position, true);

                    // adding the camera velocity when asked
                    if (includeCameraVelocity)
                    {
                        // adding the velocity
                        windVelocity -= cameraVelocity;
                    }
                }

                // getting the magnitude and normailzed wind vector to ease computation (with a safe division)
                float windStrength = windVelocity.magnitude;
                Vector3 windVector = windStrength>0.01f ? windVelocity/windStrength : Vector3.zero;


                // calculating the projection of the wind onto the camera foward vector
                float forwardIntensity = Vector3.Dot(-Camera.main.transform.forward, windVector);

                // calculating the projection of the wind onto the camera right vector
                float rightIntensity = Vector3.Dot(-Camera.main.transform.right, windVector);

                // setting the volume and pitch of the audio source according to the wind strength
                audioSource.volume = Mathf.Min(1f, windStrength*volumeFactor)*(0.7f + 0.2f*forwardIntensity +0.3f*(rightIntensity>0f ? rightIntensity : -rightIntensity)); // Mathf.Min(1f, windStrength*volumeFactor)*(0.7f+0.3f*forwardIntensity);
                audioSource.pitch = Mathf.Sqrt(windStrength)*pitchFactor;

                // setting the pan
                audioSource.panStereo = rightIntensity*panFactor;
            }
        }

        private void FixedUpdate()
        {
            // computing here the camera Veclocity for a smoother evolution
            if (includeCameraVelocity)
            {
                // computing the velocity
                cameraVelocity = (cameraTransform.position - previousCameraPosition)/Time.fixedDeltaTime;

                // updating previous position for next frame
                previousCameraPosition = cameraTransform.position;
            }
        }

        // editor script
#if UNITY_EDITOR
        private void OnDrawGizmos() // drawing wind vector for a better visualitzation
        {
            // drawing in blue
            Gizmos.color = Color.blue;

            // getting the position of the object and removing y coordinate to make sure the wind is drawn from the sea level
            Vector3 position = new Vector3(transform.position.x, 0f, transform.position.z);

            // draw several lines every 3m heigth untul 50m
            for (float y = 0f; y <= 50f; y+= 3f)
            {
                // getting the wind at this height
                Vector3 wind = Ocean.GetWindVelocity(new Vector3(0f, y, 0f));

                // simplifying the code
                Vector3 from = position + Vector3.up*y;
                Vector3 to = from + wind;

                // drawing the wind vector at this position
                Gizmos.DrawLine(from, to);

                // drawing the "arrow"
                Gizmos.DrawSphere(to, 0.1f);
            }
        }

        private void OnValidate()
        {
            // updating the static properties when modifying properties in the inspector
            UpdateWindProperties();

            // setting the ocean material properties
            // IS A CODE COPY OF OCEAN CONTROLLER
            if (Ocean.sharedMaterial != null)
            {
                // setting the ripples direction function of the wind direction
                Ocean.sharedMaterial.SetFloat("_ripplesDirection", Ocean.Wind.direction);

                // setting the ripples intensity function of the wind strenght, is maximal as soon the wind reachs 36km/h
                //Ocean.surfaceIntensity = Mathf.Min(1f, Ocean.Wind.speed*0.1f);
                Ocean.sharedMaterial.SetFloat("_ripplesIntensity", Mathf.Min(1f, Ocean.Wind.speed*0.1f));

                // setting the surfacesmoothness function of the wind, is minimal (0.7) as soon the wind reachs 72km/h, is 1 when no wind at all.
                Ocean.sharedMaterial.SetFloat("_smoothness", Mathf.Clamp(1.1f - 0.015f*Ocean.Wind.speed, 0.7f, 1f));

                /*
                // setting the material wave intensity (which define the slope of the horizon)
                if (Ocean.intensity != null)
                {
                    float maxIntensity = Ocean.surfaceIntensity;
                    // running through each wave system
                    for (int w = 0; w<4; w++)
                    {
                        // checking the current wave intensity against the saved value to search the maximum 
                        if (Ocean.intensity[w] > maxIntensity)
                        {
                            // setting the ocean intensity with this wave intensity that could be the one with the maximal intensity
                            maxIntensity = Mathf.Min(1f, Ocean.intensity[w]);
                        }
                    }

                    // now the material can be updated
                    Ocean.surfaceIntensity = maxIntensity;
                    Ocean.sharedMaterial.SetFloat("_oceanIntensity", Ocean.surfaceIntensity);
                }
                */
            }
        }
#endif
    }
}