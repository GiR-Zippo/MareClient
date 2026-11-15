using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility;
using MareSynchronos.Services.Mediator;
using MareSynchronos.UI;

namespace Aurora.UI.Settings;

#pragma warning disable MA0048 // File name must match type name
public partial class SettingsUi : WindowMediatorSubscriberBase
#pragma warning restore MA0048 // File name must match type name
{
    [SettingsTabElement("Export MCDF", 50)]
    private void DrawExportMCDF()
    {
        _uiShared.BigText("Export MCDF");

        ImGuiHelpers.ScaledDummy(10);

        UiSharedService.ColorTextWrapped("Exporting MCDF has moved.", ImGuiColors.DalamudYellow);
        ImGuiHelpers.ScaledDummy(5);
        UiSharedService.TextWrapped("It is now found in the Main UI under \"Your User Menu\" (");
        ImGui.SameLine();
        _uiShared.IconText(FontAwesomeIcon.UserCog);
        ImGui.SameLine();
        UiSharedService.TextWrapped(") -> \"Character Data Hub\".");
        if (_uiShared.IconTextButton(FontAwesomeIcon.Running, "Open Mare Character Data Hub"))
        {
            Mediator.Publish(new UiToggleMessage(typeof(CharaDataHubUi)));
        }
        UiSharedService.TextWrapped("Note: this entry will be removed in the near future. Please use the Main UI to open the Character Data Hub.");
        ImGuiHelpers.ScaledDummy(5);
    }
}
