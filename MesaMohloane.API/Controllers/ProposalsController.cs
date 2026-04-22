using MesaMohloane.API.Data;
using MesaMohloane.API.Models;
using MesaMohloane.API.Models.DTOs;
using MesaMohloane.API.Services.Auditing;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace MesaMohloane.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class ProposalsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IAuditService _auditService;

        public ProposalsController(ApplicationDbContext context, IAuditService auditService)
        {
            _context = context;
            _auditService = auditService;
        }

        /// <summary>
        /// Get all proposals for a specific incident.
        /// </summary>
        [HttpGet("incident/{incidentId}")]
        public async Task<ActionResult<ApiResponse<List<ProposalDto>>>> GetByIncident(int incidentId)
        {
            var incident = await _context.Incidents.FindAsync(incidentId);
            if (incident == null)
                return NotFound(ApiResponse<List<ProposalDto>>.ErrorResponse("Incident not found."));

            var proposals = await _context.Proposals
                .Include(p => p.Contractor)
                .Include(p => p.LineItems)
                .Where(p => p.IncidentId == incidentId)
                .OrderByDescending(p => p.Score)
                .ToListAsync();

            var dtos = proposals.Select(MapToDto).ToList();

            return Ok(ApiResponse<List<ProposalDto>>.SuccessResponse(dtos));
        }

        /// <summary>
        /// Get proposals submitted by the current contractor.
        /// </summary>
        [HttpGet("my-proposals")]
        [Authorize(Roles = "Contractor")]
        public async Task<ActionResult<ApiResponse<List<ProposalDto>>>> GetMyProposals()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var proposals = await _context.Proposals
                .Include(p => p.Contractor)
                .Include(p => p.LineItems)
                .Where(p => p.ContractorId == userId)
                .OrderByDescending(p => p.SubmittedAt)
                .ToListAsync();

            var dtos = proposals.Select(MapToDto).ToList();

            return Ok(ApiResponse<List<ProposalDto>>.SuccessResponse(dtos));
        }

        /// <summary>
        /// Get a single proposal by ID.
        /// </summary>
        [HttpGet("{id}")]
        public async Task<ActionResult<ApiResponse<ProposalDto>>> GetById(int id)
        {
            var proposal = await _context.Proposals
                .Include(p => p.Contractor)
                .Include(p => p.LineItems)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (proposal == null)
                return NotFound(ApiResponse<ProposalDto>.ErrorResponse("Proposal not found."));

            return Ok(ApiResponse<ProposalDto>.SuccessResponse(MapToDto(proposal)));
        }

        /// <summary>
        /// Contractor submits a digital tender (proposal with line-item breakdown).
        /// </summary>
        [HttpPost("incident/{incidentId}")]
        [Authorize(Roles = "Contractor")]
        public async Task<ActionResult<ApiResponse<ProposalDto>>> Submit(int incidentId, [FromBody] CreateProposalDto dto)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            // Validate incident exists and is published for bidding
            var incident = await _context.Incidents.FindAsync(incidentId);
            if (incident == null)
                return NotFound(ApiResponse<ProposalDto>.ErrorResponse("Incident not found."));

            if (incident.Status != IncidentStatus.Published)
                return BadRequest(ApiResponse<ProposalDto>.ErrorResponse("Incident is not open for bidding."));

            // Check if contractor already submitted a proposal
            var existingProposal = await _context.Proposals
                .AnyAsync(p => p.IncidentId == incidentId && p.ContractorId == userId);

            if (existingProposal)
                return BadRequest(ApiResponse<ProposalDto>.ErrorResponse("You have already submitted a proposal for this incident."));

            // Validate line items
            if (dto.LineItems == null || !dto.LineItems.Any())
                return BadRequest(ApiResponse<ProposalDto>.ErrorResponse("At least one line item is required."));

            // Create proposal
            var proposal = new Proposal
            {
                IncidentId = incidentId,
                ContractorId = userId!,
                CoverLetter = dto.CoverLetter,
                EstimatedDays = dto.EstimatedDays,
                Status = ProposalStatus.Submitted,
                SubmittedAt = DateTime.UtcNow
            };

            // Create line items and calculate total
            foreach (var lineItemDto in dto.LineItems)
            {
                var lineItem = new ProposalLineItem
                {
                    Category = lineItemDto.Category,
                    Description = lineItemDto.Description,
                    Quantity = lineItemDto.Quantity,
                    UnitPrice = lineItemDto.UnitPrice,
                    LineTotal = lineItemDto.Quantity * lineItemDto.UnitPrice
                };
                proposal.LineItems.Add(lineItem);
            }

            proposal.TotalCost = proposal.LineItems.Sum(li => li.LineTotal);

            _context.Proposals.Add(proposal);
            await _context.SaveChangesAsync();

            // Audit log
            await _auditService.LogAsync(userId, "ProposalSubmitted", "Proposal", proposal.Id,
                newValue: $"IncidentId: {incidentId}, TotalCost: M{proposal.TotalCost:N2}, EstimatedDays: {proposal.EstimatedDays}",
                ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString());

            // Reload with navigation properties
            await _context.Entry(proposal).Reference(p => p.Contractor).LoadAsync();
 
            return Ok(ApiResponse<ProposalDto>.SuccessResponse(MapToDto(proposal), "Proposal submitted successfully."));
        }

        /// <summary>
        /// Contractor updates an existing tender application while it's still in Submitted status.
        /// </summary>
        [HttpPut("{id}")]
        [Authorize(Roles = "Contractor")]
        public async Task<ActionResult<ApiResponse<ProposalDto>>> Update(int id, [FromBody] CreateProposalDto dto)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
 
            var proposal = await _context.Proposals
                .Include(p => p.LineItems)
                .Include(p => p.Contractor)
                .FirstOrDefaultAsync(p => p.Id == id);
 
            if (proposal == null)
                return NotFound(ApiResponse<ProposalDto>.ErrorResponse("Proposal not found."));
 
            if (proposal.ContractorId != userId)
                return Forbid();
 
            if (proposal.Status != ProposalStatus.Submitted)
                return BadRequest(ApiResponse<ProposalDto>.ErrorResponse("Proposals can only be edited while in Submitted status."));
 
            // Validate line items
            if (dto.LineItems == null || !dto.LineItems.Any())
                return BadRequest(ApiResponse<ProposalDto>.ErrorResponse("At least one line item is required."));
 
            // Update fields
            proposal.CoverLetter = dto.CoverLetter;
            proposal.EstimatedDays = dto.EstimatedDays;
            proposal.SubmittedAt = DateTime.UtcNow; // Mark as updated
 
            // Replace line items
            _context.ProposalLineItems.RemoveRange(proposal.LineItems);
            proposal.LineItems.Clear();
 
            foreach (var lineItemDto in dto.LineItems)
            {
                proposal.LineItems.Add(new ProposalLineItem
                {
                    Category = lineItemDto.Category,
                    Description = lineItemDto.Description,
                    Quantity = lineItemDto.Quantity,
                    UnitPrice = lineItemDto.UnitPrice,
                    LineTotal = lineItemDto.Quantity * lineItemDto.UnitPrice
                });
            }
 
            proposal.TotalCost = proposal.LineItems.Sum(li => li.LineTotal);
 
            await _context.SaveChangesAsync();
 
            // Audit log
            await _auditService.LogAsync(userId, "ProposalUpdated", "Proposal", proposal.Id,
                newValue: $"New TotalCost: M{proposal.TotalCost:N2}, EstimatedDays: {proposal.EstimatedDays}",
                ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString());
 
            return Ok(ApiResponse<ProposalDto>.SuccessResponse(MapToDto(proposal), "Proposal updated successfully."));
        }

        /// <summary>
        /// Admin accepts or rejects a proposal.
        /// </summary>
        [HttpPut("{id}/status")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<ApiResponse<ProposalDto>>> UpdateStatus(int id, [FromBody] ProposalStatus status)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var proposal = await _context.Proposals
                .Include(p => p.Contractor)
                .Include(p => p.LineItems)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (proposal == null)
                return NotFound(ApiResponse<ProposalDto>.ErrorResponse("Proposal not found."));

            var oldStatus = proposal.Status.ToString();
            proposal.Status = status;

            await _context.SaveChangesAsync();

            await _auditService.LogAsync(userId, "ProposalStatusChanged", "Proposal", proposal.Id,
                oldValue: oldStatus,
                newValue: status.ToString(),
                ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString());

            return Ok(ApiResponse<ProposalDto>.SuccessResponse(MapToDto(proposal), $"Proposal status changed to {status}."));
        }

        // ========================
        // HELPERS
        // ========================

        private static ProposalDto MapToDto(Proposal proposal)
        {
            return new ProposalDto
            {
                Id = proposal.Id,
                IncidentId = proposal.IncidentId,
                ContractorId = proposal.ContractorId,
                ContractorName = proposal.Contractor?.FullName ?? "Unknown",
                CompanyName = proposal.Contractor?.CompanyName,
                CoverLetter = proposal.CoverLetter,
                EstimatedDays = proposal.EstimatedDays,
                TotalCost = proposal.TotalCost,
                Status = proposal.Status.ToString(),
                Score = proposal.Score,
                SubmittedAt = proposal.SubmittedAt,
                LineItems = proposal.LineItems?.Select(li => new LineItemDto
                {
                    Id = li.Id,
                    Category = li.Category.ToString(),
                    Description = li.Description,
                    Quantity = li.Quantity,
                    UnitPrice = li.UnitPrice,
                    LineTotal = li.LineTotal
                }).ToList() ?? new()
            };
        }
    }
}
