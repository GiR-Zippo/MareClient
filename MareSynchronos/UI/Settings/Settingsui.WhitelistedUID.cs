using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using FFXIVClientStructs.FFXIV.Common.Math;
using MareSynchronos.Services.Mediator;
using MareSynchronos.UI;

namespace Aurora.UI.Settings;

#pragma warning disable MA0048 // File name must match type name
public partial class SettingsUi : WindowMediatorSubscriberBase
#pragma warning restore MA0048 // File name must match type name
{
    /// <summary>
    /// WhitelistedUIDs Tab : Position 4
    /// </summary>
    [SettingsTabElement("WhitelistedUIDs", 52)]
    private void DrawWhitelistedUIDs()
    {
        _uiShared.BigText("Whitelisted UIDs");
        UiSharedService.TextWrapped("The entries in the list below will be ignored for all warnings and auto pause operations.");
        ImGui.Dummy(new Vector2(10));
        ImGui.SetNextItemWidth(200 * ImGuiHelpers.GlobalScale);
        ImGui.InputText("##ignoreuid", ref _uidToAddForIgnore, 20);
        ImGui.SameLine();
        using (ImRaii.Disabled(string.IsNullOrEmpty(_uidToAddForIgnore)))
        {
            if (_uiShared.IconTextButton(FontAwesomeIcon.Plus, "Add UID/Vanity ID to whitelist"))
            {
                if (!_playerPerformanceConfigService.Current.UIDsToIgnore.Contains(_uidToAddForIgnore, StringComparer.Ordinal))
                {
                    _playerPerformanceConfigService.Current.UIDsToIgnore.Add(_uidToAddForIgnore);
                    _playerPerformanceConfigService.Save();
                }
                _uidToAddForIgnore = string.Empty;
            }
        }
        _uiShared.DrawHelpText("Hint: UIDs are case sensitive.");
        var playerList = _playerPerformanceConfigService.Current.UIDsToIgnore;
        ImGui.SetNextItemWidth(200 * ImGuiHelpers.GlobalScale);
        using (var lb = ImRaii.ListBox("UID whitelist"))
        {
            if (lb)
            {
                for (int i = 0; i < playerList.Count; i++)
                {
                    bool shouldBeSelected = _selectedEntry == i;
                    if (ImGui.Selectable(playerList[i] + "##" + i, shouldBeSelected))
                    {
                        _selectedEntry = i;
                    }
                }
            }
        }
        using (ImRaii.Disabled(_selectedEntry == -1))
        {
            if (_uiShared.IconTextButton(FontAwesomeIcon.Trash, "Delete selected UID"))
            {
                _playerPerformanceConfigService.Current.UIDsToIgnore.RemoveAt(_selectedEntry);
                _selectedEntry = -1;
                _playerPerformanceConfigService.Save();
            }
        }
    }
}
