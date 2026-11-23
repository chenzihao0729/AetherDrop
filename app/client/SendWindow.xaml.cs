// SPDX-License-Identifier: GPL-2.0-only
using System.Collections.Specialized;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace NekoDrop;

/// <summary>
/// The window "用 nekodrop 发送" opens, like the Windows share sheet: the
/// files on top, nearby AirDrop receivers below; a click sends, and each
/// receiver shows its own progress.
/// </summary>
public partial class SendWindow : Window
{
    static App A => App.Instance;

    public SendWindow()
    {
        InitializeComponent();
        DataContext = A.Model;
        SourceInitialized += (_, _) => { if (Native.ApplyMica(this)) Background = Brushes.Transparent; };
        A.Model.Devices.CollectionChanged += OnChanged;
        A.Model.Files.CollectionChanged += OnChanged;
        Closed += (_, _) =>
        {
            A.Model.Devices.CollectionChanged -= OnChanged;
            A.Model.Files.CollectionChanged -= OnChanged;
        };
        Drop += (_, e) =>
        {
            if (e.Data.GetData(DataFormats.FileDrop) is string[] paths)
                foreach (var p in paths) if (!A.Model.Files.Contains(p)) A.Model.Files.Add(p);
        };
        Refresh();
    }

    void OnChanged(object? s, NotifyCollectionChangedEventArgs e) => Refresh();

    public void Refresh()
    {
        NoDevices.Visibility = A.Model.Devices.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        var files = A.Model.Files;
        if (files.Count == 0) { SummaryText.Text = L.T("pick.files"); SizeText.Text = ""; return; }
        SummaryText.Text = L.F("done.items", files.Count);
        long bytes = 0;
        foreach (var f in files)
        {
            try
            {
                if (File.Exists(f)) bytes += new FileInfo(f).Length;
                else if (Directory.Exists(f))
                    bytes += new DirectoryInfo(f).EnumerateFiles("*", SearchOption.AllDirectories).Sum(x => x.Length);
            }
            catch (Exception) { }
        }
        SizeText.Text = $"{L.F("done.items", files.Count)} · {Size(bytes)}";
    }

    static string Size(long b) =>
        b >= 1L << 30 ? $"{b / (double)(1L << 30):0.0} GB" :
        b >= 1L << 20 ? $"{b / (double)(1L << 20):0.0} MB" :
        b >= 1L << 10 ? $"{b / 1024.0:0} KB" : $"{b} B";

    void Device_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.Tag is not Device d || d.Busy) return;
        if (A.Model.Files.Count == 0)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog { Multiselect = true, Title = L.T("pick.files") };
            if (dlg.ShowDialog(this) != true) return;
            foreach (var f in dlg.FileNames) A.Model.Files.Add(f);
        }
        A.SendTo(d);
    }

    void More_Click(object sender, RoutedEventArgs e) => A.ShowWindow();
    void Close_Click(object sender, RoutedEventArgs e) => Close();
}
