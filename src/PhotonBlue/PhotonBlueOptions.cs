using PhotonBlue.Persistence;

namespace PhotonBlue;

public class PhotonBlueOptions
{
    public Func<IGameFileIndex>? IndexProvider { get; init; }

    public static PhotonBlueOptions Default => new()
    {
        IndexProvider = null,
    };
}