using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using MareSynchronos.Services.Mediator;
using MareSynchronos.UI;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Net.Http;
using System.Text.Json;

namespace Aurora.UI.Settings;

#pragma warning disable MA0048 // File name must match type name
public partial class SettingsUi : WindowMediatorSubscriberBase
#pragma warning restore MA0048 // File name must match type name
{
    private Task<List<string>?>? _downloadServersTask = null;
    private Task<List<string>?>? _speedTestTask = null;

    [SettingsTabElement("Transfers Ui", 11, "Ui Settings")]
    private void DrawCurrentTransfersUi()
    {
        _uiShared.BigText("Transfer UI");

        bool showTransferWindow = _configService.Current.ShowTransferWindow;
        if (ImGui.Checkbox("Show separate transfer window", ref showTransferWindow))
        {
            _configService.Current.ShowTransferWindow = showTransferWindow;
            _configService.Save();
        }
        _uiShared.DrawHelpText($"The download window will show the current progress of outstanding downloads.{Environment.NewLine}{Environment.NewLine}" +
            $"What do W/Q/P/D stand for?{Environment.NewLine}W = Waiting for Slot (see Maximum Parallel Downloads){Environment.NewLine}" +
            $"Q = Queued on Server, waiting for queue ready signal{Environment.NewLine}" +
            $"P = Processing download (aka downloading){Environment.NewLine}" +
            $"D = Decompressing download");
        if (!_configService.Current.ShowTransferWindow) ImGui.BeginDisabled();
        ImGui.Indent();
        bool editTransferWindowPosition = _uiShared.EditTrackerPosition;
        if (ImGui.Checkbox("Edit Transfer Window position", ref editTransferWindowPosition))
        {
            _uiShared.EditTrackerPosition = editTransferWindowPosition;
        }
        ImGui.Unindent();
        if (!_configService.Current.ShowTransferWindow) ImGui.EndDisabled();

        bool showTransferBars = _configService.Current.ShowTransferBars;
        if (ImGui.Checkbox("Show transfer bars rendered below players", ref showTransferBars))
        {
            _configService.Current.ShowTransferBars = showTransferBars;
            _configService.Save();
        }
        _uiShared.DrawHelpText("This will render a progress bar during the download at the feet of the player you are downloading from.");

        if (!showTransferBars) ImGui.BeginDisabled();
        ImGui.Indent();
        bool transferBarShowText = _configService.Current.TransferBarsShowText;
        if (ImGui.Checkbox("Show Download Text", ref transferBarShowText))
        {
            _configService.Current.TransferBarsShowText = transferBarShowText;
            _configService.Save();
        }
        _uiShared.DrawHelpText("Shows download text (amount of MiB downloaded) in the transfer bars");
        int transferBarWidth = _configService.Current.TransferBarsWidth;
        if (ImGui.SliderInt("Transfer Bar Width", ref transferBarWidth, 10, 500))
        {
            _configService.Current.TransferBarsWidth = transferBarWidth;
            _configService.Save();
        }
        _uiShared.DrawHelpText("Width of the displayed transfer bars (will never be less wide than the displayed text)");
        int transferBarHeight = _configService.Current.TransferBarsHeight;
        if (ImGui.SliderInt("Transfer Bar Height", ref transferBarHeight, 2, 50))
        {
            _configService.Current.TransferBarsHeight = transferBarHeight;
            _configService.Save();
        }
        _uiShared.DrawHelpText("Height of the displayed transfer bars (will never be less tall than the displayed text)");
        bool showUploading = _configService.Current.ShowUploading;
        if (ImGui.Checkbox("Show 'Uploading' text below players that are currently uploading", ref showUploading))
        {
            _configService.Current.ShowUploading = showUploading;
            _configService.Save();
        }
        _uiShared.DrawHelpText("This will render an 'Uploading' text at the feet of the player that is in progress of uploading data.");

        ImGui.Unindent();
        if (!showUploading) ImGui.BeginDisabled();
        ImGui.Indent();
        bool showUploadingBigText = _configService.Current.ShowUploadingBigText;
        if (ImGui.Checkbox("Large font for 'Uploading' text", ref showUploadingBigText))
        {
            _configService.Current.ShowUploadingBigText = showUploadingBigText;
            _configService.Save();
        }
        _uiShared.DrawHelpText("This will render an 'Uploading' text in a larger font.");

        ImGui.Unindent();

        if (!showUploading) ImGui.EndDisabled();
        if (!showTransferBars) ImGui.EndDisabled();

        if (_apiController.IsConnected)
        {
            ImGuiHelpers.ScaledDummy(5);
            ImGui.Separator();
            ImGuiHelpers.ScaledDummy(10);
            using var tree = ImRaii.TreeNode("Speed Test to Servers");
            if (tree)
            {
                if (_downloadServersTask == null || ((_downloadServersTask?.IsCompleted ?? false) && (!_downloadServersTask?.IsCompletedSuccessfully ?? false)))
                {
                    if (_uiShared.IconTextButton(FontAwesomeIcon.GroupArrowsRotate, "Update Download Server List"))
                        _downloadServersTask = GetDownloadServerList();
                }
                if (_downloadServersTask != null && _downloadServersTask.IsCompleted && !_downloadServersTask.IsCompletedSuccessfully)
                {
                    UiSharedService.ColorTextWrapped("Failed to get download servers from service, see /xllog for more information", ImGuiColors.DalamudRed);
                }
                if (_downloadServersTask != null && _downloadServersTask.IsCompleted && _downloadServersTask.IsCompletedSuccessfully)
                {
                    if (_speedTestTask == null || _speedTestTask.IsCompleted)
                    {
                        if (_uiShared.IconTextButton(FontAwesomeIcon.ArrowRight, "Start Speedtest"))
                        {
                            _speedTestTask = RunSpeedTest(_downloadServersTask.Result!, _speedTestCts?.Token ?? CancellationToken.None);
                        }
                    }
                    else if (!_speedTestTask.IsCompleted)
                    {
                        UiSharedService.ColorTextWrapped("Running Speedtest to File Servers...", ImGuiColors.DalamudYellow);
                        UiSharedService.ColorTextWrapped("Please be patient, depending on usage and load this can take a while.", ImGuiColors.DalamudYellow);
                        if (_uiShared.IconTextButton(FontAwesomeIcon.Ban, "Cancel speedtest"))
                        {
                            _speedTestCts?.Cancel();
                            _speedTestCts?.Dispose();
                            _speedTestCts = new();
                        }
                    }
                    if (_speedTestTask != null && _speedTestTask.IsCompleted)
                    {
                        if (_speedTestTask.Result != null && _speedTestTask.Result.Count != 0)
                        {
                            foreach (var result in _speedTestTask.Result)
                            {
                                UiSharedService.TextWrapped(result);
                            }
                        }
                        else
                        {
                            UiSharedService.ColorTextWrapped("Speedtest completed with no results", ImGuiColors.DalamudYellow);
                        }
                    }
                }
            }
            ImGuiHelpers.ScaledDummy(10);
        }
    }

    private async Task<List<string>?> RunSpeedTest(List<string> servers, CancellationToken token)
    {
        List<string> speedTestResults = new();
        foreach (var server in servers)
        {
            HttpResponseMessage? result = null;
            Stopwatch? st = null;
            try
            {
                result = await _fileTransferOrchestrator.SendRequestAsync(HttpMethod.Get, new Uri(new Uri(server), "speedtest/run"), token, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);
                result.EnsureSuccessStatusCode();
                using CancellationTokenSource speedtestTimeCts = new();
                speedtestTimeCts.CancelAfter(TimeSpan.FromSeconds(10));
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(speedtestTimeCts.Token, token);
                long readBytes = 0;
                st = Stopwatch.StartNew();
                try
                {
                    var stream = await result.Content.ReadAsStreamAsync(linkedCts.Token).ConfigureAwait(false);
                    byte[] buffer = new byte[8192];
                    while (!speedtestTimeCts.Token.IsCancellationRequested)
                    {
                        var currentBytes = await stream.ReadAsync(buffer, linkedCts.Token).ConfigureAwait(false);
                        if (currentBytes == 0)
                            break;
                        readBytes += currentBytes;
                    }
                }
                catch (OperationCanceledException)
                {
                    _logger.LogWarning("Speedtest to {server} cancelled", server);
                }
                st.Stop();
                _logger.LogInformation("Downloaded {bytes} from {server} in {time}", UiSharedService.ByteToString(readBytes), server, st.Elapsed);
                var bps = (long)((readBytes) / st.Elapsed.TotalSeconds);
                speedTestResults.Add($"{server}: ~{UiSharedService.ByteToString(bps)}/s");
            }
            catch (HttpRequestException ex)
            {
                if (result != null)
                {
                    var res = await result.Content.ReadAsStringAsync().ConfigureAwait(false);
                    speedTestResults.Add($"{server}: {ex.Message} - {res}");
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Speedtest on {server} cancelled", server);
                speedTestResults.Add($"{server}: Cancelled by user");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Some exception");
            }
            finally
            {
                st?.Stop();
            }
        }
        return speedTestResults;
    }

    private async Task<List<string>?> GetDownloadServerList()
    {
        try
        {
            var result = await _fileTransferOrchestrator.SendRequestAsync(HttpMethod.Get, new Uri(_fileTransferOrchestrator.FilesCdnUri!, "files/downloadServers"), CancellationToken.None).ConfigureAwait(false);
            result.EnsureSuccessStatusCode();
            return await JsonSerializer.DeserializeAsync<List<string>>(await result.Content.ReadAsStreamAsync().ConfigureAwait(false)).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get download server list");
            throw;
        }
    }
}
