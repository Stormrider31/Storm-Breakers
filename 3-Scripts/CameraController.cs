using UnityEngine;

namespace StormBreakers
{
    public class CameraController : MonoBehaviour
    {
        // This component control the position of the camera and the audio listener effects
        // It include a simple way to switch from first person to third person by scrolling the mouse wheel
        // It also contains a perspective tweak that makes the boat look from very far when putting the camera further away, making a view looking like the one in the real footage at sea.
        // It aslo contains a zoom feature in first person
        // It contains filtering to make underwater and inside audio effect 

        [Header("View properties")]
        [Tooltip("The distance at which the camera start from the center of rotation.")]
        public float distance = 10f;
        [Tooltip("The sensitivity of the mouse view.")]
        [Range(0f, 5f)] public float mouseOrbitSensitivity = 1f;
        [Tooltip("The sensitivity of the mouse zoom.")]
        [Range(0f, 50f)] public float mouseZoomSensitivity = 10f;
        [Tooltip("The vertical field of view of the camera when in first person.")]
        [Range(40f, 80f)] public float firstPersonFOV = 60f;
        [Tooltip("Makes the field of view variyng with its distance from the center of rotation. Set 0 if you want no field of view variation.")]
        [Range(0f, 2.5f)] public float distanceFOVFactor = 0.5f;
        [Tooltip("The minimal field of view when it varies with the distance.")]
        [Range(0f, 100f)] public float cameraMinFov = 20f;
        [Tooltip("When the camera is in first person, continuing scrollign will make a zoom. Set to 0 if you don't want a zoom effect.")]
        [Range(0f, 10f)] public float zoomFactor = 2f;

        [Space(10)]
        [Header("Audio properties")]
        [Tooltip("The distance of the camera to the center of rotation below which the inside atenuation audio effect is activate. Set negative value for no inside effect.")]
        public float insideDistance = 1f;
        [Tooltip("The filter factor for the inside audio effect. The lower the value the more attenuated the audio will be.")]
        [Range(0f, 0.5f)] public float insideAttenuation = 0.1f;
        [Tooltip("The filter factor for the underwater audio effect. The lower the value the more attenuated the audio will be. Set 1 for no underwater attenuation audio effect.")]
        [Range(0f, 0.05f)] public float underWaterAttenuation = 0.01f;

        // internal view data
        private Vector3 inputEuler; // the euler angle of the total camera rotation

        // internal audio data
        private float alpha = 1f; // the audio low pass filter factor
        private float lastFilteredData0 = 0f; // used to save the previous value in chanel 0
        private float lastFilteredData1 = 0f; // used to save the previous value in chanel 1
        private DetectWater detectWater;

        private void Start()
        {
            // getting the component that says when inside water
            detectWater = GetComponent<DetectWater>();

            // checking the detect water component
            if (detectWater == null) { detectWater = this.gameObject.AddComponent<DetectWater>(); }

            // checking the presence of a parent to be rotated, diabling if not
            if (transform.parent == null) { Debug.LogWarning("Please make the camera parent of an object that act as a pivot."); this.enabled=false; }

            // getting the initial rotation
            inputEuler = transform.parent.eulerAngles;
        }

        void Update()
        {
            // ------------ view control -------------

            // updating the euler with the mouse input
            inputEuler += new Vector3(0f, mouseOrbitSensitivity*Input.GetAxis("Mouse X"), -mouseOrbitSensitivity*Input.GetAxis("Mouse Y"));

            // setting the rotation of the pivot
            transform.parent.transform.localEulerAngles =  inputEuler;

            // distance management with mouse
            distance -= Input.GetAxis("Mouse ScrollWheel")*mouseZoomSensitivity;

            // when insisting in scrollign up, the camera make a zoom
            if (distance < -0.5f*mouseZoomSensitivity)
            {
                Camera.main.fieldOfView = firstPersonFOV + (distance + 0.5f*mouseZoomSensitivity)*zoomFactor;
            }
            // when far away it also make kind of a zoom to emphasise the size of the things
            else
            {
                // setting the camera FOV
                Camera.main.fieldOfView = firstPersonFOV - distance*distanceFOVFactor;
                if (Camera.main.fieldOfView < cameraMinFov) { Camera.main.fieldOfView = cameraMinFov; }
            }

            // setting the position
            transform.localPosition = Vector3.right*(distance<0f ? 0f : distance);

            // ------------- audio control ------------------

            // enabling the audio pass filter when inside and underwater
            if (distance < insideDistance)
            {
                if (detectWater.IsUnderwater) { alpha = underWaterAttenuation; }
                else { alpha = insideAttenuation; }
            }
            else
            {
                if (detectWater.IsUnderwater) { alpha = underWaterAttenuation; }
                else { alpha = 1f; }
            }


        }
        void OnAudioFilterRead(float[] data, int channels)
        {
            // -------- audio filtering --------

            // looping through each sample group
            for (int s = 0; s < data.Length; s += channels)
            {
                // looping through sample of each channel in sample group
                for (int c = 0; c < channels; c++)
                {
                    if (c == 0) // chanel 0
                    {
                        // low pass filter
                        float filteredData = alpha*data[s+c] + (1f-alpha)*lastFilteredData0;

                        // saving the data for the next loop
                        lastFilteredData0 = filteredData;

                        // applying the filter to the audio stream
                        data[s+c] = filteredData;
                    }
                    else // chanel 1
                    {
                        // low pass filter
                        float filteredData = alpha*data[s+c] + (1f-alpha)*lastFilteredData1;

                        // saving the data for the next loop
                        lastFilteredData1 = filteredData;

                        // applying the filter to the audio stream
                        data[s+c] = filteredData;
                    }
                }
            }
        }

    }
}