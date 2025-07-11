using System.ComponentModel.Composition;
using System.Drawing;
using System.Windows.Forms;
using vatsys;
using vatsys.Plugin;

namespace FullscreenPlugin;

[Export(typeof(IPlugin))]
public class Plugin : IPlugin
{
    bool _isFullscreen;
    
    public string Name => "Fullscreen Plugin";
    
    public void OnFDRUpdate(FDP2.FDR updated) { }

    public void OnRadarTrackUpdate(RDP.RadarTrack updated) { }

    public Plugin()
    {
        AddMenuItem();
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