using UnityEngine;
using UnityEngine.UI;

namespace StormBreakers
{
    public class IndicateSpeed : MonoBehaviour
    {
        public Rigidbody rb;
        public Text text;


        // Update is called once per frame
        void Update()
        {
            float speed = Mathf.Round(rb.velocity.magnitude*3.6f);
            text.text = "SPEED = " + speed + "KM/H";
            text.color = Color.Lerp(Color.black, Color.red, Mathf.Clamp01(speed*0.02f-1f));
        }
    }
}
