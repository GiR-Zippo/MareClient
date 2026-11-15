using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using MareSynchronos.API.Data.Comparer;
using MareSynchronos.Services.Mediator;
using MareSynchronos.UI;

namespace Aurora.UI.Settings;

public partial class SettingsUi : WindowMediatorSubscriberBase
{
    [SettingsTabElement("General", 0)]
    private void DrawGeneral() { }

    [SettingsTabElement("Notes", 1, "General")]
    private void DrawNotes()
    {
        _uiShared.BigText("Notes");
        if (_uiShared.IconTextButton(FontAwesomeIcon.StickyNote, "Export all your user notes to clipboard"))
        {
            ImGui.SetClipboardText(UiSharedService.GetNotes(_pairManager.DirectPairs.UnionBy(_pairManager.GroupPairs.SelectMany(p => p.Value), p => p.UserData, UserDataComparer.Instance).ToList()));
        }
        if (_uiShared.IconTextButton(FontAwesomeIcon.FileImport, "Import notes from clipboard"))
        {
            _notesSuccessfullyApplied = null;
            var notes = ImGui.GetClipboardText();
            _notesSuccessfullyApplied = _uiShared.ApplyNotesFromClipboard(notes, _overwriteExistingLabels);
        }

        ImGui.SameLine();
        ImGui.Checkbox("Overwrite existing notes", ref _overwriteExistingLabels);
        _uiShared.DrawHelpText("If this option is selected all already existing notes for UIDs will be overwritten by the imported notes.");
        if (_notesSuccessfullyApplied.HasValue && _notesSuccessfullyApplied.Value)
        {
            UiSharedService.ColorTextWrapped("User Notes successfully imported", ImGuiColors.HealerGreen);
        }
        else if (_notesSuccessfullyApplied.HasValue && !_notesSuccessfullyApplied.Value)
        {
            UiSharedService.ColorTextWrapped("Attempt to import notes from clipboard failed. Check formatting and try again", ImGuiColors.DalamudRed);
        }

        var openPopupOnAddition = _configService.Current.OpenPopupOnAdd;

        if (ImGui.Checkbox("Open Notes Popup on user addition", ref openPopupOnAddition))
        {
            _configService.Current.OpenPopupOnAdd = openPopupOnAddition;
            _configService.Save();
        }
        _uiShared.DrawHelpText("This will open a popup that allows you to set the notes for a user after successfully adding them to your individual pairs.");

        var autoPopulateNotes = _configService.Current.AutoPopulateEmptyNotesFromCharaName;
        if (ImGui.Checkbox("Automatically populate notes using player names", ref autoPopulateNotes))
        {
            _configService.Current.AutoPopulateEmptyNotesFromCharaName = autoPopulateNotes;
            _configService.Save();
        }
        _uiShared.DrawHelpText("This will automatically populate user notes using the first encountered player name if the note was not set prior");
    }
}
