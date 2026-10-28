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
            internal const float DefaultWetStackMaxAlpha = 0.4f;
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
                OilTintStrength = Mathf.Clamp01(OilTintStrength);

                CleanBurnHeat = Mathf.Max(epsilon, CleanBurnHeat);
                SootCurveExponent = Mathf.Max(epsilon, SootCurveExponent);
                SootPowerExponent = Mathf.Max(epsilon, SootPowerExponent);
                SootIncreaseTau = Mathf.Max(epsilon, SootIncreaseTau);
                SootDecreaseTau = Mathf.Max(epsilon, SootDecreaseTau);
                OilRpmExponent = Mathf.Max(epsilon, OilRpmExponent);

                SootOnsetLambda = Mathf.Max(SootOnsetLambda, SootOpaqueLambda + epsilon);
                SootOpaqueLambda = Mathf.Min(SootOpaqueLambda, SootOnsetLambda - epsilon);
            }
        }

        private float _soot;

        public Settings Tuning { get; set; } = new Settings();

        public Color Color { get; private set; } = Color.clear;

        /// <summary>
        /// Share of the exhaust gases and particulates that leave through this exhasut (1 = 100%).
        /// </summary>
        public float ExhaustShare { get; set; } = 1f;

        /// <summary>
        /// Sum of all particulate mass fractions leaving this exhaust per unit time.
        /// </summary>
        public float ParticulateMass { get; private set; }

        /// <summary>
        /// Upper bound of <see cref="ParticulateMass"/> derived from the current tuning and exhaust share.
        /// As long as neither changes, ParticulateMass is guaranteed to never exceed this.
        /// </summary>
        public float MaxParticulateMass => Tuning.Density * ExhaustShare
            * (Tuning.CleanMaxHeatAlpha + Tuning.WetStackMaxAlpha + Tuning.SootMaxAlpha);

        public void Update(float lambda, float rpmNorm, float heat, bool engineOn, float delta, float vapour = 0f)
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

            var wetAlpha = s.WetStackMaxAlpha * vapour;
            var totalWeight = baseAlpha + wetAlpha + sootAlpha;
            var finalColor = (baseColor * baseAlpha
                              + s.ColorWetStack * wetAlpha
                              + s.ColorHeavySoot * sootAlpha) / totalWeight;

            // unused as ParticulateMass encodes density, but set it to 1 for good practice
            finalColor.a = 1f;

            Color = finalColor;
            ParticulateMass = s.Density * ExhaustShare * totalWeight;
        }
    }
}
