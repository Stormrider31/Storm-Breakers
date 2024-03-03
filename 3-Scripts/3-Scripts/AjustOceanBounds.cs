using UnityEngine;

namespace StormBreakers
{
    public class AjustOceanBounds : MonoBehaviour
    {
        // This component ajusts the bounds of a ocean mesh to avoid bad frustrum culling

        void OnEnable() // called first
        {
            // getting the mesh of the object
            Mesh mesh = GetComponent<MeshFilter>().mesh;
            if (mesh == null) { return; }

            // calculating the horizontal and vertical extending value, there are the sum of the maximal horizontal deformation of each waves systems
            float horizontalExtending = 0f;
            float verticalExtending = 0f;
            for (int w = 0; w<4; w++)
            {
                horizontalExtending +=Ocean.wavelength[w]*0.15f*1.5f;
                verticalExtending += Ocean.wavelength[w]*0.085f*1.5f;
            }

            // calculating the new bounding volume
            Bounds newBounds = new Bounds(mesh.bounds.center, mesh.bounds.size + 0.5f*(new Vector3(horizontalExtending, verticalExtending, horizontalExtending)));

            // assigning the new bounds
            mesh.bounds = newBounds;
        }
    }
}
