using UnityEngine;
using UnityEngine.SceneManagement;

namespace StormBreakers
{
    public class GameControl : MonoBehaviour
    {


        void Update()
        {
            if (Input.GetKeyDown(KeyCode.Backspace) || Input.GetKeyDown(KeyCode.Return))
            {
                SceneManager.LoadScene(SceneManager.GetActiveScene().name);
            }

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                Application.Quit();
            }
        }
    }
}