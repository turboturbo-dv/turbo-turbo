using UnityEngine;

namespace TurboTurbo.Modeling
{
    /// <summary>
    /// Exhaust smoke appearance model: determines smoke color
    /// and density from engine state (lambda, throttle demand, rpm). Layered:
    /// idle haze, oil blowby, lambda-driven soot, wet-stack burn-off.
    ///
    /// Engine off -> clear color, zero density (guard).
    /// </summary>
    public class ExhaustSmokeModel
    {
        // Color palette: yellowish/straw tones for worn ex-Yugoslav stock.
        // Internal so tests can reference the palette instead of hardcoding rgb.
        internal static readonly Color ColorIdleHaze = new Color(0.62f, 0.59f, 0.51f, 0.22f);  // aged warm grey-tan (sulfurous idle gas)
        internal static readonly Color ColorHeavySoot = new Color(0.05f, 0.05f, 0.05f, 0.95f); // deep black charcoal
        internal static readonly Color ColorWetStack = new Color(0.85f, 0.82f, 0.68f, 0.85f);  // straw vapor (unburned fuel droplets)
        internal static readonly Color ColorOilBurn = new Color(0.44f, 0.52f, 0.55f, 0.50f);   // dull blue-grey oil burn

        // ---- tuning constants ----

        /// <summary>Demand below which unburned fuel accumulates (wet stacking);
        /// also the demand where the burn-off ramp begins.</summary>
        public const float WetStackIdleDemand = 0.1f;

        /// <summary>Accumulator fill rate [1/s] while idling.</summary>
        public const float WetStackFillRate = 0.08f;

        /// <summary>The accumulator must exceed this before burn-off becomes visible.</summary>
        public const float WetStackBurnThreshold = 0.05f;

        /// <summary>Demand above which the wet-stack burn produces straw puffs.</summary>
        public const float WetStackBurnDemand = 0.15f;

        /// <summary>Burn-off rate [1/s] per unit demand.</summary>
        public const float WetStackBurnRate = 0.75f;

        /// <summary>Demand at which the burn-off ramp reaches full strength
        /// (ramps up from WetStackIdleDemand).</summary>
        public const float WetStackBurnRampDemand = 0.5f;

        /// <summary>Max blend toward the oil-burn color, reached at full rpm.</summary>
        public const float OilBlowbyTintStrength = 0.25f;

        /// <summary>Gamma shaping the soot ladder over the lambda deficit.</summary>
        public const float SootCurveExponent = 1.1f;

        /// <summary>Puff opacity range: haze floor .. soot ceiling.</summary>
        public const float AlphaFloor = 0.05f;
        public const float AlphaCeiling = 0.95f;

        /// <summary>How strongly the wet-stack burn pushes opacity toward the ceiling.</summary>
        public const float WetStackAlphaScale = 0.8f;

        private float _wetStackAccumulator;

        /// <summary>Lambda thresholds (injectable so the model works outside the
        /// mod project; the mod can wire these from its own configuration).</summary>
        public float SootOnsetLambda = 0.75f;
        public float SootOpaqueLambda = 0.35f;

        /// <summary>Smoke particle color, rgb + opacity. Authoritative.</summary>
        public Color Color { get; private set; } = Color.clear;

        /// <summary>Smoke density 0..1 (soot + wet-stack burn-off) - scales
        /// emission rate. Idle haze is expressed via color, not density.</summary>
        public float Density { get; private set; }

        public void Update(float lambda, float demand, float rpmNorm, bool engineOn, float delta)
        {
            if (!engineOn)
            {
                Color = Color.clear;
                Density = 0f;
                return;
            }

            // 1. wet stacking: unburned fuel accumulates at idle, burns off
            //    under load (straw-colored puffs when the throttle opens)
            if (demand < WetStackIdleDemand)
            {
                _wetStackAccumulator = Mathf.Min(1f, _wetStackAccumulator + delta * WetStackFillRate);
            }
            else
            {
                _wetStackAccumulator = Mathf.Max(0f, _wetStackAccumulator - delta * demand * WetStackBurnRate);
            }

            // 2. base: warm, yellowish-tinted idle haze (never completely clear)
            Color current = ColorIdleHaze;

            // 3. oil blowby layer, scaling with engine speed
            current = Color.Lerp(current, ColorOilBurn, OilBlowbyTintStrength * rpmNorm);

            // 4. soot layer: onset at SootOnsetLambda, opaque at SootOpaqueLambda
            float sootFactor = Mathf.Clamp01(
                (SootOnsetLambda - lambda) / (SootOnsetLambda - SootOpaqueLambda));
            sootFactor = Mathf.Pow(sootFactor, SootCurveExponent);
            current = Color.Lerp(current, ColorHeavySoot, sootFactor);

            // 5. wet-stack straw vapor puffs when the throttle opens after idling
            float wetBurn = 0f;
            if (_wetStackAccumulator > WetStackBurnThreshold && demand > WetStackBurnDemand)
            {
                float ramp = Mathf.Clamp01(
                    (demand - WetStackIdleDemand) / (WetStackBurnRampDemand - WetStackIdleDemand));
                wetBurn = _wetStackAccumulator * ramp;
                current = Color.Lerp(current, ColorWetStack, wetBurn);
            }

            // 6. opacity: haze floor up to near-opaque soot / wet-stack burn
            //    cloud. Gated on wetBurn (the demand-gated burn-off), NOT the
            //    raw accumulator: idling never densifies the exhaust.
            current.a = Mathf.Lerp(AlphaFloor, AlphaCeiling, Mathf.Max(sootFactor, wetBurn * WetStackAlphaScale));

            Color = current;
            Density = Mathf.Max(sootFactor, wetBurn);
        }
    }
}
