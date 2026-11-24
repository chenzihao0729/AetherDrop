// SPDX-License-Identifier: GPL-2.0-only
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Shape = System.Windows.Shapes.Shape;

namespace AetherDrop;

/// <summary>
/// The main window: its own title bar (status pill with the quick controls,
/// Wi-Fi, settings), a newer-version bar, someone asking to send, nearby
/// devices (radar or list), the files to send, what came in lately, and where
/// it is saved.
/// </summary>
public partial class MainWindow : Window
{
    static App A => App.Instance;

    public MainWindow()
    {
        InitializeComponent();
        Icon = Branding.WindowIcon;
        StatusPopup.CustomPopupPlacementCallback = PlaceStatusPopup;
        Native.Chrome(this, caption: UiLayout.MainTitleBarHeight);
        Loaded += (_, _) => CenterOnDesktop();
        DataContext = A.Model;
        ReceivedList.ItemsSource = A.Model.ReceivedFiles;
        A.Model.Files.CollectionChanged += OnFiles;
        A.Model.ReceivedFiles.CollectionChanged += OnReceived;
        A.Model.PropertyChanged += OnModel;
        A.ReceivingChanged += ShowMark;
        Drop += OnDrop;
        DragOver += (_, e) => { e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None; e.Handled = true; };
        Closed += (_, _) =>
        {
            A.Model.Files.CollectionChanged -= OnFiles;
            A.Model.ReceivedFiles.CollectionChanged -= OnReceived;
            A.Model.PropertyChanged -= OnModel;
            A.ReceivingChanged -= ShowMark;
            A.WindowClosed();
        };
        FolderChanged();
        OnFiles(null, null);
        OnReceived(null, null);
        ShowState();
    }

    public void FolderChanged() => FolderText.Text = L.T("main.saveTo") + " " + A.Settings.SaveFolder;

    void OnModel(object? s, PropertyChangedEventArgs e) => ShowState();

    /// <summary>The status pill, the Wi-Fi button and the update bar.</summary>
    void ShowState()
    {
        var m = A.Model;
        var (label, dot) = Status(m.Air, m.Allow);
        StatusText.Text = m.Air == "service_stopped" || m.Air is not ("ready" or "starting" or "standby" or "off" or "error" or "no_adapter")
            ? L.T("pill.noService") : label;
        StatusDot.SetResourceReference(Shape.FillProperty, dot);
        StatusPill.ToolTip = m.Air == "ready" ? L.F("status.on", m.Channel) : label;
        UpdateBar.Visibility = m.Update == "" ? Visibility.Collapsed : Visibility.Visible;
        UpdateText.Text = L.F(m.UpdatePreview ? "update.barPreview" : "update.bar", m.Update);
        ShowMark();
    }

    /// <summary>The words and dot colour of a state (the design's status pill).</summary>
    public static (string Label, string Dot) Status(string air, bool allow = true) => air switch
    {
        "ready" or "starting" or "standby" when !allow => (L.T("recv.off"), "Text3"),
        "ready" => (L.T("pill.ready"), "Success"),
        "starting" => (L.T("status.starting"), "Warn"),
        "standby" => (L.T("pill.standby"), "Text3"),
        "off" => (L.T("pill.off"), "Text3"),
        "error" => (L.T("status.error"), "Error"),
        "no_adapter" => (L.T("radar.noCard"), "Warn"),
        _ => (L.T("status.noService"), "Error"),
    };

    // ------------------------------------------------------- quick controls

    System.Windows.Controls.Primitives.CustomPopupPlacement[] PlaceStatusPopup(Size popupSize, Size targetSize, Point offset)
    {
        var target = StatusPill.TranslatePoint(new Point(), this);
        // Align the visible card, excluding the transparent shadow gutter.
        var gutter = StatusPopupSurface.Margin;
        var x = target.X + targetSize.Width - popupSize.Width - gutter.Left;
        x = Math.Clamp(x, 0, Math.Max(0, ActualWidth - popupSize.Width - gutter.Left - gutter.Right));
        return [new(new Point(x - target.X, targetSize.Height - gutter.Top), System.Windows.Controls.Primitives.PopupPrimaryAxis.Vertical)];
    }

    void StatusPill_Checked(object sender, RoutedEventArgs e)
    {
        var bottom = StatusPill.TranslatePoint(new Point(0, StatusPill.ActualHeight), this).Y;
        StatusPopupSurface.MaxHeight = Math.Max(1, ActualHeight - bottom - StatusPopup.VerticalOffset - StatusPopupSurface.Margin.Bottom - 8);
        RefreshReceiveControls();
        ModeList.Children.Clear();
        foreach (var (key, text) in new[] { ("concurrent", L.T("mode.concurrent.short")), ("ondemand", L.T("mode.ondemand.short")), ("off", L.T("mode.off.short")) })
            ModeList.Children.Add(ModeChoice(key, text, A.Model.Mode == key));
        ChannelTip.Text = A.Model.Air == "ready" ? L.F("status.on", A.Model.Channel) : Status(A.Model.Air).Label;
        StatusPopup.IsOpen = true;
    }

    Button ModeChoice(string key, string text, bool chosen) =>
        Choice(text, chosen, () => { A.Service.SetMode(key); StatusPopup.IsOpen = false; });

    // ------------------------------------------------- who can send to this PC

    void RefreshReceiveControls()
    {
        AllowToggle.IsChecked = A.Model.Allow;
        var feature = A.Settings.AppleIdOn;
        var choices = new[] { ("everyone", L.T("recv.everyone")), ("contacts", L.T("aid.contacts")) };
        // Keep both rows mounted while selection and identity availability change.
        if (!ReceiveList.Children.OfType<Button>().Select(b => b.Tag as string).SequenceEqual(choices.Select(c => c.Item1)))
        {
            ReceiveList.Children.Clear();
            foreach (var (key, text) in choices)
            {
                var button = Choice(text, false, () => { StatusPopup.IsOpen = false; A.SetReceive(key); });
                button.Tag = key; ReceiveList.Children.Add(button);
            }
        }
        var receiving = AppleId.ResolveReceiving(true, A.Settings.Receive, feature, AppleId.Present);
        foreach (var button in ReceiveList.Children.OfType<Button>())
        {
            button.IsEnabled = (string?)button.Tag != "contacts" || AppleId.InUse;
            UiMotion.SetSelected(button, (string?)button.Tag == receiving);
        }
        bool present = AppleId.Present;
        ReceiveTip.Text = !feature ? L.T("recv.enableHint") : !present ? L.T("recv.needIdentity") : "";
        ReceiveTip.Visibility = ReceiveTip.Text.Length == 0 ? Visibility.Collapsed : Visibility.Visible;
    }

    public void ShowMark()
    {
        Mark.Opacity = A.Model.Allow ? 1 : .55;
        if (StatusPopup.IsOpen) RefreshReceiveControls();
    }

    /// <summary>A shared choice row: highlighted fill and a trailing check.</summary>
    Button Choice(string text, bool chosen, Action pick)
    {
        var button = new Button { Style = (Style)FindResource("ChoiceRow"), Content = text, Margin = new Thickness(0, 1, 0, 1) };
        UiMotion.SetSelected(button, chosen);
        button.Click += (_, _) => pick();
        return button;
    }

    void StatusPopup_Closed(object? sender, EventArgs e) => StatusPill.IsChecked = false;
    void Allow_Click(object sender, RoutedEventArgs e) => A.SetDiscoverable(AllowToggle.IsChecked == true);

    // ----------------------------------------------------------- the files

    void OnFiles(object? s, NotifyCollectionChangedEventArgs? e)
    {
        var n = A.Model.Files.Count;
        FilesRow.Visibility = n == 0 ? Visibility.Collapsed : Visibility.Visible;
        ClearButton.Visibility = n == 0 ? Visibility.Collapsed : Visibility.Visible;
        FilesCount.Text = L.F("main.count", n);
        FilesOutline.StrokeDashArray = n == 0 ? new DoubleCollection { 4, 3 } : null;
    }

    void OnReceived(object? s, NotifyCollectionChangedEventArgs? e)
    {
        NothingReceived.Visibility = A.Model.ReceivedFiles.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        // The latest three, as in the design.
        ReceivedList.ItemsSource = A.Model.ReceivedFiles.Take(3).ToList();
    }

    void OnDrop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is string[] paths)
            foreach (var p in paths) if (!A.Model.Files.Contains(p)) A.Model.Files.Add(p);
    }

    void Add_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.OpenFileDialog { Multiselect = true, Title = L.T("pick.files") };
        if (Modal(() => Native.ShowDialog(this, dlg, dlg.Title)) == true)
            foreach (var f in dlg.FileNames) if (!A.Model.Files.Contains(f)) A.Model.Files.Add(f);
    }

    void Clear_Click(object sender, RoutedEventArgs e) => A.Model.Files.Clear();

    void RemoveFile_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is string f) A.Model.Files.Remove(f);
    }

    // ------------------------------------------------------------ the rest

    void Settings_Click(object sender, RoutedEventArgs e) => A.ShowSettings("general");
    void Wifi_Click(object sender, RoutedEventArgs e) => A.ShowWifi();
    void Report_Click(object sender, RoutedEventArgs e) { StatusPopup.IsOpen = false; App.RunReport(); }
    void Close_Click(object sender, RoutedEventArgs e) => Shut();

    /// <summary>When it last came out.</summary>
    public DateTime ShownAt { get; private set; }

    /// <summary>When it last went away, so a click on the tray icon that took the focus from it does not bring it straight back.</summary>
    public DateTime ShutAt { get; private set; }

    /// <summary>Hides the panel (it is kept, with what it shows).</summary>
    public void Shut()
    {
        if (_closing) return;
        StatusPopup.IsOpen = false;
        ShutAt = DateTime.UtcNow;
        _closing = true;
        var generation = ++_visibilityGeneration;
        Motion.FadeOut(Root, 14, () =>
        {
            if (generation != _visibilityGeneration) return;
            _closing = false; Hide(); A.WindowClosed();
        });
    }

    int _visibilityGeneration;
    bool _closing;
    internal bool IsClosing => _closing;

    /// <summary>Opening cancels any unfinished hide before animating into the centered position.</summary>
    public void Reveal()
    {
        ++_visibilityGeneration;
        Root.BeginAnimation(OpacityProperty, null);
        Root.Opacity = 1;
        Root.RenderTransform = Transform.Identity;
        _closing = false;
        UiMotion.Reveal(Root, 8);
    }

    /// <summary>Each opening starts centered on the current monitor; dragging never persists a position.</summary>
    public void CenterOnDesktop()
    {
        if (WindowState != WindowState.Normal) WindowState = WindowState.Normal;
        var work = FloatingCard.WorkArea(this, System.Windows.Forms.Cursor.Position);
        MaxHeight = Math.Max(1, work.Height - 24);
        Height = Math.Min(712, MaxHeight);
        Left = work.Left + (work.Width - Width) / 2;
        Top = work.Top + (work.Height - Height) / 2;
        ShownAt = DateTime.UtcNow;
    }

    /// <summary>A file or folder dialog takes the focus without the panel going away.</summary>
    T Modal<T>(Func<T> show)
    {
        try { return show(); }
        finally { Activate(); }
    }

    async void UpdateNow_Click(object sender, RoutedEventArgs e)
    {
        UpdateBar.IsEnabled = false;
        var why = await A.InstallUpdate();
        UpdateBar.IsEnabled = true;
        if (why != "") UpdateText.Text = L.T("update.failed") + ": " + why;
    }

    void UpdateLater_Click(object sender, RoutedEventArgs e)
    {
        A.Settings.SkipVersion = A.Model.Update;
        A.Settings.Save();
        A.Model.Update = "";
    }

    void ChangeFolder_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.OpenFolderDialog { Title = L.T("pick.folder"), InitialDirectory = A.Settings.SaveFolder };
        if (Modal(() => Native.ShowDialog(this, dlg, dlg.Title)) != true) return;
        A.Settings.SaveFolder = dlg.FolderName;
        A.Settings.Save();
        A.Engine.Apply(A.Settings);
        FolderChanged();
    }

    void OpenFolder_Click(object sender, RoutedEventArgs e)
    {
        Directory.CreateDirectory(A.Settings.SaveFolder);
        Process.Start("explorer.exe", $"\"{A.Settings.SaveFolder}\"");
    }

    void Open_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is string path && File.Exists(path))
            Process.Start("explorer.exe", $"/select,\"{path}\"");
    }
}

/// <summary>A path's last part, for the file chips.</summary>
public sealed class FileName : System.Windows.Data.IValueConverter
{
    public static readonly FileName Instance = new();
    public object? Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture) =>
        value is string s ? Path.GetFileName(s.TrimEnd('\\')) : "";
    public object? ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture) =>
        throw new NotSupportedException();
}
