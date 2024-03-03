using UnityEngine;

namespace StormBreakers
{
    /// <summary>
    /// This static class gather all the data of the waves, wind and lighting, that can be accessed by any object in the game.
    /// </summary>
    /// <param name="waterColor">The albedo color of the water.</param>
    /// <param name="wavelength">Each system's wavelength in meters. (L)</param>
    /// <param name="direction">Each system's direction in degree. (d)</param>
    /// <param name="intensity">Each system's intensity. Above 1 the waves break. (I)</param>
    /// <param name="randomization">Each system's randomization value. When 0 the waves make a full density of rhombus shape group, should be stricilty inferior to 1. (r)</param>
    /// <param name="setNumber">Each system's number of wave per group (or set). (n)</param>
    /// <param name="breakSpeedFactor">The factor used to compute the speed of the water when the waves are breaking.</param>
    /// <param name="wavenumber">Each system's spatial frequency. (k = 2.Pi/L)</param>
    /// <param name="groupSpeed">Each system's group speed, is half wave celerity in deep water. (c = 0.5.sqrt(g/k)</param>
    /// <param name="pulsation">Each system's temporal frequency. (w = sqrt(g.k)</param>
    /// <param name="directionVector">Each system's direction vector. (D = {cos(d),0,sin(d)})</param>
    /// <param name="iVector">Each system's first vector of the rhombus grid.</param>
    /// <param name="jVector">Each system's second vector of the rhombus grid.</param>
    /// <param name="Wind"></param>
    /// <param name="totalLight">The sum of the ambient light color and the directional light color.</param>
    /// <param name="areBreakers">Are there breaking waves in the ocean ?</param>
    /// <param name="surfaceIntensity">Defines the maximal slope of the water surface including ripples and waves.</param>
    /// <param name="sharedMaterial">A read/write access to the ocean material.</param>
    /// <param name="useTerrain">Whether the simualtion include a terrain.</param>
    /// <param name="terrain">A read/write access to the terrain when included in the simulation.</param>
    public static class Ocean
    {
        // This static class gather all the data of the waves, wind and lighting, that can be accessed by any object in the game
        // It contains all the wave data, plus the wind and the lighting

        // wave inputs
        public static Color waterColor;
        public static float[] wavelength;
        public static float[] direction;
        public static float[] intensity;
        public static float[] randomization;
        public static float[] setNumber;
        public static float breakSpeedFactor;
        public static float breakTorqueFactor;

        // baked waves data
        public static float[] wavenumber;
        public static float[] groupSpeed;
        public static float[] pulsation;
        public static Vector3[] directionVector;
        public static Vector3[] iVector;
        public static Vector3[] jVector;

        // packed wave data to be parsed to material and visual effect
        public static Matrix4x4 LIDR;
        public static Matrix4x4 NKVW;

        /// <summary>
        /// The wind properties.
        /// </summary>
        /// <param name="speed">The wind speed in meter per second.</param>
        /// <param name="inverseHeight">The inverse of the height at which the wind is full strength.</param>
        /// <param name="cosDirection">The baked cosine of the direction of the wind.</param>
        /// <param name="sinDirection">The baked sine of the direction of the wind.</param>
        /// <param name="direction">The direction of the wind in degree relative to the world red axis.</param>
        public struct Wind
        {
            public static float speed = 0f;
            public static float inverseHeight = 0.05f;
            public static float cosDirection = -1f;
            public static float sinDirection = 0f;
            public static float direction = 0f;
            public static float turbulenceAmplitude = 0f;
            public static float turbulenceFrequency = 0f;
        }

        // lighting
        public static Color totalLight;

        public static bool areBreakers;
        public static float surfaceIntensity;

        // object reference accesible from anywhere
        public static Material sharedMaterial;
        public static Terrain terrain;
        public static bool useTerrain = false;


        /// <summary>
        /// Use this function before setting the static data because they can't be managed.
        /// </summary>
        public static void ConstructStaticData()
        {
            wavelength = new float[4];
            direction = new float[4];
            intensity = new float[4];
            randomization = new float[4];
            setNumber = new float[4];
            wavenumber = new float[4];
            groupSpeed = new float[4];
            pulsation = new float[4];
            directionVector = new Vector3[4];
            iVector = new Vector3[4];
            jVector = new Vector3[4];
        }

        /// <summary>
        /// Compute the baked wave data with scientific formulas and pack all the data to the matrix that can be used by material and visual effect.
        /// </summary>
        public static void BakeAndPackWaveData()
        {
            // looping trough the 4 waves systems to bake datas 
            for (int w = 0; w<4; w++)
            {
                // baked datas are calculated with scientific formulas
                wavenumber[w] = 2f*Mathf.PI/wavelength[w];
                groupSpeed[w] = 0.5f*Mathf.Sqrt(1.5613f*wavelength[w]);
                pulsation[w] = Mathf.Sqrt(20f*Mathf.PI/wavelength[w]);

                // direction vector
                directionVector[w] = new Vector3(Mathf.Cos(direction[w]), 0f, Mathf.Sin(direction[w]));

                // calculating the transverse vector as being directly perpendicualr to grid direction
                Vector3 transverse = new Vector3(directionVector[w].z, 0f, -directionVector[w].x);

                // calculating vector i and j (axis of the losange grid)
                iVector[w] = (transverse + directionVector[w])/Mathf.Sqrt(2f)/(setNumber[w]*wavelength[w]);
                jVector[w] = (transverse - directionVector[w])/Mathf.Sqrt(2f)/(setNumber[w]*wavelength[w]);

                // setting the matrix rows
                LIDR[w, 0] = wavelength[w];
                LIDR[w, 1] = intensity[w];
                LIDR[w, 2] = direction[w];
                LIDR[w, 3] = randomization[w];

                NKVW[w, 0] = setNumber[w];
                NKVW[w, 1] = wavenumber[w];
                NKVW[w, 2] = groupSpeed[w];
                NKVW[w, 3] = pulsation[w];
            }
        }

        /// <summary>
        /// Compute the deformation caused by the waves at 'time' and at 'position'. This also compute the additional speed caused by the breaking waves.
        /// </summary>
        /// <param name="time">The time at which to compute the ocean.</param>
        /// <param name="undeformedPosition"> The position where the deformation is computed.</param>
        /// <param name="breakingVelocity"> The velocity caused by the wave breaking.</param>
        static public Vector3 OceanDeformation(float time, Vector3 undeformedPosition, out Vector3 breakingVelocity, float groundDepth)
        {          
            // initializing ocean deformation on which eachs system will add its own deformation
            Vector3 oceanDeformation = Vector3.zero;

            // initializing the local oriented amplitude as 0 because there is no bigger wave than the ocean systems for now
            Vector3 localAmplitude = Vector3.zero;

            // initializing the local breaking intensity
            breakingVelocity = Vector3.zero;

            // looping trough waves in decreasing size order
            for (int w = 0; w<4; w++)
            {
                // computing the wave deformation
                Vector3 waveDeformation;

                // calculating the system position
                float systemPosition = -time*groupSpeed[w];

                // group amplitude and unphasing
                float unclampedAmplitude;
                float unphasing;
                float phaseAdvance;
                bool isFrontOfTheGroup;

                // oriented losange grid
                Vector3 worldAxisPosition = undeformedPosition - directionVector[w]*systemPosition;
                float Ai = Vector3.Dot(worldAxisPosition, iVector[w]);
                float Aj = Vector3.Dot(worldAxisPosition, jVector[w]);

                // calculating the coordiante of the center of the losange M
                float im = Mathf.Floor(Ai) + 0.5f;
                float jm = Mathf.Floor(Aj) + 0.5f;

                // calculating the randomized variation of the center of the group C relative to the center of the losange
                // doing in several steps to save values for others computation
                float randomimjm = Mathf.Cos(33f*im+53f*jm)*Mathf.Cos(20f*im+48f*jm);
                float deltai = randomization[w]*randomimjm;
                float randomjmim = Mathf.Cos(33f*jm+53f*im)*Mathf.Cos(20f*jm+48f*im);
                float deltaj = randomization[w]*randomjmim; ;

                // calculating the minimal distance of the center of the group C to the edge of the losange
                // non optimized : float distance = Mathf.Min(0.5f-Mathf.Abs(deltai), 0.5f-Mathf.Abs(deltaj));
                float distance = Mathf.Min(0.5f-(deltai<0f ? -deltai : deltai), 0.5f-(deltaj<0f ? -deltaj : deltaj));

                // coordinate of the center of the group in losange grid, can be used for the breaking speed
                float ic = im + deltai;
                float jc = jm + deltaj;

                // relative coordinate of the point in losange grid
                float ir = (ic - Ai)/distance;
                float jr = (jc - Aj)/distance;

                // calculating the amplitude in i and j direction by making sure the value is clamped between 0 and 1
                // non optimized : float relativeDistancei = Mathf.Min(1f, Mathf.Abs(ic));
                float relativeDistancei = ir<0f ? -ir : ir; if (relativeDistancei > 1f) { relativeDistancei = 1f; }
                float ai = 1f - relativeDistancei*relativeDistancei;
                float relativeDistancej = jr<0f ? -jr : jr; if (relativeDistancej > 1f) { relativeDistancej = 1f; }
                float aj = 1f - relativeDistancej*relativeDistancej;

                // define wether is in front of the group or not
                isFrontOfTheGroup = (ir-jr) > 0f;

                // calculating the group amplitude variation over time
                float variation = 0.8f + 0.2f*Mathf.Cos(10f*randomjmim + 0.1f*pulsation[w]*time);

                // calculating the phase advance
                phaseAdvance = ir+jr;
                phaseAdvance = variation*(1f - phaseAdvance*phaseAdvance);

                // calculating the unphasing by using one of the random function
                unphasing = 10f*randomimjm;

                // the final amplitude is the squarred product of the two component amplitude by making sure the value is clamped between 0 and 1
                unclampedAmplitude = ai*aj*ai*aj*2*distance*variation;

                // scalar unclamped amplitude
                unclampedAmplitude*=intensity[w];

                // calculating the oriented amplitude simply by multipling the direction vector to the scalar amplitude
                Vector3 orientedAmplitude = directionVector[w]*unclampedAmplitude;

                // clamping the oriented amplitude amplitude to [-1,1] taking in acount the existing local amplitude
                // non optimized : orientedAmplitude.x = Mathf.Clamp(orientedAmplitude.x, -1f-localAmplitude.x, 1f-localAmplitude.x); 
                // non optimizedorientedAmplitude.z = Mathf.Clamp(orientedAmplitude.z, -1f-localAmplitude.z, 1f-localAmplitude.z);
                if (orientedAmplitude.x >  1f-localAmplitude.x) { orientedAmplitude.x =  1f-localAmplitude.x; }
                else if (orientedAmplitude.x < -1f-localAmplitude.x) { orientedAmplitude.x = -1f-localAmplitude.x; }
                if (orientedAmplitude.z >  1f-localAmplitude.z) { orientedAmplitude.z =  1f-localAmplitude.z; }
                else if (orientedAmplitude.z < -1f-localAmplitude.z) { orientedAmplitude.z = -1f-localAmplitude.z; }

                // calculating the phase
                float phase = Vector3.Dot(undeformedPosition, directionVector[w])*wavenumber[w] + time*pulsation[w] + unphasing + intensity[w]*phaseAdvance;

                // wave deformation

                // clamping the scalar amplitude for the flatenable cos computation
                // non optimized : float clampedAmplitude = Mathf.Min(unclampedAmplitude, 1f);
                float clampedAmplitude = unclampedAmplitude > 1f ? 1f : unclampedAmplitude;

                // calculating the relative phase that vary between 0 and 1 so it can be used with the pow function to make sharper waves
                float relativePhase = phase/Mathf.PI -1f;
                // modulo 2 : x - 2*Mathf.Floor(x/2) and subtracting 1
                relativePhase = relativePhase - 2f*Mathf.Floor(relativePhase*0.5f) -1f;
                // absolute
                if (relativePhase < 0f) { relativePhase = -relativePhase; }

                // calculating the vertical amplitude 
                float verticalAmplitude = 0.085f*wavelength[w];

                // including the terrain if any
                if (useTerrain)
                {
                    // the sahllow water amplitude is clamped to 0.5 time the depth plus a bit of its wavelength to make the waves rise a bit on the beach
                    float shallowWaterAmplitude = 0.1f*verticalAmplitude + 0.5f*groundDepth;
                    // clamping to 0
                    shallowWaterAmplitude = shallowWaterAmplitude < 0f ? 0f : shallowWaterAmplitude;

                    // computing the clamping factor as defined in the VFX graph, and clamping to 0;1
                    float clampingFactor = 1f - 0.5f*groundDepth/verticalAmplitude;
                    if (clampingFactor > 1f) { clampingFactor = 1f; }
                    else if (clampingFactor < 0f) { clampingFactor = 0f; }

                    // taking the shallow amplitude when smaller
                    if (shallowWaterAmplitude < verticalAmplitude)
                    {
                        verticalAmplitude =  shallowWaterAmplitude;
                    }

                    // adding it to the amplitude to make the wave break
                    unclampedAmplitude += clampingFactor;
                }

                // calculating the vertical deformation, ajusted so the wave height is 0.17 time the wave lenght when maximal amplitude
                float deltav = verticalAmplitude*(0.2f+Mathf.Cos(Mathf.PI*Mathf.Pow(relativePhase, 1f+clampedAmplitude*0.5f)));

                // calculating the horizontal deformation (in "direction" direction)
                float deltah = -0.12f*wavelength[w]*Mathf.Sin(phase);

                // if the amplitude exceed a trigger, is in front of the wave and in back of the group, then it is likely there is a break force in the sea foam turmoils
                if (unclampedAmplitude > 0.85f && deltah > 0f && isFrontOfTheGroup)
                {
                    // then we must know if the phase at the middle of the group reached the breaking too
                    float scaleij = setNumber[w]*wavelength[w];
                    Vector3 centerGroupPosition = scaleij*scaleij*(ic*iVector[w] + jc*jVector[w]) + systemPosition*directionVector[w];
                    float centerGroupPhase = Vector3.Dot(centerGroupPosition, directionVector[w])*wavenumber[w] + time*pulsation[w] + unphasing + intensity[w]*variation + 0.7f;

                    // then checking the horizontal deformation in the center of the group to see if it's the back of the wave
                    if (Mathf.Sin(centerGroupPhase) > 0f)
                    {
                        // the breaking velocity can then be computed an store in x and z compoment
                        breakingVelocity -= groupSpeed[w]*(unclampedAmplitude-0.85f)*breakSpeedFactor*directionVector[w];

                        // in the y component we store the height of the vortex where the breaking torque happens, this height is arbitray an cannot be tweaking from unity
                        breakingVelocity.y += (unclampedAmplitude-0.85f)*wavelength[w]*0.01f;
                    }
                }
                // the vertical deformation is attenuated by the oriented amplitude
                waveDeformation = orientedAmplitude.magnitude*Vector3.up*deltav + orientedAmplitude*deltah;

                // updating the local oriented amplitude by adding the oriented amplitude (which by math cannont make components larger than 1 or -1)
                localAmplitude += orientedAmplitude;

                // computing the ocean deformation by adding the wave deformation
                oceanDeformation += waveDeformation;
            }

            // after the loop finished the ocean deformation can finally be returned
            return oceanDeformation;
        }

        /// <summary>
        /// Use this function to get the height of the water below or above position and update the undeformed position.
        /// </summary>
        /// <param name="time"> The time at which to compute the ocean. </param>
        /// <param name="position"> The position below or above which the water height will be computed. </param>
        /// <param name="previousUndeformedPosition"> The position from which the water deformation caused the water to be bellow or above 'position' at the previous frame. Will be uptated for the next frame. </param>
        /// <param name="deformation"> The deformation of the water that makes it bellow or above 'position'. Save this value for water velocity and normal computation. </param>
        /// <param name="recomputeOcean"> Recomputing the ocean a second time is costly but required to get water velocity.</param>
        static public float GetHeight(float time, Vector3 position, ref Vector3 previousUndeformedPosition, out Vector3 deformation, float groundDepth = 200f, bool recomputeOcean = true)
        {
            // this function calculate the water height (y) below position
            // to do this, the function update the undeformed position to get as close as possible to "position" (horizontal wise) in deformed space

            // calculating the ocean deformation on the current undeformed water position (whih is still the previous one) so to compare to position
            deformation =  OceanDeformation(time, previousUndeformedPosition, out _, groundDepth);

            // then we can calculate the difference of the deformed water to the position
            float deltaX = position.x - deformation.x - previousUndeformedPosition.x;
            float deltaZ = position.z - deformation.z - previousUndeformedPosition.z;

            // estimating the best undeformed position as being the previous one added to the difference calculated
            // the previous undeformed position become the new one and will be returned by the function to be looped again
            previousUndeformedPosition.x += deltaX;
            previousUndeformedPosition.z += deltaZ;

            // lopping once more if there is too much difference
            if (deltaX > 1f || deltaX < -1f || deltaZ > 1f || deltaZ < -1f)
            {
                // iterating a new time
                deformation =  OceanDeformation(time, previousUndeformedPosition, out _, groundDepth);

                // then we can calculate the difference of the deformed water to the position
                deltaX = position.x - deformation.x - previousUndeformedPosition.x;
                deltaZ = position.z - deformation.z - previousUndeformedPosition.z;

                // estimating the best undeformed position as being the previous one added to the difference calculated
                // the previous undeformed position become the new one and will be returned by the function to be looped again
                previousUndeformedPosition.x += deltaX;
                previousUndeformedPosition.z += deltaZ;
            }

            // recomputing and outing the new ocean deformation. 
            //This is required when getting the velocity of the water, otherwise for some rough approximation you can leave it to save on performance
            if (recomputeOcean)
            {
                deformation = OceanDeformation(time, previousUndeformedPosition, out _, groundDepth);
            }

            // the height is then the y component of the deformation hapening at the new undeformed position
            return deformation.y;
        }

        /// <summary>
        /// Compute the water normal given data that have already been computed with GetHeight. This function is costly and should be performed only when required.
        /// </summary>
        /// <param name="time"> The time at which to compute the ocean.</param>
        /// <param name="undeformedPosition"> The undeformed position of the water to which the normal will be computed.</param>
        /// <param name="alreadyComputedDeformation"> The deformation of the water at 'undeformedPosition', must have been already compted from GetHeight. </param>
        /// <param name="precision"> The spacing in meters of the water sampling. The bigger, the smoother the normal will vary. </param>
        static public Vector3 GetNormal(float time, Vector3 undeformedPosition, Vector3 alreadyComputedDeformation, float groundDepth = 200f, float precision = 0.1f)
        {
            // this function will compute the normal at the undeformed space position
            // to ease computation, it is assumed that the function GetHeight has already been called and so the local deforamtion is known and don't need to be computed again
            // what's left to be computed is 2 other water points and make a normalized cross product to get the normal

            // defining the 2 undeformed points
            Vector3 undeformedForwardPoint = undeformedPosition + Vector3.forward*precision;
            Vector3 undeformedLeftPoint = undeformedPosition + Vector3.right*precision;

            // calculating the 2 deformed vectors
            Vector3 deformedVector1 = undeformedPosition + alreadyComputedDeformation - undeformedForwardPoint - OceanDeformation(time, undeformedForwardPoint, out _, groundDepth);
            Vector3 deformedVector2 = undeformedPosition + alreadyComputedDeformation - undeformedLeftPoint    - OceanDeformation(time, undeformedLeftPoint, out _, groundDepth);

            //calculating the normal as being the cross product of t1 and t2
            Vector3 normal = Vector3.Cross(deformedVector1, deformedVector2);

            // returning the normalized vector
            return normal.normalized;
        }

        /// <summary>
        /// Compute the water velocity given data that have already been computed with GetHeight. The depth is required because there is a speed atenuation based on it.
        /// </summary>
        /// <param name="time"> The time at which to compute the ocean. </param>
        /// <param name="undeformedPosition"> The undeformed position of the water to which the velocity will be computed.</param>
        /// <param name="alreadyComputedDeformation"> The deformation of the water at 'undeformedPosition', must have been already compted from GetHeight. </param>
        /// <param name="depth"> The depth of the position where we compute the velocity. Must be positive under water (depth = water height - position.y). Set it to zero to have no speed attenuation </param>
        /// <param name="dt"> The delta time beetween 2 water sampling. </param>
        static public Vector3 GetVelocity(float time, Vector3 undeformedPosition, Vector3 alreadyComputedDeformation, out Vector3 breakingTorque, float depth = 0f, float groundDepth = 200f, float dt = 0.1f)
        {
            // this function will compute the water velocity vector at the undeformed space position
            // to ease computation, it is assumed that the function GetHeight has already been called and so the local deformation is known and don't need to be computed again
            // what's left to be computed is 1 other water points and make difference to get the velocity
            // the parameter dt [s] affect the precision of the calculation, too low it can lead to float unprecision, too high it won't take in count what happen in beetween

            // calculating the curent and future deformed position, outing the breaking torque here
            Vector3 currentDeformedPosition = undeformedPosition + alreadyComputedDeformation;
            Vector3 furtureDeformedPosition = undeformedPosition + OceanDeformation(time + dt, undeformedPosition, out Vector3 breakVelocity, groundDepth);

            // to ease code writing, the breakingVelocity is removed from its y component used to store the vortex height
            float vortexHeight = breakVelocity.y;
            breakVelocity.y = 0f;

            // the vector speed is the difference of the two divided by the time increment and adding the breaking velocity
            Vector3 waterVelocity = (furtureDeformedPosition - currentDeformedPosition)/dt + breakVelocity;

            // the breaking torque happens when the depth is under the vortex height, which has been stored in the breakVelocity.y
            if (depth > -vortexHeight)
            {
                // the breaking torque can be computed with the cross product of the breaking speed and the vertical to make a perpendicular vector whose magnitude is proportional to the speed
                breakingTorque = Vector3.Cross(breakVelocity, Vector3.down)*breakTorqueFactor;
            }
            else // otherwise there is no breaking torque
            { breakingTorque = Vector3.zero; }


            if (depth <= 0f) // if depth is zero or negative there is no need to attenuate the speed
            {
                // returning the velocity
                return waterVelocity;
            }
            else // when set to a positive value, the attenuation happens
            {
                // We can retrace the hypothetical wave number k = g/||V||²,and set no more speed at a depth of 1/k
                // non optimized :float attenuation = Mathf.Clamp(1f - 5f*depth/Mathf.Max(1f,waterVelocity.sqrMagnitude), 0f, 1f);

                // getting the water velocity but not bellow 1m/s to avoid a division by zero
                float waterSpeed = waterVelocity.sqrMagnitude; if (waterSpeed < 1f) { waterSpeed = 1f; }

                // computing the attenuation factor and clamping it
                float attenuation = 1f - 5f*depth/waterSpeed;
                if (attenuation > 1f) { attenuation = 1f; }
                else if (attenuation < 0f) { attenuation = 0f; }

                // attenuating the torque
                breakingTorque *= attenuation;

                // returning the attenuated velocity
                return attenuation*waterVelocity;
            }

        }

        /// <summary>
        /// Compute the wind speed at position height. The wind is half stregnth at y=0 and full strength above wind height.
        /// </summary>
        /// <param name="position"> The position at which to evalute the wind. </param>
        static public float GetWindSpeed(Vector3 position, bool computeTurbulence = false)
        {
            // compute the relative height and clamping
            float relativeHeight = 0.5f + 0.5f*position.y*Wind.inverseHeight;
            if (relativeHeight > 1f) { relativeHeight = 1f; }

            // computing the wind in meter per second, is proportional to the relative height
            float wind = relativeHeight*Wind.speed;

            // adding the turbulence
            if (computeTurbulence)
            {
                // Curently only dependend on time
                wind +=  Ocean.Wind.speed*Wind.turbulenceAmplitude*(Mathf.PerlinNoise(Time.time*Wind.turbulenceFrequency, 0f)-0.2f);
            }

            // returning the wind 
            return wind;
        }

        /// <summary>
        /// Compute the wind velocity at position height. The wind is half stregnth at y=0 and full strength above wind height.
        /// </summary>
        /// <param name="position"> The position at which to evalute the wind. </param>
        static public Vector3 GetWindVelocity(Vector3 position, bool computeTurbulence = false)
        {
            // compute the relative height and clamping
            float relativeHeight = 0.5f + 0.5f*position.y*Wind.inverseHeight;
            if (relativeHeight > 1f) { relativeHeight = 1f; }

            // computing the wind vector in meter per second,  is proportional to the relative height
            Vector3 wind = new Vector3(Wind.cosDirection, 0f, Wind.sinDirection)*relativeHeight*Wind.speed;

            // adding the turbulences
            if (computeTurbulence)
            {
                wind  += new Vector3(Wind.cosDirection, 0f, Wind.sinDirection)*Wind.speed*Wind.turbulenceAmplitude*(Mathf.PerlinNoise(Time.time*Wind.turbulenceFrequency, 0f)-0.2f);
            }

            // return the wind 
            return wind;
        }
    }
}