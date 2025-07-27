using ClosedXML.Excel;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace GadgetTools
{
    public enum OutputFormat
    {
        Markdown,
        CSV,
        JSON,
        HTML
    }

    public static class GadgetToolsConverter
    {
        public static string ConvertExcelToMarkdown(string filePath, string? targetSheet = null)
        {
            return ConvertExcel(filePath, targetSheet, OutputFormat.Markdown);
        }

        public static string ConvertExcel(string filePath, string? targetSheet = null, OutputFormat format = OutputFormat.Markdown)
        {
            return ConvertExcelWithOptions(filePath, targetSheet, format);
        }

        public static string ConvertExcelWithOptions(string filePath, string? targetSheet, OutputFormat format)
        {
            var sb = new StringBuilder();

            using (var workbook = new XLWorkbook(filePath))
            {
                if (targetSheet != null)
                {
                    var worksheet = workbook.Worksheets.FirstOrDefault(ws => ws.Name == targetSheet);
                    if (worksheet == null)
                    {
                        throw new ArgumentException($"シート '{targetSheet}' が見つかりません。");
                    }

                    return ConvertWorksheet(worksheet, format, targetSheet);
                }
                else
                {
                    if (format == OutputFormat.JSON)
                    {
                        return ConvertAllSheetsToJson(workbook);
                    }
                    else if (format == OutputFormat.HTML)
                    {
                        return ConvertAllSheetsToHtml(workbook);
                    }
                    else
                    {
                        foreach (var worksheet in workbook.Worksheets)
                        {
                            if (format == OutputFormat.Markdown)
                            {
                                sb.AppendLine($"# {worksheet.Name}");
                                sb.AppendLine();
                            }
                            sb.AppendLine(ConvertWorksheet(worksheet, format, worksheet.Name));
                            sb.AppendLine();
                        }
                    }
                }
            }

            return sb.ToString().Trim();
        }

        public static List<string> GetSheetNames(string filePath)
        {
            using (var workbook = new XLWorkbook(filePath))
            {
                return workbook.Worksheets.Select(ws => ws.Name).ToList();
            }
        }

        private static string ConvertWorksheet(IXLWorksheet worksheet, OutputFormat format, string sheetName)
        {
            return format switch
            {
                OutputFormat.Markdown => ConvertWorksheetToMarkdown(worksheet),
                OutputFormat.CSV => ConvertWorksheetToCsv(worksheet),
                OutputFormat.JSON => ConvertWorksheetToJson(worksheet),
                OutputFormat.HTML => ConvertWorksheetToHtml(worksheet, sheetName),
                _ => ConvertWorksheetToMarkdown(worksheet)
            };
        }

        private static string ConvertWorksheetToMarkdown(IXLWorksheet worksheet)
        {
            var usedRange = worksheet.RangeUsed();
            if (usedRange == null)
            {
                return "（空のシート）";
            }

            var sb = new StringBuilder();
            int maxRow = usedRange.LastRow().RowNumber();
            int maxCol = usedRange.LastColumn().ColumnNumber();

            for (int row = 1; row <= maxRow; row++)
            {
                sb.Append("|");
                for (int col = 1; col <= maxCol; col++)
                {
                    var cellValue = worksheet.Cell(row, col).GetString();
                    cellValue = cellValue.Replace("|", "\\|").Replace("\n", " ").Replace("\r", "");
                    sb.Append($" {cellValue} |");
                }
                sb.AppendLine();

                if (row == 1)
                {
                    sb.Append("|");
                    for (int col = 1; col <= maxCol; col++)
                    {
                        sb.Append(" --- |");
                    }
                    sb.AppendLine();
                }
            }

            return sb.ToString();
        }

        private static string ConvertWorksheetToCsv(IXLWorksheet worksheet)
        {
            var usedRange = worksheet.RangeUsed();
            if (usedRange == null) return "";

            var sb = new StringBuilder();
            int maxRow = usedRange.LastRow().RowNumber();
            int maxCol = usedRange.LastColumn().ColumnNumber();

            for (int row = 1; row <= maxRow; row++)
            {
                var values = new List<string>();
                for (int col = 1; col <= maxCol; col++)
                {
                    var cellValue = worksheet.Cell(row, col).GetString();
                    
                    // CSVのエスケープ処理
                    if (cellValue.Contains(",") || cellValue.Contains("\"") || cellValue.Contains("\n"))
                    {
                        cellValue = "\"" + cellValue.Replace("\"", "\"\"") + "\"";
                    }
                    
                    values.Add(cellValue);
                }
                sb.AppendLine(string.Join(",", values));
            }

            return sb.ToString();
        }

        private static string ConvertWorksheetToJson(IXLWorksheet worksheet)
        {
            var usedRange = worksheet.RangeUsed();
            if (usedRange == null) return "[]";

            var result = new JArray();
            int maxRow = usedRange.LastRow().RowNumber();
            int maxCol = usedRange.LastColumn().ColumnNumber();

            if (maxRow < 2) return "[]";

            // ヘッダー行を取得
            var headers = new List<string>();
            for (int col = 1; col <= maxCol; col++)
            {
                var header = worksheet.Cell(1, col).GetString();
                headers.Add(string.IsNullOrEmpty(header) ? $"Column_{col}" : header);
            }

            // データ行を処理
            for (int row = 2; row <= maxRow; row++)
            {
                var rowObject = new JObject();
                for (int col = 1; col <= maxCol; col++)
                {
                    var cell = worksheet.Cell(row, col);
                    var header = headers[col - 1];
                    
                    JToken value;
                    if (cell.DataType == XLDataType.Number)
                    {
                        value = new JValue(cell.Value.GetNumber());
                    }
                    else if (cell.DataType == XLDataType.Boolean)
                    {
                        value = new JValue(cell.Value.GetBoolean());
                    }
                    else if (cell.DataType == XLDataType.DateTime && cell.Value.IsDateTime)
                    {
                        value = new JValue(cell.Value.GetDateTime().ToString("yyyy-MM-ddTHH:mm:ss"));
                    }
                    else
                    {
                        value = new JValue(cell.GetString());
                    }
                    
                    rowObject[header] = value;
                }
                result.Add(rowObject);
            }

            return JsonConvert.SerializeObject(result, Formatting.Indented);
        }

        private static string ConvertWorksheetToHtml(IXLWorksheet worksheet, string sheetName)
        {
            var sb = new StringBuilder();
            var usedRange = worksheet.RangeUsed();

            sb.AppendLine("<!DOCTYPE html>");
            sb.AppendLine("<html>");
            sb.AppendLine("<head>");
            sb.AppendLine("    <meta charset=\"UTF-8\">");
            sb.AppendLine($"    <title>{HtmlEncode(sheetName)}</title>");
            sb.AppendLine("    <style>");
            sb.AppendLine("        table { border-collapse: collapse; width: 100%; }");
            sb.AppendLine("        th, td { border: 1px solid #ddd; padding: 8px; text-align: left; }");
            sb.AppendLine("        th { background-color: #f2f2f2; }");
            sb.AppendLine("    </style>");
            sb.AppendLine("</head>");
            sb.AppendLine("<body>");
            sb.AppendLine($"    <h1>{HtmlEncode(sheetName)}</h1>");

            if (usedRange == null)
            {
                sb.AppendLine("    <p>（空のシート）</p>");
            }
            else
            {
                sb.AppendLine("    <table>");
                int maxRow = usedRange.LastRow().RowNumber();
                int maxCol = usedRange.LastColumn().ColumnNumber();

                for (int row = 1; row <= maxRow; row++)
                {
                    sb.AppendLine(row == 1 ? "        <tr>" : "        <tr>");
                    
                    for (int col = 1; col <= maxCol; col++)
                    {
                        var cellValue = HtmlEncode(worksheet.Cell(row, col).GetString());
                        var tag = row == 1 ? "th" : "td";
                        sb.AppendLine($"            <{tag}>{cellValue}</{tag}>");
                    }
                    
                    sb.AppendLine("        </tr>");
                }
                
                sb.AppendLine("    </table>");
            }

            sb.AppendLine("</body>");
            sb.AppendLine("</html>");

            return sb.ToString();
        }

        private static string ConvertAllSheetsToJson(IXLWorkbook workbook)
        {
            var result = new JObject();
            
            foreach (var worksheet in workbook.Worksheets)
            {
                var sheetData = JsonConvert.DeserializeObject<JArray>(ConvertWorksheetToJson(worksheet));
                result[worksheet.Name] = sheetData;
            }

            return JsonConvert.SerializeObject(result, Formatting.Indented);
        }

        private static string ConvertAllSheetsToHtml(IXLWorkbook workbook)
        {
            var sb = new StringBuilder();
            
            sb.AppendLine("<!DOCTYPE html>");
            sb.AppendLine("<html>");
            sb.AppendLine("<head>");
            sb.AppendLine("    <meta charset=\"UTF-8\">");
            sb.AppendLine("    <title>Excel Workbook</title>");
            sb.AppendLine("    <style>");
            sb.AppendLine("        table { border-collapse: collapse; width: 100%; margin-bottom: 20px; }");
            sb.AppendLine("        th, td { border: 1px solid #ddd; padding: 8px; text-align: left; }");
            sb.AppendLine("        th { background-color: #f2f2f2; }");
            sb.AppendLine("        h2 { color: #333; }");
            sb.AppendLine("    </style>");
            sb.AppendLine("</head>");
            sb.AppendLine("<body>");
            sb.AppendLine("    <h1>Excel Workbook</h1>");

            foreach (var worksheet in workbook.Worksheets)
            {
                sb.AppendLine($"    <h2>{HtmlEncode(worksheet.Name)}</h2>");
                var usedRange = worksheet.RangeUsed();

                if (usedRange == null)
                {
                    sb.AppendLine("    <p>（空のシート）</p>");
                }
                else
                {
                    sb.AppendLine("    <table>");
                    int maxRow = usedRange.LastRow().RowNumber();
                    int maxCol = usedRange.LastColumn().ColumnNumber();

                    for (int row = 1; row <= maxRow; row++)
                    {
                        sb.AppendLine("        <tr>");
                        
                        for (int col = 1; col <= maxCol; col++)
                        {
                            var cellValue = HtmlEncode(worksheet.Cell(row, col).GetString());
                            var tag = row == 1 ? "th" : "td";
                            sb.AppendLine($"            <{tag}>{cellValue}</{tag}>");
                        }
                        
                        sb.AppendLine("        </tr>");
                    }
                    
                    sb.AppendLine("    </table>");
                }
            }

            sb.AppendLine("</body>");
            sb.AppendLine("</html>");

            return sb.ToString();
        }

        private static string HtmlEncode(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            
            return text
                .Replace("&", "&amp;")
                .Replace("<", "&lt;")
                .Replace(">", "&gt;")
                .Replace("\"", "&quot;")
                .Replace("'", "&#39;");
        }
    }
}