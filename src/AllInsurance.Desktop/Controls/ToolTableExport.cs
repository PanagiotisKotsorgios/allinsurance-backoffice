using System.Diagnostics;
using System.Net;
using System.Text;

namespace AllInsurance.Desktop.Controls;

internal static class ToolTableExport
{
    public static string Csv(string name, string[] headers, IEnumerable<string[]> rows)
    {
        var path = NewPath(name, ".csv");
        using var writer = new StreamWriter(path, false, new UTF8Encoding(true));
        writer.WriteLine(string.Join(';', headers.Select(Escape)));
        foreach (var row in rows) writer.WriteLine(string.Join(';', row.Select(Escape)));
        return path;
    }

    public static string Print(string name, string[] headers, IEnumerable<string[]> rows)
    {
        var path = NewPath(name, ".html");
        var html = new StringBuilder("<!doctype html><html lang='el'><meta charset='utf-8'><title>");
        html.Append(WebUtility.HtmlEncode(name)).Append("</title><style>body{font:13px Arial;margin:24px}table{border-collapse:collapse;width:100%}th,td{border:1px solid #536476;padding:6px;text-align:left}th{background:#dce4ee}</style><body onload='window.print()'><h1>");
        html.Append(WebUtility.HtmlEncode(name)).Append("</h1><table><thead><tr>");
        foreach (var header in headers) html.Append("<th>").Append(WebUtility.HtmlEncode(header)).Append("</th>");
        html.Append("</tr></thead><tbody>");
        foreach (var row in rows)
        {
            html.Append("<tr>");
            foreach (var value in row) html.Append("<td>").Append(WebUtility.HtmlEncode(value)).Append("</td>");
            html.Append("</tr>");
        }
        File.WriteAllText(path, html.Append("</tbody></table></body></html>").ToString(), Encoding.UTF8);
        Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        return path;
    }

    private static string NewPath(string name, string extension)
    {
        var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "AllInsurance", "Εξαγωγές");
        Directory.CreateDirectory(folder);
        return Path.Combine(folder, $"{name}-{DateTime.Now:yyyyMMdd-HHmmss}{extension}");
    }

    private static string Escape(string? value)
    {
        var text = value ?? "";
        return text.IndexOfAny([';', '"', '\n', '\r']) >= 0
            ? "\"" + text.Replace("\"", "\"\"") + "\"" : text;
    }
}
