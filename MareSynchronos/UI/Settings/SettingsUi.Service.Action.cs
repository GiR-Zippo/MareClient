using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using MareSynchronos.Services.Mediator;
using MareSynchronos.UI;
using MareSynchronos.WebAPI;
using System.Numerics;

namespace Aurora.UI.Settings;

#pragma warning disable MA0048 // File name must match type name
public partial class SettingsUi : WindowMediatorSubscriberBase
#pragma warning restore MA0048 // File name must match type name
{
    [SettingsTabElement("Service Action", 41, "Service Settings")]
    private void DrawServerConfigurationAction()
    {
        if (ApiController.ServerAlive)
        {
            _uiShared.BigText("Service Actions");
            ImGuiHelpers.ScaledDummy(new Vector2(5, 5));
            if (ImGui.Button("Delete all my files"))
            {
                _deleteFilesPopupModalShown = true;
                ImGui.OpenPopup("Delete all your files?");
            }

            _uiShared.DrawHelpText("Completely deletes all your uploaded files on the service.");

            if (ImGui.BeginPopupModal("Delete all your files?", ref _deleteFilesPopupModalShown, UiSharedService.PopupWindowFlags))
            {
                UiSharedService.TextWrapped(
                    "All your own uploaded files on the service will be deleted.\nThis operation cannot be undone.");
                ImGui.TextUnformatted("Are you sure you want to continue?");
                ImGui.Separator();
                ImGui.Spacing();

                var buttonSize = (ImGui.GetWindowContentRegionMax().X - ImGui.GetWindowContentRegionMin().X -
                                 ImGui.GetStyle().ItemSpacing.X) / 2;

                if (ImGui.Button("Delete everything", new Vector2(buttonSize, 0)))
                {
                    _ = Task.Run(_fileTransferManager.DeleteAllFiles);
                    _deleteFilesPopupModalShown = false;
                }

                ImGui.SameLine();

                if (ImGui.Button("Cancel##cancelDelete", new Vector2(buttonSize, 0)))
                {
                    _deleteFilesPopupModalShown = false;
                }

                UiSharedService.SetScaledWindowSize(325);
                ImGui.EndPopup();
            }
            ImGui.SameLine();
            if (ImGui.Button("Delete account"))
            {
                _deleteAccountPopupModalShown = true;
                ImGui.OpenPopup("Delete your account?");
            }

            _uiShared.DrawHelpText("Completely deletes your account and all uploaded files to the service.");

            if (ImGui.BeginPopupModal("Delete your account?", ref _deleteAccountPopupModalShown, UiSharedService.PopupWindowFlags))
            {
                UiSharedService.TextWrapped(
                    "Your account and all associated files and data on the service will be deleted.");
                UiSharedService.TextWrapped("Your UID will be removed from all pairing lists.");
                ImGui.TextUnformatted("Are you sure you want to continue?");
                ImGui.Separator();
                ImGui.Spacing();

                var buttonSize = (ImGui.GetWindowContentRegionMax().X - ImGui.GetWindowContentRegionMin().X -
                                  ImGui.GetStyle().ItemSpacing.X) / 2;

                if (ImGui.Button("Delete account", new Vector2(buttonSize, 0)))
                {
                    _ = Task.Run(ApiController.UserDelete);
                    _deleteAccountPopupModalShown = false;
                    Mediator.Publish(new SwitchToIntroUiMessage());
                }

                ImGui.SameLine();

                if (ImGui.Button("Cancel##cancelDelete", new Vector2(buttonSize, 0)))
                {
                    _deleteAccountPopupModalShown = false;
                }

                UiSharedService.SetScaledWindowSize(325);
                ImGui.EndPopup();
            }
        }
    }
}
