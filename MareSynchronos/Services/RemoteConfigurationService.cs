using Chaos.NaCl;
using MareSynchronos.MareConfiguration;
using MareSynchronos.Services.ServerConfiguration;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace MareSynchronos.Services;

public sealed class RemoteConfigurationService
{
    private readonly ILogger<RemoteConfigurationService> _logger;
    private readonly RemoteConfigCacheService _configService;
    private readonly ServerConfigurationManager _serverConfiguration;
    private readonly Task _initTask;

    public RemoteConfigurationService(ILogger<RemoteConfigurationService> logger, RemoteConfigCacheService configService, ServerConfigurationManager serverManager)
    {
        _logger = logger;
        _configService = configService;
        _serverConfiguration = serverManager;
        _initTask = Task.Run(DownloadConfig);
    }

    public async Task<JsonObject> GetConfigAsync(string sectionName)
    {
        await _initTask.ConfigureAwait(false);
        if (!_configService.Current.Configuration.TryGetPropertyValue(sectionName, out var section))
            section = null;
        return (section as JsonObject) ?? new();
    }

    public async Task<T?> GetConfigAsync<T>(string sectionName)
    {
        try
        {
            var json = await GetConfigAsync(sectionName).ConfigureAwait(false);
            return JsonSerializer.Deserialize<T>(json);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Invalid JSON in remote config: {sectionName}", sectionName);
            return default;
        }
    }

    private async Task DownloadConfig()
    {
        // Removed Lop's remote config code. Function exists purely to keep things clean.
        //and it's useless, why not delete?
        LoadConfig();
        
    }

    private static bool VerifySignature(string message, ulong ts, string signature, string pubKey)
    {
        byte[] msg = [.. BitConverter.GetBytes(ts), .. Encoding.UTF8.GetBytes(message)];
        byte[] sig = Convert.FromBase64String(signature);
        byte[] pub = Convert.FromBase64String(pubKey);
        return Ed25519.Verify(sig, msg, pub);
    }

    //Hardcoded config will never fail... nice...
    private void LoadConfig()
    {
        ulong ts = (ulong)((DateTimeOffset)DateTime.UtcNow).ToUnixTimeSeconds();
        string server = _serverConfiguration.CurrentServer.ServerUri.TrimEnd('/');
        var configString = "{\"mainServer\":{\"api_url\":\""+ server + "/\",\"hub_url\":\""+ server + "/mare\"},\"noSnap\":{\"listOfPlugins\":[\"Meddle.Plugin\"]}}";

        _configService.Current.Configuration = JsonNode.Parse(configString)!.AsObject();
        _configService.Current.Timestamp = ts;
        _configService.Save();
    }
}
