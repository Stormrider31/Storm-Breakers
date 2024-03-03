using UnityEngine.UI;
using UnityEngine;

namespace StormBreakers
{
    public class MassIncrease : MonoBehaviour
    {
        // this component increase the mass of an object at at constant rate

        [Tooltip("The mass at target time.")]
        public float targetMass;
        public float maxMass;
        [Tooltip("The time when the target mass is reached.")]
        public float targetTime;

        public BoatController controlerToDeactivate;
        public float timeToDeactivateController;
        private bool controllerIsDeactivated = false;

        private Rigidbody rb;

        // Start is called before the first frame update
        void Start()
        {
            // getting the rigidbody we want the weight to be increased
            rb = GetComponent<Rigidbody>();
        }

        // Update is called once per frame
        void Update()
        {

            float t = Time.timeSinceLevelLoad;
            rb.mass = Mathf.Min(maxMass, targetMass*t*t/(targetTime*targetTime));

            // disabling the controler when the time is out
            if (!controllerIsDeactivated && t > timeToDeactivateController)
            {
                controlerToDeactivate.enabled = false;
                controllerIsDeactivated = true;
            }

        }

        private void OnDisable()
        {
            controlerToDeactivate.enabled = true;
            controllerIsDeactivated = false;
        }
    }
}