using UnityEngine;

namespace TurboTurbo.Modeling
{
    public class ExhaustSmokeModel
    {
        public sealed class Settings
        {
            public Color ColorIdleHaze = new Color(0.62f, 0.59f, 0.47f, 1f);
            public Color ColorCleanBurn = new Color(0.45f, 0.45f, 0.45f, 1f);
            public Color ColorHeavySoot = new Color(0.05f, 0.05f, 0.05f, 1f);
            public Color ColorWetStack = new Color(0.93f, 0.93f, 0.93f, 1f);
            public Color ColorOilBurn = new Color(0.44f, 0.52f, 0.85f, 1f);

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

            public Settings()
            {
            }

            public Settings(Settings other)
            {
                ColorIdleHaze = other.ColorIdleHaze;
                ColorCleanBurn = other.ColorCleanBurn;
                ColorHeavySoot = other.ColorHeavySoot;
                ColorWetStack = other.ColorWetStack;
                ColorOilBurn = other.ColorOilBurn;

                CleanExhaustLambda = other.CleanExhaustLambda;
                CleanExhaustAlpha = other.CleanExhaustAlpha;
                HazeAlpha = other.HazeAlpha;
                CleanBurnHeat = other.CleanBurnHeat;

                SootOnsetLambda = other.SootOnsetLambda;
                SootOpaqueLambda = other.SootOpaqueLambda;
                SootCurveExponent = other.SootCurveExponent;
                SootMaxAlpha = other.SootMaxAlpha;

                WetStackFillHeat = other.WetStackFillHeat;
                WetStackReleaseHeat = other.WetStackReleaseHeat;
                WetStackFillRate = other.WetStackFillRate;
                WetStackReleaseRate = other.WetStackReleaseRate;
                WetStackMistStrength = other.WetStackMistStrength;
                WetStackMaxAlpha = other.WetStackMaxAlpha;

                OilTintStrength = other.OilTintStrength;
                OilRpmExponent = other.OilRpmExponent;
            }

            /// <summary>
            /// Restores the documented invariants after tuning.
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
        }

        public Settings Tuning { get; set; } = new Settings();

        public float WetStackAccumulator { get; private set; }

        public Color Color { get; private set; } = Color.clear;

        public void FillWetStack() => WetStackAccumulator = 1f;

        public void Update(float lambda, float rpmNorm, float heat, bool engineOn, float delta)
        {
            var s = Tuning;

            if (!engineOn)
            {
                Color = Color.clear;
                return;
            }

            rpmNorm = Mathf.Clamp01(rpmNorm);
            heat = Mathf.Clamp01(heat);

            var flowColorFactor = Mathf.InverseLerp(0f, s.CleanBurnHeat, heat);
            var baseColor = Color.Lerp(s.ColorIdleHaze, s.ColorCleanBurn, flowColorFactor);

            var cleanliness = Mathf.InverseLerp(s.SootOnsetLambda, s.CleanExhaustLambda, lambda);
            var baseAlpha = Mathf.Lerp(s.HazeAlpha, s.CleanExhaustAlpha, cleanliness);

            var oilFactor = Mathf.Clamp01(s.OilTintStrength * Mathf.Pow(rpmNorm, s.OilRpmExponent));
            baseColor = Color.Lerp(baseColor, s.ColorOilBurn, oilFactor);

            var sootFactor = Mathf.InverseLerp(s.SootOnsetLambda, s.SootOpaqueLambda, lambda);
            sootFactor = Mathf.Pow(sootFactor, s.SootCurveExponent);
            var sootAlpha = s.SootMaxAlpha * sootFactor;

            var wetFactor = 0f;
            if (heat < s.WetStackFillHeat)
            {
                var fillProgress = Mathf.InverseLerp(0f, s.WetStackFillHeat, heat);
                var fillFactor = 1f - Mathf.SmoothStep(0f, 1f, fillProgress);
                WetStackAccumulator = Mathf.Min(
                    1f,
                    WetStackAccumulator + s.WetStackFillRate * fillFactor * delta);
            }
            else if (heat > s.WetStackReleaseHeat)
            {
                var releaseProgress = Mathf.InverseLerp(s.WetStackReleaseHeat, 1f, heat);
                var releaseFactor = Mathf.SmoothStep(0f, 1f, releaseProgress);
                wetFactor = Mathf.Clamp01(
                    s.WetStackReleaseRate * WetStackAccumulator * releaseFactor * s.WetStackMistStrength);

                WetStackAccumulator = Mathf.Max(
                    0f,
                    WetStackAccumulator - s.WetStackReleaseRate * releaseFactor * delta);
            }

            var wetAlpha = s.WetStackMaxAlpha * wetFactor;
            var totalWeight = baseAlpha + wetAlpha + sootAlpha;
            var finalColor = (baseColor * baseAlpha
                              + s.ColorWetStack * wetAlpha
                              + s.ColorHeavySoot * sootAlpha) / totalWeight;
            finalColor.a = 1f - (1f - baseAlpha) * (1f - wetAlpha) * (1f - sootAlpha);

            Color = finalColor;
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