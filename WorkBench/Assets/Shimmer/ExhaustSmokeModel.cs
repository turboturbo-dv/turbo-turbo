using UnityEngine;

namespace TurboTurbo.Modeling
{
    public class ExhaustSmokeModel
    {
        public static readonly Color ColorIdleHaze = new Color(0.62f, 0.59f, 0.47f, 0.22f);
        public static readonly Color ColorHeavySoot = new Color(0.05f, 0.05f, 0.05f, 0.95f);
        public static readonly Color ColorWetStack = new Color(0.85f, 0.82f, 0.78f, 0.85f);
        public static readonly Color ColorOilBurn = new Color(0.44f, 0.52f, 0.85f, 0.50f);

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

        public float SootOnsetLambda = 1.1f;
        public float SootOpaqueLambda = 0.8f;

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

            // wet stacking: unburned fuel accumulates at idle, burns off under load
            if (demand < WetStackIdleDemand)
            {
                _wetStackAccumulator = Mathf.Min(1f, _wetStackAccumulator + delta * WetStackFillRate);
            }
            else
            {
                _wetStackAccumulator = Mathf.Max(0f, _wetStackAccumulator - delta * demand * WetStackBurnRate);
            }

            var current = ColorIdleHaze;

            // oil blowby layer
            current = Color.Lerp(current, ColorOilBurn, OilBlowbyTintStrength * rpmNorm);

            // soot layer
            var sootFactor = Mathf.Clamp01(
                (SootOnsetLambda - lambda) / (SootOnsetLambda - SootOpaqueLambda));
            sootFactor = Mathf.Pow(sootFactor, SootCurveExponent);
            current = Color.Lerp(current, ColorHeavySoot, sootFactor);

            // wet-stack layer
            var wetBurn = 0f;
            if (_wetStackAccumulator > WetStackBurnThreshold && demand > WetStackBurnDemand)
            {
                var ramp = Mathf.Clamp01(
                    (demand - WetStackIdleDemand) / (WetStackBurnRampDemand - WetStackIdleDemand));
                wetBurn = _wetStackAccumulator * ramp;
                current = Color.Lerp(current, ColorWetStack, wetBurn);
            }

            current.a = Mathf.Lerp(AlphaFloor, AlphaCeiling, Mathf.Max(sootFactor, wetBurn * WetStackAlphaScale));
            Color = current;

            Density = Mathf.Max(sootFactor, wetBurn);
        }
    }

    // TODO: this should be a tunable parameter
    public static class ExhaustVelocity
    {
        public const float Idle = 1.5f;
        public const float FullLoad = 10f;

        public static float Calculate(float heat)
        {
            return Mathf.Lerp(Idle, FullLoad, Mathf.Clamp01(heat));
        }
    }

    /// <summary>
    /// Translates positions and directions into a custom simulation space,
    /// if present (not null). Otherwise, returns the input unchanged.
    /// </summary>
    public static class ParticleSimSpace
    {
        public static Vector3 Position(Transform simSpace, Vector3 position)
            => simSpace?.InverseTransformPoint(position) ?? position;

        public static Vector3 Direction(Transform simSpace, Vector3 direction)
            => simSpace?.InverseTransformDirection(direction) ?? direction;
    }

    public static class ExhaustPlacement
    {
        public static void PlaceAt(Transform emitter, Vector3 exhaustPosition, Transform parent, Vector3 offset)
        {
            // probably not the easiest way, but hey, it seems to work even under the heaviest of derailments.
            // if ever you wanted to test if the exhaust emits in the right direction even when the loco is upside down
            // boy have I got you covered
            emitter.SetParent(parent, worldPositionStays: false);
            emitter.localPosition = parent.InverseTransformPoint(exhaustPosition) + offset;
            emitter.localRotation = Quaternion.Euler(-90f, 0f, 0f);
        }
    }
}