// SPDX-License-Identifier: GPL-2.0-only
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace AetherDrop;

public sealed class UiDebugWindow : Window
{
    readonly UiDebugSession _session;
    readonly TextBlock _status;
    readonly Button _pause;
    public UiDebugWindow(UiDebugSession session)
    {
        _session = session;
        Title = "AetherDrop · UI 调试"; Icon = Branding.WindowIcon;
        Width = 480; Height = 550; MinWidth = 420; MinHeight = 500;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Background = Palette.Brush(246, 249, 252);
        Foreground = Palette.Brush(28, 32, 36);
        Typography.Apply(this); FontSize = 14;
        var root = new StackPanel { Margin = new Thickness(24) };
        root.Children.Add(new TextBlock { Text = "UI 调试", FontSize = 24, FontWeight = Typography.EmphasisWeight });
        root.Children.Add(new TextBlock { Text = "模拟数据 · 不连接后台服务 · 不接管网卡\n不修改正式设置，不会实际接收文件。", Foreground = Palette.Brush(108,115,122),
            TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 16) });
        root.Children.Add(ActionButton("① 打开托盘弹窗", session.ShowTray));
        root.Children.Add(ActionButton("预览主界面", () =>
        {
            var window = Application.Current.Windows.OfType<MainWindow>().FirstOrDefault();
            if (window is null) { window = new MainWindow(); window.Show(); }
            window.Activate();
        }));
        root.Children.Add(ActionButton("② 模拟接收邀请（手动接受 / 拒绝）", () => session.Invite()));
        root.Children.Add(ActionButton("③ 模拟动态传输（自动接受，非真实网速）", () => session.Invite(true)));
        var actions = new WrapPanel { Margin = new Thickness(0, 4, 0, 4) };
        _pause = ActionButton("暂停 / 继续", session.TogglePause); actions.Children.Add(_pause);
        actions.Children.Add(ActionButton("直接完成", session.Complete));
        actions.Children.Add(ActionButton("模拟失败", session.Fail));
        actions.Children.Add(ActionButton("邀请超时", session.Expire)); root.Children.Add(actions);
        root.Children.Add(ActionButton("清空模拟弹窗", session.Reset));
        _status = new TextBlock { Text = session.Status, TextWrapping = TextWrapping.Wrap, Foreground = Palette.Brush(108,115,122), Margin = new Thickness(0, 14, 0, 0) };
        root.Children.Add(_status);
        root.Children.Add(new TextBlock { Text = "也可点击调试托盘图标打开弹窗。\n修改源码后，关闭此窗口并重新运行调试入口。", TextWrapping = TextWrapping.Wrap,
            Foreground = Palette.Brush(108,115,122), FontSize = 12, Margin = new Thickness(0, 16, 0, 0) });
        Content = new ScrollViewer { Content = root, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        session.Changed += Refresh; Closed += (_, _) => session.Changed -= Refresh;
    }
    void Refresh() { _status.Text = _session.Status; _pause.Content = _session.Paused ? "继续" : "暂停 / 继续"; }
    static Button ActionButton(string label, Action action)
    {
        var button = new Button { Style = Application.Current.TryFindResource("WinButton") as Style, Content = label, Padding = new Thickness(14, 9, 14, 9), Margin = new Thickness(0, 0, 8, 8),
            HorizontalContentAlignment = HorizontalAlignment.Left, Cursor = System.Windows.Input.Cursors.Hand };
        button.Click += (_, _) => action(); return button;
    }
}
