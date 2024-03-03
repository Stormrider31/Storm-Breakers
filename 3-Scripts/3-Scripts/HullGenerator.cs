using UnityEngine;

namespace StormBreakers
{
    public class HullGenerator : MonoBehaviour // CHANGE CLASS TYPE
    {
        // This component generate procedurally boat hull mesh for the use of simulation. Use it in case the model of boat or ship doesn't contain a suitable mesh.

        [Tooltip("Defines whether the mesh collider (if any) will be overtwritten with the generated mesh.")]
        public bool useThisAsMeshCollider = true;

        [Space(10)]
        [Tooltip("The local position of the bow.")]
        public Vector3 bowPosition = Vector3.zero;

        [Space(10)]
        [Tooltip("The length of the hull in meter. Factors the raking curves.")]
        public float length = 10f;
        [Tooltip("Defines the factored bow position along the factored height. Top of the hull is at 0, bottom at 1")]
        public AnimationCurve bowRaking = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0.75f));
        [Tooltip("Defines the factored aft position along the factored height. Top of the hull is at 0, bottom at 1. Must be negative value.")]
        public AnimationCurve sternRaking = new AnimationCurve(new Keyframe(0f, -1f), new Keyframe(1f, -1f));

        [Space(10)]
        [Tooltip("The height of the hull in meters. Factor the depth curve.")]
        public float depth = 4f;
        [Tooltip("Defines the factored height of the hull along its factored length. Aft is at -1, bow at +1 ")]
        public AnimationCurve depthCurve = new AnimationCurve(new Keyframe(-1f, -0.5f), new Keyframe(0f, -1f), new Keyframe(1f, -0.75f));

        [Space(10)]
        [Tooltip("The width of the hull in meter. Factor the cross section curves")]
        public float beam = 4f;
        [Tooltip("Defines the factored width near the bow along the factored heigth. Top of the hull is at 0, bottom at 1.")]
        public AnimationCurve bowCrossSection = new AnimationCurve(new Keyframe(0f, 0.5f), new Keyframe(1f, 0f));
        [Tooltip("Defines the factored width at the center of the hull along the factored heigth. Top of the hull is at 0, bottom at 1.")]
        public AnimationCurve centerCrossSection = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(0.75f, 1f), new Keyframe(1f, 0f));
        [Tooltip("Defines the factored width at the aft of the hull along the factored heigth. Top of the hull is at 0, bottom at 1.")]
        public AnimationCurve aftCrossSection = new AnimationCurve(new Keyframe(0f, 0.75f), new Keyframe(1f, 0f));

        [Space(10)]
        [Tooltip("Defines how many divisions there are along the length. The more there are, the more expansive the simulation will be but also the more precise. Keep divisions as square as possible.")]
        [Range(5, 50)] public int horizontalNumberOfDivision = 20;
        [Tooltip("Defines how many divisions there are along the height. The more there are, the more expansive the simulation will be but also the more precise. Keep divisions as square as possible.")]
        [Range(2, 20)] public int verticalNumberOfDivision = 8;

        [Space(10)]
        [Tooltip("Defines how much the helper (when the corresponding material is set) glows when close to the geometry.")]
        [Range(0f, 0.1f)] public float helperHighligthSensitivity = 0.01f;

        // internal data
        private int nv; // total number of division

        private void Start()
        {
            // generate the hull ??
            //GenerateHull();

            // getting the render component
            MeshRenderer meshRenderer = GetComponent<MeshRenderer>();

            // checking if the hull builder helper is attached
            if (meshRenderer.material != null && meshRenderer.material.HasFloat("_nx") && meshRenderer.material.HasFloat("_ny") && meshRenderer.material.HasFloat("_maxVisibleDistance"))
            {
                // in that case we don't want it rendered
                meshRenderer.enabled = false;
            }
            // if it 's another material it's likely we want to keep it render

        }
        public Mesh GenerateHull() // generate a mesh according to the inputs
        {
            // creating a mesh
            Mesh mesh = new Mesh();

            // calculating the number of vertices on a lateral face, value used many times
            nv = (horizontalNumberOfDivision+1)*(verticalNumberOfDivision+1);

            // vertices temporary constructions
            {
                // contains 2 face of nv vertices, plus the center line of nx vertices
                Vector3[] V = new Vector3[2*nv + horizontalNumberOfDivision+1];

                // longitudinal looping
                for (int X = 0; X <= horizontalNumberOfDivision; X++)
                {
                    // vertical looping
                    for (int Y = 0; Y <= verticalNumberOfDivision; Y++)
                    {
                        //calculating the indice
                        int i = X + (horizontalNumberOfDivision+1)*Y;

                        // calculating x position
                        float x = (float)X/(float)horizontalNumberOfDivision*length- length/2f;
                        //calculating y position
                        float y = -(float)Y/(float)verticalNumberOfDivision*depth;

                        // setting the vertice in the starbord face
                        V[i] = new Vector3(x, y, -depth*0.5f);
                        // setting the vertice in the portside face
                        V[i + nv] = new Vector3(x, y, depth*0.5f);
                    }

                    // center line of the decking
                    {
                        // calculating the indice
                        int i = 2*nv + X;

                        // calculating x position
                        float x = (float)X/(float)horizontalNumberOfDivision*length- length/2f;
                        //calculating y position
                        float y = 0f;

                        // setting the vertice
                        V[i] = new Vector3(x, y, 0f);
                    }
                }
                //saving vertices to the mesh
                mesh.vertices = V;
            }
            // triangles constructions
            {
                // contains 2 faces of nx*ny square that each contains 2 triangles that each contains 3 points
                // plus the decking that contains 2.nx squares with 2*3 points each
                // minus the 2 triangles (6 points) of the deck bow that would be 0 area
                // plus the transom that contains 2.ny triangle of 3 points each 
                int[] T = new int[2*2*3*horizontalNumberOfDivision*verticalNumberOfDivision + 2*horizontalNumberOfDivision*2*3 - 6 + 2*verticalNumberOfDivision*3];

                // the index k in the array will be incremented blindly
                int k = 0;

                // looping trough the side vertices, first horizontally
                for (int X = 0; X < horizontalNumberOfDivision; X++)
                {
                    // then vertical looping
                    for (int Y = 0; Y < verticalNumberOfDivision; Y++)
                    {
                        // calculating the indice of the botom left corner
                        int i = X + (horizontalNumberOfDivision+1)*Y;

                        //first starbord triangle
                        T[k++] = i; T[k++] = i+1; T[k++] = i+1 + horizontalNumberOfDivision+1;
                        //second starbord triangle
                        T[k++] = i; T[k++] = i+1 + horizontalNumberOfDivision+1; T[k++] = i   + horizontalNumberOfDivision+1;

                        //first portside triangle
                        T[k++] = i +nv; T[k++] = i +nv +1 + horizontalNumberOfDivision+1; T[k++] = i +nv + 1;
                        //second portside triangle
                        T[k++] = i +nv; T[k++] = i +nv    + horizontalNumberOfDivision+1; T[k++] = i +nv + 1 + horizontalNumberOfDivision+1;
                    }

                    // building the deck
                    {
                        // setting the deck starbord triangles
                        // removing the bow triangle that would be 0 area
                        if (X != horizontalNumberOfDivision -1)
                        {
                            T[k++] = 2*nv + X; T[k++] = 2*nv + X + 1; T[k++] = X + 1;
                        }
                        T[k++] = 2*nv + X; T[k++] =        X + 1; T[k++] = X;
                        // setting the deck portside triangles
                        // removing the bow traingle that would be 0 area
                        if (X != horizontalNumberOfDivision -1)
                        {
                            T[k++] = 2*nv + X; T[k++] = X + nv + 1; T[k++] = 2*nv + X + 1;
                        }
                        T[k++] = 2*nv + X; T[k++] = X + nv; T[k++] = X + nv + 1;
                    }
                }

                // building the transom
                for (int Y = 0; Y < verticalNumberOfDivision; Y++)
                {
                    // setting the transom starbord triangle
                    T[k++] = 2*nv; T[k++] =  Y*(horizontalNumberOfDivision+1); T[k++] = (Y+1)*(horizontalNumberOfDivision+1);
                    // setting the transom portside triangle
                    T[k++] = 2*nv; T[k++] = nv+ (Y+1)*(horizontalNumberOfDivision+1); T[k++] = nv+ Y*(horizontalNumberOfDivision+1);
                }

                // saving triangles to mesh
                mesh.triangles = T;
            }
            // uv constructions
            {
                // the second UV are used to shape the hull according the the input curves
                Vector2[] UV2 = new Vector2[mesh.vertices.Length];
                // the third UV are used to differentiate the portside (U3=1f) to starbord (U3=-1f) and the center line (U3=0)
                Vector2[] UV3 = new Vector2[mesh.vertices.Length];

                // looping trough the side vertices, first horizontally
                for (int X = 0; X <= horizontalNumberOfDivision; X++)
                {
                    // then vertically
                    for (int Y = 0; Y <= verticalNumberOfDivision; Y++)
                    {
                        //calculating the indice of the current vertex with the same formula already used
                        int i = X + (horizontalNumberOfDivision+1)*Y;

                        // calculating u2 (is -1 at the aft, 0 in the center, and 1 at the bow
                        float u2 = (float)X/horizontalNumberOfDivision*2 - 1;
                        // calculating v2 (is 0 on the deck, 1 on the keel)
                        float v2 = (float)Y/verticalNumberOfDivision;

                        // setting the uv to starbord
                        UV2[i] = new Vector2(u2, v2);
                        UV3[i] = new Vector2(-1f, 1f);
                        // setting the uv to portside
                        UV2[i+nv] = new Vector2(u2, v2);
                        UV3[i+nv] = new Vector2(1f, 1f);
                    }
                    // setting the uv to the center line
                    {
                        // calculating the indice
                        int i = 2*nv + X;

                        // calculating U2 position
                        float u2 = (float)X/horizontalNumberOfDivision*2 - 1;

                        // setting the uvs
                        UV2[i] =  new Vector2(u2, 0f);
                        UV3[i] =  new Vector2(0f, 1f);
                    }
                }
                //saving uv
                mesh.uv = UV2;
                mesh.uv2 = UV3;
            }
            // shaping the hull
            {
                // copying the vertices and uvs (USEFULL ?)
                var V = mesh.vertices;
                var UV2 = mesh.uv;
                var UV3 = mesh.uv2;

                // looping through all the vertices
                for (int i = 0; i < mesh.vertices.Length; i++)
                {
                    // easing the writing
                    float u = UV2[i].x;
                    float v = UV2[i].y;
                    float u2 = UV3[i].x;

                    // calculating Y with the depth curve (vertical)
                    float Y = depth*v*depthCurve.Evaluate(u);

                    //calculating X with the racking curves (longitudinal)
                    float X;
                    if (u > 0f)
                    {
                        // then the current vertices is on the forward part of the hull
                        X = length*0.5f*bowRaking.Evaluate(v)*u;
                    }
                    else
                    {
                        // else the current vertices is on the aft part of the hull
                        X = -length*0.5f*sternRaking.Evaluate(v)*u;
                    }


                    // evaluating the 3 cross section at v
                    float Zbow = bowCrossSection.Evaluate(v); //at u=0.75
                    float Zcenter = centerCrossSection.Evaluate(v); // at u=0
                    float Zaft = aftCrossSection.Evaluate(v); //at u=-1

                    // creating an animation curve over u with the 3 cross section and with the sharp bow
                    AnimationCurve beamCurve = new AnimationCurve(new Keyframe(-1f, Zaft), new Keyframe(0f, Zcenter), new Keyframe(0.75f, Zbow), new Keyframe(1f, 0f));
                    beamCurve.SmoothTangents(1, 0f); beamCurve.SmoothTangents(2, 0f);

                    // calculating Z (width) by making an interporaltion of the 3 cross section
                    float Z = u2*0.5f*beam*beamCurve.Evaluate(u);

                    //nesting the resuts and transforming
                    V[i] = bowPosition - Vector3.right*0.5f*length + new Vector3(X, Y, Z);
                }

                //saving vertices to the mesh
                mesh.vertices = V;

                // recalculate the normals
                mesh.RecalculateNormals();
            }

            // returning the mesh
            return mesh;

        }

        // editor scripts
#if UNITY_EDITOR
        private void OnValidate() => UnityEditor.EditorApplication.delayCall += OnValidateDebuged;
        private void OnValidateDebuged()
        {
            // getting the mesh filter component
            if (this != null)
            {
                MeshFilter meshFilter = GetComponent<MeshFilter>();

                // if there is actually a mesh filter attached, then it is updated
                if (meshFilter != null)
                {
                    // generating the hull when a modification is done
                    meshFilter.sharedMesh =  GenerateHull();

                    // getting the render component
                    MeshRenderer meshRenderer = GetComponent<MeshRenderer>();

                    // if there is actually a mesh renderer then it is updated
                    if (meshRenderer != null)
                    {
                        // getting the material
                        Material material = meshRenderer.sharedMaterial;

                        // if the material is the hull builder helper then it is updated
                        if (material != null && material.HasFloat("_nx") && material.HasFloat("_ny") && material.HasFloat("_maxVisibleDistance"))
                        {
                            // setting the properties
                            material.SetFloat("_nx", horizontalNumberOfDivision);
                            material.SetFloat("_ny", verticalNumberOfDivision);
                            material.SetFloat("_maxVisibleDistance", length*helperHighligthSensitivity);
                        }
                    }
                }
            }
        }
#endif



    }
}