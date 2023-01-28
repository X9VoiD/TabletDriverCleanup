using System.Diagnostics;

namespace PnpUtil;

public record PnpUtilDriver(
    string DriverName,
    string? OriginalName,
    string ProviderName,
    string ClassName,
    string ClassGUID,
    string? ClassVersion,
    string DriverVersion,
    string? SignerName,
    string? MatchingDeviceID,
    string? DriverRank,
    string? DriverStatus
) : IPnpUtilParseable<PnpUtilDriver>
{
    public static PnpUtilDriver Parse(string[] lines, int startingIndex, int endingIndex, out int linesParsed)
    {
        var i = startingIndex;

        string? driverName = null;
        string? originalName = null;
        string? providerName = null;
        string? className = null;
        string? classGuid = null;
        string? classVersion = null;
        string? driverVersion = null;
        string? signerName = null;
        string? matchingDeviceId = null;
        string? driverRank = null;
        string? driverStatus = null;

        var regex = StringExtensions.GetPropertyRegex();
        Debug.WriteLine($"Start of {nameof(PnpUtilDriver)}");

        for (; i < lines.Length && i <= endingIndex; i++)
        {
            var line = lines[i];

            if (string.IsNullOrEmpty(line))
                break;

            Debug.WriteLine($"Parsing: {line}");

            var match = regex.Match(line);
            Debug.Assert(match.Success);
            var prop = match.Groups["prop"].Value;
            var value = match.Groups["value"];

            switch (prop)
            {
                case "Driver Name" or "Published Name":
                    driverName = value.Value;
                    break;
                case "Original Name":
                    originalName = value.Value;
                    break;
                case "Provider Name":
                    providerName = value.Value;
                    break;
                case "Class Name":
                    className = value.Value;
                    break;
                case "Class GUID":
                    classGuid = value.Value;
                    break;
                case "Class Version":
                    classVersion = value.Value;
                    break;
                case "Driver Version":
                    driverVersion = value.Value;
                    break;
                case "Signer Name":
                    signerName = value.Value;
                    break;
                case "Matching Device ID":
                    matchingDeviceId = value.Value;
                    break;
                case "Driver Rank":
                    driverRank = value.Value;
                    break;
                case "Driver Status":
                    driverStatus = value.Value;
                    break;
                default:
                    throw new Exception($"Unknown property '{prop}'");
            }
        }

        linesParsed = i - startingIndex;
        Debug.WriteLine($"End of {nameof(PnpUtilDriver)}, parsed {linesParsed} lines");

        return new PnpUtilDriver(
            driverName ?? throw new InvalidOperationException("Driver Name is null"),
            originalName,
            providerName ?? throw new InvalidOperationException("Provider Name is null"),
            className ?? throw new InvalidOperationException("Class Name is null"),
            classGuid ?? throw new InvalidOperationException("Class GUID is null"),
            classVersion,
            driverVersion ?? throw new InvalidOperationException("Driver Version is null"),
            signerName,
            matchingDeviceId,
            driverRank,
            driverStatus
        );
    }
}