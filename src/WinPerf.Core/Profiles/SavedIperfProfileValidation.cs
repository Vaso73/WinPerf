namespace WinPerf.Core.Profiles;

public static class SavedIperfProfileValidation
{
    public static IReadOnlyList<string> Validate(SavedIperfProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        var errors = new List<string>();

        if (profile.Id == Guid.Empty)
        {
            errors.Add("Profile id must not be empty.");
        }

        if (string.IsNullOrWhiteSpace(profile.Name))
        {
            errors.Add("Profile name is required.");
        }

        if (!Enum.IsDefined(profile.RunMode))
        {
            errors.Add("Run mode is invalid.");
        }

        if (!Enum.IsDefined(profile.Protocol))
        {
            errors.Add("Protocol is invalid.");
        }

        if (!Enum.IsDefined(profile.AddressFamily))
        {
            errors.Add("Address family is invalid.");
        }

        if (profile.RunMode == SavedIperfRunMode.Client && string.IsNullOrWhiteSpace(profile.Server))
        {
            errors.Add("Client profiles require a server address.");
        }

        if (profile.Port is < 1 or > 65535)
        {
            errors.Add("Port must be between 1 and 65535.");
        }

        if (profile.Streams < 1)
        {
            errors.Add("Streams must be at least 1.");
        }

        if (profile.DurationSeconds < 1)
        {
            errors.Add("Duration must be at least 1 second.");
        }

        if (profile.ReportIntervalSeconds is <= 0)
        {
            errors.Add("Report interval must be empty or a positive number.");
        }

        if (profile.OmitSeconds is < 0)
        {
            errors.Add("Omit seconds must be empty, zero, or a positive number.");
        }

        if (profile.ClientPort is < 1 or > 65535)
        {
            errors.Add("Client port must be empty or between 1 and 65535.");
        }

        if (!string.IsNullOrWhiteSpace(profile.TcpMss)
            && (!int.TryParse(profile.TcpMss.Trim(), out var tcpMss) || tcpMss < 1))
        {
            errors.Add("TCP MSS must be empty or a positive number.");
        }

        if (profile.Reverse && profile.Bidirectional)
        {
            errors.Add("Reverse and bidirectional cannot be enabled together.");
        }

        if (profile.Bidirectional && profile.Protocol != SavedIperfProtocol.Tcp)
        {
            errors.Add("Bidirectional mode is only supported for TCP profiles.");
        }

        if (profile.Protocol == SavedIperfProtocol.Udp && string.IsNullOrWhiteSpace(profile.UdpBandwidth))
        {
            errors.Add("UDP profiles require a bandwidth value.");
        }

        if (string.IsNullOrWhiteSpace(profile.ReportFormat))
        {
            errors.Add("Report format is required.");
        }

        return errors;
    }

    public static IReadOnlyList<string> Validate(SavedIperfProfilesDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var errors = new List<string>();

        if (document.SchemaVersion != 1)
        {
            errors.Add($"Unsupported saved profiles schema version: {document.SchemaVersion}.");
        }

        if (document.Profiles is null)
        {
            errors.Add("Profiles collection is required.");
            return errors;
        }

        for (var i = 0; i < document.Profiles.Count; i++)
        {
            var profile = document.Profiles[i];

            if (profile is null)
            {
                errors.Add($"Profile at index {i} must not be null.");
                continue;
            }

            errors.AddRange(Validate(profile).Select(error => $"Profile '{profile.Name}' [{i}]: {error}"));
        }

        var duplicateIds = document.Profiles
            .Where(profile => profile is not null && profile.Id != Guid.Empty)
            .GroupBy(profile => profile.Id)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToArray();

        foreach (var duplicateId in duplicateIds)
        {
            errors.Add($"Duplicate profile id: {duplicateId}.");
        }

        return errors;
    }

    public static void ThrowIfInvalid(SavedIperfProfile profile)
    {
        var errors = Validate(profile);

        if (errors.Count > 0)
        {
            throw new InvalidDataException(string.Join(Environment.NewLine, errors));
        }
    }

    public static void ThrowIfInvalid(SavedIperfProfilesDocument document)
    {
        var errors = Validate(document);

        if (errors.Count > 0)
        {
            throw new InvalidDataException(string.Join(Environment.NewLine, errors));
        }
    }
}
