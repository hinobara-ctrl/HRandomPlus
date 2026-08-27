using System.Text.Json;

namespace HRandomPlus.Integration.Tosu;

public sealed record TosuResult(TosuSnapshot? Snapshot, string Status, bool IsAvailable)
{
    public bool Success => Snapshot is not null;
}

public sealed class TosuClient : IDisposable
{
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromMilliseconds(750);
    private readonly HttpClient httpClient;
    private readonly bool disposeHttpClient;

    public TosuClient(HttpClient httpClient, string host = "127.0.0.1", int port = 24050, bool disposeHttpClient = false)
    {
        this.httpClient = httpClient;
        this.disposeHttpClient = disposeHttpClient;
        if (httpClient.BaseAddress is null)
            httpClient.BaseAddress = new UriBuilder("http", host, port).Uri;
        if (httpClient.Timeout == Timeout.InfiniteTimeSpan || httpClient.Timeout == TimeSpan.FromSeconds(100))
            httpClient.Timeout = DefaultTimeout;
    }

    public async Task<TosuResult> GetCurrentAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using HttpResponseMessage response = await httpClient.GetAsync("json/v2", cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
                return new TosuResult(null, $"tosu respondió HTTP {(int)response.StatusCode}", true);
            string json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            TosuSnapshot snapshot = TosuSnapshot.Parse(json);
            if (string.IsNullOrWhiteSpace(snapshot.Beatmap.OsuFileName))
                return new TosuResult(null, "tosu está conectado, pero todavía no informa un beatmap", true);
            return new TosuResult(snapshot, "tosu connected", true);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new TosuResult(null, "tosu no respondió dentro del tiempo límite", false);
        }
        catch (HttpRequestException ex)
        {
            return new TosuResult(null, $"tosu no está disponible: {ex.Message}", false);
        }
        catch (JsonException ex)
        {
            return new TosuResult(null, $"tosu devolvió JSON no válido: {ex.Message}", true);
        }
    }

    public void Dispose()
    {
        if (disposeHttpClient) httpClient.Dispose();
    }
}
