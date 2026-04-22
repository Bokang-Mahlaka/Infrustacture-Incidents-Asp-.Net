using MesaMohloane.API.Data;
using MesaMohloane.API.Models;
using MesaMohloane.API.Models.DTOs;
using MesaMohloane.API.Services.Auditing;
using MesaMohloane.API.Services.InvoiceValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace MesaMohloane.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class InvoicesController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IAuditService _auditService;
        private readonly IInvoiceValidationService _validationService;

        public InvoicesController(
            ApplicationDbContext context,
            IAuditService auditService,
            IInvoiceValidationService validationService)
        {
            _context = context;
            _auditService = auditService;
            _validationService = validationService;
        }

        /// <summary>
        /// Get all invoices (Admin/Auditor view).
        /// </summary>
        [HttpGet]
        [Authorize(Roles = "Admin,Auditor")]
        public async Task<ActionResult<ApiResponse<List<InvoiceDto>>>> GetAll()
        {
            var invoices = await _context.Invoices
                .Include(i => i.Contractor)
                .Include(i => i.LineItems)
                .Include(i => i.Proposal)
                .OrderByDescending(i => i.SubmittedAt)
                .ToListAsync();

            var dtos = invoices.Select(MapToDto).ToList();

            return Ok(ApiResponse<List<InvoiceDto>>.SuccessResponse(dtos));
        }

        /// <summary>
        /// Get flagged invoices (Auditor review).
        /// </summary>
        [HttpGet("flagged")]
        [Authorize(Roles = "Auditor,Admin")]
        public async Task<ActionResult<ApiResponse<List<InvoiceDto>>>> GetFlagged()
        {
            var invoices = await _context.Invoices
                .Include(i => i.Contractor)
                .Include(i => i.LineItems)
                .Include(i => i.Proposal)
                .Where(i => i.DeviationFlagged || i.Status == InvoiceStatus.Flagged)
                .OrderByDescending(i => i.SubmittedAt)
                .ToListAsync();

            var dtos = invoices.Select(MapToDto).ToList();

            return Ok(ApiResponse<List<InvoiceDto>>.SuccessResponse(dtos));
        }

        /// <summary>
        /// Get a single invoice by ID.
        /// </summary>
        [HttpGet("{id}")]
        public async Task<ActionResult<ApiResponse<InvoiceDto>>> GetById(int id)
        {
            var invoice = await _context.Invoices
                .Include(i => i.Contractor)
                .Include(i => i.LineItems)
                .Include(i => i.Proposal)
                .FirstOrDefaultAsync(i => i.Id == id);

            if (invoice == null)
                return NotFound(ApiResponse<InvoiceDto>.ErrorResponse("Invoice not found."));

            return Ok(ApiResponse<InvoiceDto>.SuccessResponse(MapToDto(invoice)));
        }

        /// <summary>
        /// Contractor submits a final invoice for an accepted proposal.
        /// Runs all validation checks: Integrity, Reference, and Deviation.
        /// </summary>
        [HttpPost("proposal/{proposalId}")]
        [Authorize(Roles = "Contractor")]
        public async Task<ActionResult<ApiResponse<InvoiceDto>>> Submit(int proposalId, [FromBody] CreateInvoiceDto dto)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            // Get proposal with details
            var proposal = await _context.Proposals
                .Include(p => p.LineItems)
                .FirstOrDefaultAsync(p => p.Id == proposalId);

            if (proposal == null)
                return NotFound(ApiResponse<InvoiceDto>.ErrorResponse("Proposal not found."));

            if (proposal.ContractorId != userId)
                return Forbid();

            // Check if invoice already exists
            var existingInvoice = await _context.Invoices.AnyAsync(i => i.ProposalId == proposalId);
            if (existingInvoice)
                return BadRequest(ApiResponse<InvoiceDto>.ErrorResponse("An invoice already exists for this proposal."));

            // Build the invoice
            var invoice = new Invoice
            {
                ProposalId = proposalId,
                ContractorId = userId!,
                TotalAmount = dto.TotalAmount,
                SubmittedAt = DateTime.UtcNow
            };

            // Create line items
            foreach (var lineItemDto in dto.LineItems)
            {
                invoice.LineItems.Add(new InvoiceLineItem
                {
                    Category = lineItemDto.Category,
                    Description = lineItemDto.Description,
                    Quantity = lineItemDto.Quantity,
                    UnitPrice = lineItemDto.UnitPrice,
                    LineTotal = lineItemDto.Quantity * lineItemDto.UnitPrice
                });
            }

            // ========================
            // RUN VALIDATION CHECKS
            // ========================
            var validationResult = _validationService.Validate(invoice, proposal);

            if (!validationResult.IsValid && validationResult.Errors.Any(e => e.Contains("Integrity Check")))
            {
                return BadRequest(ApiResponse<InvoiceDto>.ErrorResponse(
                    "Invoice validation failed.", validationResult.Errors));
            }

            if (!validationResult.IsValid && validationResult.Errors.Any(e => e.Contains("Reference Check")))
            {
                return BadRequest(ApiResponse<InvoiceDto>.ErrorResponse(
                    "Invoice validation failed.", validationResult.Errors));
            }

            // Set deviation flags
            invoice.DeviationFlagged = validationResult.DeviationFlagged;
            invoice.DeviationPercentage = validationResult.DeviationPercentage;

            if (validationResult.DeviationFlagged)
            {
                invoice.Status = InvoiceStatus.Flagged;
            }

            _context.Invoices.Add(invoice);
            
            // Update incident status to 'Completed' so citizen knows to acknowledge
            var incident = await _context.Incidents.FindAsync(proposal.IncidentId);
            if (incident != null)
            {
                incident.Status = IncidentStatus.Completed;
            }

            await _context.SaveChangesAsync();

            // Create payment record
            var payment = new Payment
            {
                InvoiceId = invoice.Id,
                Amount = invoice.TotalAmount,
                Status = PaymentStatus.Pending,
                CreatedAt = DateTime.UtcNow
            };
            _context.Payments.Add(payment);
            await _context.SaveChangesAsync();

            // Audit log
            var auditMessage = validationResult.DeviationFlagged
                ? $"Invoice submitted — FLAGGED ({validationResult.DeviationPercentage}% deviation)"
                : $"Invoice submitted — Total: M{invoice.TotalAmount:N2}";

            await _auditService.LogAsync(userId, "InvoiceSubmitted", "Invoice", invoice.Id,
                newValue: auditMessage,
                ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString());

            // Reload
            await _context.Entry(invoice).Reference(i => i.Contractor).LoadAsync();
            await _context.Entry(invoice).Reference(i => i.Proposal).LoadAsync();

            var message = validationResult.DeviationFlagged
                ? "Invoice submitted but flagged for auditor review due to cost deviation."
                : "Invoice submitted successfully.";

            return CreatedAtAction(nameof(GetById), new { id = invoice.Id },
                ApiResponse<InvoiceDto>.SuccessResponse(MapToDto(invoice), message));
        }

        /// <summary>
        /// Admin approves an invoice.
        /// </summary>
        [HttpPut("{id}/approve")]
        [Authorize(Roles = "Admin,Auditor")]
        public async Task<ActionResult<ApiResponse<InvoiceDto>>> Approve(int id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var invoice = await _context.Invoices
                .Include(i => i.Contractor)
                .Include(i => i.LineItems)
                .Include(i => i.Proposal)
                .Include(i => i.Payment)
                .FirstOrDefaultAsync(i => i.Id == id);

            if (invoice == null)
                return NotFound(ApiResponse<InvoiceDto>.ErrorResponse("Invoice not found."));

            var oldStatus = invoice.Status.ToString();
            invoice.Status = InvoiceStatus.Approved;
            invoice.ApprovedAt = DateTime.UtcNow;

            // Mark payment as admin approved
            if (invoice.Payment != null)
            {
                invoice.Payment.AdminApproved = true;
            }

            await _context.SaveChangesAsync();

            await _auditService.LogAsync(userId, "InvoiceApproved", "Invoice", invoice.Id,
                oldValue: oldStatus,
                newValue: "Approved",
                ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString());

            return Ok(ApiResponse<InvoiceDto>.SuccessResponse(MapToDto(invoice), "Invoice approved."));
        }

        /// <summary>
        /// Admin or Auditor rejects an invoice.
        /// </summary>
        [HttpPut("{id}/reject")]
        [Authorize(Roles = "Admin,Auditor")]
        public async Task<ActionResult<ApiResponse<InvoiceDto>>> Reject(int id, [FromBody] RejectInvoiceDto dto)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var invoice = await _context.Invoices
                .Include(i => i.Contractor)
                .Include(i => i.LineItems)
                .Include(i => i.Proposal)
                .FirstOrDefaultAsync(i => i.Id == id);

            if (invoice == null)
                return NotFound(ApiResponse<InvoiceDto>.ErrorResponse("Invoice not found."));

            var oldStatus = invoice.Status.ToString();
            invoice.Status = InvoiceStatus.Rejected;
            invoice.RejectionReason = dto.Reason;

            await _context.SaveChangesAsync();

            await _auditService.LogAsync(userId, "InvoiceRejected", "Invoice", invoice.Id,
                oldValue: oldStatus,
                newValue: $"Rejected — Reason: {dto.Reason}",
                ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString());

            return Ok(ApiResponse<InvoiceDto>.SuccessResponse(MapToDto(invoice), "Invoice rejected."));
        }

        // ========================
        // HELPERS
        // ========================

        private static InvoiceDto MapToDto(Invoice invoice)
        {
            return new InvoiceDto
            {
                Id = invoice.Id,
                ProposalId = invoice.ProposalId,
                ContractorId = invoice.ContractorId,
                ContractorName = invoice.Contractor?.FullName ?? "Unknown",
                TotalAmount = invoice.TotalAmount,
                Status = invoice.Status.ToString(),
                DeviationFlagged = invoice.DeviationFlagged,
                DeviationPercentage = invoice.DeviationPercentage,
                RejectionReason = invoice.RejectionReason,
                SubmittedAt = invoice.SubmittedAt,
                ApprovedAt = invoice.ApprovedAt,
                OriginalProposalCost = invoice.Proposal?.TotalCost ?? 0,
                LineItems = invoice.LineItems?.Select(li => new LineItemDto
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
