using HRandomPlus.Core;
using HRandomPlus.Integration.Beatmaps;

namespace HRandomPlus.Desktop.Platform;

internal static partial class PlatformSourceFactory
{
    public static partial IBeatmapSource Create(AppSettings settings);
}
