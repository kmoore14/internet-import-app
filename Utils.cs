using System;
using System.Linq;

public static class Utils
{
    public static bool IsValidZip5(string? zip)
    {
        if (string.IsNullOrWhiteSpace(zip)) return false;
        zip = zip.Trim();
        return zip.Length == 5 && zip.All(char.IsDigit);
    }

    public static string FormatPercent(double fraction)
    {
        return (fraction * 100).ToString("0.00") + "%";
    }

    public static string HtmlEncode(string s)
    {
        return System.Net.WebUtility.HtmlEncode(s);
    }
}
