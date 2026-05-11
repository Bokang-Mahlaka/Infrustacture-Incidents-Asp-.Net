using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Mesa_Mohloane_internal.Models;
using Mesa_Mohloane_internal.Services;

namespace Mesa_Mohloane_internal.Controllers
{
    [Authorize(Roles = "Auditor,Admin")]
    public class AuditorController : Controller
    {
        private readonly IApiClient _apiClient;

        public AuditorController(IApiClient apiClient)
        {
            _apiClient = apiClient;
        }

        public IActionResult Dashboard()
        {
            return View();
        }

        public async Task<IActionResult> AuditLogs()
        {
            var response = await _apiClient.GetAsync<ApiResponse<List<AuditLogViewModel>>>("/api/Audit");
            
            var logs = new List<AuditLogViewModel>();
            if (response != null && response.Success && response.Data != null)
            {
                logs = response.Data.OrderByDescending(l => l.Timestamp).ToList();
            }

            return View(logs);
        }

        public async Task<IActionResult> FlaggedInvoices()
        {
            var response = await _apiClient.GetAsync<ApiResponse<List<InvoiceViewModel>>>("/api/Invoices/flagged");
            
            var flagged = new List<InvoiceViewModel>();
            if (response != null && response.Success && response.Data != null)
            {
                flagged = response.Data.OrderByDescending(i => i.SubmittedAt).ToList();
            }

            return View(flagged);
        }

        [HttpGet]
        public async Task<IActionResult> ExportAudit(string format = "csv", DateTime? startDate = null, DateTime? endDate = null, string? entity = null)
        {
            var url = $"/api/Audit/export?format={format}";
            if (startDate.HasValue) url += $"&startDate={startDate.Value:yyyy-MM-dd}";
            if (endDate.HasValue) url += $"&endDate={endDate.Value:yyyy-MM-dd}";
            if (!string.IsNullOrEmpty(entity)) url += $"&entity={entity}";

            var bytes = await _apiClient.GetByteArrayAsync(url);
            if (bytes == null) return BadRequest("Failed to export logs.");

            var contentType = format.ToLower() == "csv" ? "text/csv" : "text/html";
            var fileName = $"audit-logs_{DateTime.UtcNow:yyyyMMdd}.{format}";
            
            return File(bytes, contentType, fileName);
        }
    }
}
