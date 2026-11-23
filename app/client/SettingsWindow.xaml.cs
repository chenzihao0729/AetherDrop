// SPDX-License-Identifier: GPL-2.0-only
using System.Windows;
using System.Windows.Media;

namespace NekoDrop;

public partial class SettingsWindow : Window
{
    static App A => App.Instance;

    public SettingsWindow()
    {
        InitializeComponent();
        Native.UseSmoothCorners(this);
        Loaded += (_, _) => CenterOnDesktop();
        // The popup palette stays light regardless of the Windows app theme.
        MaxHeight = SystemParameters.WorkArea.Height;
        Height = Math.Min(Height, MaxHeight);
        VersionText.Text = "AetherDrop · " + Updater.Current;
        var s = A.Settings;
        NameBox.Text = s.DeviceName;
        FolderBox.Text = s.SaveFolder;
        AskBox.IsChecked = s.Ask;
        StartupBox.IsChecked = s.StartWithWindows;
        LanguageBox.SelectedIndex = s.Language switch { "zh" => 1, "en" => 2, _ => 0 };
        foreach (var t in s.AlwaysAccept) TrustedList.Items.Add(t.Replace("|", " · "));
        var mode = (string?)A.Service.Last?["mode"] ?? "concurrent";
        ModeConcurrent.IsChecked = mode == "concurrent";
        ModeOnDemand.IsChecked = mode == "ondemand";
        ModeOff.IsChecked = mode == "off";
        FastUnlockBox.IsChecked = (bool?)A.Service.Last?["fastUnlock"] ?? false;
        CoexPlanBox.IsChecked = (bool?)A.Service.Last?["coexPlan"] ?? false;
        BtCoexBox.IsChecked = (bool?)A.Service.Last?["btCoex"] ?? false;
        CheckUpdatesBox.IsChecked = Updater.Enabled && s.CheckUpdates;
        CheckUpdatesBox.IsEnabled = Updater.Enabled;
        PreviewUpdatesBox.IsEnabled = Updater.Enabled;
        CheckNowButton.IsEnabled = Updater.Enabled;
        PreviewUpdatesBox.IsChecked = s.PreviewUpdates;
        UpdateStatus.Text = Updater.Enabled ? "AetherDrop " + Updater.Current : L.T("update.unconfigured");
        ShowCard(A.Service.Last);
        A.Service.Reply += OnCardsReply;
        Closed += (_, _) => A.Service.Reply -= OnCardsReply;
        A.Service.RequestCards();
        /* Always visible, also after "Check now" replaces the status line (#11). */
        Title = L.T("set.title") + " · AetherDrop " + Updater.Current;
    }
    public void CenterOnDesktop()
    {
        if (WindowState != WindowState.Normal) WindowState = WindowState.Normal;
        var work = FloatingCard.WorkArea(this, System.Windows.Forms.Cursor.Position);
        MaxHeight = work.Height; Height = Math.Min(Height, work.Height);
        Left = work.Left + (work.Width - Width) / 2;
        Top = work.Top + (work.Height - Height) / 2;
    }
    void Header_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        for (DependencyObject? node = e.OriginalSource as DependencyObject; node is not null && node != sender; node = VisualTreeHelper.GetParent(node))
            if (node is System.Windows.Controls.Primitives.ButtonBase) return;
        if (e.LeftButton == System.Windows.Input.MouseButtonState.Pressed) DragMove();
    }
    void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    string _releasesUrl = Updater.ReleasesPage;

    /// <summary>A row of the card or model list.</summary>
    sealed record Item(string Key, string Label, string Vendor = "");

    void ShowCard(System.Text.Json.Nodes.JsonObject? state)
    {
        var card = (string?)state?["card"] ?? "";
        var manual = (string?)state?["manualCard"] ?? "";
        CardStatus.Text = card != "" ? L.F(manual != "" ? "set.card.manual" : "set.card.detected", card)
                                     : L.T("set.card.none");
    }

    /// <summary>The service's card list and models (the "cards" event); a manual choice's answer ("card-manual").</summary>
    void OnCardsReply(System.Text.Json.Nodes.JsonObject o) => Dispatcher.BeginInvoke(() =>
    {
        var ev = (string?)o["event"];
        if (ev == "card-manual")
        {
            var error = (string?)o["error"] ?? "";
            CardError.Text = error == "" ? L.T("set.card.applied") : L.T("set.card.mismatch");
            return;
        }
        if (ev != "cards") return;
        var manualHw = (string?)A.Service.Last?["manualCard"] ?? "";
        var manualModel = (string?)A.Service.Last?["manualModel"] ?? "";
        CardBox.Items.Clear();
        foreach (var c in o["candidates"]?.AsArray() ?? new System.Text.Json.Nodes.JsonArray())
        {
            var hw = (string?)c?["hw"] ?? "";
            var name = (string?)c?["name"] ?? "";
            var tag = (bool?)c?["recognized"] == true ? L.T("set.card.recognized") : L.T("set.card.unrecognized");
            CardBox.Items.Add(new Item(hw, $"{(name == "" ? hw : name)} ({hw.Replace("PCI\\", "")}) · {tag}"));
        }
        ModelBox.Items.Clear();
        foreach (var m in o["models"]?.AsArray() ?? new System.Text.Json.Nodes.JsonArray())
            ModelBox.Items.Add(new Item((string?)m?["key"] ?? "", (string?)m?["name"] ?? "", (string?)m?["vendor"] ?? ""));
        CardBox.SelectedItem = CardBox.Items.Cast<Item>().FirstOrDefault(i => i.Key == manualHw) ??
                               CardBox.Items.Cast<Item>().FirstOrDefault();
        ModelBox.SelectedItem = ModelBox.Items.Cast<Item>().FirstOrDefault(i => i.Key == manualModel);
        if (CardBox.Items.Count == 0) CardError.Text = L.T("set.card.nolist");
    });

    void CardApply_Click(object sender, RoutedEventArgs e)
    {
        if (CardBox.SelectedItem is not Item card || ModelBox.SelectedItem is not Item model)
        {
            CardError.Text = L.T("set.card.pick");
            return;
        }
        CardError.Text = "";
        A.Service.SetManualCard(card.Key, model.Key);
    }

    void CardAuto_Click(object sender, RoutedEventArgs e)
    {
        CardError.Text = "";
        A.Service.SetManualCard("", "");
    }

    async void CheckNow_Click(object sender, RoutedEventArgs e)
    {
        CheckNowButton.IsEnabled = false;
        OpenReleasesButton.Visibility = Visibility.Collapsed;
        UpdateStatus.Text = L.T("update.checking");
        try
        {
            A.Settings.PreviewUpdates = PreviewUpdatesBox.IsChecked == true;
            var res = await A.CheckForUpdateDetailed(true);
            switch (res.Status)
            {
                case Updater.CheckStatus.UpToDate:
                    UpdateStatus.Text = L.T("update.none");
                    return;
                case Updater.CheckStatus.RateLimited:
                    _releasesUrl = res.PageUrl ?? Updater.ReleasesPage;
                    OpenReleasesButton.Visibility = Visibility.Visible;
                    UpdateStatus.Text = res.NewerTag is not null
                        ? L.F("update.rateLimitedFound", res.NewerTag)
                        : L.T("update.rateLimited");
                    return;
                case Updater.CheckStatus.Failed:
                    UpdateStatus.Text = L.T("update.checkFailed") +
                        (string.IsNullOrEmpty(res.Detail) ? "" : ": " + res.Detail);
                    return;
            }
            var found = res.Release;
            if (found is null) { UpdateStatus.Text = L.T("update.none"); return; }
            UpdateStatus.Text = L.F("update.found", found.Version.ToString());
            var progress = new Progress<int>(p => UpdateStatus.Text = L.F("update.found", found.Version.ToString()) + $" {p}%");
            var why = await A.InstallUpdate(progress);
            if (why != "") UpdateStatus.Text = L.T("update.failed") + ": " + why;
        }
        finally { CheckNowButton.IsEnabled = true; }
    }

    void OpenReleases_Click(object sender, RoutedEventArgs e)
    {
        try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(_releasesUrl) { UseShellExecute = true }); }
        catch (Exception) { }
    }

    void Browse_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.OpenFolderDialog { Title = L.T("pick.folder"), InitialDirectory = FolderBox.Text };
        if (dlg.ShowDialog(this) == true) FolderBox.Text = dlg.FolderName;
    }

    void RemoveTrusted_Click(object sender, RoutedEventArgs e)
    {
        if (TrustedList.SelectedIndex >= 0) TrustedList.Items.RemoveAt(TrustedList.SelectedIndex);
    }

    void Save_Click(object sender, RoutedEventArgs e)
    {
        var s = A.Settings;
        var name = NameBox.Text.Trim();
        if (name.Length > 0) s.DeviceName = name;
        s.SaveFolder = FolderBox.Text;
        s.Ask = AskBox.IsChecked == true;
        s.StartWithWindows = StartupBox.IsChecked == true;
        s.CheckUpdates = CheckUpdatesBox.IsChecked == true;
        s.PreviewUpdates = PreviewUpdatesBox.IsChecked == true;
        var language = (LanguageBox.SelectedItem as System.Windows.Controls.ComboBoxItem)?.Tag as string ?? "auto";
        var languageChanged = language != s.Language;
        s.Language = language;
        s.AlwaysAccept = TrustedList.Items.Cast<string>().Select(x => x.Replace(" · ", "|")).ToList();
        s.Save();
        try { Shell.Register(s.StartWithWindows); } catch (Exception) { }
        A.Engine.Apply(s);
        var mode = ModeConcurrent.IsChecked == true ? "concurrent" : ModeOnDemand.IsChecked == true ? "ondemand" : "off";
        if (mode != (string?)A.Service.Last?["mode"]) A.Service.SetMode(mode);
        var fastUnlock = FastUnlockBox.IsChecked == true;
        if (fastUnlock != ((bool?)A.Service.Last?["fastUnlock"] ?? false)) A.Service.SetFastUnlock(fastUnlock);
        var coexPlan = CoexPlanBox.IsChecked == true;
        if (coexPlan != ((bool?)A.Service.Last?["coexPlan"] ?? false)) A.Service.SetCoexPlan(coexPlan);
        var btCoex = BtCoexBox.IsChecked == true;
        if (btCoex != ((bool?)A.Service.Last?["btCoex"] ?? false)) A.Service.SetBtCoex(btCoex);
        A.Window?.FolderChanged();
        if (languageChanged) A.ApplyLanguage();
        Close();
    }

    void Cancel_Click(object sender, RoutedEventArgs e) => Close();
}
