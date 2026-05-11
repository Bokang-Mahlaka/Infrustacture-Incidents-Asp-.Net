using MesaMohloane.API.Models;
using System.Text;

namespace MesaMohloane.API.Services.Auditing
{
    public interface IExportService
    {
        Task ExportAuditLogsToCsvAsync(List<AuditLog> logs, Stream stream);
        Task ExportAuditLogsToPdfAsync(List<AuditLog> logs, Stream stream);
    }

    public class ExportService : IExportService
    {
        public async Task ExportAuditLogsToCsvAsync(List<AuditLog> logs, Stream stream)
        {
            using (var writer = new StreamWriter(stream, Encoding.UTF8, leaveOpen: true))
            {
                // Write CSV header
                await writer.WriteLineAsync("Timestamp,User ID,Action,Entity,Entity ID,Old Value,New Value,IP Address");

                // Write rows
                foreach (var log in logs.OrderByDescending(l => l.Timestamp))
                {
                    var timestamp = log.Timestamp.ToString("yyyy-MM-dd HH:mm:ss");
                    var userId = EscapeCsvField(log.UserId ?? "");
                    var action = EscapeCsvField(log.Action);
                    var entity = EscapeCsvField(log.Entity);
                    var oldValue = EscapeCsvField(log.OldValue ?? "");
                    var newValue = EscapeCsvField(log.NewValue ?? "");
                    var ipAddress = EscapeCsvField(log.IpAddress ?? "");

                    var line = $"{timestamp},{userId},{action},{entity},{log.EntityId},{oldValue},{newValue},{ipAddress}";
                    await writer.WriteLineAsync(line);
                }

                await writer.FlushAsync();
            }
        }

        public async Task ExportAuditLogsToPdfAsync(List<AuditLog> logs, Stream stream)
        {
            // This is a simple HTML-to-PDF approach using basic HTML generation
            // For production, consider using iTextSharp or SelectPdf
            var htmlContent = GenerateHtmlTable(logs);
            
            // For now, we'll write a formatted HTML file that can be opened in a browser
            // and printed as PDF. In production, integrate a proper HTML-to-PDF library.
            using (var writer = new StreamWriter(stream, Encoding.UTF8, leaveOpen: true))
            {
                await writer.WriteAsync(htmlContent);
                await writer.FlushAsync();
            }
        }

        private string GenerateHtmlTable(List<AuditLog> logs)
        {
            var html = new StringBuilder();
            html.AppendLine("<!DOCTYPE html>");
            html.AppendLine("<html>");
            html.AppendLine("<head>");
            html.AppendLine("<meta charset='utf-8'/>");
            html.AppendLine("<title>Audit Log Export</title>");
            html.AppendLine("<style>");
            html.AppendLine("body { font-family: Arial, sans-serif; margin: 20px; }");
            html.AppendLine("h1 { color: #2563eb; }");
            html.AppendLine("table { width: 100%; border-collapse: collapse; margin-top: 20px; }");
            html.AppendLine("th { background-color: #2563eb; color: white; padding: 12px; text-align: left; border: 1px solid #ddd; }");
            html.AppendLine("td { padding: 10px; border: 1px solid #ddd; }");
            html.AppendLine("tr:nth-child(even) { background-color: #f9fafb; }");
            html.AppendLine(".timestamp { font-family: monospace; font-size: 0.9em; }");
            html.AppendLine("</style>");
            html.AppendLine("</head>");
            html.AppendLine("<body>");
            html.AppendLine("<h1>Audit Log Report</h1>");
            html.AppendLine($"<p>Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss UTC}</p>");
            html.AppendLine("<table>");
            html.AppendLine("<thead>");
            html.AppendLine("<tr>");
            html.AppendLine("<th>Timestamp</th>");
            html.AppendLine("<th>User ID</th>");
            html.AppendLine("<th>Action</th>");
            html.AppendLine("<th>Entity</th>");
            html.AppendLine("<th>Entity ID</th>");
            html.AppendLine("<th>Old Value</th>");
            html.AppendLine("<th>New Value</th>");
            html.AppendLine("<th>IP Address</th>");
            html.AppendLine("</tr>");
            html.AppendLine("</thead>");
            html.AppendLine("<tbody>");

            foreach (var log in logs.OrderByDescending(l => l.Timestamp))
            {
                html.AppendLine("<tr>");
                html.AppendLine($"<td class='timestamp'>{log.Timestamp:yyyy-MM-dd HH:mm:ss}</td>");
                html.AppendLine($"<td>{HtmlEncode(log.UserId ?? "")}</td>");
                html.AppendLine($"<td>{HtmlEncode(log.Action)}</td>");
                html.AppendLine($"<td>{HtmlEncode(log.Entity)}</td>");
                html.AppendLine($"<td>{log.EntityId}</td>");
                html.AppendLine($"<td>{HtmlEncode(log.OldValue ?? "")}</td>");
                html.AppendLine($"<td>{HtmlEncode(log.NewValue ?? "")}</td>");
                html.AppendLine($"<td>{HtmlEncode(log.IpAddress ?? "")}</td>");
                html.AppendLine("</tr>");
            }

            html.AppendLine("</tbody>");
            html.AppendLine("</table>");
            html.AppendLine("</body>");
            html.AppendLine("</html>");

            return html.ToString();
        }

        private string EscapeCsvField(string field)
        {
            if (string.IsNullOrEmpty(field))
                return "\"\"";

            if (field.Contains(",") || field.Contains("\"") || field.Contains("\n"))
            {
                return "\"" + field.Replace("\"", "\"\"") + "\"";
            }

            return field;
        }

        private string HtmlEncode(string text)
        {
            if (string.IsNullOrEmpty(text))
                return "";

            return System.Net.WebUtility.HtmlEncode(text);
        }
    }
}
