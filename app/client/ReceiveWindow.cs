// SPDX-License-Identifier: GPL-2.0-only
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace NekoDrop;

public sealed class ReceiveWindow : FloatingCard
{
    const double FooterHeight = 28;
    const double ButtonUnitWidth = 72;
    const double ButtonGap = 6;
    const double RateFontSize = 22;
    readonly Request _request;
    readonly Action<string, bool> _answer;
    readonly Action<string> _openFolder;
    string? _savedFolder;
    readonly TextBlock _heading, _rate, _percent, _sender;
    readonly FrameworkElement _deviceGlyph;
    readonly Grid _illustration;
    readonly System.Windows.Shapes.Path _completedGlyph;
    readonly Button _close;
    readonly Button _openLocation;
    readonly DispatcherTimer _speedTimer = new() { Interval = TimeSpan.FromMilliseconds(250) };
    DispatcherTimer? _finishTimer;
    readonly Grid _detail;
    readonly Grid _footer = new() { Height = FooterHeight, VerticalAlignment = VerticalAlignment.Bottom };
    readonly Grid _actions = new();
    readonly Ring _ring = new();
    readonly Stopwatch _clock = Stopwatch.StartNew();
    readonly System.Drawing.Point _screen;
    double _lastBytes, _latestBytes, _lastTime;
    bool _transferring;
    double _destination;
    bool _accepted, _finished, _hidden;
    public string RequestId => _request.Id;
    public ReceiveWindow(Request request, Action<string, bool> answer, Action<string>? openFolder = null) : base(302, 142)
    {
        _openFolder = openFolder ?? OpenFolder;
        _request = request; _answer = answer; _screen = System.Windows.Forms.Cursor.Position;
        Title = Text("收到隔空投送邀请", "隔空投送 invitation");
        Body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(77) }); Body.ColumnDefinitions.Add(new ColumnDefinition());
        var illustration = _illustration = new Grid { Width = 72, Height = 72, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(-4, 0, 6, 0) };
        illustration.Children.Add(new Ellipse { Fill = Palette.Brush(214, 232, 255), Width = 52, Height = 52 });
        _ring.Width = 72; _ring.Height = 72; _ring.Visibility = Visibility.Collapsed; illustration.Children.Add(_ring);
        var device = new Device { Id = "", Name = request.Sender, Model = request.Model };
        if (device.Glyph == "\uE8EA")
        {
            var phone = new Grid { Width = 18, Height = 28, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            phone.Children.Add(new Border { BorderBrush = Ink, BorderThickness = new Thickness(1.6), CornerRadius = new CornerRadius(4) });
            phone.Children.Add(new Border { Name = "DynamicIsland", Width = 7, Height = 2.5, CornerRadius = new CornerRadius(1.25), Background = Ink,
                HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 3.5, 0, 0) });
            _deviceGlyph = phone;
        }
        else
        {
            var glyph = Label(device.Glyph, 26);
            glyph.FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"); glyph.HorizontalAlignment = HorizontalAlignment.Center;
            _deviceGlyph = glyph;
        }
        illustration.Children.Add(_deviceGlyph);
        _completedGlyph = new System.Windows.Shapes.Path
        {
            Data = Geometry.Parse("M 2,11 L 10,19 L 26,3"), Width = 28, Height = 22,
            Stroke = Blue, StrokeThickness = 3, StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round, StrokeLineJoin = PenLineJoin.Round,
            HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
            Visibility = Visibility.Collapsed,
        };
        illustration.Children.Add(_completedGlyph);
        Body.Children.Add(illustration);
        // Fixed slots keep all three positions identical across states,
        // regardless of the current footer's font size or button height.
        _detail = new Grid { Height = 78, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(2, 0, 0, 0) };
        _detail.RowDefinitions.Add(new RowDefinition { Height = new GridLength(22) });
        _detail.RowDefinitions.Add(new RowDefinition { Height = new GridLength(28) });
        _detail.RowDefinitions.Add(new RowDefinition { Height = new GridLength(FooterHeight) });
        _heading = Label(Text("收到隔空投送邀请", "隔空投送 invitation"), 15, true); _heading.VerticalAlignment = VerticalAlignment.Top; _heading.Margin = new Thickness(0, 0, 14, 0); _detail.Children.Add(_heading);
        var sender = _sender = Label(request.Sender, 13, false, Muted); Grid.SetRow(sender, 1); _detail.Children.Add(sender);
        _actions.Height = FooterHeight; _actions.Width = ButtonUnitWidth * 2 + ButtonGap; _actions.HorizontalAlignment = HorizontalAlignment.Left; _actions.VerticalAlignment = VerticalAlignment.Bottom;
        _actions.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(ButtonUnitWidth + ButtonGap) });
        _actions.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(ButtonUnitWidth) });
        var decline = FooterButton(Text("拒绝", "Decline"), () => { if (!_accepted && !_finished) _answer(request.Id, false); }); decline.Margin = new Thickness(0, 0, ButtonGap, 0);
        var accept = FooterButton(Text("接受", "Accept"), () => { if (!_accepted && !_finished) _answer(request.Id, true); }, true);
        Grid.SetRow(_footer, 2); _detail.Children.Add(_footer);
        _actions.Children.Add(decline); Grid.SetColumn(accept, 1); _actions.Children.Add(accept); _footer.Children.Add(_actions);
        _openLocation = FooterButton(Text("打开所在位置", "Open folder"), () =>
        {
            if (_savedFolder is null) return;
            _openFolder(_savedFolder);
            _finishTimer?.Stop();
            Close();
        }, true, 2);
        _openLocation.Visibility = Visibility.Collapsed; _footer.Children.Add(_openLocation);
        _rate = Label("", RateFontSize, true); _rate.Height = _rate.LineHeight = FooterHeight; _rate.LineStackingStrategy = LineStackingStrategy.BlockLineHeight; _rate.VerticalAlignment = VerticalAlignment.Bottom; _rate.Visibility = Visibility.Collapsed; _footer.Children.Add(_rate);
        _percent = Label("", 12, false, Muted); _percent.VerticalAlignment = VerticalAlignment.Bottom; _percent.Visibility = Visibility.Collapsed; _footer.Children.Add(_percent);
        Grid.SetColumn(_detail, 1); Body.Children.Add(_detail);
        var close = _close = CloseButton(() => { if (!_accepted && !_finished) _answer(request.Id, false); else { _hidden = true; Dismiss(); } }); Grid.SetColumn(close, 1); Body.Children.Add(close);
        _speedTimer.Tick += (_, _) => SampleSpeed();
        Closed += (_, _) =>
        {
            _speedTimer.Stop(); _finishTimer?.Stop();
            foreach (var element in new FrameworkElement[] { _heading, _sender, _footer, _deviceGlyph, _ring, _close, _completedGlyph })
            {
                element.BeginAnimation(OpacityProperty, null);
                if (element.RenderTransform is TranslateTransform offset) offset.BeginAnimation(TranslateTransform.YProperty, null);
            }
            _completedGlyph.BeginAnimation(System.Windows.Shapes.Shape.StrokeDashOffsetProperty, null);
        };
    }
    static Button FooterButton(string text, Action action, bool primary = false, int widthUnits = 1)
    {
        var button = Button(text, action, primary);
        button.MinWidth = 0; button.Width = ButtonUnitWidth * widthUnits; button.Height = FooterHeight;
        button.HorizontalAlignment = HorizontalAlignment.Left; button.VerticalAlignment = VerticalAlignment.Bottom;
        return button;
    }
    internal static ReceiveWindow CreatePreview()
    {
        var card = new ReceiveWindow(new Request { Id = "preview-progress", Sender = "iPhone", Model = "iPhone15,2", What = "IMG_0074.JPG" }, (_, _) => { });
        card.Accept(); card.Update(68_000_000, 100_000_000); card._speedTimer.Stop(); card._rate.Text = "— MB/s";
        return card;
    }
    internal static ReceiveWindow CreateCompletedPreview()
    {
        var card = new ReceiveWindow(new Request { Id = "preview-complete", Sender = "iPhone", Model = "iPhone15,2", What = "IMG_0074.JPG" }, (_, _) => { });
        card.Finish(true, "", Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments));
        return card;
    }
    internal static ReceiveWindow CreateFailedPreview()
    {
        var card = new ReceiveWindow(new Request { Id = "preview-failed", Sender = "iPhone", Model = "iPhone15,2", What = "IMG_0074.JPG" }, (_, _) => { });
        card.Accept(); card.Finish(false, Text("连接中断", "Connection interrupted"));
        return card;
    }
    public void Present(int slot = 0)
    {
        var work = WorkArea(this, _screen); _destination = work.Right - Width - 6;
        Top = Math.Max(work.Top, work.Bottom - Height - 8 - slot * (Height - 12));
        FlyIn(work.Right + 12); Show();
    }
    void FlyIn(double from)
    {
        // Start off screen before Show; settle into the final coordinates once.
        // Moving the native window keeps both the layout and text at full size.
        var arrival = new DoubleAnimation(from, _destination, TimeSpan.FromSeconds(Motion ? .46 : 0))
        {
            EasingFunction = new QuinticEase { EasingMode = EasingMode.EaseOut },
            FillBehavior = FillBehavior.Stop,
        };
        Left = _destination; BeginAnimation(LeftProperty, arrival);
    }
    public void Accept()
    {
        if (_accepted || _finished) return; _accepted = true;
        _actions.Visibility = Visibility.Collapsed;
        // Reuse the final coordinates and fly the progress card in from the same edge.
        _heading.Text = Text("正在接收 0%", "Receiving 0%"); _rate.Text = Text("等待传输…", "Waiting…");
        _percent.Visibility = Visibility.Collapsed;
        _rate.Visibility = _ring.Visibility = Visibility.Visible;
        _detail.BeginAnimation(OpacityProperty, Animate(.84, 1, .18));
        if (IsVisible) FlyIn(_destination + Width + 18);
        _clock.Reset(); _lastBytes = _latestBytes = _lastTime = 0; _transferring = false;
    }
    public void Update(double done, double total)
    {
        Accept(); if (_finished) return;
        done = Math.Max(0, done);
        if (!_transferring || done < _latestBytes)
        {
            // Start timing with the first payload event, excluding the user's
            // decision and connection setup. An initial counter alone has no rate.
            _transferring = true; _clock.Restart();
            _lastBytes = done; _lastTime = 0;
            _rate.Text = Text("正在计算…", "Measuring…");
            _speedTimer.Start();
        }
        _latestBytes = done;
        var fraction = total > 0 ? Math.Clamp(done / total, 0, 1) : 0;
        _ring.Value = fraction;
        _heading.Text = total > 0 ? $"{Text("正在接收", "Receiving")} {fraction:P0}" : Text("正在接收", "Receiving");
    }
    void SampleSpeed()
    {
        if (!_transferring || _finished || Dismissing) return;
        double now = _clock.Elapsed.TotalSeconds;
        double seconds = now - _lastTime;
        if (seconds <= 0) return;
        double bytesPerSecond = Math.Max(0, _latestBytes - _lastBytes) / seconds;
        _lastTime = now; _lastBytes = _latestBytes;
        // The display is calculated solely from received bytes and monotonic
        // elapsed time. No new bytes naturally renders 0, including during stalls.
        _rate.Text = bytesPerSecond >= 999_500_000 ? $"{bytesPerSecond / 1_000_000_000:F1} GB/s" :
            bytesPerSecond >= 99_950_000 ? $"{bytesPerSecond / 1_000_000:F0} MB/s" :
            bytesPerSecond >= 999_500 ? $"{bytesPerSecond / 1_000_000:F1} MB/s" : $"{bytesPerSecond / 1000:F0} KB/s";
    }
    static void OpenFolder(string folder)
    {
        if (!System.IO.Directory.Exists(folder)) return;
        var start = new ProcessStartInfo("explorer.exe") { UseShellExecute = true };
        start.ArgumentList.Add(folder); Process.Start(start);
    }
    public void Finish(bool success, string detail, string? savedFolder = null)
    {
        if (_hidden || Dismissing || _finished) return; _finished = true;
        _speedTimer.Stop(); _clock.Stop();
        bool transition = _accepted && IsVisible && Motion;
        ReplaceSlot(_heading, () => _heading.Text = success
            ? Text("接收完成", "Received") : Text("接收已结束", "Transfer ended"), transition, 0);
        ReplaceSlot(_footer, () =>
        {
            _actions.Visibility = _rate.Visibility = Visibility.Collapsed;
            _openLocation.Visibility = success ? Visibility.Visible : Visibility.Collapsed;
            _percent.Visibility = success ? Visibility.Collapsed : Visibility.Visible;
            _percent.Text = success ? "" : Text("连接中断", "Connection interrupted");
            _percent.FontSize = 14; _percent.FontWeight = Typography.EmphasisWeight;
        }, transition, .10);
        FadeAway(_ring, transition);
        if (success)
        {
            _savedFolder = savedFolder;
            _openLocation.IsEnabled = savedFolder is not null;
            ReplaceSlot(_sender, () =>
            {
                _sender.Text = Text("已保存到本地", "Saved locally");
            }, transition, .05);
            FadeAway(_deviceGlyph, transition); FadeAway(_close, transition);
            _completedGlyph.Visibility = Visibility.Visible;
            if (transition)
            {
                _completedGlyph.Opacity = 0; _completedGlyph.StrokeDashOffset = 12;
                _completedGlyph.StrokeDashArray = new DoubleCollection { 12, 12 };
                var draw = Animate(12, 0, .26); draw.BeginTime = TimeSpan.FromSeconds(.08);
                draw.FillBehavior = FillBehavior.HoldEnd;
                draw.Completed += (_, _) =>
                {
                    _completedGlyph.StrokeDashOffset = 0;
                    _completedGlyph.BeginAnimation(System.Windows.Shapes.Shape.StrokeDashOffsetProperty, null);
                    _completedGlyph.ClearValue(System.Windows.Shapes.Shape.StrokeDashArrayProperty);
                };
                _completedGlyph.BeginAnimation(System.Windows.Shapes.Shape.StrokeDashOffsetProperty, draw);
                var reveal = Animate(0, 1, .12); reveal.BeginTime = TimeSpan.FromSeconds(.08);
                reveal.FillBehavior = FillBehavior.HoldEnd;
                reveal.Completed += (_, _) => { _completedGlyph.Opacity = 1; _completedGlyph.BeginAnimation(OpacityProperty, null); };
                _completedGlyph.BeginAnimation(OpacityProperty, reveal);
            }
        }
        var timer = _finishTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds((success ? 4 : 5) + (transition ? .4 : 0)) };
        timer.Tick += (_, _) => { timer.Stop(); Dismiss(); }; timer.Start();
    }
    void ReplaceSlot(FrameworkElement element, Action replace, bool motion, double delay)
    {
        if (!motion) { replace(); return; }
        var offset = new TranslateTransform(); element.RenderTransform = offset;
        element.IsHitTestVisible = false;
        var leave = Animate(1, 0, .10); leave.BeginTime = TimeSpan.FromSeconds(delay);
        var lift = Animate(0, -4, .10); lift.BeginTime = leave.BeginTime;
        leave.Completed += (_, _) =>
        {
            if (Dismissing || !IsVisible) return;
            replace();
            offset.BeginAnimation(TranslateTransform.YProperty, Animate(4, 0, .18));
            var enter = Animate(0, 1, .18);
            enter.Completed += (_, _) =>
            {
                element.RenderTransform = Transform.Identity; element.IsHitTestVisible = true;
            };
            element.BeginAnimation(OpacityProperty, enter);
        };
        offset.BeginAnimation(TranslateTransform.YProperty, lift);
        element.BeginAnimation(OpacityProperty, leave);
    }
    static void FadeAway(FrameworkElement element, bool motion)
    {
        if (!motion) { element.Visibility = Visibility.Collapsed; return; }
        var fade = Animate(1, 0, .12);
        fade.Completed += (_, _) => element.Visibility = Visibility.Collapsed;
        element.BeginAnimation(OpacityProperty, fade);
    }
    public override void Dismiss()
    {
        if (Dismissing) return; Dismissing = true;
        _speedTimer.Stop();
        var fly = Animate(Left, _destination + Width + 36, .28);
        fly.EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn };
        fly.Completed += (_, _) => Close(); BeginAnimation(LeftProperty, fly);
    }
    sealed class Ring : FrameworkElement
    {
        double _value;
        public double Value { get => _value; set { _value = value; InvalidateVisual(); } }
        protected override void OnRender(DrawingContext dc)
        {
            var center = new Point(ActualWidth / 2, ActualHeight / 2);
            double radius = Math.Max(0, Math.Min(ActualWidth, ActualHeight) / 2 - 4);
            dc.DrawEllipse(null, new Pen(Palette.Brush(222, 228, 233), 6), center, radius, radius);
            var angle = Math.Clamp(_value, .001, .99999) * Math.PI * 2;
            var end = new Point(center.X + radius * Math.Sin(angle), center.Y - radius * Math.Cos(angle));
            var figure = new PathFigure { StartPoint = new Point(center.X, center.Y - radius), IsClosed = false };
            figure.Segments.Add(new ArcSegment(end, new Size(radius, radius), 0, angle > Math.PI, SweepDirection.Clockwise, true));
            dc.DrawGeometry(null, new Pen(Blue, 6) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round }, new PathGeometry([figure]));
        }
    }
}
