using System.Globalization;

namespace RedisVL.Queries;

/// <summary>
/// Describes how the text (<c>SEARCH</c>) and vector (<c>VSIM</c>) branches of a native
/// <c>FT.HYBRID</c> query are fused into a single ranked result set.
/// </summary>
/// <remarks>
/// When no combination method is supplied to a <see cref="HybridSearchQuery" />, Redis applies its
/// default Reciprocal Rank Fusion (RRF) with a window of 20 and a constant of 60.
/// </remarks>
public abstract class HybridCombinationMethod
{
    private protected HybridCombinationMethod()
    {
    }

    internal abstract void AppendTo(List<object> arguments);
}

/// <summary>
/// Fuses the text and vector branches with a weighted linear combination
/// (<c>COMBINE LINEAR ALPHA &lt;alpha&gt; BETA &lt;beta&gt;</c>).
/// </summary>
public sealed class LinearHybridCombination : HybridCombinationMethod
{
    /// <summary>
    /// Initializes a new instance of the <see cref="LinearHybridCombination" /> class.
    /// </summary>
    /// <param name="alpha">The weight applied to the text (<c>SEARCH</c>) score.</param>
    /// <param name="beta">The weight applied to the vector (<c>VSIM</c>) score.</param>
    /// <param name="window">The optional ranking window considered from each branch.</param>
    public LinearHybridCombination(double alpha, double beta, int? window = null)
    {
        if (!double.IsFinite(alpha))
        {
            throw new ArgumentOutOfRangeException(nameof(alpha), alpha, "Alpha must be a finite number.");
        }

        if (!double.IsFinite(beta))
        {
            throw new ArgumentOutOfRangeException(nameof(beta), beta, "Beta must be a finite number.");
        }

        if (window is <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(window), window, "Window must be greater than zero.");
        }

        Alpha = alpha;
        Beta = beta;
        Window = window;
    }

    /// <summary>Gets the weight applied to the text (<c>SEARCH</c>) score.</summary>
    public double Alpha { get; }

    /// <summary>Gets the weight applied to the vector (<c>VSIM</c>) score.</summary>
    public double Beta { get; }

    /// <summary>Gets the optional ranking window considered from each branch.</summary>
    public int? Window { get; }

    internal override void AppendTo(List<object> arguments)
    {
        var clause = new List<object>
        {
            "ALPHA",
            Alpha.ToString("G", CultureInfo.InvariantCulture),
            "BETA",
            Beta.ToString("G", CultureInfo.InvariantCulture)
        };

        if (Window is int window)
        {
            clause.Add("WINDOW");
            clause.Add(window.ToString(CultureInfo.InvariantCulture));
        }

        arguments.Add("COMBINE");
        arguments.Add("LINEAR");
        arguments.Add(clause.Count.ToString(CultureInfo.InvariantCulture));
        arguments.AddRange(clause);
    }
}

/// <summary>
/// Fuses the text and vector branches with Reciprocal Rank Fusion
/// (<c>COMBINE RRF [CONSTANT &lt;constant&gt;] [WINDOW &lt;window&gt;]</c>).
/// </summary>
/// <remarks>
/// At least one of <see cref="Constant" /> or <see cref="Window" /> must be supplied. To use the
/// server defaults, pass no combination method to the <see cref="HybridSearchQuery" /> instead.
/// </remarks>
public sealed class ReciprocalRankFusionHybridCombination : HybridCombinationMethod
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ReciprocalRankFusionHybridCombination" /> class.
    /// </summary>
    /// <param name="constant">The optional RRF constant.</param>
    /// <param name="window">The optional ranking window considered from each branch.</param>
    public ReciprocalRankFusionHybridCombination(int? constant = null, int? window = null)
    {
        if (constant is <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(constant), constant, "Constant must be greater than zero.");
        }

        if (window is <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(window), window, "Window must be greater than zero.");
        }

        if (constant is null && window is null)
        {
            throw new ArgumentException(
                "Specify at least one of constant or window. To use the server defaults, omit the combination method.",
                nameof(constant));
        }

        Constant = constant;
        Window = window;
    }

    /// <summary>Gets the optional RRF constant.</summary>
    public int? Constant { get; }

    /// <summary>Gets the optional ranking window considered from each branch.</summary>
    public int? Window { get; }

    internal override void AppendTo(List<object> arguments)
    {
        var clause = new List<object>();
        if (Constant is int constant)
        {
            clause.Add("CONSTANT");
            clause.Add(constant.ToString(CultureInfo.InvariantCulture));
        }

        if (Window is int window)
        {
            clause.Add("WINDOW");
            clause.Add(window.ToString(CultureInfo.InvariantCulture));
        }

        arguments.Add("COMBINE");
        arguments.Add("RRF");
        arguments.Add(clause.Count.ToString(CultureInfo.InvariantCulture));
        arguments.AddRange(clause);
    }
}
