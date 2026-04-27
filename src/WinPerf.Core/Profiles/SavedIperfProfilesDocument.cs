namespace WinPerf.Core.Profiles;

public sealed record SavedIperfProfilesDocument
{
    public int SchemaVersion { get; init; } = 1;
    public Guid? DefaultProfileId { get; init; }
    public Guid? LastSelectedProfileId { get; init; }
    public List<SavedIperfProfile> Profiles { get; init; } = new();

    public SavedIperfProfile? ResolveLastSelectedProfile()
    {
        return ResolveProfile(LastSelectedProfileId)
               ?? ResolveDefaultProfile()
               ?? Profiles.FirstOrDefault();
    }

    public SavedIperfProfile? ResolveDefaultProfile()
    {
        return ResolveProfile(DefaultProfileId)
               ?? Profiles.FirstOrDefault();
    }

    public SavedIperfProfile? ResolveProfile(Guid? profileId)
    {
        return profileId is Guid id
            ? Profiles.FirstOrDefault(profile => profile.Id == id)
            : null;
    }
}
