namespace TurboTurbo.Configuration;

/// <summary>A drawable block in the profile editor.</summary>
internal interface IEditorPanel
{
    /// <summary>Whether the panel is expanded. Persisted across rebuilds.</summary>
    bool Open { get; set; }

    void Draw();
}