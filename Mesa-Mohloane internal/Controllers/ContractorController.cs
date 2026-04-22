using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Mesa_Mohloane_internal.Models;
using Mesa_Mohloane_internal.Services;

namespace Mesa_Mohloane_internal.Controllers
{
    [Authorize(Roles = "Contractor")]
    public class ContractorController : Controller
    {
        private readonly IApiClient _apiClient;

        public ContractorController(IApiClient apiClient)
        {
            _apiClient = apiClient;
        }

        public async Task<IActionResult> Dashboard()
        {
            // View available incidents 
            var response = await _apiClient.GetAsync<ApiResponse<List<IncidentViewModel>>>("/api/Incidents");
            
            var openIncidents = new List<IncidentViewModel>();
            var submittedProposals = new Dictionary<int, int>(); // IncidentId -> ProposalId
            int activeJobsCount = 0;

            if (response != null && response.Success && response.Data != null)
            {
                // Visible to contractors only once Published
                openIncidents = response.Data.Where(i => i.Status == "Published").ToList();

                // Fetch my proposals to identify where I've already applied
                var propResp = await _apiClient.GetAsync<ApiResponse<List<ProposalViewModel>>>("/api/Proposals/my-proposals");
                if (propResp != null && propResp.Success && propResp.Data != null)
                {
                    submittedProposals = propResp.Data.ToDictionary(p => p.IncidentId, p => p.Id);
                    
                    // Count my currently active jobs (Accepted state)
                    activeJobsCount = propResp.Data.Count(p => p.Status == "Accepted");
                }
            }

            ViewBag.SubmittedProposals = submittedProposals;
            ViewBag.ActiveJobsCount = activeJobsCount;
            return View(openIncidents);
        }

        public async Task<IActionResult> MyJobs(bool showHistory = false)
        {
            // Fetch all my proposals
            var proposalsResponse = await _apiClient.GetAsync<ApiResponse<List<ProposalViewModel>>>("/api/Proposals/my-proposals");
            
            var jobs = new List<ProposalViewModel>();
            if (proposalsResponse != null && proposalsResponse.Success && proposalsResponse.Data != null)
            {
                // Grab all accepted jobs first
                jobs = proposalsResponse.Data.Where(p => p.Status == "Accepted").ToList();

                var incidentsResponse = await _apiClient.GetAsync<ApiResponse<List<IncidentViewModel>>>("/api/Incidents");
                if (incidentsResponse != null && incidentsResponse.Success && incidentsResponse.Data != null)
                {
                    foreach (var job in jobs)
                    {
                        var incident = incidentsResponse.Data.FirstOrDefault(i => i.Id == job.IncidentId);
                        if (incident != null) job.IncidentTitle = incident.Title;

                        // Use the new proposal lookup endpoint for payments
                        var payResp = await _apiClient.GetAsync<ApiResponse<PaymentViewModel>>($"/api/Payments/proposal/{job.Id}");
                        if (payResp != null && payResp.Success && payResp.Data != null)
                        {
                            job.PaymentStatus = payResp.Data.Status;
                            job.CitizenAcknowledged = payResp.Data.CitizenAcknowledged;
                        }
                    }
                }

                // Final filtering for View
                if (showHistory)
                {
                    jobs = jobs.Where(j => j.PaymentStatus != null && (j.PaymentStatus.Equals("Disbursed", StringComparison.OrdinalIgnoreCase) || j.PaymentStatus.Equals("Paid", StringComparison.OrdinalIgnoreCase))).ToList();
                }
                else
                {
                    jobs = jobs.Where(j => !(j.PaymentStatus != null && (j.PaymentStatus.Equals("Disbursed", StringComparison.OrdinalIgnoreCase) || j.PaymentStatus.Equals("Paid", StringComparison.OrdinalIgnoreCase)))).ToList();
                }
            }

            ViewBag.ShowHistory = showHistory;
            return View(jobs);
        }

        [HttpGet]
        public async Task<IActionResult> SubmitInvoice(int proposalId)
        {
            // Safeguard: Check if we already have an invoice or payment record
            var payResp = await _apiClient.GetAsync<ApiResponse<PaymentViewModel>>($"/api/Payments/proposal/{proposalId}");
            if (payResp != null && payResp.Success && payResp.Data != null)
            {
                TempData["SuccessMessage"] = "This invoice has already been submitted and is currently being processed.";
                return RedirectToAction("MyJobs");
            }

            return View(new CreateInvoiceViewModel { ProposalId = proposalId });
        }

        [HttpPost]
        public async Task<IActionResult> SubmitInvoice(CreateInvoiceViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            // Structure matches backend CreateInvoiceDto
            var payload = new 
            {
                TotalAmount = model.TotalAmount,
                LineItems = new List<object>
                {
                    new 
                    {
                        Category = 3, // Maintenance
                        Description = model.Description,
                        Quantity = 1,
                        UnitPrice = model.TotalAmount
                    }
                }
            };

            var response = await _apiClient.PostAsync<object, ApiResponse<InvoiceViewModel>>($"/api/Invoices/proposal/{model.ProposalId}", payload);

            if (response != null && response.Success)
            {
                TempData["SuccessMessage"] = "Invoice submitted for approval. Funds will be disbursed upon Admin verification.";
                return RedirectToAction("MyJobs");
            }

            ModelState.AddModelError(string.Empty, response?.Message ?? "Failed to submit invoice.");
            return View(model);
        }

        [HttpGet]
        public IActionResult SubmitProposal(int incidentId)
        {
            var model = new CreateProposalViewModel 
            { 
                IncidentId = incidentId,
                LineItems = new List<ProposalLineItemViewModel>
                {
                    new ProposalLineItemViewModel { Category = 0, Description = "Core Materials" },
                    new ProposalLineItemViewModel { Category = 1, Description = "Contractor Labor" },
                    new ProposalLineItemViewModel { Category = 2, Description = "Site Logistics & Transport" },
                    new ProposalLineItemViewModel { Category = 3, Description = "Specialized Equipment Hiring" }
                }
            };
            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> SubmitProposal(CreateProposalViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            // Filter out empty lines if any
            var activeItems = model.LineItems.Where(li => li.UnitPrice > 0).ToList();
            if(!activeItems.Any())
            {
                ModelState.AddModelError(string.Empty, "At least one line item with a price is required.");
                return View(model);
            }

            // Map and send to API
            var payload = new 
            {
                CoverLetter = model.CoverLetter,
                EstimatedDays = model.EstimatedDays,
                LineItems = activeItems.Select(li => new 
                {
                    Category = li.Category,
                    Description = li.Description,
                    Quantity = li.Quantity,
                    UnitPrice = li.UnitPrice
                }).ToArray()
            };

            var response = await _apiClient.PostAsync<object, ApiResponse<object>>($"/api/Proposals/incident/{model.IncidentId}", payload);

            if (response != null && response.Success)
            {
                TempData["SuccessMessage"] = "Proposal submitted securely. Waiting for Admin Evaluation.";
                return RedirectToAction("Dashboard");
            }

            ModelState.AddModelError(string.Empty, response?.Errors?.FirstOrDefault() ?? "Failed to submit proposal.");
            return View(model);
        }
        [HttpGet]
        public async Task<IActionResult> EditProposal(int id)
        {
            var response = await _apiClient.GetAsync<ApiResponse<ProposalViewModel>>($"/api/Proposals/{id}");
            
            if (response != null && response.Success && response.Data != null)
            {
                var p = response.Data;
                
                // Security: Only editable if Submitted
                if (p.Status != "Submitted")
                {
                    TempData["ErrorMessage"] = "This proposal has already been processed and can no longer be edited.";
                    return RedirectToAction("Dashboard");
                }

                var model = new CreateProposalViewModel
                {
                    Id = p.Id,
                    IncidentId = p.IncidentId,
                    CoverLetter = p.CoverLetter,
                    EstimatedDays = p.EstimatedDays,
                    LineItems = p.LineItems.Select(li => new ProposalLineItemViewModel
                    {
                        Category = (int)Enum.Parse<LineItemCategory>(li.Category),
                        Description = li.Description,
                        Quantity = li.Quantity,
                        UnitPrice = li.UnitPrice
                    }).ToList()
                };

                // Ensure at least 4 lines for UI
                while (model.LineItems.Count < 4) 
                {
                    model.LineItems.Add(new ProposalLineItemViewModel());
                }

                return View(model);
            }

            TempData["ErrorMessage"] = "Could not retrieve proposal details.";
            return RedirectToAction("Dashboard");
        }

        [HttpPost]
        public async Task<IActionResult> EditProposal(CreateProposalViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            var activeItems = model.LineItems.Where(li => li.UnitPrice > 0).ToList();
            if (!activeItems.Any())
            {
                ModelState.AddModelError(string.Empty, "At least one line item with a price is required.");
                return View(model);
            }

            var payload = new 
            {
                CoverLetter = model.CoverLetter,
                EstimatedDays = model.EstimatedDays,
                LineItems = activeItems.Select(li => new 
                {
                    Category = li.Category,
                    Description = li.Description,
                    Quantity = li.Quantity,
                    UnitPrice = li.UnitPrice
                }).ToArray()
            };

            var response = await _apiClient.PutAsync<object, ApiResponse<object>>($"/api/Proposals/{model.Id}", payload);

            if (response != null && response.Success)
            {
                TempData["SuccessMessage"] = "Proposal updated successfully. The rankings have been refreshed.";
                return RedirectToAction("Dashboard");
            }

            ModelState.AddModelError(string.Empty, response?.Message ?? "Failed to update proposal.");
            return View(model);
        }
    }
}
