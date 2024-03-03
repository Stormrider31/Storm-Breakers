using UnityEngine;

namespace StormBreakers
{
    public class UnderwaterEffect : MonoBehaviour
    {
        // this component active the underwater effect by enabling the renderer of the quad that is in front of the camera

        [Tooltip("The depth of the camera in the water at which the underwater activate. When negative the underwater activate before the camera is fully inside water.")]
        [Range(-0.2f, 0.2f)] public float depthTrigger = -0.1f;

        // internal data
        private MeshRenderer meshRenderer;
        private DetectWater detectWater;
        private int underwaterID; // used to set ocean material
        private bool previousUnderwater; // used to avoid setting material every frame



        void Start()
        {
            // getting the component that detect wether the camera is underwater or not
            if (Camera.main == null) { Debug.LogError("Please set the tag 'main camera' to a camera in the scene."); }
            detectWater = Camera.main.GetComponent<DetectWater>();

            // checking the detect water component
            if (detectWater == null) { detectWater = Camera.main.gameObject.AddComponent<DetectWater>(); }

            // getting the mesh renderer
            meshRenderer = GetComponent<MeshRenderer>();

            // checking the component, disabling this if nothing is found
            if (meshRenderer == null) { Debug.Log("Please attach a quad renderer with underwater.mat material to this game object."); this.enabled = false; return; }

            // setting the water color
            if (meshRenderer.material.HasColor("_color"))
            {
                meshRenderer.material.SetColor("_color", Ocean.waterColor*Ocean.totalLight);
            }
            else
            {
                Debug.Log("Please set underwater.mat material to this game object."); this.enabled = false; return;
            }

            // getting the ID of the ocean material underwater bool
            underwaterID = Shader.PropertyToID("_underwater");

            // setting the initial underwater
            previousUnderwater = false;
        }

        void Update()
        {
            // checking wether underwater   
            bool underwater = detectWater.Depth > depthTrigger;

            // acting only when there is a change
            if (underwater != previousUnderwater)
            {
                // setting the saved underwater 
                previousUnderwater = underwater;

                // setting the renderer active when the camera is underwater
                meshRenderer.enabled = underwater;

                // setting the ocean material
                if (underwater)
                {
                    // set underwater to true
                    Ocean.sharedMaterial.SetFloat(underwaterID, 1f);

                    // set rendering order to late opaque alpha test so the underwater shader can read water screen depth
                    Ocean.sharedMaterial.renderQueue = 2490;
                }
                else
                {
                    // set underwater to false
                    Ocean.sharedMaterial.SetFloat(underwaterID, 0f);

                    // set rendering order to early transparent so the water shader can read depth buffer
                    Ocean.sharedMaterial.renderQueue = 2510;
                }
            }
        }

        private void OnDisable()
        {
            // reset the ocean material as outside water
            // set underwater to false
            Ocean.sharedMaterial.SetFloat(underwaterID, 0f);

            // set rendering order to early transparent so the water shader can read depth buffer
            Ocean.sharedMaterial.renderQueue = 2510;
        }
    }
}