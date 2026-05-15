using Brio.API;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin;
using MareSynchronos.API.Dto.CharaData;
using MareSynchronos.Services;
using Microsoft.Extensions.Logging;
using System.Numerics;
using System.Text.Json.Nodes;

namespace MareSynchronos.Interop.Ipc;

public sealed class IpcCallerBrio : IIpcCaller
{
    private readonly ILogger<IpcCallerBrio> _logger;
    private readonly DalamudUtilService _dalamudUtilService;

    private readonly ApiVersion _apiVersion;
    private readonly SpawnActor _brioSpawnActorAsync;
    private readonly DespawnActor _brioDespawnActor;
    private readonly SetModelTransform _brioSetModelTransform;
    private readonly GetModelTransform _brioGetModelTransform;
    private readonly GetPoseAsJson _brioGetPoseAsJson;
    private readonly LoadPoseFromJson _brioSetPoseFromJson;
    private readonly FreezeActor _brioFreezeActor;
    private readonly FreezePhysics _brioFreezePhysics;


    public bool APIAvailable { get; private set; }

    public IpcCallerBrio(ILogger<IpcCallerBrio> logger, IDalamudPluginInterface dalamudPluginInterface,
        DalamudUtilService dalamudUtilService)
    {
        _logger = logger;
        _dalamudUtilService = dalamudUtilService;

        _apiVersion = new ApiVersion(dalamudPluginInterface);
        _brioSpawnActorAsync = new SpawnActor(dalamudPluginInterface);
        _brioDespawnActor = new DespawnActor(dalamudPluginInterface);
        _brioSetModelTransform = new SetModelTransform(dalamudPluginInterface);
        _brioGetModelTransform = new GetModelTransform(dalamudPluginInterface);
        _brioGetPoseAsJson = new GetPoseAsJson(dalamudPluginInterface);
        _brioSetPoseFromJson = new LoadPoseFromJson(dalamudPluginInterface);
        _brioFreezeActor = new FreezeActor(dalamudPluginInterface);
        _brioFreezePhysics = new FreezePhysics(dalamudPluginInterface);

        CheckAPI();
    }

    public void CheckAPI()
    {
        try
        {
            var version = _apiVersion.Invoke();
            APIAvailable = (version.Item1 == 3 && version.Item2 >= 0);
        }
        catch
        {
            APIAvailable = false;
        }
    }

    public async Task<IGameObject?> SpawnActorAsync()
    {
        if (!APIAvailable) return null;
        _logger.LogDebug("Spawning Brio Actor");
        return await _dalamudUtilService.RunOnFrameworkThread(() => _brioSpawnActorAsync.Invoke(Brio.API.Enums.SpawnFlags.Default, spawnFrozen: true)).ConfigureAwait(false);
    }

    public async Task<bool> DespawnActorAsync(nint address)
    {
        if (!APIAvailable) return false;
        var gameObject = await _dalamudUtilService.CreateGameObjectAsync(address).ConfigureAwait(false);
        if (gameObject == null) return false;
        _logger.LogDebug("Despawning Brio Actor {actor}", gameObject.Name.TextValue);
        return await _dalamudUtilService.RunOnFrameworkThread(() => _brioDespawnActor.Invoke(gameObject)).ConfigureAwait(false);
    }

    public async Task<bool> ApplyTransformAsync(nint address, WorldData data)
    {
        if (!APIAvailable) return false;
        var gameObject = await _dalamudUtilService.CreateGameObjectAsync(address).ConfigureAwait(false);
        if (gameObject == null) return false;
        _logger.LogDebug("Applying Transform to Actor {actor}", gameObject.Name.TextValue);

        return await _dalamudUtilService.RunOnFrameworkThread(() =>
            _brioSetModelTransform.Invoke(gameObject,
            new Vector3(data.PositionX, data.PositionY, data.PositionZ),
            new Quaternion(data.RotationX, data.RotationY, data.RotationZ, data.RotationW),
            new Vector3(data.ScaleX, data.ScaleY, data.ScaleZ), relativeMode: false)).ConfigureAwait(false);
    }

    public async Task<WorldData> GetTransformAsync(nint address)
    {
        if (!APIAvailable) return default;
        var gameObject = await _dalamudUtilService.CreateGameObjectAsync(address).ConfigureAwait(false);
        if (gameObject == null) return default;
        var data = await _dalamudUtilService.RunOnFrameworkThread(() => _brioGetModelTransform.Invoke(gameObject)).ConfigureAwait(false);
        //_logger.LogDebug("Getting Transform from Actor {actor}", gameObject.Name.TextValue);
        
        if (data.Item1 == null || data.Item2 == null || data.Item3 == null) 
            return default;
        
        return new WorldData()
        {
            PositionX = data.Item1.Value.X,
            PositionY = data.Item1.Value.Y,
            PositionZ = data.Item1.Value.Z,
            RotationX = data.Item2.Value.X,
            RotationY = data.Item2.Value.Y,
            RotationZ = data.Item2.Value.Z,
            RotationW = data.Item2.Value.W,
            ScaleX = data.Item3.Value.X,
            ScaleY = data.Item3.Value.Y,
            ScaleZ = data.Item3.Value.Z
        };
    }

    public async Task<string?> GetPoseAsync(nint address)
    {
        if (!APIAvailable) return null;
        var gameObject = await _dalamudUtilService.CreateGameObjectAsync(address).ConfigureAwait(false);
        if (gameObject == null) return null;
        _logger.LogDebug("Getting Pose from Actor {actor}", gameObject.Name.TextValue);

        return await _dalamudUtilService.RunOnFrameworkThread(() => _brioGetPoseAsJson.Invoke(gameObject)).ConfigureAwait(false);
    }

    public async Task<bool> SetPoseAsync(nint address, string pose)
    {
        if (!APIAvailable) return false;
        var gameObject = await _dalamudUtilService.CreateGameObjectAsync(address).ConfigureAwait(false);
        if (gameObject == null) return false;
        _logger.LogDebug("Setting Pose to Actor {actor}", gameObject.Name.TextValue);

        var applicablePose = JsonNode.Parse(pose)!;
        var currentPose = await _dalamudUtilService.RunOnFrameworkThread(() => _brioGetPoseAsJson.Invoke(gameObject)).ConfigureAwait(false);
        if (currentPose == null) return false;

        applicablePose["ModelDifference"] = JsonNode.Parse(JsonNode.Parse(currentPose)!["ModelDifference"]!.ToJsonString());
        await _dalamudUtilService.RunOnFrameworkThread(() =>
        {
            _brioFreezeActor.Invoke(gameObject);
            _brioFreezePhysics.Invoke();
        }).ConfigureAwait(false);
        return await _dalamudUtilService.RunOnFrameworkThread(() => _brioSetPoseFromJson.Invoke(gameObject, applicablePose.ToJsonString(), isLegacyCMToolPose: false)).ConfigureAwait(false);
    }

    public void Dispose()
    {
    }
}
