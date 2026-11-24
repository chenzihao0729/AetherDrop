// SPDX-License-Identifier: GPL-2.0-only
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace AetherDrop;

/// <summary>
/// Settings in four tabs (the design's pivot): general (name, folder,
/// language, accent, start with Windows, hotkey), network and receiving
/// (mode, ask first, always-accept list), card and advanced (detected or
/// chosen card, experimental switches, diagnostic report), about and updates.
/// Save keeps the window open and says so; Cancel closes it.
/// </summary>
public partial class SettingsWindow : Window
{
    static App A => App.Instance;
    string _folder;
    readonly List<string> _trusted;
    bool _observedAppleIdOn, _observedIdentity;
    string _observedReceive = "";
    string _releasesUrl = Updater.ReleasesPage;
    Dictionary<string, string>? _savedDraft;
    bool _saving, _dirtyQueued;

    public SettingsWindow(string tab = "general")
    {
        InitializeComponent();
        Native.Chrome(this);
        Loaded += (_, _) => CenterOnDesktop();
        var s = A.Settings;
        NameBox.Text = s.DeviceName;
        _folder = s.SaveFolder;
        FolderText.Text = _folder;
        AskBox.IsChecked = s.Ask;
        StartupBox.IsChecked = s.StartWithWindows;
        HotkeyBox.IsChecked = s.Hotkey;
        AppleIdOnBox.IsChecked = s.AppleIdOn;
        AppleIdOn_Changed(AppleIdOnBox, new RoutedEventArgs());
        ShowReceive();
        ShowIdentity();
        _observedAppleIdOn = s.AppleIdOn; _observedReceive = s.Receive; _observedIdentity = AppleId.Present;
        _hotkeyKeys = Hotkey.TryParse(s.HotkeyKeys, out _, out _) ? s.HotkeyKeys : Hotkey.Default;
        ShowHotkey();
        LanguageBox.SelectedIndex = s.Language switch { "zh" => 1, "en" => 2, _ => 0 };
        (s.Appearance switch { "light" => AppearanceLight, "dark" => AppearanceDark, _ => AppearanceAuto }).IsChecked = true;
        (s.HubIcon switch { "desktop" => HubDesktop, "laptop" => HubLaptop, _ => HubAuto }).IsChecked = true;
        _trusted = [.. s.AlwaysAccept];
        ShowTrusted();
        var mode = (string?)A.Service.Last?["mode"] ?? "concurrent";
        ModeConcurrent.IsChecked = mode == "concurrent";
        ModeOnDemand.IsChecked = mode == "ondemand";
        ModeOff.IsChecked = mode == "off";
        FastUnlockBox.IsChecked = (bool?)A.Service.Last?["fastUnlock"] ?? false;
        CoexPlanBox.IsChecked = (bool?)A.Service.Last?["coexPlan"] ?? false;
        CheckUpdatesBox.IsChecked = s.CheckUpdates;
        PreviewUpdatesBox.IsChecked = s.PreviewUpdates;
        DeleteInstallerBox.IsChecked = s.DeleteInstallerAfterInstall;
        VersionText.Text = "AetherDrop " + Updater.Current;
        Title = L.T("set.title") + " · AetherDrop " + Updater.Current;
        ShowCard(A.Service.Last);
        A.Service.Reply += OnCardsReply;
        A.ReceivingChanged += SyncReceivingState;
        Closed += (_, _) => { ++_saveNoticeGeneration; A.Service.Reply -= OnCardsReply; A.ReceivingChanged -= SyncReceivingState; };
        A.Service.RequestCards();
        (tab switch { "network" => TabNetwork, "appleid" => TabAppleId, "hardware" => TabHardware, "updates" => TabUpdates, _ => TabGeneral }).IsChecked = true;
        _savedDraft = ReadDraft();
        AddHandler(System.Windows.Controls.Primitives.ToggleButton.CheckedEvent, new RoutedEventHandler(DraftChanged));
        AddHandler(System.Windows.Controls.Primitives.ToggleButton.UncheckedEvent, new RoutedEventHandler(DraftChanged));
        AddHandler(System.Windows.Controls.Primitives.ButtonBase.ClickEvent, new RoutedEventHandler(DraftChanged));
        AddHandler(TextBox.TextChangedEvent, new TextChangedEventHandler((_, _) => QueueDirtyCheck()));
        AddHandler(System.Windows.Controls.Primitives.Selector.SelectionChangedEvent, new SelectionChangedEventHandler((_, _) => QueueDirtyCheck()));
        UpdateDirty();
    }

    Dictionary<string, string> ReadDraft() => new()
    {
        ["name"] = NameBox.Text.Trim(), ["folder"] = _folder,
        ["ask"] = (AskBox.IsChecked == true).ToString(), ["startup"] = (StartupBox.IsChecked == true).ToString(),
        ["apple"] = (AppleIdOnBox.IsChecked == true).ToString(),
        ["receive"] = AppleIdOnBox.IsChecked == true && ReceiveContacts.IsChecked == true && AppleId.Present ? "contacts" : "everyone",
        ["hotkey"] = (HotkeyBox.IsChecked == true).ToString(), ["keys"] = _hotkeyKeys,
        ["language"] = (LanguageBox.SelectedItem as ComboBoxItem)?.Tag as string ?? "auto",
        ["appearance"] = ChosenAppearance(), ["hub"] = HubDesktop.IsChecked == true ? "desktop" : HubLaptop.IsChecked == true ? "laptop" : "auto",
        ["trusted"] = System.Text.Json.JsonSerializer.Serialize(_trusted.OrderBy(t => t, StringComparer.Ordinal)),
        ["mode"] = ModeConcurrent.IsChecked == true ? "concurrent" : ModeOnDemand.IsChecked == true ? "ondemand" : "off",
        ["unlock"] = (FastUnlockBox.IsChecked == true).ToString(), ["coex"] = (CoexPlanBox.IsChecked == true).ToString(),
        ["updates"] = (CheckUpdatesBox.IsChecked == true).ToString(), ["preview"] = (PreviewUpdatesBox.IsChecked == true).ToString(),
        ["deleteInstaller"] = (DeleteInstallerBox.IsChecked == true).ToString(),
    };

    void DraftChanged(object sender, RoutedEventArgs e) => QueueDirtyCheck();
    void QueueDirtyCheck()
    {
        if (_savedDraft is null || _dirtyQueued) return;
        _dirtyQueued = true;
        Dispatcher.BeginInvoke(() => { _dirtyQueued = false; UpdateDirty(); });
    }
    void UpdateDirty()
    {
        if (_savedDraft is null) return;
        SaveButton.IsEnabled = !_saving && ReadDraft().Any(pair => _savedDraft[pair.Key] != pair.Value);
    }

    public void CenterOnDesktop()
    {
        if (WindowState != WindowState.Normal) WindowState = WindowState.Normal;
        var work = FloatingCard.WorkArea(this, System.Windows.Forms.Cursor.Position);
        MaxHeight = work.Height; Height = Math.Min(Height, work.Height);
        Left = work.Left + (work.Width - Width) / 2;
        Top = work.Top + (work.Height - Height) / 2;
    }

    void Tab_Checked(object sender, RoutedEventArgs e)
    {
        if (PageGeneral is null) return; // still loading
        PageGeneral.Visibility = TabGeneral.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        PageNetwork.Visibility = TabNetwork.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        PageAppleId.Visibility = TabAppleId.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        PageHardware.Visibility = TabHardware.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        PageUpdates.Visibility = TabUpdates.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        if (IsLoaded)
        {
            var page = new[] { PageGeneral, PageNetwork, PageAppleId, PageHardware, PageUpdates }.FirstOrDefault(p => p.Visibility == Visibility.Visible);
            if (page is not null) UiMotion.Reveal(page);
        }
    }

    string ChosenAppearance() => AppearanceLight.IsChecked == true ? "light" : AppearanceDark.IsChecked == true ? "dark" : "auto";

    // ----------------------------------------------------- always accept

    /// <summary>
    /// The list could only shrink: nothing added to it (#45). Offers the
    /// senders that asked lately, by the same name and model the check uses.
    /// </summary>
    void TrustedAdd_Click(object sender, RoutedEventArgs e)
    {
        var menu = new ContextMenu { PlacementTarget = TrustedAdd, Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom };
        foreach (var key in A.Settings.RecentSenders.Where(k => !_trusted.Contains(k)))
        {
            var item = new MenuItem { Header = key.Replace("|", " · ") };
            item.Click += (_, _) => { _trusted.Add(key); ShowTrusted(); };
            menu.Items.Add(item);
        }
        if (menu.Items.Count == 0) menu.Items.Add(new MenuItem { Header = L.T("set.trustedAddNone"), IsEnabled = false });
        menu.IsOpen = true;
    }

    void ShowTrusted()
    {
        QueueDirtyCheck();
        TrustedTitle.Text = L.F("set.trustedCount", _trusted.Count);
        TrustedEmpty.Visibility = _trusted.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        TrustedList.Children.Clear();
        foreach (var t in _trusted)
        {
            var row = new DockPanel { Margin = new Thickness(0, 4, 0, 0) };
            var remove = new Button { Style = (Style)FindResource("WinButton"), Content = L.T("set.remove"), Padding = new Thickness(10, 4, 10, 4) };
            remove.Click += (_, _) => { _trusted.Remove(t); ShowTrusted(); };
            DockPanel.SetDock(remove, Dock.Right);
            row.Children.Add(remove);
            row.Children.Add(new TextBlock { Text = t.Replace("|", " · "), FontSize = 13, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis });
            TrustedList.Children.Add(row);
        }
    }

    // ------------------------------------------------------------ the card

    /// <summary>A row of the card or model list.</summary>
    sealed record Item(string Key, string Label, string Vendor = "");

    void ShowCard(JsonObject? state)
    {
        var card = (string?)state?["card"] ?? "";
        var manual = (string?)state?["manualCard"] ?? "";
        var found = card != "";
        CardStatus.Text = found ? (manual != "" ? L.F("set.card.manual", card) : card) : L.T("set.card.none");
        CardBadgeText.Text = found ? L.T("set.card.detectedBadge") : L.T("set.card.unrecognized");
        CardBadge.SetResourceReference(Border.BackgroundProperty, found ? "SuccessBg" : "WarnBg");
        CardBadgeText.SetResourceReference(TextBlock.ForegroundProperty, found ? "Success" : "Warn");
        // The manual choice is for a card AetherDrop does not know; shown when none was found or one was chosen by hand.
        CardManual.Visibility = !found || manual != "" ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>The service's card list and models (the "cards" event); a manual choice's answer ("card-manual").</summary>
    void OnCardsReply(JsonObject o) => Dispatcher.BeginInvoke(() =>
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
        foreach (var c in o["candidates"]?.AsArray() ?? [])
        {
            var hw = (string?)c?["hw"] ?? "";
            var name = (string?)c?["name"] ?? "";
            var tag = (bool?)c?["recognized"] == true ? L.T("set.card.recognized") : L.T("set.card.unrecognized");
            CardBox.Items.Add(new Item(hw, $"{(name == "" ? hw : name)} ({hw.Replace("PCI\\", "")}) · {tag}"));
        }
        ModelBox.Items.Clear();
        foreach (var m in o["models"]?.AsArray() ?? [])
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

    void Report_Click(object sender, RoutedEventArgs e) => App.RunReport();

    // ------------------------------------------------------------- updates

    void Status(string text, bool releases = false, double? progress = null)
    {
        UpdateBox.Visibility = Visibility.Visible;
        UpdateStatus.Text = text;
        OpenReleasesButton.Visibility = releases ? Visibility.Visible : Visibility.Collapsed;
        UpdateBox.SetResourceReference(Border.BackgroundProperty, releases ? "WarnBg" : "AccentSubtle");
        UpdateStatus.SetResourceReference(TextBlock.ForegroundProperty, releases ? "Warn" : "Text");
        UpdateBarBox.Visibility = progress is null ? Visibility.Collapsed : Visibility.Visible;
        UpdateBar.Progress = progress ?? 0;
    }

    async void CheckNow_Click(object sender, RoutedEventArgs e)
    {
        CheckNowButton.IsEnabled = false;
        Status(L.T("update.checking"));
        try
        {
            A.Settings.PreviewUpdates = PreviewUpdatesBox.IsChecked == true;
            A.Settings.DeleteInstallerAfterInstall = DeleteInstallerBox.IsChecked == true;
            var res = await A.CheckForUpdateDetailed(true);
            switch (res.Status)
            {
                case Updater.CheckStatus.UpToDate:
                    Status(L.T("update.none"));
                    return;
                case Updater.CheckStatus.RateLimited:
                    _releasesUrl = res.PageUrl ?? Updater.ReleasesPage;
                    Status(res.NewerTag is not null ? L.F("update.rateLimitedFound", res.NewerTag) : L.T("update.rateLimited"), releases: true);
                    return;
                case Updater.CheckStatus.Failed:
                    Status(L.T("update.checkFailed") + (string.IsNullOrEmpty(res.Detail) ? "" : ": " + res.Detail));
                    return;
            }
            var found = res.Release;
            if (found is null) { Status(L.T("update.none")); return; }
            var text = L.F("update.found", found.Version.ToString());
            Status(text, progress: 0);
            var progress = new Progress<int>(p => Status(text + $" {p}%", progress: p / 100.0));
            var why = await A.InstallUpdate(progress);
            if (why != "") Status(L.T("update.failed") + ": " + why);
        }
        finally { CheckNowButton.IsEnabled = true; }
    }

    void OpenReleases_Click(object sender, RoutedEventArgs e)
    {
        try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(_releasesUrl) { UseShellExecute = true }); }
        catch (Exception) { }
    }

    // ---------------------------------------------------------------- save

    void Browse_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.OpenFolderDialog { Title = L.T("pick.folder"), InitialDirectory = _folder };
        if (Native.ShowDialog(this, dlg, dlg.Title) != true) return;
        _folder = dlg.FolderName;
        FolderText.Text = _folder;
    }

    async void Save_Click(object sender, RoutedEventArgs e)
    {
        UpdateDirty();
        if (!SaveButton.IsEnabled) return;
        _saving = true;
        SaveButton.IsEnabled = false;
        try
        {
            var s = A.Settings;
            var startupChanged = s.StartWithWindows != (StartupBox.IsChecked == true);
            var name = NameBox.Text.Trim();
            if (name.Length > 0) s.DeviceName = name;
            s.SaveFolder = _folder;
            s.Ask = AskBox.IsChecked == true;
            s.StartWithWindows = StartupBox.IsChecked == true;
            var appleIdOn = AppleIdOnBox.IsChecked == true;
            var receive = appleIdOn && ReceiveContacts.IsChecked == true && AppleId.Present ? "contacts" : "everyone";
            var receiveChanged = receive != s.Receive || appleIdOn != s.AppleIdOn;
            s.AppleIdOn = appleIdOn;
            s.Receive = receive;
            var hotkeyChanged = (HotkeyBox.IsChecked == true) != s.Hotkey || _hotkeyKeys != s.HotkeyKeys;
            s.Hotkey = HotkeyBox.IsChecked == true;
            s.HotkeyKeys = _hotkeyKeys;
            s.CheckUpdates = CheckUpdatesBox.IsChecked == true;
            s.PreviewUpdates = PreviewUpdatesBox.IsChecked == true;
            s.DeleteInstallerAfterInstall = DeleteInstallerBox.IsChecked == true;
            var language = (LanguageBox.SelectedItem as ComboBoxItem)?.Tag as string ?? "auto";
            var languageChanged = language != s.Language;
            s.Language = language;
            s.AlwaysAccept = [.. _trusted];
            s.HubIcon = HubDesktop.IsChecked == true ? "desktop" : HubLaptop.IsChecked == true ? "laptop" : "auto";
            var appearance = ChosenAppearance();
            var appearanceChanged = appearance != s.Appearance;
            s.Appearance = appearance;
            s.Save();
            // Full Explorer/shortcut registration is startup work, not work for every save.
            if (!A.UiDebugMode && startupChanged) Shell.SetStartup(s.StartWithWindows);
            A.Engine.Apply(s);
            var mode = ModeConcurrent.IsChecked == true ? "concurrent" : ModeOnDemand.IsChecked == true ? "ondemand" : "off";
            if (mode != (string?)A.Service.Last?["mode"]) A.Service.SetMode(mode);
            var fastUnlock = FastUnlockBox.IsChecked == true;
            if (fastUnlock != ((bool?)A.Service.Last?["fastUnlock"] ?? false)) A.Service.SetFastUnlock(fastUnlock);
            var coexPlan = CoexPlanBox.IsChecked == true;
            if (coexPlan != ((bool?)A.Service.Last?["coexPlan"] ?? false)) A.Service.SetCoexPlan(coexPlan);
            A.Window?.FolderChanged();
            var hotkeyTaken = hotkeyChanged && !A.ApplyHotkey();
            A.Window?.ShowMark();
            A.NotifyReceivingChanged();
            if (appearanceChanged) A.ApplyTheme();
            _savedDraft = ReadDraft();
            if (languageChanged) { A.ApplyLanguage(); Close(); return; }
            SavedText.Text = hotkeyTaken ? L.F("set.hotkeyTaken", Hotkey.Show(_hotkeyKeys)) : "";
            ShowSaveNotice();
            if (receiveChanged) await A.RestartEngineAsync();
        }
        catch (Exception ex)
        {
            SaveNotice.Visibility = Visibility.Collapsed;
            SavedText.Text = ex.Message;
        }
        finally { _saving = false; UpdateDirty(); }
    }

    int _saveNoticeGeneration;
    async void ShowSaveNotice()
    {
        var generation = ++_saveNoticeGeneration;
        var fresh = SaveNotice.Visibility != Visibility.Visible;
        SaveNotice.Visibility = Visibility.Visible;
        // The surface and foreground are separate tracks: the vector surface can
        // unfold, while the readable text never scales or becomes translucent glass.
        SaveNotice.Opacity = 1;
        AnimateNotice(SaveSurfaceScale, ScaleTransform.ScaleXProperty, 1, 240, fresh ? 0 : null);
        AnimateNotice(SaveSurfaceScale, ScaleTransform.ScaleYProperty, 1, 240, fresh ? 0 : null);
        if (SaveCheck.RenderTransform is not TranslateTransform) SaveCheck.RenderTransform = new TranslateTransform();
        if (SaveNoticeLabel.RenderTransform is not TranslateTransform) SaveNoticeLabel.RenderTransform = new TranslateTransform();
        AnimateNotice(SaveCheck, UIElement.OpacityProperty, 1, 80, fresh ? 0 : null, delay: 80);
        AnimateNotice((TranslateTransform)SaveCheck.RenderTransform, TranslateTransform.YProperty, 0, 220, fresh ? 5 : null, delay: 80);
        AnimateNotice(SaveCheck, System.Windows.Shapes.Shape.StrokeDashOffsetProperty, 0, 300, 20, delay: 100);
        AnimateNotice(SaveNoticeLabel, UIElement.OpacityProperty, 1, 140, fresh ? 0 : null, delay: 150);
        AnimateNotice((TranslateTransform)SaveNoticeLabel.RenderTransform, TranslateTransform.YProperty, 0, 200, fresh ? 4 : null, delay: 130);

        // The completed result gets one second of reading time. A later save
        // keeps this same mounted surface and supersedes all pending exits.
        await Task.Delay(Motion.Reduced ? 1000 : 1400);
        if (generation != _saveNoticeGeneration) return;
        AnimateNotice(SaveNoticeLabel, UIElement.OpacityProperty, 0, 100);
        AnimateNotice((TranslateTransform)SaveNoticeLabel.RenderTransform, TranslateTransform.YProperty, -3, 120);
        AnimateNotice(SaveCheck, UIElement.OpacityProperty, 0, 100, delay: 30);
        AnimateNotice(SaveSurfaceScale, ScaleTransform.ScaleXProperty, 0, 180, delay: 60, leaving: true);
        AnimateNotice(SaveSurfaceScale, ScaleTransform.ScaleYProperty, 0, 180, delay: 60, leaving: true);
        if (!Motion.Reduced) await Task.Delay(240);
        if (generation == _saveNoticeGeneration) SaveNotice.Visibility = Visibility.Collapsed;
    }

    static void AnimateNotice(DependencyObject target, DependencyProperty property, double to, int ms,
        double? from = null, int delay = 0, bool leaving = false)
    {
        var start = from ?? (double)target.GetValue(property);
        void Begin(AnimationTimeline? animation)
        {
            if (target is UIElement element) element.BeginAnimation(property, animation);
            else if (target is Animatable value) value.BeginAnimation(property, animation);
        }
        Begin(null);
        target.SetValue(property, Motion.Reduced ? to : start);
        if (Motion.Reduced) return;
        Begin(new DoubleAnimation(start, to, TimeSpan.FromMilliseconds(ms))
        {
            BeginTime = TimeSpan.FromMilliseconds(delay),
            EasingFunction = new CubicEase { EasingMode = leaving ? EasingMode.EaseIn : EasingMode.EaseOut },
            FillBehavior = FillBehavior.HoldEnd,
        });
    }

    void Cancel_Click(object sender, RoutedEventArgs e) => Close();

    // ------------------------------------------------ Apple ID, Contacts Only

    void SyncReceivingState()
    {
        var s = A.Settings;
        bool present = AppleId.Present;
        bool featureChanged = s.AppleIdOn != _observedAppleIdOn;
        bool scopeChanged = s.Receive != _observedReceive;
        bool identityChanged = present != _observedIdentity;
        _observedAppleIdOn = s.AppleIdOn; _observedReceive = s.Receive; _observedIdentity = present;
        if (featureChanged) AppleIdOnBox.IsChecked = s.AppleIdOn;
        ShowIdentity();
        // A receive on/off toggle must not overwrite an unsaved scope edit.
        if (featureChanged || scopeChanged || identityChanged) ShowReceive();
        if (_savedDraft is not null)
        {
            var draft = ReadDraft();
            if (featureChanged) _savedDraft["apple"] = draft["apple"];
            if (featureChanged || scopeChanged || identityChanged) _savedDraft["receive"] = draft["receive"];
            QueueDirtyCheck();
        }
    }

    /// <summary>Who can send, as saved (also when the AirDrop mark changed it while this window is open).</summary>
    public void ShowReceive()
    {
        (A.Settings.AppleIdOn && A.Settings.Receive == "contacts" && AppleId.Present ? ReceiveContacts : ReceiveEveryone).IsChecked = true;
    }

    /// <summary>The page below the experimental switch works only while it is on.</summary>
    void AppleIdOn_Changed(object sender, RoutedEventArgs e)
    {
        if (AppleIdBody is null) return; // still loading
        AppleIdBody.IsEnabled = AppleIdOnBox.IsChecked == true;
        AppleIdBody.Opacity = AppleIdBody.IsEnabled ? 1 : 0.5;
    }

    /// <summary>What identity there is, and which buttons make sense with it.</summary>
    void ShowIdentity()
    {
        var present = AppleId.Present;
        ReceiveContacts.IsEnabled = present;
        if (!present) ReceiveEveryone.IsChecked = true;
        SignInButton.IsEnabled = AppleId.CanSignIn;
        SignInButton.ToolTip = AppleId.CanSignIn ? null : L.T("aid.noTool");
        SignOutButton.Visibility = present || AppleId.HasLocalState ? Visibility.Visible : Visibility.Collapsed;
        ContactsButton.Visibility = AppleId.Source == "appleid" ? Visibility.Collapsed : Visibility.Visible;
        if (!present) { IdentityText.Text = L.T("aid.none"); return; }
        var until = AppleId.CertificateUntil is { } d ? d.ToString("yyyy-MM-dd") : "?";
        var who = AppleId.Source == "appleid" ? L.F("aid.signedIn", AppleId.Account) : L.T("aid.fromMac");
        IdentityText.Text = who + " · " + L.F("aid.until", until) + " · " + L.F("aid.contactCount", AppleId.ContactCount);
    }

    void IdentityChanged()
    {
        ShowIdentity();
        ShowReceive();
        A.RestartEngine();
        A.NotifyReceivingChanged();
    }

    void SignIn_Click(object sender, RoutedEventArgs e)
    {
        var w = new AppleIdWindow { Owner = this };
        if (w.ShowDialog() == true)
        {
            IdentityChanged();
            SavedText.Text = "✓ " + L.T("aid.done");
        }
    }

    void ImportMac_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.OpenFolderDialog { Title = L.T("aid.pickMac") };
        if (Native.ShowDialog(this, dlg, dlg.Title) != true) return;
        var why = AppleId.ImportFromMac(dlg.FolderName, null);
        if (why == "need_passphrase")
        {
            var pass = Microsoft.VisualBasic.Interaction.InputBox(L.T("aid.passphrase"), "AetherDrop");
            if (pass.Length == 0) return;
            why = AppleId.ImportFromMac(dlg.FolderName, pass);
        }
        if (why is not null) { MessageBox.Show(this, why, "AetherDrop", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
        IdentityChanged();
        SavedText.Text = "✓ " + L.T("aid.imported");
    }

    void ImportContacts_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.OpenFileDialog { Title = L.T("aid.pickVcf"), Filter = "vCard (*.vcf)|*.vcf" };
        if (Native.ShowDialog(this, dlg, dlg.Title) != true) return;
        try
        {
            var people = AppleId.ImportContacts(dlg.FileName);
            IdentityChanged();
            SavedText.Text = "✓ " + L.F("aid.contactsImported", people);
        }
        catch (Exception x) { MessageBox.Show(this, x.Message, "AetherDrop", MessageBoxButton.OK, MessageBoxImage.Warning); }
    }

    void SignOut_Click(object sender, RoutedEventArgs e)
    {
        if (MessageBox.Show(this, L.T("aid.signOutAsk"), "AetherDrop", MessageBoxButton.OKCancel, MessageBoxImage.Question) != MessageBoxResult.OK) return;
        try { AppleId.SignOut(); } catch (Exception x) { MessageBox.Show(this, x.Message, "AetherDrop"); return; }
        if (A.Settings.Receive == "contacts") { A.Settings.Receive = "everyone"; A.Settings.Save(); }
        IdentityChanged();
    }

    // ------------------------------------------------- recording the shortcut

    string _hotkeyKeys = Hotkey.Default;
    bool _recording;

    void ShowHotkey()
    {
        HotkeyText.Text = _recording ? L.T("set.hotkeyPress") : Hotkey.Show(_hotkeyKeys);
        QueueDirtyCheck();
    }

    void HotkeyRecorder_Click(object sender, RoutedEventArgs e)
    {
        _recording = !_recording;
        ShowHotkey();
    }

    void HotkeyRecorder_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (!_recording) return;
        e.Handled = true;
        var key = e.Key == System.Windows.Input.Key.System ? e.SystemKey : e.Key;
        if (key == System.Windows.Input.Key.Escape) { _recording = false; ShowHotkey(); return; }
        // Keyboard.Modifiers leaves out Win; ask for it directly.
        var mods = System.Windows.Input.Keyboard.Modifiers;
        if (System.Windows.Input.Keyboard.IsKeyDown(System.Windows.Input.Key.LWin) || System.Windows.Input.Keyboard.IsKeyDown(System.Windows.Input.Key.RWin))
            mods |= System.Windows.Input.ModifierKeys.Windows;
        if (Hotkey.FromKeys(mods, key) is not { } combo) return; // only modifiers so far, or no modifier
        _hotkeyKeys = combo;
        _recording = false;
        ShowHotkey();
    }

    void HotkeyRecorder_LostFocus(object sender, System.Windows.Input.KeyboardFocusChangedEventArgs e)
    {
        if (!_recording) return;
        _recording = false;
        ShowHotkey();
    }

    void HotkeyReset_Click(object sender, RoutedEventArgs e)
    {
        _recording = false;
        _hotkeyKeys = Hotkey.Default;
        ShowHotkey();
    }
}
