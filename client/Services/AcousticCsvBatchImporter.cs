using System.Globalization;
using SmartEnergyExpert.Client.Data;
using SmartEnergyExpert.Client.Entities;

namespace SmartEnergyExpert.Client.Services;

/// <summary>Parses hydroacoustic CSV rows and appends <see cref="AcousticSample"/> with batched saves.</summary>
public static class AcousticCsvBatchImporter
{
    public const int SaveBatchRows = 2_500;

    public static async Task<int> ImportIntoDatasetAsync(
        AppDbContext dbContext,
        Dataset dataset,
        string csvContent,
        CancellationToken cancellationToken = default)
    {
        var imported = 0;
        var pendingFlush = 0;
        var lines = csvContent.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();
            if (line.Length == 0)
            {
                continue;
            }

            if (line.StartsWith("timestamp", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var cells = line.Split(',', StringSplitOptions.TrimEntries);
            if (cells.Length < 7)
            {
                continue;
            }

            if (!DateTimeOffset.TryParse(cells[0], CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var timestamp))
            {
                continue;
            }

            if (!decimal.TryParse(cells[1], CultureInfo.InvariantCulture, out var frequencyBand) ||
                !decimal.TryParse(cells[2], CultureInfo.InvariantCulture, out var amplitudeDb) ||
                !decimal.TryParse(cells[3], CultureInfo.InvariantCulture, out var depthMeters) ||
                !decimal.TryParse(cells[4], CultureInfo.InvariantCulture, out var rangeMeters))
            {
                continue;
            }

            decimal? soundSpeed = decimal.TryParse(cells[5], CultureInfo.InvariantCulture, out var speed) ? speed : null;
            decimal? noiseLevel = decimal.TryParse(cells[6], CultureInfo.InvariantCulture, out var noise) ? noise : null;

            dbContext.AcousticSamples.Add(new AcousticSample
            {
                DatasetId = dataset.Id,
                Timestamp = timestamp,
                FrequencyBand = frequencyBand,
                AmplitudeDb = amplitudeDb,
                DepthMeters = depthMeters,
                RangeMeters = rangeMeters,
                SoundSpeed = soundSpeed,
                NoiseLevelDb = noiseLevel
            });

            if (dataset.TimeRangeStart == default || timestamp < dataset.TimeRangeStart)
            {
                dataset.TimeRangeStart = timestamp;
            }

            if (dataset.TimeRangeEnd == default || timestamp > dataset.TimeRangeEnd)
            {
                dataset.TimeRangeEnd = timestamp;
            }

            imported++;
            pendingFlush++;

            if (pendingFlush >= SaveBatchRows)
            {
                await dbContext.SaveChangesAsync(cancellationToken);
                pendingFlush = 0;
            }
        }

        dataset.UpdatedAt = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        return imported;
    }
}
