using Newtonsoft.Json;
using System.ComponentModel.Composition;
using System.Net.Http;
using System.Reflection;
using System.Windows.Forms;
using vatsys;
using vatsys.Plugin;

namespace FullscreenPlugin;

[Export(typeof(IPlugin))]
public class Plugin : IPlugin
{
    static readonly string VersionUrl = "https://raw.githubusercontent.com/YuKitsune/vatsys-fullscreen-plugin/refs/heads/main/Version.json";
    static readonly HttpClient HttpClient = new();
    
    bool _isFullscreen;
    
    public string Name => "Fullscreen Plugin";

    public void OnFDRUpdate(FDP2.FDR updated) { }

    public void OnRadarTrackUpdate(RDP.RadarTrack updated) { }

    public Plugin()
    {
        AddMenuItem();

        _ = CheckVersion();
    }

    async Task CheckVersion()
    {
        try
        {
            var response = await HttpClient.GetStringAsync(VersionUrl);
            var onlineVersion = JsonConvert.DeserializeObject<Version>(response);
            if (onlineVersion == null)
                return;

            var version = Assembly.GetExecutingAssembly()
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
                .InformationalVersion;
            
            if (string.IsNullOrEmpty(version))
                return;

            var parts = version!.Split('.', '+');
            var major = int.Parse(parts[0]);
            var minor = int.Parse(parts[1]);
            var build = int.Parse(parts[2]);
            
            if (onlineVersion.Major == major &&
                onlineVersion.Minor == minor &&
                onlineVersion.Build == build)
                return;

            Errors.Add(new Exception("A new version of the plugin is available."), Name);
        }
        catch
        {
            // Ignored
        }
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