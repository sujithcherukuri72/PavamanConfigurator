namespace PavamanDroneConfigurator.Infrastructure.Services;

/// <summary>
/// Implements the Largest Triangle Three Buckets (LTTB) algorithm for time-series downsampling.
/// This preserves visual fidelity while reducing point count for efficient rendering.
/// Reference: https://skemman.is/bitstream/1946/15343/3/SS_MSthesis.pdf
/// </summary>
public static class LttbDecimation
{
    /// <summary>
    /// Downsamples a time series using the LTTB algorithm.
    /// </summary>
    /// <param name="times">Array of timestamps</param>
    /// <param name="values">Array of values corresponding to timestamps</param>
    /// <param name="targetPointCount">Desired number of points in output</param>
    /// <returns>Tuple of (decimated times, decimated values)</returns>
    public static (double[] Times, double[] Values) Decimate(
        double[] times,
        double[] values,
        int targetPointCount)
    {
        if (times.Length != values.Length)
            throw new ArgumentException("Times and values arrays must have the same length");

        var inputLength = times.Length;

        // If data is already small enough, return as-is
        if (inputLength <= targetPointCount || targetPointCount < 3)
        {
            return (times.ToArray(), values.ToArray());
        }

        var resultTimes = new double[targetPointCount];
        var resultValues = new double[targetPointCount];

        // Always keep first point
        resultTimes[0] = times[0];
        resultValues[0] = values[0];

        // Always keep last point
        resultTimes[targetPointCount - 1] = times[inputLength - 1];
        resultValues[targetPointCount - 1] = values[inputLength - 1];

        // Bucket size for middle points
        var bucketSize = (double)(inputLength - 2) / (targetPointCount - 2);

        int previousSelectedIndex = 0;

        for (int i = 1; i < targetPointCount - 1; i++)
        {
            // Calculate bucket boundaries
            var bucketStart = (int)Math.Floor((i - 1) * bucketSize) + 1;
            var bucketEnd = (int)Math.Floor(i * bucketSize) + 1;
            bucketEnd = Math.Min(bucketEnd, inputLength - 1);

            // Calculate average of next bucket for triangle area calculation
            var nextBucketStart = (int)Math.Floor(i * bucketSize) + 1;
            var nextBucketEnd = (int)Math.Floor((i + 1) * bucketSize) + 1;
            nextBucketEnd = Math.Min(nextBucketEnd, inputLength);

            double avgX = 0, avgY = 0;
            var nextBucketCount = nextBucketEnd - nextBucketStart;
            if (nextBucketCount > 0)
            {
                for (int j = nextBucketStart; j < nextBucketEnd; j++)
                {
                    avgX += times[j];
                    avgY += values[j];
                }
                avgX /= nextBucketCount;
                avgY /= nextBucketCount;
            }
            else
            {
                avgX = times[inputLength - 1];
                avgY = values[inputLength - 1];
            }

            // Find point in current bucket that forms largest triangle
            var maxArea = -1.0;
            var maxIndex = bucketStart;

            var pointAX = times[previousSelectedIndex];
            var pointAY = values[previousSelectedIndex];

            for (int j = bucketStart; j < bucketEnd; j++)
            {
                // Calculate triangle area using cross product
                var area = Math.Abs(
                    (pointAX - avgX) * (values[j] - pointAY) -
                    (pointAX - times[j]) * (avgY - pointAY)
                ) * 0.5;

                if (area > maxArea)
                {
                    maxArea = area;
                    maxIndex = j;
                }
            }

            resultTimes[i] = times[maxIndex];
            resultValues[i] = values[maxIndex];
            previousSelectedIndex = maxIndex;
        }

        return (resultTimes, resultValues);
    }

    /// <summary>
    /// Downsamples using min/max preservation per bucket.
    /// Useful for preserving peaks and valleys at the cost of 2x points per bucket.
    /// </summary>
    public static (double[] Times, double[] Values) DecimateMinMax(
        double[] times,
        double[] values,
        int targetPointCount)
    {
        if (times.Length != values.Length)
            throw new ArgumentException("Times and values arrays must have the same length");

        var inputLength = times.Length;

        if (inputLength <= targetPointCount)
        {
            return (times.ToArray(), values.ToArray());
        }

        // Each bucket produces 2 points (min and max), so we need half the buckets
        var bucketCount = targetPointCount / 2;
        var bucketSize = (double)inputLength / bucketCount;

        var resultList = new List<(double Time, double Value)>();

        for (int i = 0; i < bucketCount; i++)
        {
            var start = (int)(i * bucketSize);
            var end = (int)((i + 1) * bucketSize);
            end = Math.Min(end, inputLength);

            if (start >= end) continue;

            var minIdx = start;
            var maxIdx = start;
            var minVal = values[start];
            var maxVal = values[start];

            for (int j = start + 1; j < end; j++)
            {
                if (values[j] < minVal)
                {
                    minVal = values[j];
                    minIdx = j;
                }
                if (values[j] > maxVal)
                {
                    maxVal = values[j];
                    maxIdx = j;
                }
            }

            // Add min and max in time order
            if (minIdx <= maxIdx)
            {
                resultList.Add((times[minIdx], values[minIdx]));
                if (minIdx != maxIdx)
                    resultList.Add((times[maxIdx], values[maxIdx]));
            }
            else
            {
                resultList.Add((times[maxIdx], values[maxIdx]));
                resultList.Add((times[minIdx], values[minIdx]));
            }
        }

        return (
            resultList.Select(p => p.Time).ToArray(),
            resultList.Select(p => p.Value).ToArray()
        );
    }

    /// <summary>
    /// Performs progressive decimation for large datasets.
    /// Returns increasingly refined results suitable for progressive rendering.
    /// </summary>
    public static IEnumerable<(double[] Times, double[] Values)> DecimateProgressive(
        double[] times,
        double[] values,
        int[] progressiveCounts)
    {
        // Sort counts ascending
        var sortedCounts = progressiveCounts.OrderBy(c => c).ToArray();

        foreach (var count in sortedCounts)
        {
            yield return Decimate(times, values, count);
        }
    }

    /// <summary>
    /// Calculates statistics for a data series.
    /// </summary>
    public static (double Min, double Max, double Mean, double StdDev) CalculateStatistics(double[] values)
    {
        if (values.Length == 0)
            return (0, 0, 0, 0);

        var min = double.MaxValue;
        var max = double.MinValue;
        var sum = 0.0;

        foreach (var v in values)
        {
            if (v < min) min = v;
            if (v > max) max = v;
            sum += v;
        }

        var mean = sum / values.Length;

        // Calculate standard deviation
        var sumSquares = 0.0;
        foreach (var v in values)
        {
            var diff = v - mean;
            sumSquares += diff * diff;
        }
        var stdDev = Math.Sqrt(sumSquares / values.Length);

        return (min, max, mean, stdDev);
    }
}
