using UnityEngine;

namespace TurboTurbo.Modeling
{
    public class ExhaustSmokeModel
    {
        internal static readonly Color ColorIdleHaze = new Color(0.62f, 0.59f, 0.47f, 0.22f);  // aged warm grey-tan (sulfurous idle gas)
        internal static readonly Color ColorHeavySoot = new Color(0.05f, 0.05f, 0.05f, 0.95f); // deep black charcoal
        internal static readonly Color ColorWetStack = new Color(0.85f, 0.82f, 0.78f, 0.85f);  // white vapor (unburned fuel droplets)
        internal static readonly Color ColorOilBurn = new Color(0.44f, 0.52f, 0.85f, 0.50f);   // dull blue-grey oil burn

        public static float WetStackIdleDemand = 0.1f;
        public static float WetStackFillRate = 0.005f;
        public static float WetStackBurnThreshold = 0.05f;
        public static float WetStackBurnDemand = 0.15f;
        public static float WetStackBurnRate = 0.75f;
        public static float WetStackBurnRampDemand = 0.5f;
        public static float OilBlowbyTintStrength = 0.25f;
        public static float SootCurveExponent = 1.1f;
        public static float AlphaFloor = 0.02f;
        public static float AlphaCeiling = 0.95f;
        public static float WetStackAlphaScale = 0.8f;

        private float _wetStackAccumulator;

        public float SootOnsetLambda = 0.65f;
        public float SootOpaqueLambda = 0.46f;

        public Color Color { get; private set; } = Color.clear;

        // used for determining emission rate; basically a slightly more pure view over the alpha channel
        public float Density { get; private set; }

        internal float WetStackAccumulator => _wetStackAccumulator;

        public void Update(float lambda, float demand, float rpmNorm, bool engineOn, float delta)
        {
            if (!engineOn)
            {
                Color = Color.clear;
                Density = 0f;
                return;
            }

            // 1. wet stacking: unburned fuel accumulates at idle, burns off under load
            if (demand < WetStackIdleDemand)
            {
                _wetStackAccumulator = Mathf.Min(1f, _wetStackAccumulator + delta * WetStackFillRate);
            }
            else
            {
                _wetStackAccumulator = Mathf.Max(0f, _wetStackAccumulator - delta * demand * WetStackBurnRate);
            }

            // 2. base: slightly tinted idle haze
            Color current = ColorIdleHaze;

            // 3. oil blowby layer, scaled with engine speed
            current = Color.Lerp(current, ColorOilBurn, OilBlowbyTintStrength * rpmNorm);

            // 4. soot layer
            float sootFactor = Mathf.Clamp01(
                (SootOnsetLambda - lambda) / (SootOnsetLambda - SootOpaqueLambda));
            sootFactor = Mathf.Pow(sootFactor, SootCurveExponent);
            current = Color.Lerp(current, ColorHeavySoot, sootFactor);

            // 5. wet-stack vapor puffs when the throttle opens after idling
            float wetBurn = 0f;
            if (_wetStackAccumulator > WetStackBurnThreshold && demand > WetStackBurnDemand)
            {
                float ramp = Mathf.Clamp01(
                    (demand - WetStackIdleDemand) / (WetStackBurnRampDemand - WetStackIdleDemand));
                wetBurn = _wetStackAccumulator * ramp;
                current = Color.Lerp(current, ColorWetStack, wetBurn);
            }

            // 6. opacity: haze floor up to near-opaque soot / wet-stack burn cloud
            current.a = Mathf.Lerp(AlphaFloor, AlphaCeiling, Mathf.Max(sootFactor, wetBurn * WetStackAlphaScale));

            Color = current;
            Density = Mathf.Max(sootFactor, wetBurn);
        }
    }
}
