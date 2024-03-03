using UnityEngine;

namespace StormBreakers
{
    public class InitialSpeed : MonoBehaviour
    {
        // set the initial speed of the rigidbody as per the propertie in the red axis.

        public float initialSpeed;

        void Start()
        {
            GetComponent<Rigidbody>().velocity = transform.right*initialSpeed;
        }
    }
}
