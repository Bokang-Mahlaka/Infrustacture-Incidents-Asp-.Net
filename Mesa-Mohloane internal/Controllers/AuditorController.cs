using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Mesa_Mohloane_internal.Models;
using Mesa_Mohloane_internal.Services;

namespace Mesa_Mohloane_internal.Controllers
{
    [Authorize(Roles = "Auditor")]
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
    }
}
