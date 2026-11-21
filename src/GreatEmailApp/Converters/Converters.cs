// FILE: src/GreatEmailApp/Converters/Converters.cs
// Created: 2026-04-29 | Revised: 2026-11-21 | Rev: 2
// Changed by: Claude Opus 5.5 on behalf of James Reed

using System;
using System.Collections;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace GreatEmailApp.Converters;

public sealed class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => (value is bool b && b) ? Visibility.Visible : Visibility.Collapsed;
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => value is Visibility v && v == Visibility.Visible;
}

public sealed class InverseBoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => (value is bool b && b) ? Visibility.Collapsed : Visibility.Visible;
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

public sealed class BoolToFontWeightConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => (value is bool b && b) ? FontWeights.SemiBold : FontWeights.Normal;
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

public sealed class StringEqualsToBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => string.Equals(value as string, parameter as string, StringComparison.OrdinalIgnoreCase);
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => (value is bool b && b) ? parameter ?? "" : Binding.DoNothing;
}

public sealed class StringEqualsToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => string.Equals(value as string, parameter as string, StringComparison.OrdinalIgnoreCase)
            ? Visibility.Visible : Visibility.Collapsed;
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

public sealed class IntGreaterThanZeroToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => (value is int i && i > 0) ? Visibility.Visible : Visibility.Collapsed;
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

public sealed class HexToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not string hex || string.IsNullOrEmpty(hex))
            return Brushes.Gray;
        var s = hex.TrimStart('#');
        if (s.Length == 3) s = string.Concat(s[0], s[0], s[1], s[1], s[2], s[2]);
        try
        {
            var c = Color.FromRgb(
                System.Convert.ToByte(s.Substring(0, 2), 16),
                System.Convert.ToByte(s.Substring(2, 2), 16),
                System.Convert.ToByte(s.Substring(4, 2), 16));
            var b = new SolidColorBrush(c);
            b.Freeze();
            return b;
        }
        catch { return Brushes.Gray; }
    }
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

public sealed class NonEmptyStringToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => (value is string s && !string.IsNullOrEmpty(s)) ? Visibility.Visible : Visibility.Collapsed;
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

public sealed class NullToVisibilityConverter : IValueConverter
{
    /// <summary>parameter="invert" makes null → Visible, non-null → Collapsed.</summary>
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var invert = string.Equals(parameter as string, "invert", StringComparison.OrdinalIgnoreCase);
        var hasValue = value is not null;
        if (invert) hasValue = !hasValue;
        return hasValue ? Visibility.Visible : Visibility.Collapsed;
    }
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

public sealed class CollectionNotEmptyToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is ICollection c) return c.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        if (value is IEnumerable e)
        {
            var en = e.GetEnumerator();
            return en.MoveNext() ? Visibility.Visible : Visibility.Collapsed;
        }
        return Visibility.Collapsed;
    }
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

/// <summary>
/// One-line description of a mail rule for the Settings → Rules list, e.g.
/// "1 condition · Delete permanently · New incoming mail only · Paused".
/// </summary>
public sealed class RuleSummaryConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not GreatEmailApp.Core.Models.MailRule r) return "";
        var parts = new System.Collections.Generic.List<string>
        {
            r.Conditions.Count == 1 ? "1 condition" : $"{r.Conditions.Count} conditions",
        };
        foreach (var a in r.Actions)
        {
            parts.Add(a.Type switch
            {
                GreatEmailApp.Core.Models.RuleAction.MoveToFolder => $"Move to {a.Value}",
                GreatEmailApp.Core.Models.RuleAction.MarkRead => "Mark as read",
                GreatEmailApp.Core.Models.RuleAction.Flag => "Flag",
                GreatEmailApp.Core.Models.RuleAction.Delete when a.IsPermanentDelete => "Delete permanently",
                GreatEmailApp.Core.Models.RuleAction.Delete => "Move to Trash",
                _ => a.Type.ToString(),
            });
        }
        if (r.IncomingOnly) parts.Add("New incoming mail only");
        if (!r.IsEnabled) parts.Add("Paused");
        return string.Join(" · ", parts);
    }
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
