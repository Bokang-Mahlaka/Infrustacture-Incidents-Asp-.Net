using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Mesa_Mohloane_internal.Models;
using Mesa_Mohloane_internal.Services;

namespace Mesa_Mohloane_internal.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        private readonly IApiClient _apiClient;

        public AdminController(IApiClient apiClient)
        {
            _apiClient = apiClient;
        }

        public async Task<IActionResult> Dashboard(string? status = "All")
        {
            var incidentsTask = _apiClient.GetAsync<ApiResponse<List<IncidentViewModel>>>("/api/Incidents");
            var statsTask = _apiClient.GetAsync<ApiResponse<AdminDashboardStatsViewModel>>("/api/Incidents/admin-summary");
            var pendingContractorsTask = _apiClient.GetAsync<ApiResponse<List<ContractorSummaryViewModel>>>("/api/Contractors/pending");

            await Task.WhenAll(incidentsTask, statsTask, pendingContractorsTask);

            var incidents = new List<IncidentViewModel>();
            var incidentsResponse = await incidentsTask;
            if (incidentsResponse != null && incidentsResponse.Success && incidentsResponse.Data != null)
            {
                incidents = incidentsResponse.Data.ToList();

                // Apply filtering
                if (!string.IsNullOrEmpty(status) && status != "All")
                {
                    if (status == "Assigned")
                    {
                        incidents = incidents.Where(i => i.Status == "Assigned" || i.Status == "InProgress").ToList();
                    }
                    else if (status == "Completed")
                    {
                        incidents = incidents.Where(i => i.Status == "Completed" || i.Status == "Resolved" || i.Status == "Closed").ToList();
                    }
                    else if (status == "Other")
                    {
                        incidents = incidents.Where(i => i.Status != "Assigned" && i.Status != "InProgress" && 
                                                       i.Status != "Completed" && i.Status != "Resolved" && i.Status != "Closed").ToList();
                    }
                }
            }

            var stats = new AdminDashboardStatsViewModel();
            var statsResponse = await statsTask;
            if (statsResponse != null && statsResponse.Success && statsResponse.Data != null)
            {
                stats = statsResponse.Data;
            }

            var pendingContractors = new List<ContractorSummaryViewModel>();
            var pendingResponse = await pendingContractorsTask;
            if (pendingResponse != null && pendingResponse.Success && pendingResponse.Data != null)
            {
                pendingContractors = pendingResponse.Data;
            }

            ViewBag.CurrentStatus = status;
            return View(new AdminDashboardViewModel
            {
                Incidents = incidents,
                Stats = stats,
                PendingContractors = pendingContractors
            });
        }

        public async Task<IActionResult> Details(int id)
        {
            var response = await _apiClient.GetAsync<ApiResponse<IncidentViewModel>>($"/api/Incidents/{id}");
            
            if (response != null && response.Success && response.Data != null)
            {
                return View(response.Data);
            }

            TempData["ErrorMessage"] = "Could not retrieve incident details.";
            return RedirectToAction("Dashboard");
        }

        [HttpPost]
        public async Task<IActionResult> PublishIncident(int id)
        {
            // IncidentStatus.Published = 2
            var payload = new { Status = 2 };
            var response = await _apiClient.PutAsync<object, ApiResponse<object>>($"/api/Incidents/{id}/status", payload);

            if (response != null && response.Success)
            {
                TempData["SuccessMessage"] = "The incident has been verified and published to the public tender marketplace.";
            }
            else
            {
                TempData["ErrorMessage"] = response?.Message ?? "Failed to publish tender.";
            }

            return RedirectToAction("Details", new { id = id });
        }

        [HttpGet]
        public async Task<IActionResult> Manage(int id)
        {
            // Call the Evaluation Service to get algorithm rankings
            var rankingsResponse = await _apiClient.GetAsync<ApiResponse<List<ContractorRankingViewModel>>>($"/api/TenderEvaluation/incident/{id}/rankings");
            
            var rankings = new List<ContractorRankingViewModel>();
            if (rankingsResponse != null && rankingsResponse.Success && rankingsResponse.Data != null)
            {
                rankings = rankingsResponse.Data.ToList();
            }

            ViewBag.IncidentId = id;
            return View(rankings);
        }

        [HttpPost]
        public async Task<IActionResult> AssignContractor(int incidentId, int proposalId)
        {
            var payload = new { ProposalId = proposalId };
            var response = await _apiClient.PostAsync<object, ApiResponse<object>>($"/api/TenderEvaluation/incident/{incidentId}/assign", payload);

            if (response != null && response.Success)
            {
                TempData["SuccessMessage"] = "The tender has been automatically assigned via the Smart Algorithm rankings.";
                return RedirectToAction("Dashboard");
            }

            TempData["ErrorMessage"] = response?.Message ?? "Failed to assign the contract.";
            return RedirectToAction("Dashboard");
        }

        public async Task<IActionResult> Invoices()
        {
            // Fetch all invoices
            var invResp = await _apiClient.GetAsync<ApiResponse<List<InvoiceViewModel>>>("/api/Invoices");
            
            var invoices = new List<InvoiceViewModel>();
            if (invResp != null && invResp.Success && invResp.Data != null)
            {
                invoices = invResp.Data;

                // Fetch all payments to join them
                var payResp = await _apiClient.GetAsync<ApiResponse<List<PaymentViewModel>>>("/api/Payments");
                if (payResp != null && payResp.Success && payResp.Data != null)
                {
                    foreach (var inv in invoices)
                    {
                        var payment = payResp.Data.FirstOrDefault(p => p.InvoiceId == inv.Id);
                        if (payment != null)
                        {
                            inv.PaymentId = payment.Id;
                            inv.CitizenAcknowledged = payment.CitizenAcknowledged;
                            inv.ActualPaymentStatus = payment.Status;
                        }
                    }
                }

                invoices = invoices.OrderByDescending(i => i.SubmittedAt).ToList();
            }

            return View(invoices);
        }

        [HttpPost]
        public async Task<IActionResult> DisbursePayment(int paymentId)
        {
            var response = await _apiClient.PutAsync<object, ApiResponse<object>>($"/api/Payments/{paymentId}/disburse", null);

            if (response != null && response.Success)
            {
                TempData["SuccessMessage"] = "Payment disbursed successfully. The contractor has been notified via email.";
            }
            else
            {
                TempData["ErrorMessage"] = response?.Message ?? "Failed to disburse payment. Ensure the citizen has acknowledged the work first.";
            }

            return RedirectToAction("Invoices");
        }

        [HttpPost]
        public async Task<IActionResult> ApproveInvoice(int id)
        {
            var response = await _apiClient.PutAsync<object, ApiResponse<object>>($"/api/Invoices/{id}/approve", null);

            if (response != null && response.Success)
            {
                TempData["SuccessMessage"] = "Invoice approved and payment has been queued for disbursement.";
            }
            else
            {
                TempData["ErrorMessage"] = response?.Message ?? "Failed to approve invoice.";
            }

            return RedirectToAction("Invoices");
        }

        [HttpPost]
        public async Task<IActionResult> RejectInvoice(RejectInvoiceViewModel model)
        {
            if (!ModelState.IsValid)
            {
                TempData["ErrorMessage"] = "Rejection reason is required.";
                return RedirectToAction("Invoices");
            }

            var payload = new { Reason = model.Reason };
            var response = await _apiClient.PutAsync<object, ApiResponse<object>>($"/api/Invoices/{model.InvoiceId}/reject", payload);

            if (response != null && response.Success)
            {
                TempData["SuccessMessage"] = "Invoice rejected. The contractor has been notified.";
            }
            else
            {
                TempData["ErrorMessage"] = response?.Message ?? "Failed to reject invoice.";
            }

            return RedirectToAction("Invoices");
        }

        public async Task<IActionResult> Contractors()
        {
            // View will load contractors via JavaScript API call
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> ApproveContractor(string id)
        {
            var response = await _apiClient.PostAsync<object, ApiResponse<bool>>($"/api/Contractors/{id}/approve", null);
            if (response != null && response.Success)
            {
                TempData["SuccessMessage"] = "Contractor has been approved and granted access to the platform.";
            }
            else
            {
                TempData["ErrorMessage"] = response?.Message ?? "Failed to approve contractor.";
            }
            return RedirectToAction("Dashboard");
        }

        [HttpPost]
        public async Task<IActionResult> RejectContractor(string id)
        {
            var response = await _apiClient.PostAsync<object, ApiResponse<bool>>($"/api/Contractors/{id}/reject", null);
            if (response != null && response.Success)
            {
                TempData["SuccessMessage"] = "Contractor registration rejected.";
            }
            else
            {
                TempData["ErrorMessage"] = response?.Message ?? "Failed to reject contractor.";
            }
            return RedirectToAction("Dashboard");
        }
    }
}
