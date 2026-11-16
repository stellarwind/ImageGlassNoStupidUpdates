/*
ImageGlass - A Fast, Seamless Photo Viewer
Copyright (C) 2010 - 2027 DUONG DIEU PHAP
Project homepage: https://imageglass.org

This program is free software: you can redistribute it and/or modify
it under the terms of the GNU General Public License as published by
the Free Software Foundation, either version 3 of the License, or
(at your option) any later version.

This program is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU General Public License for more details.

You should have received a copy of the GNU General Public License
along with this program.  If not, see <https://www.gnu.org/licenses/>.
*/
using Avalonia.Controls;
using ImageGlass.Common.Localization;
using ImageGlass.UI.Windowing;
using System.IO;

namespace ImageGlass.Common.Windows;

public partial class GeneralSettingsView : SettingsPageView
{
    public GeneralSettingsView()
    {
        InitializeComponent();
    }


    /// <summary>
    /// Creates the page bound to the given working-copy view model.
    /// </summary>
    public GeneralSettingsView(SettingsViewModel vm, SettingsNavId navId, LangId? pageLabel = null) : this()
    {
        Initialize(vm, navId, pageLabel);
    }


    protected override void Build()
    {
        var startupDir = BHelper.BasePath;

        // real physical path (MSIX-aware) so the links open where the files actually live
        var configDir = BHelper.GetRealPlatformConfigDir();
        var userConfig = BHelper.GetRealPlatformConfigDir(Config.CONFIG_USER);

        // Locations
        BindLink(PART_StartupDir, LangId.Settings_StartupDir, startupDir,
            () => BHelper.OpenFolderPath(startupDir));
        BindLink(PART_ConfigDir, LangId.Settings_ConfigDir, configDir,
            () => BHelper.OpenFolderPath(configDir));
        BindLink(PART_UserConfig, LangId.Settings_UserConfigFile, userConfig,
            () => OpenUserConfigFile(userConfig));

        // Actions
        BindLink(PART_GetHelp, LangId._GetHelp, OpenHelpDocs);
        BindLink(PART_ResetSettings, LangId.Settings_ResetSettings, ResetSettings);

        // Startup
        BindToggle(PART_WelcomeImage, ConfigId.EnableWelcomeImage,
            LangId.Settings_EnableWelcomeImage, LangId.Settings_Startup);
        BindToggle(PART_LastSeenImage, ConfigId.EnableLastSeenImage,
            LangId.Settings_EnableLastSeenImage, LangId.Settings_Startup);
        BindToggle(PART_MultiInstances, ConfigId.EnableMultiInstances,
            LangId.Settings_EnableMultiInstances, LangId.Settings_Startup);

        // Others
        BindIntInput(PART_MsgDuration, ConfigId.InAppMessageDuration,
            LangId.Settings_InAppMessageDuration, LangId.Settings_Others);
    }


    /// <summary>
    /// Opens the online documentation.
    /// </summary>
    private async void OpenHelpDocs()
    {
        try
        {
            await BHelper.OpenUrlAsync(this, "https://imageglass.org/docs", $"from_setting_{NavId}");
        }
        catch
        {
            // an async void handler must never reach the unhandled-error dialog
        }
    }


    private async void ResetSettings()
    {
        var owner = TopLevel.GetTopLevel(this) as PhWindow;
        var title = Core.Lang[LangId.Settings_ResetSettings];

        try
        {
            var confirmation = await ModalWindow.ShowWarningAsync(owner, new ModalWindowOptions
            {
                Title = title,
                Heading = Core.Lang[LangId.Settings_ResetSettings_Confirm],
            }, ModalWindowButton.Yes_No);
            if (confirmation.ExitCode != DialogExitCode.OK) return;

            if (BHelper.HasOtherInstances())
            {
                await ModalWindow.ShowWarningAsync(owner, new ModalWindowOptions
                {
                    Title = title,
                    Heading = Core.Lang[LangId.Settings_ResetSettings_CloseOtherInstances],
                });
                return;
            }

            var defaults = new Config();
            if (!await defaults.SaveAsync())
            {
                await ModalWindow.ShowSettingsSaveErrorAsync(owner, title);
                return;
            }

            App.MainWindow.SkipSettingsSaveOnClose = true;
            BHelper.RestartApp();
        }
        catch (System.Exception ex)
        {
            await ModalWindow.ShowUnhandledErrorAsync(ex, owner);
        }
    }


    /// <summary>
    /// Opens the user config file in the system's default editor.
    /// Falls back to revealing it in the file explorer when no editor is associated.
    /// </summary>
    private static async void OpenUserConfigFile(string filePath)
    {
        if (!File.Exists(filePath))
        {
            // open the file's own (already-resolved) folder, not the unresolved config dir
            BHelper.OpenFolderPath(Path.GetDirectoryName(filePath));
            return;
        }

        try
        {
            // open the JSON file in the app associated with its file type (shell-execute)
            var exitCode = await BHelper.RunExeAsync(filePath);
            if (exitCode == 0) return;
        }
        catch { }

        // no associated editor -> reveal in explorer instead
        BHelper.OpenFilePath(filePath);
    }

}
