using System.ComponentModel.Composition;
using System.Windows.Forms;
using vatsys;
using vatsys.Plugin;

namespace FullscreenPlugin;

[Export(typeof(IPlugin))]
public class Plugin : IPlugin
{
    FormBorderStyle? _originalBorderStyle;
    
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
            throw new Exception("Cannot find MainForm.");
        }

        _originalBorderStyle ??= mainForm.FormBorderStyle;

        mainForm.FormBorderStyle = mainForm.FormBorderStyle == FormBorderStyle.None
            ? _originalBorderStyle.Value
            : FormBorderStyle.None;

        mainForm.WindowState = mainForm.WindowState == FormWindowState.Normal
            ? FormWindowState.Maximized
            : FormWindowState.Normal;
    }
}