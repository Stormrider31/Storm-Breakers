using UnityEngine;

namespace StormBreakers
{
    public class BreakersAudio : MonoBehaviour
    {
        // This component create the procedural audio of the waves breaking

        [Tooltip("The overall volume of the audio. When too loud, audio saturation might happen.")]
        [Range(0f, 0.5f)] public float volumeFactor = 0.1f;
        [Tooltip("Defines how high pitched the audio of the wave is. Low value make low audio that looks like bigger wave. Volume factor might need to be increased in case this value decrease, and conversevely.")]
        [Range(0f, 0.1f)] public float pitchFactor = 0.02f;
        [Tooltip("Defines how the sound of the wave fade off with the distance from the camera (volume and pitch). The higer the value, the further we can hear the waves.")]
        [Range(0f, 25f)] public float fadeOffFactor = 5f;

        // internal emitter arrays
        // each array is indexed in wave system w and emitter number e
        private float[,] losangei; // the losange grid i coordinate of the losange that contains the group
        private float[,] losangej; // the losange grid j coordinate of the losange that contains the group
        private float[,] groupi; // the precise losange grid i coordinate of the group
        private float[,] groupj; // the precise losange grid j coordinate of the group
        private float[,] amplitude; // the amplitude at the middle of the group, is updated only when ij are modified
        private float[,] unphasing; // the unphasing of the group
        private float[,] amplitudeUnphasing; // the unphasing caused by high amplitude
                                             // internal audio parameters pre-initialized because audio starts before Awake()
        private float[,] volume = new float[4, 9];
        private float[,] fadeOff = new float[4, 9];
        private float[,] leftChannelIntensity = new float[4, 9];
        private float[,] rightChannelIntensity = new float[4, 9];
        private float[,] roarLastFilteredData = new float[4, 9];
        private float[,] fadeoffLastFilteredData = new float[4, 9];

        // random generator that is accesible outside the main loop
        readonly System.Random rand = new System.Random();

        void Awake()
        {
            // -------- constructing the arrays ------------

            losangei = new float[4, 9];
            losangej = new float[4, 9];
            groupi = new float[4, 9];
            groupj = new float[4, 9];
            amplitude = new float[4, 9];
            unphasing = new float[4, 9];
            amplitudeUnphasing = new float[4, 9];
            volume = new float[4, 9];
            fadeOff = new float[4, 9];
            leftChannelIntensity = new float[4, 9];
            rightChannelIntensity = new float[4, 9];
            roarLastFilteredData = new float[4, 9];
            fadeoffLastFilteredData = new float[4, 9];


            // --------- populating the array --------------

            // running through each wave system
            for (int w = 0; w < 4; w++)
            {
                // first, calculating the losange grid coordinate of the origin :

                // calculating the system position
                float groupPosition = -Time.time*Ocean.groupSpeed[w];

                // the origin is computed by projecting on the losange grid axis
                float origini = Vector3.Dot((transform.position-Ocean.directionVector[w]*groupPosition), Ocean.iVector[w]);
                float originj = Vector3.Dot((transform.position-Ocean.directionVector[w]*groupPosition), Ocean.jVector[w]);

                // the losange grid coordinate of the origin is set to the nearest half integer 
                origini = Mathf.Floor(origini) + 0.5f;
                originj = Mathf.Floor(originj) + 0.5f;

                // initializing the index that will define which emitter it is, it should not go outside bounds but no check is done
                int e = 0;

                // running through i axis
                for (float i = -1f; i <= 1f; i+=1f)
                {
                    // running trough j axis
                    for (float j = -1f; j <= 1f; j+=1f)
                    {
                        // setting the losange grid coordinate of the losange that the groups lies in
                        losangei[w, e] = origini+i;
                        losangej[w, e] = originj+j;

                        // settting the per group data
                        UpdateGroupData(w, e);

                        // updating the emitter index
                        e++;
                    }
                }
            }
        }

        void Start()
        {
            // checking the prescence of an audio source
            AudioSource audioSource = GetComponent<AudioSource>();

            // if there is not then this component is disabled
            if (audioSource == null) { Debug.LogWarning("Please attach an audio source to the ocean."); this.enabled = false; return; }
        }

        private void Update()
        {
            // running through each wave system
            for (int w = 0; w < 4; w++)
            {
                // the system position is common for all the emitter so it can be computed before
                float systemPosition = -Time.time*Ocean.groupSpeed[w];

                // gettting the ij component of the origin (position of the camera)
                float origini = Vector3.Dot((Camera.main.transform.position-Ocean.directionVector[w]*systemPosition), Ocean.iVector[w]);
                float originj = Vector3.Dot((Camera.main.transform.position-Ocean.directionVector[w]*systemPosition), Ocean.jVector[w]);

                // running through each emitter in the wave system
                for (int e = 0; e < 9; e++)
                {
                    // ---------- checking the bounds --------------

                    // calclulating the relative position in the losange grid
                    float relativei = losangei[w, e] - origini;
                    float relativej = losangej[w, e] - originj;

                    // making the checks in ij coordinate and updating the ij coordinate accordingly
                    bool isUpdated = false;
                    if (relativei >  2f) { losangei[w, e] -= 3f; isUpdated = true; }
                    if (relativei <=-1f) { losangei[w, e] += 3f; isUpdated = true; }
                    if (relativej >= 1f) { losangej[w, e] -= 3f; isUpdated = true; }
                    if (relativej < -2f) { losangej[w, e] += 3f; isUpdated = true; }

                    // --------------- per group data ------------------------

                    // updating the group data only if a change has been made to avoid excessive computation
                    if (isUpdated) { UpdateGroupData(w, e); }

                    // --------------- local data --------------------

                    // while the emitters ij coordinate must be continously checked, the following steps can be discarded when there are no wave breaking
                    if (Ocean.areBreakers)
                    {
                        // calculating the world space position of the emmitter by multiplying ij component to i and j vector and their scale (n.L)² and adding the group position
                        float scaleij = Ocean.setNumber[w]*Ocean.wavelength[w];
                        Vector3 emitterPosition = scaleij*scaleij*(groupi[w, e]*Ocean.iVector[w] + groupj[w, e]*Ocean.jVector[w]) + systemPosition*Ocean.directionVector[w];

                        // calculating the group amplitude variation over time
                        float amplitudeVariation = 0.8f + 0.2f*Mathf.Cos(amplitudeUnphasing[w, e] + 0.1f*Ocean.pulsation[w]*Time.time);

                        // calculating the phase
                        float phase = Vector3.Dot(emitterPosition, Ocean.directionVector[w])*Ocean.wavenumber[w] + Time.time*Ocean.pulsation[w] + unphasing[w, e] + amplitudeVariation*this.amplitude[w, e];

                        // calculating the amplitude, is saved in case there is a terrain that modifies it
                        float totalAmplitude = amplitudeVariation*amplitude[w, e];

                        // calculating the compression
                        float compression = totalAmplitude*Mathf.Cos(phase -1.2f);

                        // calculating the excess compression due to the breaking on the shore line
                        if (Ocean.useTerrain)
                        {
                            // getting the ground depth
                            float groundDepth = -Ocean.terrain.SampleHeight(emitterPosition) - Ocean.terrain.transform.position.y;

                            // getting shallow water amplitude
                            float shallowWaterWaveHeight = 0.5f*groundDepth;

                            //deducing the clamping factor 
                            // When under 1 the wave are reduced so they break, at 1 they are in deep water, when 0 the wave are under the terrain
                            float clampingFactor = shallowWaterWaveHeight/(0.085f*Ocean.wavelength[w]);

                            // clamping the factor to [0-1]
                            if (clampingFactor > 1f) { clampingFactor = 1f; }
                            else if (clampingFactor < 0f) { clampingFactor = 0f; }

                            // adding the excess compression due to this breaking
                            compression += 1f-clampingFactor;
                            totalAmplitude += 1.3f*(1f-clampingFactor);
                        }

                        
                        // when the compression exceed the trigger, then the audio parameters can be generated
                        if (totalAmplitude > 1f && compression > 0f)
                        {
                            // adding the wave advance to the position to make the sound closer to the actual wave  
                            emitterPosition += (-0.1f*totalAmplitude  - 0.12f*Mathf.Sin(phase))*Ocean.directionVector[w]*Ocean.wavelength[w] + 0.085f*Vector3.up*totalAmplitude*Ocean.wavelength[w];

                            // computing the distance once as it it served several time
                            float invSqrDistance = 1f/Vector3.SqrMagnitude(emitterPosition - Camera.main.transform.position);
                            float invDistance = Mathf.Min(1f, Mathf.Sqrt(invSqrDistance));
                            float invDistanceClamped = Mathf.Min(1f/Ocean.wavelength[w], invDistance);

                            // the normalized direction vector
                            Vector3 emitterDirection = (emitterPosition - Camera.main.transform.position)*invDistance;

                            // setting the pan intensity
                            float pan = Vector3.Dot(Camera.main.transform.right, emitterDirection);

                            // setting the fadeoff caused by the orientation of the wave
                            float orientationFadeoff = 0.8f + Vector3.Dot(Ocean.directionVector[w], emitterDirection)*0.2f;

                            // setting the noise volume function of the compression excess and the direction
                            volume[w, e] =  compression*Ocean.setNumber[w]*Ocean.wavelength[w]*Ocean.wavelength[w]*volumeFactor*orientationFadeoff*invDistanceClamped;

                            // setting the distance fadeoff of the distance to the current camera, which is asumed to be the listener
                            fadeOff[w, e] = Mathf.Min(1f, fadeOffFactor*invDistanceClamped*orientationFadeoff*Ocean.groupSpeed[w]);

                            // setting the left and right channel intensity 
                            leftChannelIntensity[w, e] = -pan*0.2f +0.8f;
                            rightChannelIntensity[w, e] =  pan*0.2f +0.8f;
                        }

                        // when the compression is under the trigger, volume is set to zero to make no sound
                        else
                        {
                            volume[w, e] = 0f;
                        }

                        //Debug.DrawLine(emitterPosition, emitterPosition + 5f*compression*Vector3.up, Color.green);
                        //if (totalAmplitude != 0f) { Debug.DrawLine(emitterPosition, emitterPosition - 5f*Vector3.up, Color.white); }
                        //Debug.DrawLine(emitterPosition, emitterPosition - 5f*totalAmplitude*Vector3.up, Color.blue);
                        //Debug.DrawLine(emitterPosition, emitterPosition + 5f*volume[w, e]*Vector3.up, Color.red);
                    }

                    // if there are no breakers, then the volume must be set to zero  to make no sound
                    else
                    {
                        volume[w, e] = 0f;
                    }
                }
            }

        }

        private void UpdateGroupData(int w, int e) // this function update the constants in a group of wave
        {
            // renaming i and j for easiness of reading
            float i = losangei[w, e];
            float j = losangej[w, e];

            // random function and ij center variation
            float randomimjm = Mathf.Cos(33f*i+53f*j)*Mathf.Cos(20f*i+48f*j);
            float deltai = Ocean.randomization[w]*randomimjm;
            float randomjmim = Mathf.Cos(33f*j+53f*i)*Mathf.Cos(20f*j+48f*i);
            float deltaj = Ocean.randomization[w]*randomjmim;

            // deducing the ij coordinate of the center of the group
            groupi[w, e] = i + deltai;
            groupj[w, e] = j + deltaj;

            // deducing the unphasing
            unphasing[w, e] = 10f*randomimjm;
            amplitudeUnphasing[w, e] = 10f*randomjmim;

            // deducing the amplitude at the top of the group
            amplitude[w, e] = Ocean.intensity[w]*2f*Mathf.Min(0.5f-Mathf.Abs(deltai), 0.5f-Mathf.Abs(deltaj));
        }

        void OnAudioFilterRead(float[] data, int channels) // procedural audio is generated here
        {
            // looping through each sample
            for (int s = 0; s < data.Length; s += channels)
            {
                // initializing the total sample that is the sum of each wave sample
                float leftTotalValue = 0f;
                float rightTotalValue = 0f;

                // looping through each wave system
                for (int w = 0; w < 4; w++)
                {
                    // looping through each emitter
                    for (int e = 0; e < 9; e++)
                    {
                        // checking if the emitter is breaking for optimization
                        if (volume[w, e] != 0f)
                        {
                            // setting the new random value
                            float newValue = (float)rand.NextDouble()*2f - 1f; ;

                            // fadeoff low pass filter
                            float fadeoffValue = fadeOff[w, e]*newValue + (1f-fadeOff[w, e])*fadeoffLastFilteredData[w, e];

                            // saving the last filtered data for next sample
                            fadeoffLastFilteredData[w, e] = fadeoffValue;

                            // roar low pass filter 
                            float roarValue = pitchFactor*fadeoffValue + (1f-pitchFactor)*roarLastFilteredData[w, e];

                            // saving the last filtered data for next sample
                            roarLastFilteredData[w, e] = roarValue;

                            // adding the value to the total
                            leftTotalValue += roarValue*leftChannelIntensity[w, e]*volume[w, e];
                            rightTotalValue += roarValue*rightChannelIntensity[w, e]*volume[w, e];
                        }
                    }
                }

                // setting the sample to the audio stream :

                // looping through each channel
                for (int c = 0; c < channels; c++)
                {
                    if (c == 0) // chanel left
                    {
                        data[s + c]  =  leftTotalValue;
                    }
                    else // right chanel or other
                    {
                        data[s + c]  =  rightTotalValue;
                    }
                }
            }
        }
    }
}