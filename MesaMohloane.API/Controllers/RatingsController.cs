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
    public class RatingsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IAuditService _auditService;

        public RatingsController(ApplicationDbContext context, IAuditService auditService)
        {
            _context = context;
            _auditService = auditService;
        }

        /// <summary>
        /// Citizen rates a contractor after job completion.
        /// </summary>
        [HttpPost("incident/{incidentId}")]
        [Authorize(Roles = "Citizen")]
        public async Task<ActionResult<ApiResponse<RatingDto>>> RateContractor(int incidentId, [FromBody] CreateRatingDto dto)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var incident = await _context.Incidents
                .Include(i => i.AssignedContractor)
                .FirstOrDefaultAsync(i => i.Id == incidentId);

            if (incident == null)
                return NotFound(ApiResponse<RatingDto>.ErrorResponse("Incident not found."));

            // Citizen must own the incident
            if (incident.CitizenId != userId)
                return Forbid();

            // Incident must be completed
            if (incident.Status != IncidentStatus.Completed && incident.Status != IncidentStatus.Closed)
                return BadRequest(ApiResponse<RatingDto>.ErrorResponse("Can only rate after work is completed."));

            // Must have an assigned contractor
            if (incident.AssignedContractorId == null)
                return BadRequest(ApiResponse<RatingDto>.ErrorResponse("No contractor assigned to this incident."));

            // Check if already rated
            var existingRating = await _context.ContractorRatings
                .AnyAsync(r => r.IncidentId == incidentId);

            if (existingRating)
                return BadRequest(ApiResponse<RatingDto>.ErrorResponse("You have already rated this contractor for this incident."));

            var rating = new ContractorRating
            {
                IncidentId = incidentId,
                ContractorId = incident.AssignedContractorId,
                CitizenId = userId!,
                Rating = dto.Rating,
                Comment = dto.Comment,
                CreatedAt = DateTime.UtcNow
            };

            _context.ContractorRatings.Add(rating);
            await _context.SaveChangesAsync();

            // Update contractor's average rating
            var contractor = incident.AssignedContractor!;
            var allRatings = await _context.ContractorRatings
                .Where(r => r.ContractorId == contractor.Id)
                .ToListAsync();

            contractor.AverageRating = allRatings.Average(r => r.Rating);
            await _context.SaveChangesAsync();

            // Audit log
            await _auditService.LogAsync(userId, "ContractorRated", "ContractorRating", rating.Id,
                newValue: $"Rating: {dto.Rating}/5 for Contractor: {contractor.FullName}",
                ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString());

            var user = await _context.Users.FindAsync(userId);

            return Ok(ApiResponse<RatingDto>.SuccessResponse(new RatingDto
            {
                Id = rating.Id,
                IncidentId = rating.IncidentId,
                ContractorId = rating.ContractorId,
                ContractorName = contractor.FullName,
                CitizenId = rating.CitizenId,
                CitizenName = user?.FullName ?? "Unknown",
                Rating = rating.Rating,
                Comment = rating.Comment,
                CreatedAt = rating.CreatedAt
            }, "Contractor rated successfully."));
        }

        /// <summary>
        /// Get all ratings for a contractor.
        /// </summary>
        [HttpGet("contractor/{contractorId}")]
        public async Task<ActionResult<ApiResponse<List<RatingDto>>>> GetContractorRatings(string contractorId)
        {
            var ratings = await _context.ContractorRatings
                .Include(r => r.Contractor)
                .Include(r => r.Citizen)
                .Where(r => r.ContractorId == contractorId)
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync();

            var dtos = ratings.Select(r => new RatingDto
            {
                Id = r.Id,
                IncidentId = r.IncidentId,
                ContractorId = r.ContractorId,
                ContractorName = r.Contractor?.FullName ?? "Unknown",
                CitizenId = r.CitizenId,
                CitizenName = r.Citizen?.FullName ?? "Unknown",
                Rating = r.Rating,
                Comment = r.Comment,
                CreatedAt = r.CreatedAt
            }).ToList();

            return Ok(ApiResponse<List<RatingDto>>.SuccessResponse(dtos));
        }
    }
}
