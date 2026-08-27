using HRandomPlus.Core;
using HRandomPlus.Integration.Beatmaps;
using HRandomPlus.Integration.Tosu;

namespace HRandomPlus.Desktop.Platform;

internal static partial class PlatformSourceFactory
{
    public static partial IBeatmapSource Create(AppSettings settings)
    {
        var http = new HttpClient { Timeout = TimeSpan.FromMilliseconds(750) };
        return new TosuBeatmapSource(
            new TosuClient(http, settings.TosuHost, settings.TosuPort),
            new BeatmapPathResolver(),
            () => settings.LinuxOsuPath);
    }
}
