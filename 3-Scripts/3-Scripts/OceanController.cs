using UnityEditor;
using UnityEngine;
using UnityEngine.VFX;

using UnityEngine.SceneManagement;

namespace StormBreakers
{

    [ExecuteInEditMode]
    public class OceanController : MonoBehaviour
    {
        // This component is mandatory as it allows to edit ocean global properties 
        // This component also position the mesh roughly where the camera looks
        // It must be attached to a mesh rendered with ocean material.

        [Tooltip("Update the materials as it seems there is a problem with serialization when saving the scene.")]
        public bool clickHereToRefresh = false;

        [Space(10)]
        [Tooltip("The albedo color of the water. In deep clear water it must be very dark blue as the light ray penetrate deeply into the water and scatter few light. In shallow water the color can be brigther with more yellow coming from the sand color. In dirty water the color can be bright too as the light ray scatter quickly when penettrating the water.  ")]
        public Color waterColor = new Color(0.068f, 0.150f, 0.217f);
        [Tooltip("The terrrain  used to compute the shore effects")]
        public Terrain terrain;

        [Tooltip("Allows you to quickly set all the wave on or off.")]
        [Range(0f, 1f)] public float waveIntensity = 1f;

        [Space(10)]
        [Tooltip("The wavelength in meters of the first wave sytem. Big wavelength makes bigger waves.")]
        [Range(1f, 100f)] public float wavelength0 = 15f;
        [Tooltip("The wavelength in meters of the second wave sytem. Big wavelength makes bigger waves. Must be smaller that the one of the previous system.")]
        [Range(1f, 100f)] public float wavelength1 = 10f;
        [Tooltip("The wavelength in meters of the third wave sytem. Big wavelength makes bigger waves. Must be smaller that the one of the previous system.")]
        [Range(1f, 100f)] public float wavelength2 = 5f;
        [Tooltip("The wavelength in meters of the dourth wave sytem. Big wavelength makes bigger waves. Must be smaller that the one of the previous system.")]
        [Range(1f, 100f)] public float wavelength3 = 1f;

        [Space(10)]
        [Tooltip("The stepness of the first wave system. Above 1 waves start to break.")]
        [Range(0f, 1.5f)] public float intensity0 = 1.3f;
        [Tooltip("The stepness of the second wave system. Above 1 waves start to break.")]
        [Range(0f, 1.5f)] public float intensity1 = 1.2f;
        [Tooltip("The stepness of the third wave system. Above 1 waves start to break.")]
        [Range(0f, 1.5f)] public float intensity2 = 1.1f;
        [Tooltip("The stepness of the fourth wave system. Above 1 waves start to break.")]
        [Range(0f, 1.5f)] public float intensity3 = 1.4f;

        [Space(10)]
        [Tooltip("The direction in degree of the first wave system relative to the opposite red world axis.")]
        [Range(-180f, 180f)] public float direction0 = 0f;
        [Tooltip("The direction in degree of the second wave system relative to the opposite red world axis.")]
        [Range(-180f, 180f)] public float direction1 = 15f;
        [Tooltip("The direction in degree of the third wave system relative to the opposite red world axis.")]
        [Range(-180f, 180f)] public float direction2 = -20f;
        [Tooltip("The direction in degree of the fourth wave system relative to the opposite red world axis.")]
        [Range(-180f, 180f)] public float direction3 = -5f;

        [Space(10)]
        [Tooltip("Wave density of the first wave system. Reduce this value to get a more scattered and random wave pattern. This value also affect each wave group intensity so you might update the system's intensity as well.")]
        [Range(0.7f, 1f)] public float waveDensity0 = 0.9f;
        [Tooltip("Wave density of the second wave system. Reduce this value to get a more scattered and random wave pattern. This value also affect each wave group intensity so you might update the system's intensity as well.")]
        [Range(0.7f, 1f)] public float waveDensity1 = 0.85f;
        [Tooltip("Wave density of the third wave system. Reduce this value to get a more scattered and random wave pattern. This value also affect each wave group intensity so you might update the system's intensity as well.")]
        [Range(0.7f, 1f)] public float waveDensity2 = 0.8f;
        [Tooltip("Wave density of the fourth wave system. Reduce this value to get a more scattered and random wave pattern. This value also affect each wave group intensity so you might update the system's intensity as well.")]
        [Range(0.7f, 1f)] public float waveDensity3 = 0.75f;

        [Space(10)]
        [Tooltip("Number of waves per groups (or set) in the first system. High number are only suitable for an old swell.")]
        [Range(1f, 5f)] public float setNumber0 = 2.2f;
        [Tooltip("Number of waves per groups (or set) in the second system. High number are only suitable for an old swell.")]
        [Range(1f, 5f)] public float setNumber1 = 1.5f;
        [Tooltip("Number of waves per groups (or set) in the third system. High number are only suitable for an old swell.")]
        [Range(1f, 5f)] public float setNumber2 = 1.5f;
        [Tooltip("Number of waves per groups (or set) in the fourth system. High number are only suitable for an old swell.")]
        [Range(1f, 5f)] public float setNumber3 = 1.5f;

        [Space(10)]
        [Tooltip("Read only field that indicate the wave height of the first system which is by definition the swell.")]
        public string swellHeightRef;
        [Tooltip("Read only field that indicate the period of the first system which is by definition the swell.")]
        public string swellPeriodRef;

        [Space(10)]
        [Header("Global tweaks")] 
        [Tooltip("The factor used to compute the velocity of the breaking wave. Set it to zero if you want no breaking physic effect.")]
        [Range(0, 5f)] public float breakersExtraSpeedFactor = 1f;
        [Tooltip("The factor used to compute the torque of the breaking wave. Set it to zero if you want no breaking torque effect.")]
        [Range(0f, 5f)] public float breakersTorqueFactor = 1f;
        [Tooltip("The fixed frame rate used in physics. Because the object in water have in general slow acceleration, this value can be trimmed down to inmprove FPS and avoid CPU overhead peaks. Particles generation depends on it.")]
        [Range(0f, 300f)] public float fixedFrameRate = 60;
        [Tooltip("The lighting of the particles is computed with this component, but sometimes it gives the worng intensity and you can tweak it with this value.")]
        [Range(0f, 5f)] public float particleLightingFactor = 1f;
        [Tooltip("Make the game object ocean move to this distance in camera forward direction to set the detailled mesh and VFX in focus.")]
        [Range(0f, 350f)] public float oceanObjectDisplacementWithCameraAngle = 70f;

        // internal object reference
        private VisualEffect oceanVFX; // the visual effect attached to ocean controller's object, can be null
        private WindController windController; // the wind controller of the scene, can be null
        private Light mainLight; // the directional light with the higher intensity of the scene, can be null

        private void OnEnable()
        {
            // ------------- physics ---------------
            Time.fixedDeltaTime = 1f/fixedFrameRate;
            // -------------- static construction ------------
            //constructing statics arrays that cannot be managed
            Ocean.ConstructStaticData();
            // -------------- wind ---------------
            #region
            // getting the mesh render and checking if null
            MeshRenderer meshRenderer = GetComponent<MeshRenderer>();
            if (this != null && meshRenderer == null)
            {
                Debug.LogError("Please attach a mesh renderer with ocean.mat along with ocean controller.");
            }

            // getting the material attached, using shared means that all the other materials will be uptaded as well
            Ocean.sharedMaterial = meshRenderer.sharedMaterial;


            // checking if it's the correct material
            if (Ocean.sharedMaterial != null)
            {
                // cannot check matrices here because frequently return false
                if (!Ocean.sharedMaterial.HasColor("_totalLigthColor") ||
                    !Ocean.sharedMaterial.HasFloat("_oceanIntensity") ||
                    !Ocean.sharedMaterial.HasFloat("_smoothness") ||
                    !Ocean.sharedMaterial.HasFloat("_ripplesIntensity") ||
                    !Ocean.sharedMaterial.HasFloat("_ripplesDirection"))
                { Debug.LogError("Please set ocean.mat to the mesh renderer of ocean controller object."); }
            }
            else { Debug.LogError("Please set ocean.mat to the mesh renderer of ocean controller object."); }

            // getting the wind controller
            windController = GameObject.FindObjectOfType<WindController>();

            // updating the wind data and send the message to every object that need an update
            UpdateWind();

            #endregion

            // ------------- waves -------------
            #region
            // getting the visual effect attached
            oceanVFX = GetComponent<VisualEffect>();

            // checking the visual effect
            if (oceanVFX != null)
            {
                if (!oceanVFX.HasMatrix4x4("_LIDR") ||
                    !oceanVFX.HasMatrix4x4("_NKVW") ||
                    !oceanVFX.HasVector3("_wind") ||
                    !oceanVFX.HasVector4("_totalLigthColor") ||
                    !oceanVFX.HasVector4("_waterColor"))
                { Debug.LogWarning("Wrong VFX template, please attach oceanVFX.vfx, particle system won't play."); oceanVFX = null; }
            }

            // update the wave
            UpdateWaves();

            // WILL BE IMPLEMENTED IN FUTURE UPDATES, WATER SHOULD HAVE THE POSSIBILITY TO BE RENDERED IN OPAQUE
            // checking if the water need to be transparent by looking at the minimal transparency
            //if (Ocean.sharedMaterial.GetFloat("_minimalTransparency") == 1) // need to be opaque
            //{
            //    // set rendering order to late opaque alpha test so the depth mask work
            //    Ocean.sharedMaterial.renderQueue = 2490;
            //}
            //else // need to be transparent
            //{
            //    // set rendering order to early transparent so the water shader can read depth buffer
            //    Ocean.sharedMaterial.renderQueue = 2510;
            //}

            #endregion

            // -----------  total light ---------------
            #region
            // getting all the directional light to find the main light
            Light[] lights = GameObject.FindObjectsOfType<Light>();

            if (lights != null)
            {
                // finding the maximal intensity and saving its reference object 
                float maxIntensity = 0f;
                foreach (Light light in lights)
                {
                    if (light.intensity > maxIntensity)
                    {
                        // updating the maximum
                        maxIntensity = light.intensity;

                        // setting the probable main light obejct reference
                        mainLight = light;
                    }
                }
            }

            // updating light data and sending message to every object that need such an update.
            UpdateLighting();

            #endregion

            // ----------- terrain ---------------
            #region
            // checking prescence of a terrain
            if (terrain != null)
            {
                // setting the statics
                Ocean.useTerrain = true;
                Ocean.terrain = terrain;

                // setting the material
                Ocean.sharedMaterial.SetTexture("_terrainHeightmap", Ocean.terrain.terrainData.heightmapTexture);
                Ocean.sharedMaterial.SetVector("_terrainPosition", Ocean.terrain.transform.position);
                Ocean.sharedMaterial.SetVector("_terrainScale", Ocean.terrain.terrainData.size);

                // setting the VFX
                if (oceanVFX != null)
                {
                    oceanVFX.SetTexture("_terrainHeightmap", Ocean.terrain.terrainData.heightmapTexture);
                    oceanVFX.SetVector3("_terrainPosition", Ocean.terrain.transform.position);
                    oceanVFX.SetVector3("_terrainScale", Ocean.terrain.terrainData.size);
                }
            }
            else
            {
                // if no terrain setting the defautl value
                Ocean.useTerrain = false;

                // setting the material with default value of a deep flat sea bed
                Ocean.sharedMaterial.SetTexture("_terrainHeightmap", new RenderTexture(1, 1, 0));
                Ocean.sharedMaterial.SetVector("_terrainPosition", new Vector3(0f, -200f, 0f));
                Ocean.sharedMaterial.SetVector("_terrainScale", new Vector3(1f, 1f, 1f));

                // setting the VFX with default value of a deep flat sea bed
                if (oceanVFX != null)
                {
                    oceanVFX.SetTexture("_terrainHeightmap", new RenderTexture(1, 1, 0));
                    oceanVFX.SetVector3("_terrainPosition", new Vector3(0f, -200f, 0f));
                    oceanVFX.SetVector3("_terrainScale", new Vector3(1f, 1f, 1f));
                }
            }

            #endregion
        }

        private void Update()
        {
            // -------- positioning the mesh --------------
            // folowing the main camera in game mode
            if (Camera.main == null) { Debug.LogError("Please set the tag 'main camera' to a camera in the scene."); }
            Camera camera = Camera.main;

#if UNITY_EDITOR
            // folowing the editor camera in edit mode if it exist, using game camera otherwise
            if (!Application.isPlaying)
            {
                if (SceneView.lastActiveSceneView != null && SceneView.lastActiveSceneView.camera != null)
                {
                    camera = SceneView.lastActiveSceneView.camera;
                }
                else
                {
                    camera = Camera.main;
                }

                // setting constantly the material matrices that can't be serialized
                // THIS COST A BIT IN EDITOR, BUT NO OTHER SOLUTION HAS BEEN FOUND TO SET THE MATRICES AFTER A SCENE SAVE OR REFRESH, WHICH CAUSED THE WATER TO DISAPPEAR
                var meshRenderer = GetComponent<MeshRenderer>();
                if (meshRenderer == null) { Debug.LogError("Please attach a mesh renderer with ocean.mat along with ocean controller."); }
                Ocean.sharedMaterial = meshRenderer.sharedMaterial;
                Ocean.sharedMaterial.SetMatrix("_LIDR", Ocean.LIDR);
                Ocean.sharedMaterial.SetMatrix("_NKVW", Ocean.NKVW);
            }
#endif
            // computing the new position
            Vector3 newPosition = new Vector3(camera.transform.position.x, 0f, camera.transform.position.z);

            // adding some distance in front of the camera, is not excalty the camera focus. This distance is dependant of the scale of the ocean
            newPosition += Vector3.Scale(transform.localScale, oceanObjectDisplacementWithCameraAngle*Vector3.ProjectOnPlane(camera.transform.forward, Vector3.up));

            // rounding to 2 meters avoid jittering in the wave crest
            transform.position = new Vector3(2f*Mathf.Round(newPosition.x*0.5f), 0f, 2f*Mathf.Round(newPosition.z*0.5f));
        }


        /// <summary>
        /// Call this method whenever the waves are modified. This will update the ocean material and the statics fields.
        /// </summary>
        public void UpdateWaves()
        {
            // ----------- statics ----------------
            #region
            Ocean.waterColor = waterColor;

            Ocean.wavelength[0] = wavelength0;
            Ocean.wavelength[1] = wavelength1;
            Ocean.wavelength[2] = wavelength2;
            Ocean.wavelength[3] = wavelength3;

            Ocean.direction[0] = Mathf.Deg2Rad*direction0;
            Ocean.direction[1] = Mathf.Deg2Rad*direction1;
            Ocean.direction[2] = Mathf.Deg2Rad*direction2;
            Ocean.direction[3] = Mathf.Deg2Rad*direction3;

            Ocean.intensity[0] = intensity0*waveIntensity;
            Ocean.intensity[1] = intensity1*waveIntensity;
            Ocean.intensity[2] = intensity2*waveIntensity;
            Ocean.intensity[3] = intensity3*waveIntensity;

            Ocean.randomization[0] = 1f - waveDensity0;
            Ocean.randomization[1] = 1f - waveDensity1;
            Ocean.randomization[2] = 1f - waveDensity2;
            Ocean.randomization[3] = 1f - waveDensity3;

            Ocean.setNumber[0] = setNumber0;
            Ocean.setNumber[1] = setNumber1;
            Ocean.setNumber[2] = setNumber2;
            Ocean.setNumber[3] = setNumber3;

            // define wether there are breakers, some component can be disabled when there are not (VFX, audio)
            Ocean.areBreakers = Mathf.Max(Ocean.intensity[0], Ocean.intensity[1], Ocean.intensity[2], Ocean.intensity[3]) > 1f | Ocean.useTerrain;

            // setting the material wave intensity (which define the slope of the horizon and some other things), first reinitializing the static
            Ocean.surfaceIntensity = 0f;

            // running through each wave system, Ocean intensity should have been initialized by the ripples intensity
            for (int w = 0; w<4; w++)
            {
                // checking the current wave intensity against the saved value to search the maximum 
                if (Ocean.intensity[w] > Ocean.surfaceIntensity)
                {
                    // setting the ocean intensity with this wave intensity that could be the one with the maximal intensity
                    Ocean.surfaceIntensity = Mathf.Min(1f, Ocean.intensity[w]);
                }
            }

            // tweaks
            Ocean.breakSpeedFactor = breakersExtraSpeedFactor;
            Ocean.breakTorqueFactor = breakersTorqueFactor;

            // baking the missing value and packing the data into matrices 4x4
            Ocean.BakeAndPackWaveData();
            #endregion

            // ------------ object's component -----------
            #region
            // ---------- material
            //uploading the wave data to the material }
            Ocean.sharedMaterial.SetMatrix("_LIDR", Ocean.LIDR);
            Ocean.sharedMaterial.SetMatrix("_NKVW", Ocean.NKVW);

            // setting the material color
            Ocean.sharedMaterial.SetColor("_waterColor", waterColor);

            // setting the wave intensity for the horizon slope
            Ocean.sharedMaterial.SetFloat("_oceanIntensity", Ocean.surfaceIntensity);

            // ---------- VFX
            // uploading the wave data to the VFX
            if (oceanVFX != null)
            {
                oceanVFX.SetMatrix4x4("_LIDR", Ocean.LIDR);
                oceanVFX.SetMatrix4x4("_NKVW", Ocean.NKVW);
                oceanVFX.SetVector4("_waterColor", Ocean.waterColor);
            }
            #endregion
        }


        /// <summary>
        /// Call this method whenever the wind is modified. This will update every wind-dependent object in the scene.
        /// </summary>
        public void UpdateWind()
        {
            // ---------- statics ------------
            #region
            // if there is wind controller in the scene its values can be taken
            if (windController != null)
            {
                // call the method that fill the static data according to the public variable
                windController.UpdateWindProperties();
            }
            // else default wind properties are to be taken
            else
            {
                Ocean.Wind.speed = 5f; // 18km/h of wind
                Ocean.Wind.inverseHeight = 1f/5f; // at 5m height
                Ocean.Wind.direction = 0f; // in opposite red axis direction
                Ocean.Wind.cosDirection = -1f;
                Ocean.Wind.sinDirection = 0f;
            }
            #endregion

            // ---------- object's component -------------
            #region
            // ----------- material
            // setting the ripples direction function of the wind direction
            Ocean.sharedMaterial.SetFloat("_ripplesDirection", Ocean.Wind.direction);

            // setting the ripples intensity function of the wind strenght, is maximal as soon the wind reachs 36km/h
            float ripplesIntensity = Mathf.Min(1f, Ocean.Wind.speed*0.1f);
            Ocean.sharedMaterial.SetFloat("_ripplesIntensity", ripplesIntensity);

            // saving the ripple intensity so when the wave are updated we now which has the maximal intensity
            //Ocean.surfaceIntensity = Mathf.Max(Ocean.surfaceIntensity, ripplesIntensity);

            // setting the surfacesmoothness function of the wind, is minimal (0.7) as soon the wind reachs 72km/h, is 1 when no wind at all.
            Ocean.sharedMaterial.SetFloat("_smoothness", Mathf.Clamp(1.1f - 0.015f*Ocean.Wind.speed, 0.7f, 1f));

            // ------- VFX
            // setting the wind
            if (oceanVFX != null)
            {
                oceanVFX.SetVector3("_wind", new Vector3(Ocean.Wind.speed*Ocean.Wind.cosDirection, Ocean.Wind.inverseHeight, Ocean.Wind.speed*Ocean.Wind.sinDirection));
            }
            #endregion
        }


        /// <summary>
        /// Call this method whenever the lighting is modified. This will update every light-dependent object in the scene.
        /// </summary>
        public void UpdateLighting()
        {
            // ---------- statics ------------
            #region
            // ambient light initialized to fallback value
            Color ambientColor = Color.gray;

            // checking the render ambient mode, since getting the color from the skybox doesn't seem to work, it is required to use color or gradient mode
            if (SceneManager.GetActiveScene().name != "" && RenderSettings.ambientMode == UnityEngine.Rendering.AmbientMode.Skybox)
            {
                Debug.LogWarning("Storm Breakers does not support skybox ambient mode. Please set ambient mode to either gradient or color in Lighting window/Environment/Environment Lighting. Default ambient color is set to mid gray.");
            }
            else // ambient color is set to the sky color
            {
                ambientColor = RenderSettings.ambientLight;
            }

            // directional light initialized to fallback value
            Color mainLightColor = Color.black;

            if (mainLight != null)
            {
                mainLightColor = mainLight.color*mainLight.intensity;
            }

            // making the sum of ambient and directional light (approx), can be above pure white
            Color totalLight = RenderSettings.ambientIntensity*ambientColor + mainLightColor*0.5f;

            // unsaturating 
            //totalLight = particleLightingFactor * new Color(Mathf.Atan(totalLight.r), Mathf.Atan(totalLight.g), Mathf.Atan(totalLight.b));
            totalLight = particleLightingFactor * new Color(Mathf.Min(1f, totalLight.r), Mathf.Min(1f, totalLight.g), Mathf.Min(1f, totalLight.b));

            // setting the global total color so every particle system can have easily access to it
            Ocean.totalLight = totalLight;
            #endregion

            // ---------- object's component -------------
            #region
            // setting the light color to the material
            Ocean.sharedMaterial.SetColor("_totalLigthColor", Ocean.totalLight);

            // setting the VFX light color
            if (oceanVFX != null)
            {
                oceanVFX.SetVector4("_totalLigthColor", Ocean.totalLight);
            }
            #endregion
        }

        // editor script
#if UNITY_EDITOR
        private void OnValidate()
        {
            // trick to refresh scene because there is a bug with material serialization 
            clickHereToRefresh = false;

            // checking the ordering of the wave length
            //if (wavelength1 > wavelength0) { wavelength1 = wavelength0; }
            //if (wavelength2 > wavelength1) { wavelength2 = wavelength1; }
            //if (wavelength3 > wavelength2) { wavelength3 = wavelength2; }

            // computing the average wave height
            float waveHeight0 = wavelength0*Mathf.Min(1f, intensity0*waveIntensity)*(1f - 0.5f*waveDensity0)*0.17f;

            // rounding to the decimeter
            waveHeight0 = 0.1f*Mathf.Round(waveHeight0*10f);

            // indicating the wave height in m and the period in seconds
            swellHeightRef = waveHeight0 + "m";
            swellPeriodRef = Mathf.Round(Mathf.Sqrt(2f*Mathf.PI*wavelength0/9.81f)) + "s";

            // initializing everything
            OnEnable();
        }
#endif
    }

}