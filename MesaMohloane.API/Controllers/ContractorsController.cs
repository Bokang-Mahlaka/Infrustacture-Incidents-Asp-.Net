using MesaMohloane.API.Data;
using MesaMohloane.API.Models;
using MesaMohloane.API.Models.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace MesaMohloane.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "Admin")]
    public class ContractorsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public ContractorsController(ApplicationDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Get list of all contractors with summary metrics.
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<ApiResponse<List<ContractorSummaryDto>>>> GetAll(
            [FromQuery] string? searchTerm = null,
            [FromQuery] string? sortBy = "name",
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 50)
        {
            var query = _context.Users
                .Where(u => u.Proposals.Any()) // Only contractors who submitted proposals
                .AsQueryable();

            if (!string.IsNullOrEmpty(searchTerm))
            {
                var term = searchTerm.ToLower();
                query = query.Where(u => 
                    u.FullName.ToLower().Contains(term) || 
                    u.CompanyName != null && u.CompanyName.ToLower().Contains(term));
            }

            var contractors = await query
                .Select(u => new
                {
                    Contractor = u,
                    ProposalCount = u.Proposals.Count,
                    CompletedCount = u.CompletedJobs,
                    AverageCost = u.Proposals.Any() ? u.Proposals.Average(p => p.TotalCost) : 0m
                })
                .ToListAsync();

            // Sort in memory after retrieval
            if (sortBy == "rating")
                contractors = contractors.OrderByDescending(x => x.Contractor.AverageRating).ToList();
            else if (sortBy == "jobs")
                contractors = contractors.OrderByDescending(x => x.CompletedCount).ToList();
            else
                contractors = contractors.OrderBy(x => x.Contractor.FullName).ToList();

            contractors = contractors
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            var dtos = contractors.Select(c => new ContractorSummaryDto
            {
                Id = c.Contractor.Id,
                FullName = c.Contractor.FullName,
                CompanyName = c.Contractor.CompanyName,
                Email = c.Contractor.Email,
                AverageRating = c.Contractor.AverageRating,
                CompletedJobs = c.CompletedCount,
                ProposalsSubmitted = c.ProposalCount,
                AverageProposalCost = (double)c.AverageCost,
                CreatedAt = c.Contractor.CreatedAt
            }).ToList();

            return Ok(ApiResponse<List<ContractorSummaryDto>>.SuccessResponse(dtos));
        }

        /// <summary>
        /// Get detailed contractor profile with proposal and invoice history.
        /// </summary>
        [HttpGet("{contractorId}")]
        public async Task<ActionResult<ApiResponse<ContractorDetailDto>>> GetDetail(string contractorId)
        {
            var contractor = await _context.Users
                .Include(u => u.Proposals)
                    .ThenInclude(p => p.Incident)
                .Include(u => u.Proposals)
                    .ThenInclude(p => p.LineItems)
                .Include(u => u.ReceivedRatings)
                .FirstOrDefaultAsync(u => u.Id == contractorId);

            if (contractor == null)
                return NotFound(ApiResponse<ContractorDetailDto>.ErrorResponse("Contractor not found."));

            // Get invoices for this contractor
            var invoices = await _context.Invoices
                .Include(i => i.LineItems)
                .Include(i => i.Proposal)
                    .ThenInclude(p => p.Incident)
                .Where(i => i.ContractorId == contractorId)
                .ToListAsync();

            var proposals = contractor.Proposals.Select(p => new ContractorProposalDto
            {
                Id = p.Id,
                IncidentId = p.IncidentId,
                IncidentTitle = p.Incident?.Title,
                IncidentCategory = p.Incident?.Category.ToString(),
                TotalCost = p.TotalCost,
                EstimatedDays = p.EstimatedDays,
                Status = p.Status.ToString(),
                Score = (decimal)p.Score,
                SubmittedAt = p.SubmittedAt
            }).ToList();

            var invoiceDtos = invoices.Select(i => new ContractorInvoiceDto
            {
                Id = i.Id,
                ProposalId = i.ProposalId,
                IncidentTitle = i.Proposal?.Incident?.Title,
                TotalAmount = i.TotalAmount,
                Status = i.Status.ToString(),
                DeviationFlagged = i.DeviationFlagged,
                DeviationPercentage = (decimal)i.DeviationPercentage,
                SubmittedAt = i.SubmittedAt
            }).ToList();

            var detail = new ContractorDetailDto
            {
                Id = contractor.Id,
                FullName = contractor.FullName,
                CompanyName = contractor.CompanyName,
                Email = contractor.Email,
                PhoneNumber = contractor.PhoneNumber,
                AverageRating = contractor.AverageRating,
                CompletedJobs = contractor.CompletedJobs,
                LateCompletions = contractor.LateCompletions,
                CreatedAt = contractor.CreatedAt,
                Proposals = proposals,
                Invoices = invoiceDtos,
                RatingsCount = contractor.ReceivedRatings.Count
            };

            return Ok(ApiResponse<ContractorDetailDto>.SuccessResponse(detail));
        }
        /// <summary>
        /// Get list of contractors awaiting administrative approval.
        /// </summary>
        [HttpGet("pending")]
        public async Task<ActionResult<ApiResponse<List<ContractorSummaryDto>>>> GetPending()
        {
            var contractorRole = await _context.Roles.FirstOrDefaultAsync(r => r.Name == "Contractor");
            if (contractorRole == null)
            {
                return Ok(ApiResponse<List<ContractorSummaryDto>>.SuccessResponse(new List<ContractorSummaryDto>()));
            }

            var pendingUsers = await _context.Users
                .Join(_context.UserRoles, u => u.Id, ur => ur.UserId, (u, ur) => new { u, ur })
                .Where(x => x.ur.RoleId == contractorRole.Id && x.u.RegistrationStatus == RegistrationStatus.Pending)
                .Select(x => x.u)
                .ToListAsync();

            var dtos = pendingUsers.Select(u => new ContractorSummaryDto
            {
                Id = u.Id,
                FullName = u.FullName,
                CompanyName = u.CompanyName,
                Email = u.Email,
                AverageRating = u.AverageRating,
                CompletedJobs = u.CompletedJobs,
                CreatedAt = u.CreatedAt
            }).ToList();

            return Ok(ApiResponse<List<ContractorSummaryDto>>.SuccessResponse(dtos));
        }

        /// <summary>
        /// Approve a contractor's registration.
        /// </summary>
        [HttpPost("{contractorId}/approve")]
        public async Task<ActionResult<ApiResponse<bool>>> Approve(string contractorId)
        {
            var user = await _context.Users.FindAsync(contractorId);
            if (user == null)
                return NotFound(ApiResponse<bool>.ErrorResponse("Contractor not found."));

            user.RegistrationStatus = RegistrationStatus.Approved;
            await _context.SaveChangesAsync();

            return Ok(ApiResponse<bool>.SuccessResponse(true, "Contractor approved successfully."));
        }

        /// <summary>
        /// Reject a contractor's registration.
        /// </summary>
        [HttpPost("{contractorId}/reject")]
        public async Task<ActionResult<ApiResponse<bool>>> Reject(string contractorId)
        {
            var user = await _context.Users.FindAsync(contractorId);
            if (user == null)
                return NotFound(ApiResponse<bool>.ErrorResponse("Contractor not found."));

            user.RegistrationStatus = RegistrationStatus.Rejected;
            await _context.SaveChangesAsync();

            return Ok(ApiResponse<bool>.SuccessResponse(true, "Contractor rejected."));
        }
    }
}
