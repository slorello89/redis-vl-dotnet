namespace RedisVlDotNet.Queries;

public sealed record VectorKnnRuntimeOptions
{
    public VectorKnnRuntimeOptions(int? efRuntime = null)
    {
        if (efRuntime is <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(efRuntime), efRuntime, "EF runtime must be greater than zero.");
        }

        EfRuntime = efRuntime;
    }

    public int? EfRuntime { get; }
}

public sealed record VectorRangeRuntimeOptions
{
    public VectorRangeRuntimeOptions(double? epsilon = null)
    {
        if (epsilon is < 0d)
        {
            throw new ArgumentOutOfRangeException(nameof(epsilon), epsilon, "Epsilon cannot be negative.");
        }

        if (epsilon is not null && (double.IsNaN(epsilon.Value) || double.IsInfinity(epsilon.Value)))
        {
            throw new ArgumentOutOfRangeException(nameof(epsilon), epsilon, "Epsilon must be a finite value.");
        }

        Epsilon = epsilon;
    }

    public double? Epsilon { get; }
}
