namespace TurboTurbo;

/// <summary>Why a loco profile or engine configuration was rejected.</summary>
public record struct ValidationError(string Message)
{
    public override string ToString() => Message;
}