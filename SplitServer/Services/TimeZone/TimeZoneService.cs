using CSharpFunctionalExtensions;
using SplitServer.Models;

namespace SplitServer.Services.TimeZone;

public class TimeZoneService
{
    private readonly List<string> _zoneFile = File.ReadLines("Resources/zone1970.tab").ToList();

    public Result<Coordinates> CreateCoordinatesFromTimeZone(string timeZone)
    {
        foreach (var line in _zoneFile)
        {
            if (!line.Contains(timeZone, StringComparison.InvariantCulture))
            {
                continue;
            }

            var dmsCoords = line.Split('\t')[1];

            var match = RegexPatterns.Dms().Match(dmsCoords);

            if (!match.Success)
            {
                return Result.Failure<Coordinates>($"Failed to parse dms coordinates in {line}");
            }

            return new Coordinates
            {
                Latitude = (double)CovertDmsToDecimal(match.Groups[1].Value),
                Longitude = (double)CovertDmsToDecimal(match.Groups[2].Value)
            };
        }

        return Result.Failure<Coordinates>($"Time zone {timeZone} was not recognized");
    }

    private static decimal CovertDmsToDecimal(string coord)
    {
        var sign = coord[0];
        var numPart = coord[1..];
        decimal degrees, minutes, seconds = 0m;

        if (numPart.Length == 7)
        {
            degrees = decimal.Parse(numPart[..3]);
            minutes = decimal.Parse(numPart.Substring(3, 2));
            seconds = decimal.Parse(numPart.Substring(5, 2));
        }
        else if (numPart.Length == 6)
        {
            degrees = decimal.Parse(numPart[..2]);
            minutes = decimal.Parse(numPart.Substring(2, 2));
            seconds = decimal.Parse(numPart.Substring(4, 2));
        }
        else if (numPart.Length == 5)
        {
            degrees = decimal.Parse(numPart[..3]);
            minutes = decimal.Parse(numPart.Substring(3, 2));
        }
        else if (numPart.Length == 4)
        {
            degrees = decimal.Parse(numPart[..2]);
            minutes = decimal.Parse(numPart.Substring(2, 2));
        }
        else
        {
            throw new ArgumentException($"Invalid coordinate format: {coord}");
        }

        var result = degrees + minutes / 60m + seconds / 3600m;

        var resultDecimal = sign == '+' ? result : -result;

        return Math.Round(resultDecimal, 5);
    }
}