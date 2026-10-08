using System.ComponentModel;

namespace TurboTurbo.Profiles.Storage.V1;

public sealed class StackXml
{
    internal const float DefaultFillTime = 120f;
    internal const float DefaultClearTime = 60f;
    internal const float DefaultTauWall = 35f;
    internal const float DefaultFlowWarmBias = 0.25f;
    internal const float DefaultCaptureColdK = 420f;
    internal const float DefaultCaptureHotK = 520f;
    internal const float DefaultClearStartK = 540f;
    internal const float DefaultClearFullK = 660f;
    internal const float DefaultContactIdle = 0.7f;
    internal const float DefaultContactFullFlow = 0.15f;
    internal const float DefaultStickMax = 0.9f;
    internal const float DefaultReferenceUnburned = 0.0045f;

    [DefaultValue(DefaultFillTime)]
    public float FillTime { get; set; } = DefaultFillTime;

    [DefaultValue(DefaultClearTime)]
    public float ClearTime { get; set; } = DefaultClearTime;

    [DefaultValue(DefaultTauWall)]
    public float TauWall { get; set; } = DefaultTauWall;

    [DefaultValue(DefaultFlowWarmBias)]
    public float FlowWarmBias { get; set; } = DefaultFlowWarmBias;

    [DefaultValue(DefaultCaptureColdK)]
    public float CaptureColdK { get; set; } = DefaultCaptureColdK;

    [DefaultValue(DefaultCaptureHotK)]
    public float CaptureHotK { get; set; } = DefaultCaptureHotK;

    [DefaultValue(DefaultClearStartK)]
    public float ClearStartK { get; set; } = DefaultClearStartK;

    [DefaultValue(DefaultClearFullK)]
    public float ClearFullK { get; set; } = DefaultClearFullK;

    [DefaultValue(DefaultContactIdle)]
    public float ContactIdle { get; set; } = DefaultContactIdle;

    [DefaultValue(DefaultContactFullFlow)]
    public float ContactFullFlow { get; set; } = DefaultContactFullFlow;

    [DefaultValue(DefaultStickMax)]
    public float StickMax { get; set; } = DefaultStickMax;

    [DefaultValue(DefaultReferenceUnburned)]
    public float ReferenceUnburned { get; set; } = DefaultReferenceUnburned;
}
