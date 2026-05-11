using MesaMohloane.API.Data;
using MesaMohloane.API.Models;
using MesaMohloane.API.Models.DTOs;
using MesaMohloane.API.Services.Auditing;
using MesaMohloane.API.Services.Email;
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
    public class PaymentsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IAuditService _auditService;
        private readonly IEmailService _emailService;
        private readonly IInvoiceValidationService _validationService;

        public PaymentsController(
            ApplicationDbContext context,
            IAuditService auditService,
            IEmailService emailService,
            IInvoiceValidationService validationService)
        {
            _context = context;
            _auditService = auditService;
            _emailService = emailService;
            _validationService = validationService;
        }

        /// <summary>
        /// Get payment for a specific invoice.
        /// </summary>
        [HttpGet("invoice/{invoiceId}")]
        public async Task<ActionResult<ApiResponse<PaymentDto>>> GetByInvoice(int invoiceId)
        {
            var payment = await _context.Payments
                .FirstOrDefaultAsync(p => p.InvoiceId == invoiceId);

            if (payment == null)
                return NotFound(ApiResponse<PaymentDto>.ErrorResponse("Payment not found."));

            return Ok(ApiResponse<PaymentDto>.SuccessResponse(MapToDto(payment)));
        }

        /// <summary>
        /// Get payment for a specific proposal.
        /// </summary>
        [HttpGet("proposal/{proposalId}")]
        public async Task<ActionResult<ApiResponse<PaymentDto>>> GetByProposal(int proposalId)
        {
            var payment = await _context.Payments
                .Include(p => p.Invoice)
                .FirstOrDefaultAsync(p => p.Invoice != null && p.Invoice.ProposalId == proposalId);

            if (payment == null)
                return NotFound(ApiResponse<PaymentDto>.ErrorResponse("Payment record not found for this contractor proposal."));

            return Ok(ApiResponse<PaymentDto>.SuccessResponse(MapToDto(payment)));
        }

        /// <summary>
        /// Get all payments (Admin/Auditor view).
        /// </summary>
        [HttpGet]
        [Authorize(Roles = "Admin,Auditor")]
        public async Task<ActionResult<ApiResponse<List<PaymentDto>>>> GetAll()
        {
            var payments = await _context.Payments
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync();

            var dtos = payments.Select(MapToDto).ToList();

            return Ok(ApiResponse<List<PaymentDto>>.SuccessResponse(dtos));
        }

        /// <summary>
        /// Get all payments for the current contractor.
        /// </summary>
        [HttpGet("my-payments")]
        [Authorize(Roles = "Contractor")]
        public async Task<ActionResult<ApiResponse<List<ContractorPaymentDto>>>> GetMyPayments()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var payments = await _context.Payments
                .Include(p => p.Invoice)
                    .ThenInclude(i => i.Contractor)
                .Include(p => p.Invoice)
                    .ThenInclude(i => i.Proposal)
                        .ThenInclude(pr => pr.Incident)
                .Where(p => p.Invoice.ContractorId == userId)
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync();

            var dtos = payments.Select(p => new ContractorPaymentDto
            {
                Id = p.Id,
                InvoiceId = p.InvoiceId,
                IncidentId = p.Invoice?.Proposal?.IncidentId ?? 0,
                IncidentTitle = p.Invoice?.Proposal?.Incident?.Title,
                Amount = p.Amount,
                Status = p.Status.ToString(),
                CitizenAcknowledged = p.CitizenAcknowledged,
                AdminApproved = p.AdminApproved,
                DisbursedAt = p.DisbursedAt,
                CreatedAt = p.CreatedAt
            }).ToList();

            return Ok(ApiResponse<List<ContractorPaymentDto>>.SuccessResponse(dtos));
        }

        /// <summary>
        /// Citizen acknowledges that the work is complete.
        /// </summary>
        [HttpPut("{id}/acknowledge")]
        [Authorize(Roles = "Citizen")]
        public async Task<ActionResult<ApiResponse<PaymentDto>>> Acknowledge(int id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var payment = await _context.Payments
                .Include(p => p.Invoice)
                    .ThenInclude(i => i.Proposal)
                        .ThenInclude(pr => pr.Incident)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (payment == null)
                return NotFound(ApiResponse<PaymentDto>.ErrorResponse("Payment not found."));

            // Verify the citizen owns the incident
            var incident = payment.Invoice?.Proposal?.Incident;
            if (incident == null || incident.CitizenId != userId)
                return Forbid();

            payment.CitizenAcknowledged = true;
            await _context.SaveChangesAsync();

            await _auditService.LogAsync(userId, "CitizenAcknowledged", "Payment", payment.Id,
                newValue: "Citizen acknowledged work completion",
                ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString());

            // Check if payment can now be disbursed
            if (_validationService.CanDisbursePayment(payment))
            {
                return Ok(ApiResponse<PaymentDto>.SuccessResponse(MapToDto(payment),
                    "Work acknowledged. Payment is ready for disbursement."));
            }

            return Ok(ApiResponse<PaymentDto>.SuccessResponse(MapToDto(payment),
                "Work acknowledged. Awaiting Admin approval for payment."));
        }

        /// <summary>
        /// Admin disburses the payment (Status Guard enforced).
        /// </summary>
        [HttpPut("{id}/disburse")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<ApiResponse<PaymentDto>>> Disburse(int id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var payment = await _context.Payments
                .Include(p => p.Invoice)
                    .ThenInclude(i => i.Contractor)
                .Include(p => p.Invoice)
                    .ThenInclude(i => i.Proposal)
                        .ThenInclude(pr => pr.Incident)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (payment == null)
                return NotFound(ApiResponse<PaymentDto>.ErrorResponse("Payment not found."));

            // Ensure admin has approved
            payment.AdminApproved = true;

            // STATUS GUARD: Both conditions must be met
            if (!_validationService.CanDisbursePayment(payment))
            {
                await _context.SaveChangesAsync();
                return BadRequest(ApiResponse<PaymentDto>.ErrorResponse(
                    "Cannot disburse payment. The citizen has not yet acknowledged work completion."));
            }

            payment.Status = PaymentStatus.Disbursed;
            payment.DisbursedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            await _auditService.LogAsync(userId, "PaymentDisbursed", "Payment", payment.Id,
                newValue: $"Payment of M{payment.Amount:N2} disbursed",
                ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString());

            // Send notification to contractor
            var contractor = payment.Invoice?.Contractor;
            var incident = payment.Invoice?.Proposal?.Incident;
            if (contractor != null && incident != null && contractor.Email != null)
            {
                await _emailService.SendPaymentNotificationAsync(
                    contractor.Email, contractor.FullName, incident.Title, payment.Amount);
            }

            return Ok(ApiResponse<PaymentDto>.SuccessResponse(MapToDto(payment), "Payment disbursed successfully."));
        }

        // ========================
        // HELPERS
        // ========================

        private static PaymentDto MapToDto(Payment payment)
        {
            return new PaymentDto
            {
                Id = payment.Id,
                InvoiceId = payment.InvoiceId,
                Amount = payment.Amount,
                Status = payment.Status.ToString(),
                CitizenAcknowledged = payment.CitizenAcknowledged,
                AdminApproved = payment.AdminApproved,
                DisbursedAt = payment.DisbursedAt,
                CreatedAt = payment.CreatedAt
            };
        }
    }
}
