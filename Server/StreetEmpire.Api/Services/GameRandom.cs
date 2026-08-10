namespace StreetEmpire.Api.Services;

public interface IGameRandom
{
    int NextInclusive(int min, int max);
    double NextDouble();
}

public sealed class GameRandom : IGameRandom
{
    public int NextInclusive(int min, int max)
    {
        if (min > max)
            throw new ArgumentOutOfRangeException(nameof(min), "Minimum cannot be greater than maximum.");

        return Random.Shared.Next(min, max + 1);
    }

    public double NextDouble() => Random.Shared.NextDouble();
}
