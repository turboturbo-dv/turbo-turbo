using UnityEngine;

namespace TurboTurbo.Modeling
{
    /// <summary>
    /// Standalone exhaust smoke appearance model: fully determines smoke color
    /// and density from engine state (lambda, throttle demand, rpm). Layered:
    /// idle haze, oil blowby, lambda-driven soot, wet-stack burn-off.
    /// One instance per engine (owns the wet-stack accumulator).
    ///
    /// Engine off -> clear color, zero density (guard).
    /// </summary>
    public class ExhaustSmokeModel
    {
        // Color palette: yellowish/straw tones for worn ex-Yugoslav stock
        private static readonly Color ColorIdleHaze = new Color(0.62f, 0.59f, 0.51f, 0.22f);  // aged warm grey-tan (sulfurous idle gas)
        private static readonly Color ColorHeavySoot = new Color(0.05f, 0.05f, 0.05f, 0.95f); // deep black charcoal
        private static readonly Color ColorWetStack = new Color(0.85f, 0.82f, 0.68f, 0.85f);  // straw vapor (unburned fuel droplets)
        private static readonly Color ColorOilBurn = new Color(0.44f, 0.52f, 0.55f, 0.50f);   // dull blue-grey oil burn

        private float _wetStackAccumulator;

        /// <summary>Lambda thresholds (injectable so the model works outside the
        /// mod project; the mod wires these from TurboConfig).</summary>
        public float SootOnsetLambda = 0.85f;
        public float SootOpaqueLambda = 0.45f;

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
            if (demand < 0.1f)
            {
                _wetStackAccumulator = Mathf.Min(1f, _wetStackAccumulator + delta * 0.08f);
            }
            else
            {
                _wetStackAccumulator = Mathf.Max(0f, _wetStackAccumulator - delta * demand * 0.75f);
            }

            // 2. base: warm, yellowish-tinted idle haze (never completely clear)
            Color current = ColorIdleHaze;

            // 3. oil blowby layer, scaling with engine speed
            current = Color.Lerp(current, ColorOilBurn, 0.25f * rpmNorm);

            // 4. soot layer: onset at sootOnsetLambda, opaque at sootOpaqueLambda
            float sootFactor = Mathf.Clamp01(
                (SootOnsetLambda - lambda) / (SootOnsetLambda - SootOpaqueLambda));
            sootFactor = Mathf.Pow(sootFactor, 1.1f);
            current = Color.Lerp(current, ColorHeavySoot, sootFactor);

            // 5. wet-stack straw vapor puffs when the throttle opens after idling
            float wetBurn = 0f;
            if (_wetStackAccumulator > 0.05f && demand > 0.15f)
            {
                wetBurn = _wetStackAccumulator * Mathf.Clamp01((demand - 0.1f) / 0.4f);
                current = Color.Lerp(current, ColorWetStack, wetBurn);
            }

            // 6. opacity: haze floor up to near-opaque soot / wet-stack cloud
            current.a = Mathf.Lerp(0.22f, 0.95f, Mathf.Max(sootFactor, _wetStackAccumulator * 0.8f));

            Color = current;
            Density = Mathf.Max(sootFactor, wetBurn);
        }
    }
}
