using UnityEngine;

namespace TurboTurbo.Modeling
{
    public class ExhaustSmokeModel
    {
        public static Color ColorIdleHaze = new Color(0.62f, 0.59f, 0.47f, 1f);
        public static Color ColorCleanBurn = new Color(0.45f, 0.45f, 0.45f, 1f);
        public static Color ColorHeavySoot = new Color(0.05f, 0.05f, 0.05f, 1f);
        public static Color ColorWetStack = new Color(0.93f, 0.93f, 0.93f, 1f);
        public static Color ColorOilBurn = new Color(0.44f, 0.52f, 0.85f, 1f);

        public float CleanExhaustLambda = 1.7f;
        public float CleanExhaustAlpha = 0.015f;
        public float HazeAlpha = 0.08f;
        public float CleanBurnHeat = 0.2f;

        public float SootOnsetLambda = 1.05f;
        public float SootOpaqueLambda = 0.8f;
        public float SootCurveExponent = 1.1f;
        public float SootMaxAlpha = 0.95f;

        public float WetStackFillHeat = 0.1f;
        public float WetStackReleaseHeat = 0.15f;
        public float WetStackFillRate = 0.005f;
        public float WetStackReleaseRate = 0.75f;
        public float WetStackMistStrength = 4f;
        public float WetStackMaxAlpha = 0.95f;

        public float OilTintStrength = 0.3f;
        public float OilRpmExponent = 2.5f;

        private float _wetStackAccumulator;

        public Color Color { get; private set; } = Color.clear;

        internal float WetStackAccumulator => _wetStackAccumulator;

        public void FillWetStack() => _wetStackAccumulator = 1f;

        /// <summary>
        /// Restores the documented invariants after tuning. Runs when settings
        /// change, not inside Update.
        /// </summary>
        public void Validate()
        {
            const float epsilon = 0.01f;

            CleanExhaustAlpha = Mathf.Clamp01(CleanExhaustAlpha);
            HazeAlpha = Mathf.Clamp01(HazeAlpha);
            SootMaxAlpha = Mathf.Clamp01(SootMaxAlpha);
            WetStackMaxAlpha = Mathf.Clamp01(WetStackMaxAlpha);
            WetStackMistStrength = Mathf.Max(0f, WetStackMistStrength);
            OilTintStrength = Mathf.Clamp01(OilTintStrength);

            WetStackFillRate = Mathf.Max(0f, WetStackFillRate);
            WetStackReleaseRate = Mathf.Max(0f, WetStackReleaseRate);
            CleanBurnHeat = Mathf.Max(epsilon, CleanBurnHeat);
            SootCurveExponent = Mathf.Max(epsilon, SootCurveExponent);
            OilRpmExponent = Mathf.Max(epsilon, OilRpmExponent);

            WetStackFillHeat = Mathf.Clamp01(WetStackFillHeat);
            WetStackReleaseHeat = Mathf.Clamp(
                WetStackReleaseHeat, Mathf.Min(WetStackFillHeat + epsilon, 1f), 1f);
            WetStackFillHeat = Mathf.Min(WetStackFillHeat, WetStackReleaseHeat - epsilon);

            SootOnsetLambda = Mathf.Clamp(
                SootOnsetLambda, SootOpaqueLambda + epsilon, CleanExhaustLambda - epsilon);
            SootOpaqueLambda = Mathf.Min(SootOpaqueLambda, SootOnsetLambda - epsilon);
            CleanExhaustLambda = Mathf.Max(CleanExhaustLambda, SootOnsetLambda + epsilon);
        }

        public void Update(float lambda, float rpmNorm, float heat, bool engineOn, float delta)
        {
            if (!engineOn)
            {
                Color = Color.clear;
                return;
            }

            rpmNorm = Mathf.Clamp01(rpmNorm);
            heat = Mathf.Clamp01(heat);

            var flowColorFactor = Mathf.InverseLerp(0f, CleanBurnHeat, heat);
            var baseColor = Color.Lerp(ColorIdleHaze, ColorCleanBurn, flowColorFactor);

            var cleanliness = Mathf.InverseLerp(SootOnsetLambda, CleanExhaustLambda, lambda);
            var baseAlpha = Mathf.Lerp(HazeAlpha, CleanExhaustAlpha, cleanliness);

            var oilFactor = Mathf.Clamp01(OilTintStrength * Mathf.Pow(rpmNorm, OilRpmExponent));
            baseColor = Color.Lerp(baseColor, ColorOilBurn, oilFactor);

            var sootFactor = Mathf.InverseLerp(SootOnsetLambda, SootOpaqueLambda, lambda);
            sootFactor = Mathf.Pow(sootFactor, SootCurveExponent);
            var sootAlpha = SootMaxAlpha * sootFactor;

            var wetFactor = 0f;
            if (heat < WetStackFillHeat)
            {
                var fillProgress = Mathf.InverseLerp(0f, WetStackFillHeat, heat);
                var fillFactor = 1f - Mathf.SmoothStep(0f, 1f, fillProgress);
                _wetStackAccumulator = Mathf.Min(
                    1f,
                    _wetStackAccumulator + WetStackFillRate * fillFactor * delta);
            }
            else if (heat > WetStackReleaseHeat)
            {
                var releaseProgress = Mathf.InverseLerp(WetStackReleaseHeat, 1f, heat);
                var releaseFactor = Mathf.SmoothStep(0f, 1f, releaseProgress);
                wetFactor = Mathf.Clamp01(
                    WetStackReleaseRate * _wetStackAccumulator * releaseFactor * WetStackMistStrength);

                _wetStackAccumulator = Mathf.Max(
                    0f,
                    _wetStackAccumulator - WetStackReleaseRate * releaseFactor * delta);
            }

            var wetAlpha = WetStackMaxAlpha * wetFactor;
            var totalWeight = baseAlpha + wetAlpha + sootAlpha;
            var finalColor = (baseColor * baseAlpha
                              + ColorWetStack * wetAlpha
                              + ColorHeavySoot * sootAlpha) / totalWeight;
            finalColor.a = 1f - (1f - baseAlpha) * (1f - wetAlpha) * (1f - sootAlpha);

            Color = finalColor;
        }
    }

    public static class ExhaustVelocity
    {
        public static float Idle = 1.5f;
        public static float FullLoad = 15f;

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