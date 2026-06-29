namespace RedisVL.Summarization;

/// <summary>
/// Minimal, dependency-free k-means++ clustering used to pick representative points. It returns the
/// index of the point nearest each non-empty cluster centroid (Euclidean distance).
/// </summary>
internal static class KMeansClustering
{
    public static IReadOnlyList<int> SelectRepresentatives(
        IReadOnlyList<float[]> points,
        int k,
        int maxIterations,
        int seed)
    {
        var n = points.Count;
        k = Math.Min(k, n);
        var dimensions = points[0].Length;
        var random = new Random(seed);

        var data = new double[n][];
        for (var i = 0; i < n; i++)
        {
            var converted = new double[dimensions];
            var source = points[i];
            for (var d = 0; d < dimensions; d++)
            {
                converted[d] = source[d];
            }

            data[i] = converted;
        }

        var centroids = InitializeCentroidsPlusPlus(data, k, dimensions, random);
        var assignments = new int[n];

        for (var iteration = 0; iteration < maxIterations; iteration++)
        {
            var changed = false;
            for (var i = 0; i < n; i++)
            {
                var nearest = NearestCentroid(data[i], centroids);
                if (nearest != assignments[i])
                {
                    assignments[i] = nearest;
                    changed = true;
                }
            }

            RecomputeCentroids(data, assignments, centroids, k, dimensions);

            if (!changed)
            {
                break;
            }
        }

        var representatives = new List<int>(k);
        for (var c = 0; c < k; c++)
        {
            var bestIndex = -1;
            var bestDistance = double.MaxValue;
            for (var i = 0; i < n; i++)
            {
                if (assignments[i] != c)
                {
                    continue;
                }

                var distance = SquaredDistance(data[i], centroids[c]);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    bestIndex = i;
                }
            }

            if (bestIndex >= 0)
            {
                representatives.Add(bestIndex);
            }
        }

        return representatives;
    }

    private static double[][] InitializeCentroidsPlusPlus(double[][] data, int k, int dimensions, Random random)
    {
        var n = data.Length;
        var centroids = new double[k][];
        centroids[0] = (double[])data[random.Next(n)].Clone();

        var closestSquared = new double[n];
        for (var i = 0; i < n; i++)
        {
            closestSquared[i] = SquaredDistance(data[i], centroids[0]);
        }

        for (var c = 1; c < k; c++)
        {
            var total = 0.0;
            for (var i = 0; i < n; i++)
            {
                total += closestSquared[i];
            }

            var target = random.NextDouble() * total;
            var chosen = n - 1;
            var accumulated = 0.0;
            for (var i = 0; i < n; i++)
            {
                accumulated += closestSquared[i];
                if (accumulated >= target)
                {
                    chosen = i;
                    break;
                }
            }

            centroids[c] = (double[])data[chosen].Clone();
            for (var i = 0; i < n; i++)
            {
                var distance = SquaredDistance(data[i], centroids[c]);
                if (distance < closestSquared[i])
                {
                    closestSquared[i] = distance;
                }
            }
        }

        return centroids;
    }

    private static void RecomputeCentroids(double[][] data, int[] assignments, double[][] centroids, int k, int dimensions)
    {
        var sums = new double[k][];
        var counts = new int[k];
        for (var c = 0; c < k; c++)
        {
            sums[c] = new double[dimensions];
        }

        for (var i = 0; i < data.Length; i++)
        {
            var cluster = assignments[i];
            counts[cluster]++;
            var point = data[i];
            var sum = sums[cluster];
            for (var d = 0; d < dimensions; d++)
            {
                sum[d] += point[d];
            }
        }

        for (var c = 0; c < k; c++)
        {
            if (counts[c] == 0)
            {
                // Keep the previous centroid for an empty cluster rather than collapsing it to origin.
                continue;
            }

            for (var d = 0; d < dimensions; d++)
            {
                centroids[c][d] = sums[c][d] / counts[c];
            }
        }
    }

    private static int NearestCentroid(double[] point, double[][] centroids)
    {
        var best = 0;
        var bestDistance = double.MaxValue;
        for (var c = 0; c < centroids.Length; c++)
        {
            var distance = SquaredDistance(point, centroids[c]);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = c;
            }
        }

        return best;
    }

    private static double SquaredDistance(double[] a, double[] b)
    {
        var sum = 0.0;
        for (var i = 0; i < a.Length; i++)
        {
            var diff = a[i] - b[i];
            sum += diff * diff;
        }

        return sum;
    }
}
