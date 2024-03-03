using UnityEngine;
using UnityEditor;

namespace StormBreakers
{
    [ExecuteInEditMode]
    public class ReflectionProbeController : MonoBehaviour
    {
        // Attach this component to the reflection probe so it render the correct reflection

        [Tooltip("How much the reflection probes goes far from the camera function of the water surface roughness.")]
        public float mirroringTweak = 1f;

        void Update()
        {
            // folowing the main camera in game mode
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
            }
#endif
            // getting the camera position
            Vector3 camPosition = camera.transform.position;

            // setting the reflection probe at the mirror position of the camera relative to the medium plane of the water with some tweaks
            transform.position = new Vector3(camPosition.x, -(1f + mirroringTweak*Ocean.surfaceIntensity)*camPosition.y, camPosition.z);
        }
    }
}
