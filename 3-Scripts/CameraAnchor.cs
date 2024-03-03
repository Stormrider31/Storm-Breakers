using UnityEngine;

namespace StormBreakers
{
    public class CameraAnchor : MonoBehaviour
    {
        // This component make the game object it is attached follow its parent but keeping a steady rotation
        // Is usefull to make a camera anchor on a boat

        [HideInInspector] public GameObject objectFollowed; // the parent of this game object in the editor
        [HideInInspector] public Vector3 initialPosition; // the local initial position that is to be saved


        void Start()
        {
            // saving the inital position
            initialPosition = transform.localPosition;

            // getting the parent
            objectFollowed = transform.parent.gameObject;

            // checking the parent, disabling in case there is not
            if (objectFollowed == null) { Debug.LogWarning("Please make this game object parent of another game object."); this.enabled = false; return; }

            // unparenting to avoid the rotation to be fully transfered
            transform.parent = null;
        }

        void FixedUpdate()
        {
            // aligning the position of the object folowed
            transform.position = objectFollowed.transform.position + objectFollowed.transform.TransformDirection(initialPosition);
        }
    }
}