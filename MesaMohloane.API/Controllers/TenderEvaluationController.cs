using MesaMohloane.API.Data;
using MesaMohloane.API.Models;
using MesaMohloane.API.Models.DTOs;
using MesaMohloane.API.Services.Auditing;
using MesaMohloane.API.Services.Email;
using MesaMohloane.API.Services.TenderEvaluation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace MesaMohloane.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class TenderEvaluationController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ITenderEvaluationService _evaluationService;
        private readonly IAuditService _auditService;
        private readonly IEmailService _emailService;

        public TenderEvaluationController(
            ApplicationDbContext context,
            ITenderEvaluationService evaluationService,
            IAuditService auditService,
            IEmailService emailService)
        {
            _context = context;
            _evaluationService = evaluationService;
            _auditService = auditService;
            _emailService = emailService;
        }

        /// <summary>
        /// Run the Smart Tender Evaluation Algorithm and return ranked contractors for an incident.
        /// </summary>
        [HttpGet("incident/{incidentId}/rankings")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<ApiResponse<List<ContractorRankingDto>>>> GetRankings(int incidentId)
        {
            var incident = await _context.Incidents.FindAsync(incidentId);
            if (incident == null)
                return NotFound(ApiResponse<List<ContractorRankingDto>>.ErrorResponse("Incident not found."));

            if (incident.Status != IncidentStatus.Published)
                return BadRequest(ApiResponse<List<ContractorRankingDto>>.ErrorResponse(
                    "Rankings can only be generated for published incidents."));

            var rankings = await _evaluationService.EvaluateProposalsAsync(incidentId);

            if (!rankings.Any())
                return Ok(ApiResponse<List<ContractorRankingDto>>.SuccessResponse(rankings,
                    "No proposals found for this incident."));

            return Ok(ApiResponse<List<ContractorRankingDto>>.SuccessResponse(rankings,
                $"Rankings generated. {rankings.Count} contractor(s) evaluated."));
        }

        /// <summary>
        /// Admin assigns the selected contractor to an incident.
        /// Accepts the chosen proposal, rejects all others, updates incident status.
        /// </summary>
        [HttpPost("incident/{incidentId}/assign")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<ApiResponse<IncidentDto>>> AssignContractor(
            int incidentId, [FromBody] AssignContractorDto dto)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var incident = await _context.Incidents
                .Include(i => i.Citizen)
                .Include(i => i.Proposals)
                .FirstOrDefaultAsync(i => i.Id == incidentId);

            if (incident == null)
                return NotFound(ApiResponse<IncidentDto>.ErrorResponse("Incident not found."));

            var selectedProposal = await _context.Proposals
                .Include(p => p.Contractor)
                .FirstOrDefaultAsync(p => p.Id == dto.ProposalId && p.IncidentId == incidentId);

            if (selectedProposal == null)
                return NotFound(ApiResponse<IncidentDto>.ErrorResponse("Proposal not found for this incident."));

            // Accept the selected proposal
            selectedProposal.Status = ProposalStatus.Accepted;

            // Reject all other proposals
            var otherProposals = await _context.Proposals
                .Where(p => p.IncidentId == incidentId && p.Id != dto.ProposalId)
                .ToListAsync();

            foreach (var p in otherProposals)
            {
                p.Status = ProposalStatus.Rejected;
            }

            // Update incident
            incident.AssignedContractorId = selectedProposal.ContractorId;
            incident.Status = IncidentStatus.Assigned;
            incident.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            // Audit log
            await _auditService.LogAsync(userId, "ContractorAssigned", "Incident", incidentId,
                newValue: $"Contractor: {selectedProposal.Contractor.FullName}, ProposalId: {dto.ProposalId}, Cost: M{selectedProposal.TotalCost:N2}",
                ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString());

            // Send assignment email notification
            if (selectedProposal.Contractor.Email != null)
            {
                await _emailService.SendAssignmentNotificationAsync(
                    selectedProposal.Contractor.Email,
                    selectedProposal.Contractor.FullName,
                    incident.Title);
            }

            // Reload for response
            await _context.Entry(incident).Reference(i => i.AssignedContractor).LoadAsync();

            var incidentDto = new IncidentDto
            {
                Id = incident.Id,
                Title = incident.Title,
                Description = incident.Description,
                Category = incident.Category.ToString(),
                Location = incident.Location,
                Latitude = incident.Latitude,
                Longitude = incident.Longitude,
                PhotoUrl = incident.PhotoUrl,
                Status = incident.Status.ToString(),
                CitizenId = incident.CitizenId,
                CitizenName = incident.Citizen?.FullName ?? "Unknown",
                AssignedContractorId = incident.AssignedContractorId,
                AssignedContractorName = incident.AssignedContractor?.FullName,
                CreatedAt = incident.CreatedAt,
                UpdatedAt = incident.UpdatedAt,
                ProposalCount = incident.Proposals?.Count ?? 0
            };

            return Ok(ApiResponse<IncidentDto>.SuccessResponse(incidentDto,
                $"Contractor '{selectedProposal.Contractor.FullName}' assigned successfully."));
        }
    }
}
