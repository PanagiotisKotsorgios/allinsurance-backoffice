using AllInsurance.Desktop.Settings;
using System.Diagnostics;
using System.IO.Compression;
using System.Net;
using System.Text;
using System.Xml;

namespace AllInsurance.Desktop.Controls;

internal sealed record TableExportData(string Title, string[] Headers, string[][] Rows)
{
    public string[]? RowStyles { get; init; }
}

internal static class TableDataExport
{
    private static string[] ProfileLines()
    {
        var profile = AgencyProfileSettings.Load();
        return [profile.Name, $"ΑΦΜ: {profile.TaxNumber}",
            $"{profile.Address}, {profile.PostalCode} {profile.City}".Trim(' ', ','),
            $"Τηλ.: {profile.Phone}  Email: {profile.Email}".Trim()];
    }

    public static void Csv(string path, TableExportData data, int[] columns)
    {
        using var writer = new StreamWriter(path, false, new UTF8Encoding(true));
        foreach (var line in ProfileLines().Where(line => !string.IsNullOrWhiteSpace(line)))
            writer.WriteLine(Escape(line));
        writer.WriteLine(Escape(data.Title));
        writer.WriteLine(Escape($"Εξαγωγή: {DateTime.Now:dd/MM/yyyy HH:mm}"));
        writer.WriteLine();
        writer.WriteLine(string.Join(';', columns.Select(i => Escape(data.Headers[i]))));
        foreach (var row in data.Rows)
            writer.WriteLine(string.Join(';', columns.Select(i => Escape(i < row.Length ? row[i] : ""))));
        writer.WriteLine();
        writer.WriteLine(string.Join(';', Enumerable.Repeat("", Math.Max(0, columns.Length - 1))
            .Append(Escape(ExportBranding.Text))));
    }

    public static void Excel(string path, TableExportData data, int[] columns)
    {
        using var file = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
        using var zip = new ZipArchive(file, ZipArchiveMode.Create);
        Write(zip, "[Content_Types].xml", """
            <?xml version="1.0" encoding="utf-8"?>
            <Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types"><Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/><Default Extension="xml" ContentType="application/xml"/><Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/><Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/><Override PartName="/xl/styles.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml"/></Types>
            """);
        Write(zip, "_rels/.rels", """
            <?xml version="1.0" encoding="utf-8"?>
            <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/></Relationships>
            """);
        Write(zip, "xl/workbook.xml", """
            <?xml version="1.0" encoding="utf-8"?>
            <workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships"><sheets><sheet name="Δεδομένα" sheetId="1" r:id="rId1"/></sheets></workbook>
            """);
        Write(zip, "xl/_rels/workbook.xml.rels", """
            <?xml version="1.0" encoding="utf-8"?>
            <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml"/><Relationship Id="rId2" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles" Target="styles.xml"/></Relationships>
            """);
        Write(zip, "xl/styles.xml", """
            <?xml version="1.0" encoding="utf-8"?>
            <styleSheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main"><fonts count="2"><font><sz val="11"/><name val="Calibri"/></font><font><b/><sz val="11"/><name val="Calibri"/></font></fonts><fills count="5"><fill><patternFill patternType="none"/></fill><fill><patternFill patternType="gray125"/></fill><fill><patternFill patternType="solid"><fgColor rgb="FFD7E7F1"/><bgColor indexed="64"/></patternFill></fill><fill><patternFill patternType="solid"><fgColor rgb="FFCBE8D1"/><bgColor indexed="64"/></patternFill></fill><fill><patternFill patternType="solid"><fgColor rgb="FFF4D9D9"/><bgColor indexed="64"/></patternFill></fill></fills><borders count="1"><border><left/><right/><top/><bottom/><diagonal/></border></borders><cellStyleXfs count="1"><xf numFmtId="0" fontId="0" fillId="0" borderId="0"/></cellStyleXfs><cellXfs count="5"><xf numFmtId="0" fontId="0" fillId="0" borderId="0" xfId="0"/><xf numFmtId="0" fontId="1" fillId="2" borderId="0" xfId="0" applyFill="1"/><xf numFmtId="0" fontId="1" fillId="3" borderId="0" xfId="0" applyFill="1"/><xf numFmtId="0" fontId="1" fillId="2" borderId="0" xfId="0" applyFill="1"/><xf numFmtId="0" fontId="0" fillId="4" borderId="0" xfId="0" applyFill="1"/></cellXfs></styleSheet>
            """);
        var entry = zip.CreateEntry("xl/worksheets/sheet1.xml");
        using var stream = entry.Open();
        using var xml = XmlWriter.Create(stream, new XmlWriterSettings { Encoding = new UTF8Encoding(false) });
        xml.WriteStartDocument();
        xml.WriteStartElement("worksheet", "http://schemas.openxmlformats.org/spreadsheetml/2006/main");
        var headerRow = ProfileLines().Count(line => !string.IsNullOrWhiteSpace(line)) + 4;
        xml.WriteStartElement("sheetViews");
        xml.WriteStartElement("sheetView");
        xml.WriteAttributeString("workbookViewId", "0");
        xml.WriteStartElement("pane");
        xml.WriteAttributeString("ySplit", headerRow.ToString());
        xml.WriteAttributeString("topLeftCell", "A" + (headerRow + 1));
        xml.WriteAttributeString("state", "frozen");
        xml.WriteEndElement(); xml.WriteEndElement(); xml.WriteEndElement();
        xml.WriteStartElement("cols");
        for (var index = 0; index < columns.Length; index++)
        {
            xml.WriteStartElement("col");
            xml.WriteAttributeString("min", (index + 1).ToString());
            xml.WriteAttributeString("max", (index + 1).ToString());
            xml.WriteAttributeString("width", Math.Clamp(data.Headers[columns[index]].Length + 8, 18, 42).ToString());
            xml.WriteAttributeString("customWidth", "1");
            xml.WriteEndElement();
        }
        xml.WriteEndElement();
        xml.WriteStartElement("sheetData");
        var rowNumber = 0;
        foreach (var line in ProfileLines().Where(line => !string.IsNullOrWhiteSpace(line))) Row([line]);
        Row([data.Title]);
        Row([$"Εξαγωγή: {DateTime.Now:dd/MM/yyyy HH:mm}"]);
        Row([]);
        Row(columns.Select(i => data.Headers[i]).ToArray(), 1);
        for (var index = 0; index < data.Rows.Length; index++)
        {
            var style = data.RowStyles is { } kinds && index < kinds.Length ? kinds[index] : "";
            var rowStyle = style is "Subtotal" or "PartnerTotal" or "GrandTotal" or "Spacer" ? 2 :
                style is "Group" or "PartnerHeader" ? 3 : 0;
            Row(columns.Select(i => i < data.Rows[index].Length ? data.Rows[index][i] : "").ToArray(),
                rowStyle, style == "Detail");
        }
        Row([]);
        Row(Enumerable.Repeat("", Math.Max(0, columns.Length - 1))
            .Append(ExportBranding.Text).ToArray());
        xml.WriteEndElement();
        if (data.Rows.Length > 0 && columns.Length > 0)
        {
            xml.WriteStartElement("autoFilter");
            xml.WriteAttributeString("ref", $"A{headerRow}:{ColumnName(columns.Length)}{headerRow + data.Rows.Length}");
            xml.WriteEndElement();
        }
        xml.WriteEndElement();
        xml.WriteEndDocument();

        void Row(string[] values, int style = 0, bool commissions = false)
        {
            xml.WriteStartElement("row");
            xml.WriteAttributeString("r", (++rowNumber).ToString());
            for (var column = 0; column < values.Length; column++)
            {
                xml.WriteStartElement("c");
                xml.WriteAttributeString("t", "inlineStr");
                var cellStyle = commissions &&
                    (data.Headers[columns[column]].Contains("Προμήθεια") ||
                     data.Headers[columns[column]].Contains("Στην έδρα"))
                    ? data.Headers[columns[column]].Contains("συνεργάτη") ||
                        data.Headers[columns[column]].Contains("Στην έδρα") ? 2 : 4 : style;
                if (cellStyle != 0) xml.WriteAttributeString("s", cellStyle.ToString());
                xml.WriteStartElement("is");
                xml.WriteElementString("t", values[column] ?? "");
                xml.WriteEndElement(); xml.WriteEndElement();
            }
            xml.WriteEndElement();
        }
    }

    private static string ColumnName(int index)
    {
        var result = "";
        while (index > 0)
        {
            index--;
            result = (char)('A' + index % 26) + result;
            index /= 26;
        }
        return result;
    }

    public static string Print(TableExportData data, int[] columns, bool landscape)
    {
        var folder = Path.Combine(Path.GetTempPath(), "AllInsurance", "Print");
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, $"print-{DateTime.Now:yyyyMMdd-HHmmss-fff}.html");
        var html = new StringBuilder("<!doctype html><html lang='el'><meta charset='utf-8'><style>");
        html.Append("@page{size:A4 ").Append(landscape ? "landscape" : "portrait")
            .Append(";margin:12mm}body{font:10pt Arial;color:#172235}h1{font-size:17pt}small{color:#526276}table{border-collapse:collapse;width:100%;table-layout:fixed}th,td{border:1px solid #8e9aa8;padding:5px;overflow-wrap:anywhere}th{background:#e5eaf0}tr{break-inside:avoid}thead{display:table-header-group}tr.PartnerHeader, tr.Group{background:#d7e7f1;font-weight:bold}tr.Subtotal,tr.PartnerTotal,tr.GrandTotal,tr.Spacer{background:#cbe8d1;font-weight:bold}tr.Spacer td{height:9px;padding:0}tr.Detail td.office-commission{background:#f4d9d9}tr.Detail td.partner-commission{background:#cbe8d1}</style><body onload='window.print()'>");
        foreach (var line in ProfileLines().Where(line => !string.IsNullOrWhiteSpace(line)))
            html.Append("<div><small>").Append(WebUtility.HtmlEncode(line)).Append("</small></div>");
        html.Append("<h1>").Append(WebUtility.HtmlEncode(data.Title)).Append("</h1><small>")
            .Append(DateTime.Now.ToString("dd/MM/yyyy HH:mm")).Append(" · ")
            .Append(data.Rows.Length).Append(" εγγραφές</small><table><thead><tr>");
        foreach (var i in columns) html.Append("<th>").Append(WebUtility.HtmlEncode(data.Headers[i])).Append("</th>");
        html.Append("</tr></thead><tbody>");
        for (var rowIndex = 0; rowIndex < data.Rows.Length; rowIndex++)
        {
            var row = data.Rows[rowIndex];
            var style = data.RowStyles is { } kinds && rowIndex < kinds.Length ? kinds[rowIndex] : "";
            html.Append("<tr class='").Append(WebUtility.HtmlEncode(style)).Append("'>");
            foreach (var i in columns)
            {
                var css = data.Headers[i].Contains("Προμήθεια") || data.Headers[i].Contains("Στην έδρα")
                    ? data.Headers[i].Contains("συνεργάτη") || data.Headers[i].Contains("Στην έδρα")
                        ? "partner-commission" : "office-commission" : "";
                html.Append("<td class='").Append(css).Append("'>")
                    .Append(WebUtility.HtmlEncode(i < row.Length ? row[i] : "")).Append("</td>");
            }
            html.Append("</tr>");
        }
        html.Append("</tbody></table><footer style='margin-top:18px;text-align:right;color:#526276;font-size:8pt'>")
            .Append(WebUtility.HtmlEncode(ExportBranding.Text)).Append("</footer></body></html>");
        File.WriteAllText(path, html.ToString(), Encoding.UTF8);
        Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        return path;
    }

    private static void Write(ZipArchive zip, string name, string content)
    {
        using var writer = new StreamWriter(zip.CreateEntry(name).Open(), new UTF8Encoding(false));
        writer.Write(content);
    }

    private static string Escape(string? value) =>
        "\"" + (value ?? "").Replace("\"", "\"\"") + "\"";
}
