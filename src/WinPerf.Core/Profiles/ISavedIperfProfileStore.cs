namespace WinPerf.Core.Profiles;

public interface ISavedIperfProfileStore
{
    Task<SavedIperfProfilesDocument> LoadAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(SavedIperfProfilesDocument document, CancellationToken cancellationToken = default);
}
