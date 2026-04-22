using MesaMohloane.API.Data;
using MesaMohloane.API.Models;
using MesaMohloane.API.Models.DTOs;
using MesaMohloane.API.Services.Auditing;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Microsoft.Extensions.Logging;

namespace MesaMohloane.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class IncidentsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IAuditService _auditService;
        private readonly IWebHostEnvironment _environment;
        private readonly ILogger<IncidentsController> _logger;
        private readonly IConfiguration _configuration;

        public IncidentsController(
            ApplicationDbContext context,
            IAuditService auditService,
            IWebHostEnvironment environment,
            ILogger<IncidentsController> logger,
            IConfiguration configuration)
        {
            _context = context;
            _auditService = auditService;
            _environment = environment;
            _logger = logger;
            _configuration = configuration;
        }

        /// <summary>
        /// Get all incidents with optional filtering by status and category.
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<ApiResponse<List<IncidentDto>>>> GetAll(
            [FromQuery] IncidentStatus? status = null,
            [FromQuery] IncidentCategory? category = null)
        {
            var query = _context.Incidents
                .Include(i => i.Citizen)
                .Include(i => i.AssignedContractor)
                .Include(i => i.Proposals)
                    .ThenInclude(p => p.Invoice)
                        .ThenInclude(inv => inv.Payment)
                .AsQueryable();

            if (status.HasValue)
                query = query.Where(i => i.Status == status.Value);

            if (category.HasValue)
                query = query.Where(i => i.Category == category.Value);

            var incidents = await query.OrderByDescending(i => i.CreatedAt).ToListAsync();

            var dtos = incidents.Select(MapToDto).ToList();

            return Ok(ApiResponse<List<IncidentDto>>.SuccessResponse(dtos));
        }

        /// <summary>
        /// Get incidents published for bidding (visible to Contractors).
        /// </summary>
        [HttpGet("published")]
        public async Task<ActionResult<ApiResponse<List<IncidentDto>>>> GetPublished()
        {
            var incidents = await _context.Incidents
                .Include(i => i.Citizen)
                .Include(i => i.Proposals)
                    .ThenInclude(p => p.Invoice)
                        .ThenInclude(inv => inv.Payment)
                .Where(i => i.Status == IncidentStatus.Published)
                .OrderByDescending(i => i.CreatedAt)
                .ToListAsync();

            var dtos = incidents.Select(MapToDto).ToList();

            return Ok(ApiResponse<List<IncidentDto>>.SuccessResponse(dtos));
        }

        /// <summary>
        /// Get incidents reported by the current citizen.
        /// </summary>
        [HttpGet("my-reports")]
        [Authorize(Roles = "Citizen")]
        public async Task<ActionResult<ApiResponse<List<IncidentDto>>>> GetMyReports()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var incidents = await _context.Incidents
                .Include(i => i.Citizen)
                .Include(i => i.AssignedContractor)
                .Include(i => i.Proposals)
                    .ThenInclude(p => p.Invoice)
                        .ThenInclude(inv => inv.Payment)
                .Where(i => i.CitizenId == userId)
                .OrderByDescending(i => i.CreatedAt)
                .ToListAsync();

            var dtos = incidents.Select(MapToDto).ToList();

            return Ok(ApiResponse<List<IncidentDto>>.SuccessResponse(dtos));
        }

        /// <summary>
        /// Get a single incident by ID.
        /// </summary>
        [HttpGet("{id}")]
        public async Task<ActionResult<ApiResponse<IncidentDto>>> GetById(int id)
        {
            var incident = await _context.Incidents
                .Include(i => i.Citizen)
                .Include(i => i.AssignedContractor)
                .Include(i => i.Proposals)
                    .ThenInclude(p => p.Invoice)
                        .ThenInclude(inv => inv.Payment)
                .FirstOrDefaultAsync(i => i.Id == id);

            if (incident == null)
                return NotFound(ApiResponse<IncidentDto>.ErrorResponse("Incident not found."));

            return Ok(ApiResponse<IncidentDto>.SuccessResponse(MapToDto(incident)));
        }

        /// <summary>
        /// Citizen creates a new incident report with optional photo upload.
        /// </summary>
        [HttpPost]
        [Authorize(Roles = "Citizen")]
        public async Task<ActionResult<ApiResponse<IncidentDto>>> Create([FromForm] CreateIncidentDto dto)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            string? photoUrl = null;
            if (dto.Photo != null)
            {
                photoUrl = await SavePhoto(dto.Photo);
            }

            var incident = new Incident
            {
                Title = dto.Title,
                Description = dto.Description,
                Category = dto.Category,
                Location = dto.Location,
                Latitude = dto.Latitude,
                Longitude = dto.Longitude,
                PhotoUrl = photoUrl,
                Status = IncidentStatus.Reported,
                CitizenId = userId!,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.Incidents.Add(incident);
            await _context.SaveChangesAsync();

            // Audit log
            await _auditService.LogAsync(userId, "IncidentCreated", "Incident", incident.Id,
                newValue: $"Title: {incident.Title}, Category: {incident.Category}, Location: {incident.Location}",
                ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString());

            // Reload with navigation properties
            await _context.Entry(incident).Reference(i => i.Citizen).LoadAsync();

            return CreatedAtAction(nameof(GetById), new { id = incident.Id },
                ApiResponse<IncidentDto>.SuccessResponse(MapToDto(incident), "Incident reported successfully."));
        }

        /// <summary>
        /// Update an incident's details (Citizen who owns it, or Admin).
        /// </summary>
        [HttpPut("{id}")]
        [Authorize(Roles = "Citizen,Admin")]
        public async Task<ActionResult<ApiResponse<IncidentDto>>> Update(int id, [FromBody] UpdateIncidentDto dto)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var incident = await _context.Incidents.FindAsync(id);

            if (incident == null)
                return NotFound(ApiResponse<IncidentDto>.ErrorResponse("Incident not found."));

            // Citizens can only edit their own incidents
            if (User.IsInRole("Citizen") && incident.CitizenId != userId)
                return Forbid();

            var oldValues = $"Title: {incident.Title}, Category: {incident.Category}";

            if (dto.Title != null) incident.Title = dto.Title;
            if (dto.Description != null) incident.Description = dto.Description;
            if (dto.Category.HasValue) incident.Category = dto.Category.Value;
            if (dto.Location != null) incident.Location = dto.Location;
            if (dto.Latitude.HasValue) incident.Latitude = dto.Latitude.Value;
            if (dto.Longitude.HasValue) incident.Longitude = dto.Longitude.Value;
            incident.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            await _auditService.LogAsync(userId, "IncidentUpdated", "Incident", incident.Id,
                oldValue: oldValues,
                newValue: $"Title: {incident.Title}, Category: {incident.Category}",
                ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString());

            await _context.Entry(incident).Reference(i => i.Citizen).LoadAsync();
            await _context.Entry(incident).Reference(i => i.AssignedContractor).LoadAsync();
            await _context.Entry(incident).Collection(i => i.Proposals).LoadAsync();

            return Ok(ApiResponse<IncidentDto>.SuccessResponse(MapToDto(incident), "Incident updated."));
        }

        /// <summary>
        /// Admin changes the status of an incident (Verify, Publish, etc.)
        /// </summary>
        [HttpPut("{id}/status")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<ApiResponse<IncidentDto>>> UpdateStatus(int id, [FromBody] UpdateIncidentStatusDto dto)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var incident = await _context.Incidents
                .Include(i => i.Citizen)
                .Include(i => i.AssignedContractor)
                .Include(i => i.Proposals)
                .FirstOrDefaultAsync(i => i.Id == id);

            if (incident == null)
                return NotFound(ApiResponse<IncidentDto>.ErrorResponse("Incident not found."));

            var oldStatus = incident.Status.ToString();
            incident.Status = dto.Status;
            incident.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            await _auditService.LogAsync(userId, "StatusChanged", "Incident", incident.Id,
                oldValue: oldStatus,
                newValue: dto.Status.ToString(),
                ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString());

            return Ok(ApiResponse<IncidentDto>.SuccessResponse(MapToDto(incident), $"Status changed to {dto.Status}."));
        }

        /// <summary>
        /// Delete an incident (Admin only, soft concept — sets to Closed).
        /// </summary>
        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<ApiResponse<string>>> Delete(int id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var incident = await _context.Incidents.FindAsync(id);

            if (incident == null)
                return NotFound(ApiResponse<string>.ErrorResponse("Incident not found."));

            incident.Status = IncidentStatus.Closed;
            incident.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            await _auditService.LogAsync(userId, "IncidentClosed", "Incident", incident.Id,
                newValue: "Closed by Admin",
                ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString());

            return Ok(ApiResponse<string>.SuccessResponse("Incident closed.", "Incident closed successfully."));
        }

        // ========================
        // HELPERS
        // ========================

        private async Task<string?> SavePhoto(IFormFile photo)
        {
            if (photo == null || photo.Length == 0)
                return null;

            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
            var extension = Path.GetExtension(photo.FileName).ToLowerInvariant();

            if (!allowedExtensions.Contains(extension))
            {
                _logger.LogWarning("Photo upload rejected: invalid extension {Extension}", extension);
                return null;
            }

            if (photo.Length > 5 * 1024 * 1024)
            {
                _logger.LogWarning("Photo upload rejected: file too large {Size} bytes", photo.Length);
                return null;
            }

            var webRoot = _environment.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
            var uploadsFolder = Path.Combine(webRoot, "uploads", "incidents");
            Directory.CreateDirectory(uploadsFolder);

            var uniqueFileName = $"{Guid.NewGuid()}{extension}";
            var filePath = Path.Combine(uploadsFolder, uniqueFileName);

            using var stream = new FileStream(filePath, FileMode.Create);
            await photo.CopyToAsync(stream);

            _logger.LogInformation("Photo uploaded: {FileName}, {Size} bytes", uniqueFileName, photo.Length);

            var baseUrl = _configuration["ClientUrl"] ?? Request.Scheme + "://" + Request.Host.ToString();
            return $"{baseUrl}/uploads/incidents/{uniqueFileName}";
        }

        private static IncidentDto MapToDto(Incident incident)
        {
            // Traverse relationships to find acknowledgment status
            var isAcknowledged = false;
            var acceptedProposal = incident.Proposals?.FirstOrDefault(p => p.Status == ProposalStatus.Accepted);
            if (acceptedProposal != null && acceptedProposal.Invoice?.Payment != null)
            {
                isAcknowledged = acceptedProposal.Invoice.Payment.CitizenAcknowledged;
            }

            return new IncidentDto
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
                ProposalCount = incident.Proposals?.Count ?? 0,
                IsAcknowledged = isAcknowledged
            };
        }
    }
}
