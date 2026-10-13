using System.Drawing;
using System.Windows.Forms;
using MouseGestureDesktop.Config;
using MouseGestureDesktop.UI;

namespace MouseGestureDesktop.App;

/// <summary>Root of the tray app: owns the NotifyIcon, menu, config and the gesture controller.</summary>
public sealed class TrayApplicationContext : ApplicationContext
{
    private readonly ConfigStore _store = new();
    private readonly NotifyIcon _notifyIcon;
    private readonly ToolStripMenuItem _enabledItem;
    private readonly ToolStripMenuItem _autostartItem;
    private readonly GestureController _controller;

    private GestureConfig _config;
    private SettingsForm? _settingsForm;

    public TrayApplicationContext()
    {
        _config = _store.Load();
        // Reconcile persisted autostart with the actual registry state.
        _config.Autostart = AutostartManager.IsEnabled();

        _controller = new GestureController(_config);

        _enabledItem = new ToolStripMenuItem("启用手势", null, (_, _) => ToggleEnabled()) { Checked = _config.Enabled };
        _autostartItem = new ToolStripMenuItem("开机自启", null, (_, _) => ToggleAutostart()) { Checked = _config.Autostart };

        var menu = new ContextMenuStrip();
        menu.Items.Add(new ToolStripMenuItem("设置…", null, (_, _) => OpenSettings()));
        menu.Items.Add(_enabledItem);
        menu.Items.Add(_autostartItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(new ToolStripMenuItem("退出", null, (_, _) => ExitApp()));

        _notifyIcon = new NotifyIcon
        {
            Icon = SystemIcons.Application,
            Text = "鼠标手势切换桌面",
            Visible = true,
            ContextMenuStrip = menu,
        };
        _notifyIcon.DoubleClick += (_, _) => OpenSettings();

        try
        {
            _controller.Start();
            UpdateTooltip();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"无法安装鼠标钩子：{ex.Message}", "MouseGestureDesktop",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void OpenSettings()
    {
        if (_settingsForm is { IsDisposed: false })
        {
            _settingsForm.Activate();
            return;
        }

        _settingsForm = new SettingsForm();
        _settingsForm.LoadFrom(_config);
        _settingsForm.ConfigApplied += OnConfigApplied;
        _settingsForm.FormClosed += (_, _) => _settingsForm = null;
        _settingsForm.Show();
        _settingsForm.Activate();
    }

    private void OnConfigApplied(GestureConfig updated)
    {
        bool autostartChanged = updated.Autostart != _config.Autostart;

        _config = updated;
        _store.Save(_config);
        _controller.ApplyConfig(_config);

        if (autostartChanged)
            AutostartManager.SetEnabled(_config.Autostart);

        _enabledItem.Checked = _config.Enabled;
        _autostartItem.Checked = _config.Autostart;
        UpdateTooltip();
    }

    private void ToggleEnabled()
    {
        _config.Enabled = !_config.Enabled;
        _enabledItem.Checked = _config.Enabled;
        _store.Save(_config);
        _controller.ApplyConfig(_config);
        UpdateTooltip();
    }

    private void ToggleAutostart()
    {
        _config.Autostart = !_config.Autostart;
        _autostartItem.Checked = _config.Autostart;
        AutostartManager.SetEnabled(_config.Autostart);
        _store.Save(_config);
    }

    private void UpdateTooltip()
    {
        string state = _config.Enabled ? "已启用" : "已禁用";
        // NotifyIcon.Text is capped at 63 chars.
        string text = $"鼠标手势切换桌面 ({state}) · {_controller.BackendName}";
        _notifyIcon.Text = text.Length > 63 ? text[..63] : text;
    }

    private void ExitApp()
    {
        _controller.Stop();
        _controller.Dispose();
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        ExitThread();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _controller.Dispose();
            _notifyIcon.Dispose();
        }
        base.Dispose(disposing);
    }
}
