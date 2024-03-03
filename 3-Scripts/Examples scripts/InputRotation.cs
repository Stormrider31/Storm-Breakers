using UnityEngine;

namespace StormBreakers
{
    public class InputRotation : MonoBehaviour
    {
        // this simple component can be used to move command or rudder with the inputs.
        // currently work with the legacy input system

        public string axisName = "Horizontal";
        public Vector3 maxLocalAngle = Vector3.zero;
        public Vector3 rotationSpeed = Vector3.zero;
        private Vector3 initialLocalEuler = Vector3.zero;

        // Start is called before the first frame update
        void Start()
        {
            // saving the initial euler angle
            initialLocalEuler = transform.localEulerAngles;
        }

        // Update is called once per frame
        void Update()
        {
            // adding the speed
            initialLocalEuler += rotationSpeed*Time.deltaTime*Input.GetAxis(axisName);

            // changing the euler angle according to the input
            transform.localEulerAngles = initialLocalEuler + maxLocalAngle*Input.GetAxis(axisName);
        }
    }
}
