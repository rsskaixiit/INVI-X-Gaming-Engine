using System.Windows.Media;
using Invix.Models;

namespace Invix.ViewModels;

public sealed class TweakRowViewModel : ObservableObject
{
    private static readonly Brush PendingBrush = Frozen("#5A5A5A");
    private static readonly Brush AppliedBrush = Frozen("#00FF41");
    private static readonly Brush RestoredBrush = Frozen("#20B8FF");
    private static readonly Brush FailedBrush = Frozen("#FF3B3B");

    private string _glyph = "[ -- ]";
    private string _detail = "not applied";
    private Brush _statusBrush = PendingBrush;

    public TweakRowViewModel(TweakDefinition definition)
    {
        Id = definition.Id;
        Name = definition.Name;
        RequiresAdmin = definition.RequiresAdmin;
    }

    public string Id { get; }

    public string Name { get; }

    public bool RequiresAdmin { get; }

    public string Glyph
    {
        get => _glyph;
        private set => SetProperty(ref _glyph, value);
    }

    public string Detail
    {
        get => _detail;
        private set => SetProperty(ref _detail, value);
    }

    public Brush StatusBrush
    {
        get => _statusBrush;
        private set => SetProperty(ref _statusBrush, value);
    }

    public void Set(TweakStatus status, string detail)
    {
        Glyph = status switch
        {
            TweakStatus.Applied => "[ OK ]",
            TweakStatus.Reverted => "[REV ]",
            TweakStatus.Restored => "[DEF ]",
            TweakStatus.Failed => "[FAIL]",
            _ => "[ -- ]",
        };

        StatusBrush = status switch
        {
            TweakStatus.Applied => AppliedBrush,
            TweakStatus.Reverted => RestoredBrush,
            TweakStatus.Restored => RestoredBrush,
            TweakStatus.Failed => FailedBrush,
            _ => PendingBrush,
        };

        Detail = string.IsNullOrWhiteSpace(detail) ? "no detail" : detail;
    }

    public void Reset()
    {
        Glyph = "[ -- ]";
        StatusBrush = PendingBrush;
        Detail = "not applied";
    }

    private static Brush Frozen(string hex)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        brush.Freeze();
        return brush;
    }
}