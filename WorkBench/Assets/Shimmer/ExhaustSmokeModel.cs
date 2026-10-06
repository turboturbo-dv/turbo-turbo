using System;
using System.Globalization;

using UnityEngine;

namespace TurboTurbo.Modeling
{
    public class ExhaustSmokeModel
    {
        public sealed class Settings
        {
            internal const float DefaultDensity = 150f;

            // we use hex here for easy comparison with stored values, as those are hex-serialized too
            internal const string DefaultColorIdleHazeHex = "9E9678FF";
            internal const string DefaultColorCleanBurnHex = "737373FF";
            internal const string DefaultColorHeavySootHex = "0D0D0DFF";
            internal const string DefaultColorWetStackHex = "FFFFF2FF";
            internal const string DefaultColorOilBurnHex = "7085D9FF";

            public Color ColorIdleHaze = ColorHex.Parse(DefaultColorIdleHazeHex);
            public Color ColorCleanBurn = ColorHex.Parse(DefaultColorCleanBurnHex);
            public Color ColorHeavySoot = ColorHex.Parse(DefaultColorHeavySootHex);
            public Color ColorWetStack = ColorHex.Parse(DefaultColorWetStackHex);
            public Color ColorOilBurn = ColorHex.Parse(DefaultColorOilBurnHex);

            public float Density = DefaultDensity;

            internal const float DefaultCleanMinHeatAlpha = 0.002f;
            internal const float DefaultCleanMaxHeatAlpha = 0.08f;
            internal const float DefaultCleanBurnHeat = 0.2f;
            internal const float DefaultSootOnsetLambda = 1.3f;
            internal const float DefaultSootOpaqueLambda = 1.05f;
            internal const float DefaultSootCurveExponent = 2f;
            internal const float DefaultSootIncreaseTau = 0.08f;
            internal const float DefaultSootDecreaseTau = 0.4f;
            internal const float DefaultSootMaxAlpha = 0.95f;
            internal const float DefaultSootPowerFloor = 0.1f;
            internal const float DefaultSootPowerExponent = 1f;
            internal const float DefaultWetStackFillHeat = 0.1f;
            internal const float DefaultWetStackReleaseHeat = 0.15f;
            internal const float DefaultWetStackFillRate = 0.005f;
            internal const float DefaultWetStackReleaseRate = 0.1f;
            internal const float DefaultWetStackMistStrength = 4f;
            internal const float DefaultWetStackMaxAlpha = 0.95f;
            internal const float DefaultOilTintStrength = 0.3f;
            internal const float DefaultOilRpmExponent = 2.5f;

            public float CleanMinHeatAlpha = DefaultCleanMinHeatAlpha;

            public float CleanMaxHeatAlpha = DefaultCleanMaxHeatAlpha;

            public float CleanBurnHeat = DefaultCleanBurnHeat;

            public float SootOnsetLambda = DefaultSootOnsetLambda;

            public float SootOpaqueLambda = DefaultSootOpaqueLambda;

            public float SootCurveExponent = DefaultSootCurveExponent;

            public float SootIncreaseTau = DefaultSootIncreaseTau;

            public float SootDecreaseTau = DefaultSootDecreaseTau;

            public float SootMaxAlpha = DefaultSootMaxAlpha;

            public float SootPowerFloor = DefaultSootPowerFloor;

            public float SootPowerExponent = DefaultSootPowerExponent;

            public float WetStackFillHeat = DefaultWetStackFillHeat;

            public float WetStackReleaseHeat = DefaultWetStackReleaseHeat;

            public float WetStackFillRate = DefaultWetStackFillRate;

            public float WetStackReleaseRate = DefaultWetStackReleaseRate;

            public float WetStackMistStrength = DefaultWetStackMistStrength;

            public float WetStackMaxAlpha = DefaultWetStackMaxAlpha;

            public float OilTintStrength = DefaultOilTintStrength;

            public float OilRpmExponent = DefaultOilRpmExponent;

            public Settings()
            {
            }

            public Settings(Settings other)
            {
                Density = other.Density;

                ColorIdleHaze = other.ColorIdleHaze;
                ColorCleanBurn = other.ColorCleanBurn;
                ColorHeavySoot = other.ColorHeavySoot;
                ColorWetStack = other.ColorWetStack;
                ColorOilBurn = other.ColorOilBurn;

                CleanMinHeatAlpha = other.CleanMinHeatAlpha;
                CleanMaxHeatAlpha = other.CleanMaxHeatAlpha;
                CleanBurnHeat = other.CleanBurnHeat;

                SootOnsetLambda = other.SootOnsetLambda;
                SootOpaqueLambda = other.SootOpaqueLambda;
                SootCurveExponent = other.SootCurveExponent;
                SootIncreaseTau = other.SootIncreaseTau;
                SootDecreaseTau = other.SootDecreaseTau;
                SootMaxAlpha = other.SootMaxAlpha;
                SootPowerFloor = other.SootPowerFloor;
                SootPowerExponent = other.SootPowerExponent;

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
            /// Validates the model: any settings that violate invariants are adjusted.
            /// </summary>
            public void Validate()
            {
                const float epsilon = 0.01f;

                Density = Mathf.Max(0f, Density);

                CleanMinHeatAlpha = Mathf.Clamp01(CleanMinHeatAlpha);
                CleanMaxHeatAlpha = Mathf.Clamp01(CleanMaxHeatAlpha);
                SootMaxAlpha = Mathf.Clamp01(SootMaxAlpha);
                SootPowerFloor = Mathf.Clamp01(SootPowerFloor);
                WetStackMaxAlpha = Mathf.Clamp01(WetStackMaxAlpha);
                WetStackMistStrength = Mathf.Max(0f, WetStackMistStrength);
                OilTintStrength = Mathf.Clamp01(OilTintStrength);

                WetStackFillRate = Mathf.Max(0f, WetStackFillRate);
                WetStackReleaseRate = Mathf.Max(0f, WetStackReleaseRate);
                CleanBurnHeat = Mathf.Max(epsilon, CleanBurnHeat);
                SootCurveExponent = Mathf.Max(epsilon, SootCurveExponent);
                SootPowerExponent = Mathf.Max(epsilon, SootPowerExponent);
                SootIncreaseTau = Mathf.Max(epsilon, SootIncreaseTau);
                SootDecreaseTau = Mathf.Max(epsilon, SootDecreaseTau);
                OilRpmExponent = Mathf.Max(epsilon, OilRpmExponent);

                WetStackFillHeat = Mathf.Clamp01(WetStackFillHeat);
                WetStackReleaseHeat = Mathf.Clamp(
                    WetStackReleaseHeat, Mathf.Min(WetStackFillHeat + epsilon, 1f), 1f);
                WetStackFillHeat = Mathf.Min(WetStackFillHeat, WetStackReleaseHeat - epsilon);

                SootOnsetLambda = Mathf.Max(SootOnsetLambda, SootOpaqueLambda + epsilon);
                SootOpaqueLambda = Mathf.Min(SootOpaqueLambda, SootOnsetLambda - epsilon);
            }
        }

        private float _soot;

        public Settings Tuning { get; set; } = new Settings();

        public float WetStackAccumulator { get; private set; }

        public Color Color { get; private set; } = Color.clear;

        /// <summary>
        /// Total particulate mass produced per unit time (sum of all exhaust fractions).
        /// The shader uses this to decide how much smoke to render.
        /// </summary>
        public float ParticulateMass { get; private set; }

        /// <summary>
        /// Upper bound of <see cref="ParticulateMass"/> derived from the current tuning.
        /// As long as the tuning does not change, ParticulateMass is guaranteed to never exceed this.
        /// </summary>
        public float MaxParticulateMass => Tuning.Density
            * (Tuning.CleanMaxHeatAlpha + Tuning.WetStackMaxAlpha + Tuning.SootMaxAlpha);

        public void FillWetStack() => WetStackAccumulator = 1f;

        public void Update(float lambda, float rpmNorm, float heat, bool engineOn, float delta)
        {
            var s = Tuning;

            if (!engineOn)
            {
                _soot = 0f;
                Color = Color.clear;
                ParticulateMass = 0f;
                return;
            }

            rpmNorm = Mathf.Clamp01(rpmNorm);
            heat = Mathf.Clamp01(heat);

            var flowColorFactor = Mathf.InverseLerp(0f, s.CleanBurnHeat, heat);
            var baseColor = Color.Lerp(s.ColorIdleHaze, s.ColorCleanBurn, flowColorFactor);

            var baseAlpha = Mathf.Lerp(s.CleanMinHeatAlpha, s.CleanMaxHeatAlpha, heat);

            var oilFactor = Mathf.Clamp01(s.OilTintStrength * Mathf.Pow(rpmNorm, s.OilRpmExponent));
            baseColor = Color.Lerp(baseColor, s.ColorOilBurn, oilFactor);

            var sootTarget = Mathf.InverseLerp(s.SootOnsetLambda, s.SootOpaqueLambda, lambda);
            sootTarget = Mathf.Pow(sootTarget, s.SootCurveExponent);

            // the governor is instant, so lambda can cross the whole sooty band in one
            // frame; ease soot in/out so opacity cannot snap between 0 and 1
            var sootTau = sootTarget > _soot ? s.SootIncreaseTau : s.SootDecreaseTau;
            _soot += (sootTarget - _soot) * (1f - Mathf.Exp(-delta / sootTau));
            var sootPower = Mathf.Pow(heat, s.SootPowerExponent);
            var sootAlpha = s.SootMaxAlpha * _soot * Mathf.Lerp(s.SootPowerFloor, 1f, sootPower);

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

            // unused as ParticulateMass encodes density, but set it to 1 for good practice
            finalColor.a = 1f;

            Color = finalColor;
            ParticulateMass = s.Density * totalWeight;
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
        /// <summary>Parents an emitter to the car and orients it for smoke emission.</summary>
        public static void AttachTo(Transform emitter, Transform parent)
        {
            emitter.SetParent(parent, worldPositionStays: false);
            emitter.localRotation = Quaternion.Euler(-90f, 0f, 0f);
        }

        public static void PlaceAt(Transform emitter, Vector3 exhaustPosition, Transform parent, Vector3 offset)
        {
            // probably not the easiest way, but hey, it seems to work even under the heaviest of derailments.
            // if ever you wanted to test if the exhaust emits in the right direction even when the loco is upside down
            // boy have I got you covered
            AttachTo(emitter, parent);
            emitter.localPosition = parent.InverseTransformPoint(exhaustPosition) + offset;
        }
    }

    /// <summary>
    /// Exhaust plume speed range shared by a host's smoke and shimmer emitters.
    /// </summary>
    public sealed class ExhaustVelocitySettings
    {
        internal const float DefaultExhaustVelocityCoefficient = 5f;

        /// <summary>
        /// Exhaust velocity coefficient [m/s per unit mass flow per unit gas density].
        /// Can be adjusted to represent a wider/narrower exhaust mouth resulting in lower/higher exhaust velocity.
        /// </summary>
        public float ExhaustVelocityCoefficient = DefaultExhaustVelocityCoefficient;

        public ExhaustVelocitySettings()
        {
        }

        public ExhaustVelocitySettings(ExhaustVelocitySettings other)
        {
            ExhaustVelocityCoefficient = other.ExhaustVelocityCoefficient;
        }

        public void Validate()
        {
            ExhaustVelocityCoefficient = Mathf.Max(0.01f, ExhaustVelocityCoefficient);
        }

        /// <summary>Exhaust plume speed [m/s] from the current mass flow and gas density.</summary>
        public float Calculate(float massFlow, float gasDensity)
        {
            return ExhaustVelocityCoefficient * massFlow / Mathf.Max(0.0001f, gasDensity);
        }
    }

    /// <summary>
    /// Particle growth curve shared by a host's smoke and shimmer emitters.
    /// </summary>
    public static class ParticleCurves
    {
        // bakes the growth shape into a multi-key curve; exponent 1 is linear
        public static AnimationCurve BakeSizeCurve(float startSize, float endSize, float exponent)
        {
            var ratio = Mathf.Max(0.01f, endSize / Mathf.Max(0.01f, startSize));
            exponent = Mathf.Clamp(exponent, 0.05f, 4f);

            const int segments = 16;
            var times = new float[segments + 1];
            var values = new float[segments + 1];
            for (var i = 0; i <= segments; i++)
            {
                var t = (float)i / segments;
                times[i] = t;
                values[i] = Mathf.LerpUnclamped(1f, ratio, Mathf.Pow(t, exponent));
            }

            var keys = new Keyframe[segments + 1];
            for (var i = 0; i <= segments; i++)
            {
                var lo = Mathf.Max(0, i - 1);
                var hi = Mathf.Min(segments, i + 1);
                var slope = (values[hi] - values[lo]) / (times[hi] - times[lo]);
                keys[i] = new Keyframe(times[i], values[i], slope, slope);
            }

            return new AnimationCurve(keys);
        }
    }

    /// <summary>
    /// Hex color codec for XML serialization, where a Color cannot be a
    /// [DefaultValue] constant. Pure managed so it also works outside the game
    /// (ColorUtility's parse is a native ECall). Unparseable input yields magenta.
    /// </summary>
    internal static class ColorHex
    {
        public static Color Parse(string hex)
        {
            if (string.IsNullOrEmpty(hex)) return Color.magenta;

            if (hex[0] == '#') hex = hex.Substring(1);
            if ((hex.Length != 6 && hex.Length != 8)
                || !uint.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var value))
            {
                return Color.magenta;
            }

            if (hex.Length == 6)
            {
                return new Color(((value >> 16) & 0xFF) / 255f, ((value >> 8) & 0xFF) / 255f,
                    (value & 0xFF) / 255f, 1f);
            }

            return new Color(((value >> 24) & 0xFF) / 255f, ((value >> 16) & 0xFF) / 255f,
                ((value >> 8) & 0xFF) / 255f, (value & 0xFF) / 255f);
        }

        public static string Format(Color color)
        {
            var r = (byte)Math.Round(Mathf.Clamp(color.r, 0f, 1f) * 255f);
            var g = (byte)Math.Round(Mathf.Clamp(color.g, 0f, 1f) * 255f);
            var b = (byte)Math.Round(Mathf.Clamp(color.b, 0f, 1f) * 255f);
            var a = (byte)Math.Round(Mathf.Clamp(color.a, 0f, 1f) * 255f);
            return $"{r:X2}{g:X2}{b:X2}{a:X2}";
        }
    }
}
