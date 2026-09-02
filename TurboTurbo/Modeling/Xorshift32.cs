namespace TurboTurbo.Modeling;

/// <summary>
/// Deterministic, allocation-free xorshift32 PRNG for the audio thread.
/// </summary>
internal struct Xorshift32
{
    private uint _state;

    public Xorshift32(uint seed)
    {
        _state = seed == 0 ? 2463534242u : seed;
    }

    public double NextUnit()
    {
        uint x = _state;
        x ^= x << 13;
        x ^= x >> 17;
        x ^= x << 5;
        _state = x;
        return (x >> 8) * (1.0 / 16777216.0); // top 24 bits -> [0,1)
    }
}