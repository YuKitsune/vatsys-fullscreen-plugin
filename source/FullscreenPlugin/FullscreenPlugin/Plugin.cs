using Newtonsoft.Json;
using System.ComponentModel.Composition;
using System.Net.Http;
using System.Windows.Forms;
using vatsys;
using vatsys.Plugin;

namespace FullscreenPlugin;

[Export(typeof(IPlugin))]
public class Plugin : IPlugin
{
    bool _isFullscreen;
    
    public string Name => "Fullscreen Plugin";

    private static readonly Version _version = new(0, 1, 1);

    private static readonly string _versionUrl = "https://raw.githubusercontent.com/YuKitsune/vatsys-fullscreen-plugin/refs/heads/main/source/FullscreenPlugin/FullscreenPlugin/Version.json";

    private static readonly HttpClient _httpClient = new();

    public void OnFDRUpdate(FDP2.FDR updated) { }

    public void OnRadarTrackUpdate(RDP.RadarTrack updated) { }

    public Plugin()
    {
        AddMenuItem();

        _ = CheckVersion();
    }

    private async Task CheckVersion()
    {
        try
        {
            var response = await _httpClient.GetStringAsync(_versionUrl);

            var onlineVersion = JsonConvert.DeserializeObject<Version>(response);

            if (onlineVersion == null) return;

            if (onlineVersion.Major == _version.Major &&
                onlineVersion.Minor == _version.Minor &&
                onlineVersion.Build == _version.Build) return;

            Errors.Add(new Exception("A new version of the plugin is available."), Name);
        }
        catch { }
    }

    void AddMenuItem()
    {
        var menuItem = new CustomToolStripMenuItem(
            CustomToolStripMenuItemWindowType.Main,
            CustomToolStripMenuItemCategory.Windows,
            new ToolStripMenuItem("Toggle Fullscreen"));

        menuItem.Item.Click += (_, _) =>
        {
            MMI.InvokeOnGUI(ToggleFullScreen);
        };

        MMI.AddCustomMenuItem(menuItem);
    }

    void ToggleFullScreen()
    {
        var mainForm = Application.OpenForms["MainForm"];
        if (mainForm is null)
        {
            throw new Exception("Cannot find MainForm");
        }

        if (!_isFullscreen)
        {
            // Stupid WinForms bug means we set WindowState twice
            // https://stackoverflow.com/a/32821243
            mainForm.WindowState = FormWindowState.Normal;
            mainForm.FormBorderStyle = FormBorderStyle.None;
            mainForm.WindowState = FormWindowState.Maximized;
            
            _isFullscreen = true;
        }
        else
        {
            mainForm.FormBorderStyle = FormBorderStyle.FixedSingle;
            mainForm.WindowState = FormWindowState.Normal;
            _isFullscreen = false;
        }
    }
}