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
using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using ImageGlass.Common.Localization;
using ImageGlass.Common.ServiceProviders.Licensing;
using ImageGlass.UI;
using ImageGlass.UI.Windowing;
using System.Threading.Tasks;

namespace ImageGlass.Common.Windows;

/// <summary>
/// Imports or retrieves a Pro license and reports its status.
/// </summary>
public partial class UpgradeToProControl : PhControl
{
    /// <summary>
    /// Gets, sets the utm campaign tag applied to the website links.
    /// </summary>
    public string Campaign { get; set; } = "from_upgrade_dialog";


    public UpgradeToProControl()
    {
        InitializeComponent();

        Localize();

        PART_BtnRetrieveEmail.Click += (_, _) => OpenUrl("https://imageglass.org/pro/retrieve");
        PART_BtnImportLicense.Click += async (_, _) => await ImportLicenseAsync(this);
    }



    #region Overrides

    protected override void OnIgLanguageChanged()
    {
        base.OnIgLanguageChanged();
        Localize();
    }


    #endregion // Overrides



    #region Public methods

    /// <summary>
    /// Pick a license file, verify its signature, install it, and offer to restart.
    /// Returns the imported license, or
    /// <see langword="null"/> when nothing was installed.
    /// </summary>
    public static async Task<LicenseInfo?> ImportLicenseAsync(Visual source)
    {
        var topLevel = TopLevel.GetTopLevel(source);
        if (topLevel is null) return null;

        var owner = topLevel as PhWindow;
        var title = Core.Lang[LangId.Menu_MnuManageLicense];

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType(Core.Lang[LangId.Menu_MnuManageLicense_LicenseFileType])
                {
                    Patterns = ["*" + LicenseService.LICENSE_FILE_EXTENSION],
                },
            ],
        });

        var path = (files.Count > 0 ? files[0] : null)?.TryGetLocalPath();
        if (string.IsNullOrEmpty(path)) return null;

        // the signature is the source of truth; reject anything that doesn't verify
        if (!LicenseService.TryVerify(path, out var lic, out var errorCode))
        {
            await ShowErrorAsync(owner, title, LangId.Menu_MnuManageLicense_ImportFailed, path, errorCode);
            return null;
        }

        // authentic but past its expiry instant, so it can't activate Pro
        if (!LicenseService.IsWithinValidity(lic))
        {
            await ShowErrorAsync(owner, title, LangId.Menu_MnuManageLicense_ImportExpired, path);
            return null;
        }

        // install into the config dir; the license it replaces is uninstalled
        var isInstalled = LicenseService.TryInstall(path, lic, out var installError);
        if (!isInstalled)
        {
            await ShowErrorAsync(owner, title, LangId.Menu_MnuManageLicense_ImportFailed,
                installError?.Message, installError?.ToString());
            return null;
        }

        // the license only loads at startup, so offer to restart now
        var result = await ModalWindow.ShowInfoAsync(owner, new ModalWindowOptions
        {
            Title = title,
            Heading = Core.Lang[LangId.Menu_MnuManageLicense_ImportSuccess],
        }, ModalWindowButton.Yes_No);

        if (result.ExitCode == DialogExitCode.OK) BHelper.RestartApp();

        return lic;
    }


    /// <summary>
    /// Shows a licensing error dialog. The licensing UI must never raise the unhandled-error dialog.
    /// </summary>
    public static async Task ShowErrorAsync(PhWindow? owner, string title, LangId messageKey,
        string? description = null, string? details = null)
    {
        await ModalWindow.ShowErrorAsync(owner, new ModalWindowOptions
        {
            Title = title,
            Heading = Core.Lang[messageKey],
            Description = description,
            Details = details,
        });
    }

    #endregion // Public methods



    #region Methods

    /// <summary>
    /// Applies every string from the current language pack.
    /// </summary>
    private void Localize()
    {
        var lang = Core.Lang;

        PART_LblRetrieveEmail.Text = lang[LangId.Menu_MnuUpgradeLicense_RetrieveFromEmail];

        PART_LblImportLicense.Text = lang[LangId.Menu_MnuUpgradeLicense_ImportLicense];

        // an authentic license the app cannot use: name the reason, lapsed first, over a pitch
        var expiredLic = Core.ExpiredLicense;
        var outOfScopeLic = Core.OutOfScopeLicense;
        var isExpired = expiredLic is not null;

        PART_LblExpired.IsVisible = isExpired;
        PART_LblOutOfScope.IsVisible = !isExpired && outOfScopeLic is not null;

        if (expiredLic is not null)
        {
            PART_LblExpired.Text = lang[LangId.Menu_MnuUpgradeLicense_Expired,
                expiredLic.Plan, FormatExpiry(expiredLic)];
        }
        if (outOfScopeLic is not null)
        {
            PART_LblOutOfScope.Text = lang[LangId.Menu_MnuUpgradeLicense_OutOfScope,
                outOfScopeLic.Plan, outOfScopeLic.VersionScope, LicenseScope.GetRunningAppMajorText()];
        }
    }


    /// <summary>
    /// Formats the expiry of a license in local time, falling back to the raw value.
    /// </summary>
    private static string FormatExpiry(LicenseInfo license)
    {
        var expiresAt = LicenseService.GetExpiryUtc(license);
        if (expiresAt is null) return license.ExpiresAt ?? string.Empty;

        return BHelper.FormatDateTime(expiresAt.Value.ToLocalTime().DateTime);
    }


    private void OpenUrl(string url) => _ = BHelper.OpenUrlAsync(this, url, Campaign);

    #endregion // Methods

}
