using System.Collections.Immutable;
using System.Diagnostics;

namespace PnpUtil;

public partial record PnpUtilDevice(
    string InstanceID,
    string DeviceDescription,
    string ClassName,
    string ClassGUID,
    string ManufacturerName,
    string Status,
    string? BusEnumeratorName,
    Guid? BusTypeGuid,
    string? DriverName,
    string? Parent,
    ImmutableArray<string> Children,
    ImmutableArray<PnpUtilDriver> MatchingDrivers,
    string? ProblemCode,
    string? ProblemStatus
) : IPnpUtilParseable<PnpUtilDevice>
{
    public bool HasBusInformation => BusEnumeratorName != null;
    public bool HasProblem => !string.IsNullOrWhiteSpace(ProblemCode);
    public bool HasChildren => Children.Length > 0;

    public static PnpUtilDevice Parse(string[] lines, int startingIndex, int endingIndex, out int linesParsed)
    {
        var i = startingIndex;

        string? instanceId = null;
        string? deviceDescription = null;
        string? className = null;
        string? classGuid = null;
        string? manufacturerName = null;
        string? status = null;
        string? busEnumeratorName = null;
        Guid? busTypeGuid = null;
        string? driverName = null;
        string? parent = null;
        var children = ImmutableArray.CreateBuilder<string>();
        var matchingDrivers = ImmutableArray<PnpUtilDriver>.Empty;
        string? problemCode = null;
        string? problemStatus = null;

        var regex = StringExtensions.GetPropertyRegex();
        Debug.WriteLine($"Start of {nameof(PnpUtilDevice)}");

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
                case "Instance ID":
                    instanceId = value.Value;
                    break;
                case "Device Description":
                    deviceDescription = value.Value;
                    break;
                case "Class Name":
                    className = value.Value;
                    break;
                case "Class GUID":
                    classGuid = value.Value;
                    break;
                case "Manufacturer Name":
                    manufacturerName = value.Value;
                    break;
                case "Status":
                    status = value.Value;
                    break;
                case "Bus Enumerator Name":
                    busEnumeratorName = value.Value;
                    break;
                case "Bus Type GUID":
                    busTypeGuid = Guid.Parse(value.Value);
                    break;
                case "Driver Name":
                    driverName = value.Value;
                    break;
                case "Parent":
                    parent = value.Value;
                    break;
                case "Children":
                    children.Add(value.Value);
                    var childrenScopeEnd = lines.DelimitScope(++i);
                    for (; i < childrenScopeEnd; i++)
                    {
                        var childLine = lines[i];
                        if (string.IsNullOrWhiteSpace(childLine))
                            break;
                        children.Add(lines[i].Trim());
                    }
                    break;
                case "Matching Drivers":
                    var driverScopeEnd = lines.DelimitScope(++i);
                    Debug.Assert(matchingDrivers.IsDefaultOrEmpty);
                    matchingDrivers = IPnpUtilParseable<PnpUtilDriver>.ParseEnumerable(lines, i, driverScopeEnd, out var driverLinesParsed);
                    i += driverLinesParsed - 2; // compensate for the for loop increment and the empty line
                    break;
                case "Problem Code":
                    problemCode = value.Value;
                    break;
                case "Problem Status":
                    problemStatus = value.Value;
                    break;
                default:
                    throw new InvalidOperationException($"Unknown property: {prop}");
            }
        }

        linesParsed = i - startingIndex;
        Debug.WriteLine($"End of {nameof(PnpUtilDevice)}, parsed {linesParsed} lines");

        return new PnpUtilDevice(
            instanceId ?? throw new InvalidOperationException("Instance ID is null"),
            deviceDescription ?? throw new InvalidOperationException("Device Description is null"),
            className ?? throw new InvalidOperationException("Class Name is null"),
            classGuid ?? throw new InvalidOperationException("Class GUID is null"),
            manufacturerName ?? throw new InvalidOperationException("Manufacturer Name is null"),
            status ?? throw new InvalidOperationException("Status is null"),
            busEnumeratorName,
            busTypeGuid,
            driverName,
            parent,
            children.ToImmutable(),
            matchingDrivers,
            problemCode,
            problemStatus
        );
    }
}