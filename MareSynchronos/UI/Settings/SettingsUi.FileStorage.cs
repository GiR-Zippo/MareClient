using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using FFXIVClientStructs.FFXIV.Common.Math;
using MareSynchronos.Services.Mediator;
using MareSynchronos.UI;
using System.IO;

namespace Aurora.UI.Settings;

#pragma warning disable MA0048 // File name must match type name
public partial class SettingsUi : WindowMediatorSubscriberBase
#pragma warning restore MA0048 // File name must match type name
{
    /// <summary>
    /// WhitelistedUIDs Tab : Position 6
    /// </summary>
    [SettingsTabElement("File Storage", 53)]
    private void DrawFileStorageSettings()
    {
        _uiShared.BigText("Storage");

        UiSharedService.TextWrapped("Mare stores downloaded files from paired people permanently. This is to improve loading performance and requiring less downloads. " +
            "The storage governs itself by clearing data beyond the set storage size. Please set the storage size accordingly. It is not necessary to manually clear the storage.");

        _uiShared.DrawFileScanState();
        ImGui.AlignTextToFramePadding();
        ImGui.TextUnformatted("Monitoring Penumbra Folder: " + (_cacheMonitor.PenumbraWatcher?.Path ?? "Not monitoring"));
        if (string.IsNullOrEmpty(_cacheMonitor.PenumbraWatcher?.Path))
        {
            ImGui.SameLine();
            using var id = ImRaii.PushId("penumbraMonitor");
            if (_uiShared.IconTextButton(FontAwesomeIcon.ArrowsToCircle, "Try to reinitialize Monitor"))
            {
                _cacheMonitor.StartPenumbraWatcher(_ipcManager.Penumbra.ModDirectory);
            }
        }

        ImGui.AlignTextToFramePadding();
        ImGui.TextUnformatted("Monitoring Mare Storage Folder: " + (_cacheMonitor.MareWatcher?.Path ?? "Not monitoring"));
        if (string.IsNullOrEmpty(_cacheMonitor.MareWatcher?.Path))
        {
            ImGui.SameLine();
            using var id = ImRaii.PushId("mareMonitor");
            if (_uiShared.IconTextButton(FontAwesomeIcon.ArrowsToCircle, "Try to reinitialize Monitor"))
            {
                _cacheMonitor.StartMareWatcher(_configService.Current.CacheFolder);
            }
        }
        if (_cacheMonitor.MareWatcher == null || _cacheMonitor.PenumbraWatcher == null)
        {
            if (_uiShared.IconTextButton(FontAwesomeIcon.Play, "Resume Monitoring"))
            {
                _cacheMonitor.StartMareWatcher(_configService.Current.CacheFolder);
                _cacheMonitor.StartPenumbraWatcher(_ipcManager.Penumbra.ModDirectory);
                _cacheMonitor.InvokeScan();
            }
            UiSharedService.AttachToolTip("Attempts to resume monitoring for both Penumbra and Mare Storage. "
                + "Resuming the monitoring will also force a full scan to run." + Environment.NewLine
                + "If the button remains present after clicking it, consult /xllog for errors");
        }
        else
        {
            using (ImRaii.Disabled(!UiSharedService.CtrlPressed()))
            {
                if (_uiShared.IconTextButton(FontAwesomeIcon.Stop, "Stop Monitoring"))
                {
                    _cacheMonitor.StopMonitoring();
                }
            }
            UiSharedService.AttachToolTip("Stops the monitoring for both Penumbra and Mare Storage. "
                + "Do not stop the monitoring, unless you plan to move the Penumbra and Mare Storage folders, to ensure correct functionality of Mare." + Environment.NewLine
                + "If you stop the monitoring to move folders around, resume it after you are finished moving the files."
                + UiSharedService.TooltipSeparator + "Hold CTRL to enable this button");
        }

        _uiShared.DrawCacheDirectorySetting();
        ImGui.AlignTextToFramePadding();
        if (_cacheMonitor.FileCacheSize >= 0)
            ImGui.TextUnformatted($"Currently utilized local storage: {UiSharedService.ByteToString(_cacheMonitor.FileCacheSize)}");
        else
            ImGui.TextUnformatted($"Currently utilized local storage: Calculating...");
        ImGui.TextUnformatted($"Remaining space free on drive: {UiSharedService.ByteToString(_cacheMonitor.FileCacheDriveFree)}");
        bool useFileCompactor = _configService.Current.UseCompactor;
        bool isLinux = _dalamudUtilService.IsWine;
        if (!useFileCompactor && !isLinux)
        {
            UiSharedService.ColorTextWrapped("Hint: To free up space when using Mare consider enabling the File Compactor", ImGuiColors.DalamudYellow);
        }
        if (isLinux || !_cacheMonitor.StorageisNTFS) ImGui.BeginDisabled();
        if (ImGui.Checkbox("Use file compactor", ref useFileCompactor))
        {
            _configService.Current.UseCompactor = useFileCompactor;
            _configService.Save();
        }
        _uiShared.DrawHelpText("The file compactor can massively reduce your saved files. It might incur a minor penalty on loading files on a slow CPU." + Environment.NewLine
            + "It is recommended to leave it enabled to save on space.");
        ImGui.SameLine();
        if (!_fileCompactor.MassCompactRunning)
        {
            if (_uiShared.IconTextButton(FontAwesomeIcon.FileArchive, "Compact all files in storage"))
            {
                _ = Task.Run(() =>
                {
                    _fileCompactor.CompactStorage(compress: true);
                    _cacheMonitor.RecalculateFileCacheSize(CancellationToken.None);
                });
            }
            UiSharedService.AttachToolTip("This will run compression on all files in your current Mare Storage." + Environment.NewLine
                + "You do not need to run this manually if you keep the file compactor enabled.");
            ImGui.SameLine();
            if (_uiShared.IconTextButton(FontAwesomeIcon.File, "Decompact all files in storage"))
            {
                _ = Task.Run(() =>
                {
                    _fileCompactor.CompactStorage(compress: false);
                    _cacheMonitor.RecalculateFileCacheSize(CancellationToken.None);
                });
            }
            UiSharedService.AttachToolTip("This will run decompression on all files in your current Mare Storage.");
        }
        else
        {
            UiSharedService.ColorText($"File compactor currently running ({_fileCompactor.Progress})", ImGuiColors.DalamudYellow);
        }
        if (isLinux || !_cacheMonitor.StorageisNTFS)
        {
            ImGui.EndDisabled();
            ImGui.TextUnformatted("The file compactor is only available on Windows and NTFS drives.");
        }
        ImGuiHelpers.ScaledDummy(new Vector2(10, 10));

        ImGui.Separator();
        UiSharedService.TextWrapped("File Storage validation can make sure that all files in your local Mare Storage are valid. " +
            "Run the validation before you clear the Storage for no reason. " + Environment.NewLine +
            "This operation, depending on how many files you have in your storage, can take a while and will be CPU and drive intensive.");
        using (ImRaii.Disabled(_validationTask != null && !_validationTask.IsCompleted))
        {
            if (_uiShared.IconTextButton(FontAwesomeIcon.Check, "Start File Storage Validation"))
            {
                _validationCts?.Cancel();
                _validationCts?.Dispose();
                _validationCts = new();
                var token = _validationCts.Token;
                _validationTask = Task.Run(() => _fileCacheManager.ValidateLocalIntegrity(_validationProgress, token));
            }
        }
        if (_validationTask != null && !_validationTask.IsCompleted)
        {
            ImGui.SameLine();
            if (_uiShared.IconTextButton(FontAwesomeIcon.Times, "Cancel"))
            {
                _validationCts?.Cancel();
            }
        }

        if (_validationTask != null)
        {
            using (ImRaii.PushIndent(20f))
            {
                if (_validationTask.IsCompleted)
                {
                    UiSharedService.TextWrapped($"The storage validation has completed and removed {_validationTask.Result.Count} invalid files from storage.");
                }
                else
                {

                    UiSharedService.TextWrapped($"Storage validation is running: {_currentProgress.Item1}/{_currentProgress.Item2}");
                    UiSharedService.TextWrapped($"Current item: {_currentProgress.Item3.ResolvedFilepath}");
                }
            }
        }
        ImGui.Separator();

        ImGuiHelpers.ScaledDummy(new Vector2(10, 10));
        ImGui.TextUnformatted("To clear the local storage accept the following disclaimer");
        ImGui.Indent();
        ImGui.Checkbox("##readClearCache", ref _readClearCache);
        ImGui.SameLine();
        UiSharedService.TextWrapped("I understand that: " + Environment.NewLine + "- By clearing the local storage I put the file servers of my connected service under extra strain by having to redownload all data."
            + Environment.NewLine + "- This is not a step to try to fix sync issues."
            + Environment.NewLine + "- This can make the situation of not getting other players data worse in situations of heavy file server load.");
        if (!_readClearCache)
            ImGui.BeginDisabled();
        if (_uiShared.IconTextButton(FontAwesomeIcon.Trash, "Clear local storage") && UiSharedService.CtrlPressed() && _readClearCache)
        {
            _ = Task.Run(() =>
            {
                foreach (var file in Directory.GetFiles(_configService.Current.CacheFolder))
                {
                    File.Delete(file);
                }
            });
        }
        UiSharedService.AttachToolTip("You normally do not need to do this. THIS IS NOT SOMETHING YOU SHOULD BE DOING TO TRY TO FIX SYNC ISSUES." + Environment.NewLine
            + "This will solely remove all downloaded data from all players and will require you to re-download everything again." + Environment.NewLine
            + "Mares storage is self-clearing and will not surpass the limit you have set it to." + Environment.NewLine
            + "If you still think you need to do this hold CTRL while pressing the button.");
        if (!_readClearCache)
            ImGui.EndDisabled();
        ImGui.Unindent();
    }

}
