namespace TurboTurbo;

/// <summary>
/// A non-exceptional error. Should be used over exceptions in places where
/// failure is the expected outcome, such as in parsing or validation.
/// </summary>
internal record struct Error(string Message)
{
    public override string ToString() => Message;
}
