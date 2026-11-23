// SPDX-License-Identifier: GPL-2.0-only
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace NekoDrop;

public abstract class Observable : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    protected void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
    protected void Raise(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

/// <summary>An AirDrop receiver nearby.</summary>
public sealed class Device : Observable
{
    public required string Id { get; init; }
    string _name = "", _model = "", _status = "";
    double _progress;
    bool _busy;
    public string Name { get => _name; set { Set(ref _name, value); Raise(nameof(Glyph)); } }
    public string Model { get => _model; set { Set(ref _model, value); Raise(nameof(Glyph)); } }
    public string Glyph
    {
        get
        {
            if (Model.StartsWith("Mac", StringComparison.OrdinalIgnoreCase)) return "\uE7F8";
            if (Model.StartsWith("iPad", StringComparison.OrdinalIgnoreCase)) return "\uE70A";
            if (Model.StartsWith("iPhone", StringComparison.OrdinalIgnoreCase)) return "\uE8EA";
            if (Name.Contains("iPad", StringComparison.OrdinalIgnoreCase)) return "\uE70A";
            if (Name.Contains("Mac", StringComparison.OrdinalIgnoreCase)) return "\uE7F8";
            return "\uE8EA";
        }
    }
    public string Status { get => _status; set => Set(ref _status, value); }
    public double Progress { get => _progress; set => Set(ref _progress, value); }
    public bool Busy { get => _busy; set => Set(ref _busy, value); }
}

/// <summary>Someone asking to send.</summary>
public sealed class Request : Observable
{
    public required string Id { get; init; }
    public required string Sender { get; init; }
    public required string Model { get; init; }
    public required string What { get; init; }
    public bool Answered { get; set; }
    string _status = L.T("req.waiting");
    double _progress;
    public string Status { get => _status; set => Set(ref _status, value); }
    public double Progress { get => _progress; set => Set(ref _progress, value); }
}

public sealed class Received
{
    public required string Name { get; init; }
    public required string Path { get; init; }
    public required string From { get; init; }
    public DateTime When { get; init; } = DateTime.Now;
}

public sealed class AppModel : Observable
{
    public ObservableCollection<Device> Devices { get; } = [];
    public ObservableCollection<Request> Requests { get; } = [];
    public ObservableCollection<string> Files { get; } = [];
    public ObservableCollection<Received> ReceivedFiles { get; } = [];
    string _status = L.T("status.connecting"), _detail = "";
    bool _looking;
    public string Status { get => _status; set => Set(ref _status, value); }
    public string Detail { get => _detail; set => Set(ref _detail, value); }
    public bool Looking { get => _looking; set => Set(ref _looking, value); }
}
