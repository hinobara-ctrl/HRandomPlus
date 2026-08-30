using HRandomPlus.Core;
using HRandomPlus.Integration.Beatmaps;
using HRandomPlus.Integration.Tosu;
using HRandomPlus.Integration.Lazer;

namespace HRandomPlus.Desktop.Platform;

internal static partial class PlatformSourceFactory
{
    public static partial IBeatmapSource Create(AppSettings settings)
    {
        var http = new HttpClient();
        return new ArbitratingBeatmapSource(
            new TosuBeatmapSource(
                new TosuClient(http, settings.TosuHost, settings.TosuPort, disposeHttpClient: true),
                new BeatmapPathResolver(),
                () => settings.LinuxOsuPath),
            new LazerCurrentBeatmapSource());
    }
}
