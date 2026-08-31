using System;
using UnityEngine;
using UnityEngine.Rendering.PostProcessing;

namespace TurboTurbo;

/// <summary>
/// Route A2: our own post effect wrapping the game's shipped SCPE refraction
/// shader. The SCPE renderer's BlitFullscreenTriangle mis-scales in DV's
/// deferred+TAA setup; a plain Blit fixes it. Both paths are A/B-able via
/// the useFullscreenTriangle parameter.
/// </summary>
[Serializable]
[PostProcess(typeof(HeatHazeEffectRenderer), PostProcessEvent.BeforeStack, "TurboTurbo/HeatHaze", true)]
public sealed class HeatHazeEffect : PostProcessEffectSettings
{
    public TextureParameter hazeTex = new TextureParameter { value = null };
    public FloatParameter amount = new FloatParameter { value = 1f };
    public BoolParameter useFullscreenTriangle = new BoolParameter { value = false };

    public override bool IsEnabledAndSupported(PostProcessRenderContext context)
    {
        return enabled.value && amount.value > 0f && hazeTex.value != null;
    }
}

public sealed class HeatHazeEffectRenderer : PostProcessEffectRenderer<HeatHazeEffect>
{
    private Shader _shader;
    private Material _material;
    private bool _loggedMode;

    public override void Init()
    {
        _shader = Shader.Find("Hidden/SC Post Effects/Refraction");
        if (_shader != null)
        {
            _material = new Material(_shader) { name = "TurboTurbo.HeatHazeMat", hideFlags = HideFlags.HideAndDontSave };
        }
        HeatShimmer.LogEffectInit(_shader != null);
    }

    public override void Release()
    {
        if (_material != null)
        {
            UnityEngine.Object.Destroy(_material);
            _material = null;
        }
        base.Release();
    }

    public override void Render(PostProcessRenderContext context)
    {
        if (_shader == null)
        {
            if (!_loggedMode)
            {
                _loggedMode = true;
                HeatShimmer.LogBlitMode(false);
            }
            return;
        }

        bool fullscreen = settings.useFullscreenTriangle.value;

        if (fullscreen)
        {
            // SCPE-original path (known zoom issue in DV's deferred+TAA setup)
            PropertySheet sheet = context.propertySheets.Get(_shader);
            sheet.properties.SetFloat("_Amount", settings.amount);
            if (settings.hazeTex.value != null)
            {
                sheet.properties.SetTexture("_RefractionTex", settings.hazeTex);
            }
            context.command.BlitFullscreenTriangle(context.source, context.destination, sheet, 0);
        }
        else
        {
            // fixed path: standard fullscreen quad via the built-in Blit,
            // which sets _MainTex and honours the material's own passes
            _material.SetFloat("_Amount", settings.amount);
            if (settings.hazeTex.value != null)
            {
                _material.SetTexture("_RefractionTex", settings.hazeTex);
            }
            context.command.Blit(context.source, context.destination, _material, 0);
        }

        if (!_loggedMode)
        {
            _loggedMode = true;
            HeatShimmer.LogBlitMode(fullscreen);
        }
    }
}
