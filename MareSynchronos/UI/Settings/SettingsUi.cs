using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using MareSynchronos.API.Data;
using MareSynchronos.FileCache;
using MareSynchronos.Interop.Ipc;
using MareSynchronos.MareConfiguration;
using MareSynchronos.PlayerData.Handlers;
using MareSynchronos.PlayerData.Pairs;
using MareSynchronos.Services;
using MareSynchronos.Services.Mediator;
using MareSynchronos.Services.ServerConfiguration;
using MareSynchronos.UI;
using MareSynchronos.Utils;
using MareSynchronos.WebAPI;
using MareSynchronos.WebAPI.Files;
using MareSynchronos.WebAPI.Files.Models;
using MareSynchronos.WebAPI.SignalR.Utils;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Globalization;
using System.Net.Http;
using System.Numerics;
using System.Reflection;

namespace Aurora.UI.Settings;

public partial class SettingsUi : WindowMediatorSubscriberBase
{
    private readonly ApiController _apiController;
    private readonly CacheMonitor _cacheMonitor;
    private readonly MareConfigService _configService;
    private readonly ConcurrentDictionary<GameObjectHandler, Dictionary<string, FileDownloadStatus>> _currentDownloads = new();
    private readonly DalamudUtilService _dalamudUtilService;
    private readonly HttpClient _httpClient;
    private readonly FileCacheManager _fileCacheManager;
    private readonly FileCompactor _fileCompactor;
    private readonly FileUploadManager _fileTransferManager;
    private readonly FileTransferOrchestrator _fileTransferOrchestrator;
    private readonly IpcManager _ipcManager;
    private readonly PairManager _pairManager;
    private readonly PerformanceCollectorService _performanceCollector;
    private readonly PlayerPerformanceConfigService _playerPerformanceConfigService;
    private readonly AccountRegistrationService _registerService;
    private readonly ServerConfigurationManager _serverConfigurationManager;
    private readonly UiSharedService _uiShared;
    private CancellationTokenSource _secretKeysConversionCts = new CancellationTokenSource();
    private CancellationTokenSource? _speedTestCts;
    private readonly IProgress<(int, int, FileCacheEntity)> _validationProgress;
    private (int, int, FileCacheEntity) _currentProgress;
    private bool _deleteAccountPopupModalShown = false;
    private bool _deleteFilesPopupModalShown = false;
    private bool? _notesSuccessfullyApplied = null;
    private bool _overwriteExistingLabels = false;
    private bool _readClearCache = false;
    private int _selectedEntry = -1;
    private string _uidToAddForIgnore = string.Empty;
    private CancellationTokenSource? _validationCts;
    private Task<List<FileCacheEntity>>? _validationTask;
    private bool _wasOpen = false;


    private const float SidebarWidth = 160f;
    private sealed record TabEntry(string Title, ushort Index, string? Parent, DrawElementDelegate Render);
    private readonly List<TabEntry> _tabs = [];
    private Dictionary<string, List<TabEntry>> _tabsByParent = new(StringComparer.Ordinal);
    private ushort _selectedTab = 0;

    private delegate void DrawElementDelegate();

    [AttributeUsage(AttributeTargets.Method)]
    public sealed class SettingsTabElementAttribute : Attribute
    {
        public string Title { get; }
        public ushort Index { get; }
        public string? Parent { get; }

        public SettingsTabElementAttribute(string title, ushort index, string? parent = null)
        {
            Title = title;
            Index = index;
            Parent = parent;
        }
    }

    public SettingsUi(ILogger<SettingsUi> logger,
        UiSharedService uiShared, MareConfigService configService,
        PairManager pairManager,
        ServerConfigurationManager serverConfigurationManager,
        PlayerPerformanceConfigService playerPerformanceConfigService,
        MareMediator mediator, PerformanceCollectorService performanceCollector,
        FileUploadManager fileTransferManager,
        FileTransferOrchestrator fileTransferOrchestrator,
        FileCacheManager fileCacheManager,
        FileCompactor fileCompactor, ApiController apiController,
        IpcManager ipcManager, CacheMonitor cacheMonitor,
        DalamudUtilService dalamudUtilService, HttpClient httpClient, AccountRegistrationService registerService) : base(logger, mediator, "Aurora Client Settings", performanceCollector)
    {
        _configService = configService;
        _pairManager = pairManager;
        _serverConfigurationManager = serverConfigurationManager;
        _playerPerformanceConfigService = playerPerformanceConfigService;
        _performanceCollector = performanceCollector;
        _fileTransferManager = fileTransferManager;
        _fileTransferOrchestrator = fileTransferOrchestrator;
        _fileCacheManager = fileCacheManager;
        _apiController = apiController;
        _ipcManager = ipcManager;
        _cacheMonitor = cacheMonitor;
        _dalamudUtilService = dalamudUtilService;
        _httpClient = httpClient;
        _registerService = registerService;
        _fileCompactor = fileCompactor;
        _uiShared = uiShared;
        AllowClickthrough = false;
        AllowPinning = false;
        _validationProgress = new Progress<(int, int, FileCacheEntity)>(v => _currentProgress = v);

        SizeConstraints = new WindowSizeConstraints()
        {
            MinimumSize = new Vector2(800, 400),
            MaximumSize = new Vector2(800, 2000),
        };

        Mediator.Subscribe<OpenSettingsUiMessage>(this, (_) => Toggle());
        Mediator.Subscribe<SwitchToIntroUiMessage>(this, (_) => IsOpen = false);
        Mediator.Subscribe<CutsceneStartMessage>(this, (_) => UiSharedService_GposeStart());
        Mediator.Subscribe<CutsceneEndMessage>(this, (_) => UiSharedService_GposeEnd());
        Mediator.Subscribe<CharacterDataCreatedMessage>(this, (msg) => LastCreatedCharacterData = msg.CharacterData);
        Mediator.Subscribe<DownloadStartedMessage>(this, (msg) => _currentDownloads[msg.DownloadId] = msg.DownloadStatus);
        Mediator.Subscribe<DownloadFinishedMessage>(this, (msg) => _currentDownloads.TryRemove(msg.DownloadId, out _));
        RegisterSettingsTabElements();
    }

    public CharacterData? LastCreatedCharacterData { private get; set; }
    private ApiController ApiController => _uiShared.ApiController;

    public override void OnOpen()
    {
        _uiShared.ResetOAuthTasksState();
        _speedTestCts = new();
    }

    public override void OnClose()
    {
        _uiShared.EditTrackerPosition = false;
        _uidToAddForIgnore = string.Empty;
        _secretKeysConversionCts = _secretKeysConversionCts.CancelRecreate();
        _downloadServersTask = null;
        _speedTestTask = null;
        _speedTestCts?.Cancel();
        _speedTestCts?.Dispose();
        _speedTestCts = null;

        base.OnClose();
    }

    protected override void DrawInternal()
    {
        _ = _uiShared.DrawOtherPluginState();

        DrawSettingsContent();
    }

    /// <summary>
    /// Register all known Settings Elements </br>
    /// Registered via: [SettingsTabElement("Name", id, "Optional Parent")]
    /// </summary>
    private void RegisterSettingsTabElements()
    {
        foreach (var m in GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
        {
            var attr = m.GetCustomAttribute<SettingsTabElementAttribute>();
            if (attr == null) continue;

            var del = (DrawElementDelegate)Delegate.CreateDelegate(typeof(DrawElementDelegate), this, m);
            _tabs.Add(new TabEntry(attr.Title, attr.Index, attr.Parent, del));
        }

        _tabsByParent = _tabs
            .OrderBy(t => t.Index)
            .GroupBy(t => t.Parent ?? string.Empty, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.Ordinal);
    }

    private void DrawSettingsContent()
    {
        if (_apiController.ServerState is ServerState.Connected)
        {
            ImGui.TextUnformatted("Service " + _serverConfigurationManager.CurrentServer.ServerName + ":");
            ImGui.SameLine();
            ImGui.TextColored(ImGuiColors.ParsedGreen, "Available");
            ImGui.SameLine();
            ImGui.TextUnformatted("(");
            ImGui.SameLine();
            ImGui.TextColored(ImGuiColors.ParsedGreen, _apiController.OnlineUsers.ToString(CultureInfo.InvariantCulture));
            ImGui.SameLine();
            ImGui.TextUnformatted("Users Online");
            ImGui.SameLine();
            ImGui.TextUnformatted(")");
        }

        ImGui.AlignTextToFramePadding();
        ImGui.Separator();
        var avail = ImGui.GetContentRegionAvail();

        using (var sidebar = ImRaii.Child("settingsSidebar", new Vector2(SidebarWidth * ImGuiHelpers.GlobalScale, avail.Y), true))
        {
            if (sidebar)
                DrawTabTree(string.Empty);
        }

        ImGui.SameLine();

        using (var content = ImRaii.Child("settingsContent", new Vector2(0, avail.Y), false))
        {
            if (content)
                _tabs.Find(t => t.Index == _selectedTab)?.Render();
        }
    }

    private void DrawTabTree(string parent)
    {
        if (!_tabsByParent.TryGetValue(parent, out var entries)) return;

        foreach (var tab in entries)
        {
            bool hasChildren = _tabsByParent.ContainsKey(tab.Title);

            var flags = ImGuiTreeNodeFlags.SpanAvailWidth | ImGuiTreeNodeFlags.OpenOnArrow | ImGuiTreeNodeFlags.DefaultOpen;
            if (!hasChildren)
                flags |= ImGuiTreeNodeFlags.Leaf | ImGuiTreeNodeFlags.NoTreePushOnOpen;
            if (_selectedTab == tab.Index)
                flags |= ImGuiTreeNodeFlags.Selected;

            bool open = ImGui.TreeNodeEx($"{tab.Title}##tab{tab.Index}", flags);

            if (ImGui.IsItemClicked() && !ImGui.IsItemToggledOpen())
                _selectedTab = tab.Index;

            if (hasChildren && open)
            {
                DrawTabTree(tab.Title);
                ImGui.TreePop();
            }
        }
    }

    private void UiSharedService_GposeEnd()
    {
        IsOpen = _wasOpen;
    }

    private void UiSharedService_GposeStart()
    {
        _wasOpen = IsOpen;
        IsOpen = false;
    }
}