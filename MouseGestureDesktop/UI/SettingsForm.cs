using System.Drawing;
using System.Windows.Forms;
using MouseGestureDesktop.Config;

namespace MouseGestureDesktop.UI;

/// <summary>Settings page for all gesture options. Raises <see cref="ConfigApplied"/> on OK/Apply.</summary>
public sealed class SettingsForm : Form
{
    private readonly ComboBox _button = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ComboBox _switchMethod = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly NumericUpDown _moveThreshold = NumUp(1, 200);
    private readonly NumericUpDown _stepDistance = NumUp(20, 1000);
    private readonly NumericUpDown _clickMaxDuration = NumUp(50, 2000);
    private readonly NumericUpDown _directionLockRatio = NumUp(1, 5, 1, 0.1m);
    private readonly NumericUpDown _cancelThreshold = NumUp(0, 100);
    private readonly NumericUpDown _trajectoryThickness = NumUp(1, 30);
    private readonly NumericUpDown _trajectoryFadeMs = NumUp(50, 3000);
    private readonly CheckBox _invertDirection = new() { Text = "反转方向", AutoSize = true };
    private readonly CheckBox _freezeCursor = new() { Text = "手势中冻结光标", AutoSize = true };
    private readonly CheckBox _showTrajectory = new() { Text = "显示轨迹", AutoSize = true };
    private readonly CheckBox _enabled = new() { Text = "启用手势", AutoSize = true };
    private readonly CheckBox _autostart = new() { Text = "开机自启", AutoSize = true };
    private readonly Button _colorButton = new() { Text = "轨迹颜色…", AutoSize = true };

    private Color _trajectoryColor = Color.FromArgb(unchecked((int)0xFF4EA1FF));

    /// <summary>Raised with the edited config when the user clicks OK or Apply.</summary>
    public event Action<GestureConfig>? ConfigApplied;

    public SettingsForm()
    {
        Text = "鼠标手势切换桌面 — 设置";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Padding = new Padding(12);

        _button.Items.AddRange(new object[] { "后退键 (XButton1)", "前进键 (XButton2)" });
        _switchMethod.Items.AddRange(new object[] { "自动 (COM 优先，失败回退键盘)", "仅 COM", "仅键盘快捷键" });

        BuildLayout();
    }

    private static NumericUpDown NumUp(decimal min, decimal max, int decimals = 0, decimal increment = 1) => new()
    {
        Minimum = min,
        Maximum = max,
        DecimalPlaces = decimals,
        Increment = increment,
        Width = 90,
    };

    // placeholder-build-layout

    private void BuildLayout()
    {
        var grid = new TableLayoutPanel
        {
            ColumnCount = 2,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Dock = DockStyle.Top,
        };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        AddRow(grid, "触发侧键", _button);
        AddRow(grid, "切换方式", _switchMethod);
        AddRow(grid, "进入手势阈值 (px)", _moveThreshold);
        AddRow(grid, "每步距离 (px)", _stepDistance);
        AddRow(grid, "单击判定时长 (ms)", _clickMaxDuration);
        AddRow(grid, "方向锁定比", _directionLockRatio);
        AddRow(grid, "取消阈值 (px)", _cancelThreshold);
        AddRow(grid, "轨迹粗细 (px)", _trajectoryThickness);
        AddRow(grid, "轨迹淡出 (ms)", _trajectoryFadeMs);
        AddRow(grid, "", _colorButton);

        var toggles = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.TopDown,
            AutoSize = true,
            Dock = DockStyle.Top,
            Padding = new Padding(0, 8, 0, 8),
        };
        toggles.Controls.AddRange(new Control[] { _enabled, _autostart, _invertDirection, _freezeCursor, _showTrajectory });

        var ok = new Button { Text = "确定", DialogResult = DialogResult.OK, AutoSize = true };
        var apply = new Button { Text = "应用", AutoSize = true };
        var cancel = new Button { Text = "取消", DialogResult = DialogResult.Cancel, AutoSize = true };
        var buttons = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.RightToLeft,
            Dock = DockStyle.Top,
            AutoSize = true,
        };
        buttons.Controls.AddRange(new Control[] { cancel, apply, ok });

        _colorButton.Click += (_, _) => PickColor();
        apply.Click += (_, _) => ConfigApplied?.Invoke(BuildConfig());
        ok.Click += (_, _) => ConfigApplied?.Invoke(BuildConfig());
        AcceptButton = ok;
        CancelButton = cancel;

        var root = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.TopDown,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Dock = DockStyle.Fill,
        };
        root.Controls.AddRange(new Control[] { grid, toggles, buttons });
        Controls.Add(root);
    }

    private static void AddRow(TableLayoutPanel grid, string label, Control control)
    {
        var lbl = new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(3, 7, 12, 3) };
        grid.Controls.Add(lbl);
        grid.Controls.Add(control);
    }

    private void PickColor()
    {
        using var dialog = new ColorDialog { Color = _trajectoryColor, FullOpen = true };
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            _trajectoryColor = dialog.Color;
            _colorButton.BackColor = _trajectoryColor;
        }
    }

    public void LoadFrom(GestureConfig c)
    {
        _button.SelectedIndex = c.Button == TriggerButton.XButton1 ? 0 : 1;
        _switchMethod.SelectedIndex = (int)c.SwitchMethod;
        _moveThreshold.Value = Clamp(_moveThreshold, c.MoveThreshold);
        _stepDistance.Value = Clamp(_stepDistance, c.StepDistance);
        _clickMaxDuration.Value = Clamp(_clickMaxDuration, c.ClickMaxDuration);
        _directionLockRatio.Value = Clamp(_directionLockRatio, (decimal)c.DirectionLockRatio);
        _cancelThreshold.Value = Clamp(_cancelThreshold, c.CancelThreshold);
        _trajectoryThickness.Value = Clamp(_trajectoryThickness, c.TrajectoryThickness);
        _trajectoryFadeMs.Value = Clamp(_trajectoryFadeMs, c.TrajectoryFadeMs);
        _invertDirection.Checked = c.InvertDirection;
        _freezeCursor.Checked = c.FreezeCursorDuringGesture;
        _showTrajectory.Checked = c.ShowTrajectory;
        _enabled.Checked = c.Enabled;
        _autostart.Checked = c.Autostart;
        _trajectoryColor = c.TrajectoryColor;
        _colorButton.BackColor = _trajectoryColor;
    }

    public GestureConfig BuildConfig() => new()
    {
        Button = _button.SelectedIndex == 0 ? TriggerButton.XButton1 : TriggerButton.XButton2,
        SwitchMethod = (SwitchMethod)Math.Max(0, _switchMethod.SelectedIndex),
        MoveThreshold = (int)_moveThreshold.Value,
        StepDistance = (int)_stepDistance.Value,
        ClickMaxDuration = (int)_clickMaxDuration.Value,
        DirectionLockRatio = (double)_directionLockRatio.Value,
        CancelThreshold = (int)_cancelThreshold.Value,
        TrajectoryThickness = (int)_trajectoryThickness.Value,
        TrajectoryFadeMs = (int)_trajectoryFadeMs.Value,
        InvertDirection = _invertDirection.Checked,
        FreezeCursorDuringGesture = _freezeCursor.Checked,
        ShowTrajectory = _showTrajectory.Checked,
        Enabled = _enabled.Checked,
        Autostart = _autostart.Checked,
        TrajectoryColorArgb = _trajectoryColor.ToArgb(),
    };

    private static decimal Clamp(NumericUpDown n, decimal value) => Math.Clamp(value, n.Minimum, n.Maximum);
}
