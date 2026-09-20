using System;
using System.ComponentModel;
using System.Globalization;
using System.Xml.Serialization;

using UnityEngine;

namespace TurboTurbo.Modeling
{
    public class ExhaustSmokeModel
    {
        [XmlType("SmokeSettings")]
        public sealed class Settings
        {
            // colors cannot be [DefaultValue] constants, so they serialize as hex.
            // the hex is the source of truth, so the default round-trips byte-exact and is omitted.
            internal const string DefaultColorIdleHazeHex = "9E9678FF";
            internal const string DefaultColorCleanBurnHex = "737373FF";
            internal const string DefaultColorHeavySootHex = "0D0D0DFF";
            internal const string DefaultColorWetStackHex = "FFFFF2FF";
            internal const string DefaultColorOilBurnHex = "7085D9FF";

            [XmlIgnore] public Color ColorIdleHaze = ColorHex.Parse(DefaultColorIdleHazeHex);
            [XmlIgnore] public Color ColorCleanBurn = ColorHex.Parse(DefaultColorCleanBurnHex);
            [XmlIgnore] public Color ColorHeavySoot = ColorHex.Parse(DefaultColorHeavySootHex);
            [XmlIgnore] public Color ColorWetStack = ColorHex.Parse(DefaultColorWetStackHex);
            [XmlIgnore] public Color ColorOilBurn = ColorHex.Parse(DefaultColorOilBurnHex);

            [DefaultValue(DefaultColorIdleHazeHex)]
            public string ColorIdleHazeHex
            {
                get => ColorHex.Format(ColorIdleHaze);
                set => ColorIdleHaze = ColorHex.Parse(value);
            }

            [DefaultValue(DefaultColorCleanBurnHex)]
            public string ColorCleanBurnHex
            {
                get => ColorHex.Format(ColorCleanBurn);
                set => ColorCleanBurn = ColorHex.Parse(value);
            }

            [DefaultValue(DefaultColorHeavySootHex)]
            public string ColorHeavySootHex
            {
                get => ColorHex.Format(ColorHeavySoot);
                set => ColorHeavySoot = ColorHex.Parse(value);
            }

            [DefaultValue(DefaultColorWetStackHex)]
            public string ColorWetStackHex
            {
                get => ColorHex.Format(ColorWetStack);
                set => ColorWetStack = ColorHex.Parse(value);
            }

            [DefaultValue(DefaultColorOilBurnHex)]
            public string ColorOilBurnHex
            {
                get => ColorHex.Format(ColorOilBurn);
                set => ColorOilBurn = ColorHex.Parse(value);
            }

            internal const float DefaultCleanMinHeatAlpha = 0.015f;
            internal const float DefaultCleanMaxHeatAlpha = 0.08f;
            internal const float DefaultCleanBurnHeat = 0.2f;
            internal const float DefaultSootOnsetLambda = 1.3f;
            internal const float DefaultSootOpaqueLambda = 1.05f;
            internal const float DefaultSootCurveExponent = 1.1f;
            internal const float DefaultSootMaxAlpha = 0.95f;
            internal const float DefaultWetStackFillHeat = 0.1f;
            internal const float DefaultWetStackReleaseHeat = 0.15f;
            internal const float DefaultWetStackFillRate = 0.005f;
            internal const float DefaultWetStackReleaseRate = 0.75f;
            internal const float DefaultWetStackMistStrength = 4f;
            internal const float DefaultWetStackMaxAlpha = 0.95f;
            internal const float DefaultOilTintStrength = 0.3f;
            internal const float DefaultOilRpmExponent = 2.5f;

            [DefaultValue(DefaultCleanMinHeatAlpha)]
            public float CleanMinHeatAlpha = DefaultCleanMinHeatAlpha;

            [DefaultValue(DefaultCleanMaxHeatAlpha)]
            public float CleanMaxHeatAlpha = DefaultCleanMaxHeatAlpha;

            [DefaultValue(DefaultCleanBurnHeat)]
            public float CleanBurnHeat = DefaultCleanBurnHeat;

            [DefaultValue(DefaultSootOnsetLambda)]
            public float SootOnsetLambda = DefaultSootOnsetLambda;

            [DefaultValue(DefaultSootOpaqueLambda)]
            public float SootOpaqueLambda = DefaultSootOpaqueLambda;

            [DefaultValue(DefaultSootCurveExponent)]
            public float SootCurveExponent = DefaultSootCurveExponent;

            [DefaultValue(DefaultSootMaxAlpha)]
            public float SootMaxAlpha = DefaultSootMaxAlpha;

            [DefaultValue(DefaultWetStackFillHeat)]
            public float WetStackFillHeat = DefaultWetStackFillHeat;

            [DefaultValue(DefaultWetStackReleaseHeat)]
            public float WetStackReleaseHeat = DefaultWetStackReleaseHeat;

            [DefaultValue(DefaultWetStackFillRate)]
            public float WetStackFillRate = DefaultWetStackFillRate;

            [DefaultValue(DefaultWetStackReleaseRate)]
            public float WetStackReleaseRate = DefaultWetStackReleaseRate;

            [DefaultValue(DefaultWetStackMistStrength)]
            public float WetStackMistStrength = DefaultWetStackMistStrength;

            [DefaultValue(DefaultWetStackMaxAlpha)]
            public float WetStackMaxAlpha = DefaultWetStackMaxAlpha;

            [DefaultValue(DefaultOilTintStrength)]
            public float OilTintStrength = DefaultOilTintStrength;

            [DefaultValue(DefaultOilRpmExponent)]
            public float OilRpmExponent = DefaultOilRpmExponent;

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

                CleanMinHeatAlpha = other.CleanMinHeatAlpha;
                CleanMaxHeatAlpha = other.CleanMaxHeatAlpha;
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
            /// Validates the model: any settings that violate invariants are adjusted.
            /// </summary>
            public void Validate()
            {
                const float epsilon = 0.01f;

                CleanMinHeatAlpha = Mathf.Clamp01(CleanMinHeatAlpha);
                CleanMaxHeatAlpha = Mathf.Clamp01(CleanMaxHeatAlpha);
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

                SootOnsetLambda = Mathf.Max(SootOnsetLambda, SootOpaqueLambda + epsilon);
                SootOpaqueLambda = Mathf.Min(SootOpaqueLambda, SootOnsetLambda - epsilon);
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

            var baseAlpha = Mathf.Lerp(s.CleanMinHeatAlpha, s.CleanMaxHeatAlpha, heat);

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
        internal const float DefaultIdle = 1.5f;
        internal const float DefaultFullLoad = 15f;

        [DefaultValue(DefaultIdle)]
        public float Idle = DefaultIdle;

        [DefaultValue(DefaultFullLoad)]
        public float FullLoad = DefaultFullLoad;

        public ExhaustVelocitySettings()
        {
        }

        public ExhaustVelocitySettings(ExhaustVelocitySettings other)
        {
            Idle = other.Idle;
            FullLoad = other.FullLoad;
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