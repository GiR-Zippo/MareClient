using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;
using MareSynchronos.Services.Mediator;
using MareSynchronos.UI;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Aurora.UI.Settings;

#pragma warning disable MA0048 // File name must match type name
public partial class SettingsUi : WindowMediatorSubscriberBase
#pragma warning restore MA0048 // File name must match type name
{
    [SettingsTabElement("Debug", 100)]
    private void DrawDebug()
    {
        _uiShared.BigText("Debug");
#if DEBUG
        if (LastCreatedCharacterData != null && ImGui.TreeNode("Last created character data"))
        {
            foreach (var l in JsonSerializer.Serialize(LastCreatedCharacterData, new JsonSerializerOptions() { WriteIndented = true }).Split('\n'))
            {
                ImGui.TextUnformatted($"{l}");
            }

            ImGui.TreePop();
        }
#endif
        if (_uiShared.IconTextButton(FontAwesomeIcon.Copy, "[DEBUG] Copy Last created Character Data to clipboard"))
        {
            if (LastCreatedCharacterData != null)
            {
                ImGui.SetClipboardText(JsonSerializer.Serialize(LastCreatedCharacterData, new JsonSerializerOptions() { WriteIndented = true }));
            }
            else
            {
                ImGui.SetClipboardText("ERROR: No created character data, cannot copy.");
            }
        }
        UiSharedService.AttachToolTip("Use this when reporting mods being rejected from the server.");

        _uiShared.DrawCombo("Log Level", Enum.GetValues<LogLevel>(), (l) => l.ToString(), (l) =>
        {
            _configService.Current.LogLevel = l;
            _configService.Save();
        }, _configService.Current.LogLevel);

        bool logPerformance = _configService.Current.LogPerformance;
        if (ImGui.Checkbox("Log Performance Counters", ref logPerformance))
        {
            _configService.Current.LogPerformance = logPerformance;
            _configService.Save();
        }
        _uiShared.DrawHelpText("Enabling this can incur a (slight) performance impact. Enabling this for extended periods of time is not recommended.");

        using (ImRaii.Disabled(!logPerformance))
        {
            if (_uiShared.IconTextButton(FontAwesomeIcon.StickyNote, "Print Performance Stats to /xllog"))
            {
                _performanceCollector.PrintPerformanceStats();
            }
            ImGui.SameLine();
            if (_uiShared.IconTextButton(FontAwesomeIcon.StickyNote, "Print Performance Stats (last 60s) to /xllog"))
            {
                _performanceCollector.PrintPerformanceStats(60);
            }
        }

        bool stopWhining = _configService.Current.DebugStopWhining;
        if (ImGui.Checkbox("Do not notify for modified game files or enabled LOD", ref stopWhining))
        {
            _configService.Current.DebugStopWhining = stopWhining;
            _configService.Save();
        }
        _uiShared.DrawHelpText("Having modified game files will still mark your logs with UNSUPPORTED and you will not receive support, message shown or not." + UiSharedService.TooltipSeparator
            + "Keeping LOD enabled can lead to more crashes. Use at your own risk.");
    }
}