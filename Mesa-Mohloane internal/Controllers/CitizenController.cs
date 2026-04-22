using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Mesa_Mohloane_internal.Models;
using Mesa_Mohloane_internal.Services;

namespace Mesa_Mohloane_internal.Controllers
{
    [Authorize(Roles = "Citizen")]
    public class CitizenController : Controller
    {
        private readonly IApiClient _apiClient;

        public CitizenController(IApiClient apiClient)
        {
            _apiClient = apiClient;
        }

        public async Task<IActionResult> Dashboard()
        {
            // Fetch all incidents
            var response = await _apiClient.GetAsync<ApiResponse<List<IncidentViewModel>>>("/api/Incidents");
            
            var incidents = new List<IncidentViewModel>();
            if (response != null && response.Success && response.Data != null)
            {
                // In a perfect world, we'd have a 'api/Incidents/my'
                // Here, we grab them all, assuming the citizen can view the feed of infrastructure issues
                incidents = response.Data.ToList();
            }

            return View(incidents);
        }

        [HttpGet]
        public IActionResult Report()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Report(CreateIncidentViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            using var content = new MultipartFormDataContent();
            content.Add(new StringContent(model.Title), "Title");
            content.Add(new StringContent(model.Description), "Description");
            content.Add(new StringContent(model.Category), "Category");
            content.Add(new StringContent(model.Location), "Location");

            if (model.Latitude.HasValue)
                content.Add(new StringContent(model.Latitude.Value.ToString()), "Latitude");
            if (model.Longitude.HasValue)
                content.Add(new StringContent(model.Longitude.Value.ToString()), "Longitude");

            if (model.Photo != null && model.Photo.Length > 0)
            {
                var photoContent = new StreamContent(model.Photo.OpenReadStream());
                photoContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(model.Photo.ContentType);
                content.Add(photoContent, "Photo", model.Photo.FileName);
            }

            var response = await _apiClient.PostFormAsync<ApiResponse<IncidentViewModel>>("/api/Incidents", content);

            if (response != null && response.Success)
            {
                TempData["SuccessMessage"] = "Incident reported successfully.";
                return RedirectToAction("Dashboard");
            }

            ModelState.AddModelError(string.Empty, response?.Errors?.FirstOrDefault() ?? response?.Message ?? "Failed to report incident.");
            return View(model);
        }

        [HttpGet]
        public IActionResult Rate(int incidentId)
        {
            return View(new CreateRatingViewModel { IncidentId = incidentId });
        }

        [HttpPost]
        public async Task<IActionResult> Rate(CreateRatingViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            var payload = new { Rating = model.Rating, Comment = model.Comment };
            var response = await _apiClient.PostAsync<object, ApiResponse<object>>($"/api/Ratings/incident/{model.IncidentId}", payload);

            if (response != null && response.Success)
            {
                TempData["SuccessMessage"] = "Thank you! Your rating successfully affected the Contractor's algorithm score.";
                return RedirectToAction("Dashboard");
            }

            ModelState.AddModelError(string.Empty, response?.Message ?? "Failed to submit rating.");
            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> AcknowledgeWork(int incidentId)
        {
            var response = await _apiClient.GetAsync<ApiResponse<IncidentViewModel>>($"/api/Incidents/{incidentId}");
            
            if (response != null && response.Success && response.Data != null)
            {
                var model = new ProjectSignOffViewModel
                {
                    IncidentId = incidentId,
                    IncidentTitle = response.Data.Title
                };
                return View("SignOff", model);
            }

            TempData["ErrorMessage"] = "Incident details not found.";
            return RedirectToAction("Dashboard");
        }

        [HttpPost]
        public async Task<IActionResult> AcknowledgeWork(ProjectSignOffViewModel model)
        {
            if (!ModelState.IsValid) return View("SignOff", model);

            // 1. Get Proposals to find the payment ID
            var propResp = await _apiClient.GetAsync<ApiResponse<List<ProposalViewModel>>>($"/api/Proposals/incident/{model.IncidentId}");
            if (propResp?.Data == null || !propResp.Success)
            {
                TempData["ErrorMessage"] = "Could not find job details for sign-off.";
                return RedirectToAction("Dashboard");
            }

            var acceptedProposal = propResp.Data.FirstOrDefault(p => p.Status == "Accepted");
            if (acceptedProposal == null)
            {
                TempData["ErrorMessage"] = "No accepted contract found for this incident.";
                return RedirectToAction("Dashboard");
            }

            // 2. Submit Rating First (Critical for algorithm ranking)
            var ratingPayload = new { Rating = model.Rating, Comment = model.Comment };
            await _apiClient.PostAsync<object, ApiResponse<object>>($"/api/Ratings/incident/{model.IncidentId}", ratingPayload);

            // 3. Perform Acknowledgment
            var payResp = await _apiClient.GetAsync<ApiResponse<PaymentViewModel>>($"/api/Payments/proposal/{acceptedProposal.Id}");
            if (payResp != null && payResp.Success && payResp.Data != null)
            {
                var ackResp = await _apiClient.PutAsync<object, ApiResponse<object>>($"/api/Payments/{payResp.Data.Id}/acknowledge", null);
                if (ackResp != null && ackResp.Success)
                {
                    TempData["SuccessMessage"] = "Project Sign-off completed! This repair is now certified in the national registry.";
                    return RedirectToAction("Dashboard");
                }
            }

            TempData["ErrorMessage"] = "Sign-off partially failed. Work acknowledgment status was not synchronized.";
            return RedirectToAction("Dashboard");
        }
    }
}
