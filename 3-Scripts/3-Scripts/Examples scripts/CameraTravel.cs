using UnityEngine;

namespace StormBreakers
{
    public class CameraTravel : MonoBehaviour
    {
        public BoatController boatControllerToAutomate;
        public string toogleAutomaticPilotKey = "q";
        public float speed = 1f;
        private CameraAnchor cameraAnchor;


        // Start is called before the first frame update
        void Start()
        {
            // getting the camera anchor attacehd to this gameobject
            cameraAnchor = GetComponent<CameraAnchor>();
        }

        // Update is called once per frame
        void Update()
        {
            // swithching on or off the autoamtic pilot
            if (Input.GetKeyDown(toogleAutomaticPilotKey))
            {
                // swithcing the automatic pilot bool
                boatControllerToAutomate.automaticPilot = !boatControllerToAutomate.automaticPilot;

                // reseting the current course
                boatControllerToAutomate.SetCurrentCourse();
            }

            // walking on the deck only when the automatic pilot is on
            if (boatControllerToAutomate.automaticPilot)
            {
                // transforming the camera vector in local space
                Vector3 forward = new Vector3(
                    Vector3.Dot(cameraAnchor.objectFollowed.transform.right, Camera.main.transform.forward),
                    Vector3.Dot(cameraAnchor.objectFollowed.transform.up, Camera.main.transform.forward),
                    Vector3.Dot(cameraAnchor.objectFollowed.transform.forward, Camera.main.transform.forward));

                Vector3 right = new Vector3(
                    Vector3.Dot(cameraAnchor.objectFollowed.transform.right, Camera.main.transform.right),
                    Vector3.Dot(cameraAnchor.objectFollowed.transform.up, Camera.main.transform.right),
                    Vector3.Dot(cameraAnchor.objectFollowed.transform.forward, Camera.main.transform.right));

                // moving the camera anchor in local horizontal space
                cameraAnchor.initialPosition += Input.GetAxis("Vertical")*forward*speed*Time.deltaTime;
                cameraAnchor.initialPosition += Input.GetAxis("Horizontal")*right*speed*Time.deltaTime;

            }
        }
    }
}