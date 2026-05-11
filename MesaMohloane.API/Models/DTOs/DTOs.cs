using System.ComponentModel.DataAnnotations;

namespace MesaMohloane.API.Models.DTOs
{
    // ========================
    // AUTH DTOs
    // ========================

    public class RegisterDto
    {
        [Required]
        [MaxLength(100)]
        public string FullName { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        [MinLength(6)]
        public string Password { get; set; } = string.Empty;

        [Required]
        public string Role { get; set; } = string.Empty; // Citizen, Contractor, Admin, Auditor

        [Phone]
        public string? PhoneNumber { get; set; }

        /// <summary>
        /// Required for Contractor role.
        /// </summary>
        [MaxLength(200)]
        public string? CompanyName { get; set; }
    }

    public class LoginDto
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        public string Password { get; set; } = string.Empty;
    }

    public class AuthResponseDto
    {
        public string Token { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public DateTime Expiration { get; set; }
    }

    public class UserProfileDto
    {
        public string Id { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public string? PhoneNumber { get; set; }
        public string? CompanyName { get; set; }
        public double AverageRating { get; set; }
        public int CompletedJobs { get; set; }
        public int LateCompletions { get; set; }
    }

    // ========================
    // INCIDENT DTOs
    // ========================

    public class CreateIncidentDto
    {
        [Required]
        [MaxLength(200)]
        public string Title { get; set; } = string.Empty;

        [Required]
        [MaxLength(2000)]
        public string Description { get; set; } = string.Empty;

        [Required]
        public IncidentCategory Category { get; set; }

        [Required]
        [MaxLength(300)]
        public string Location { get; set; } = string.Empty;

        public double Latitude { get; set; }
        public double Longitude { get; set; }

        /// <summary>
        /// Photo file upload (optional).
        /// </summary>
        public IFormFile? Photo { get; set; }
    }

    public class UpdateIncidentDto
    {
        [MaxLength(200)]
        public string? Title { get; set; }

        [MaxLength(2000)]
        public string? Description { get; set; }

        public IncidentCategory? Category { get; set; }

        [MaxLength(300)]
        public string? Location { get; set; }

        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
    }

    public class UpdateIncidentStatusDto
    {
        [Required]
        public IncidentStatus Status { get; set; }
    }

    public class IncidentDto
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public string? PhotoUrl { get; set; }
        public string Status { get; set; } = string.Empty;
        public string CitizenId { get; set; } = string.Empty;
        public string CitizenName { get; set; } = string.Empty;
        public string? AssignedContractorId { get; set; }
        public string? AssignedContractorName { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public int ProposalCount { get; set; }
        public bool IsAcknowledged { get; set; }
    }

    public class CitizenDashboardStatsDto
    {
        public int TotalReports { get; set; }
        public int ResolvedReports { get; set; }
        public double ResolutionRate { get; set; }
        public double? AverageAssignmentDays { get; set; }
    }

    public class AdminDashboardStatsDto
    {
        public int ActiveInfrastructureSignals { get; set; }
        public int ActiveBids { get; set; }
        public int PendingApprovals { get; set; }
        public double AverageProposalCost { get; set; }
    }

    // ========================
    // PROPOSAL DTOs
    // ========================

    public class CreateProposalDto
    {
        [MaxLength(3000)]
        public string CoverLetter { get; set; } = string.Empty;

        [Required]
        [Range(1, 365)]
        public int EstimatedDays { get; set; }

        [Required]
        [MinLength(1)]
        public List<CreateLineItemDto> LineItems { get; set; } = new();
    }

    public class CreateLineItemDto
    {
        [Required]
        public LineItemCategory Category { get; set; }

        [Required]
        [MaxLength(300)]
        public string Description { get; set; } = string.Empty;

        [Required]
        [Range(1, int.MaxValue)]
        public int Quantity { get; set; }

        [Required]
        [Range(0.01, double.MaxValue)]
        public decimal UnitPrice { get; set; }
    }

    public class ProposalDto
    {
        public int Id { get; set; }
        public int IncidentId { get; set; }
        public string ContractorId { get; set; } = string.Empty;
        public string ContractorName { get; set; } = string.Empty;
        public string? CompanyName { get; set; }
        public string CoverLetter { get; set; } = string.Empty;
        public int EstimatedDays { get; set; }
        public decimal TotalCost { get; set; }
        public string Status { get; set; } = string.Empty;
        public double Score { get; set; }
        public DateTime SubmittedAt { get; set; }
        public List<LineItemDto> LineItems { get; set; } = new();
    }

    public class LineItemDto
    {
        public int Id { get; set; }
        public string Category { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal LineTotal { get; set; }
    }

    // ========================
    // INVOICE DTOs
    // ========================

    public class CreateInvoiceDto
    {
        [Required]
        [Range(0.01, double.MaxValue)]
        public decimal TotalAmount { get; set; }

        [Required]
        [MinLength(1)]
        public List<CreateLineItemDto> LineItems { get; set; } = new();

        public string? ProofOfWorkImageUrls { get; set; }
    }

    public class InvoiceDto
    {
        public int Id { get; set; }
        public int ProposalId { get; set; }
        public string ContractorId { get; set; } = string.Empty;
        public string ContractorName { get; set; } = string.Empty;
        public decimal TotalAmount { get; set; }
        public string Status { get; set; } = string.Empty;
        public bool DeviationFlagged { get; set; }
        public double DeviationPercentage { get; set; }
        public string? RejectionReason { get; set; }
        public DateTime SubmittedAt { get; set; }
        public DateTime? ApprovedAt { get; set; }
        public decimal OriginalProposalCost { get; set; }
        public string? ProofOfWorkImageUrls { get; set; }
        public List<string> ProofOfWorkImagesList => 
            string.IsNullOrEmpty(ProofOfWorkImageUrls) 
                ? new List<string>() 
                : ProofOfWorkImageUrls.Split(';', StringSplitOptions.RemoveEmptyEntries).ToList();

        public List<LineItemDto> LineItems { get; set; } = new();
    }

    public class RejectInvoiceDto
    {
        [Required]
        [MaxLength(1000)]
        public string Reason { get; set; } = string.Empty;
    }

    // ========================
    // PAYMENT DTOs
    // ========================

    public class PaymentDto
    {
        public int Id { get; set; }
        public int InvoiceId { get; set; }
        public decimal Amount { get; set; }
        public string Status { get; set; } = string.Empty;
        public bool CitizenAcknowledged { get; set; }
        public bool AdminApproved { get; set; }
        public DateTime? DisbursedAt { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    // ========================
    // RATING DTOs
    // ========================

    public class CreateRatingDto
    {
        [Required]
        [Range(1, 5)]
        public int Rating { get; set; }

        [MaxLength(1000)]
        public string? Comment { get; set; }
    }

    public class RatingDto
    {
        public int Id { get; set; }
        public int IncidentId { get; set; }
        public string ContractorId { get; set; } = string.Empty;
        public string ContractorName { get; set; } = string.Empty;
        public string CitizenId { get; set; } = string.Empty;
        public string CitizenName { get; set; } = string.Empty;
        public int Rating { get; set; }
        public string? Comment { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    // ========================
    // TENDER EVALUATION DTOs
    // ========================

    public class ContractorRankingDto
    {
        public int ProposalId { get; set; }
        public string ContractorId { get; set; } = string.Empty;
        public string ContractorName { get; set; } = string.Empty;
        public string? CompanyName { get; set; }
        public decimal TotalCost { get; set; }
        public int EstimatedDays { get; set; }
        public double AverageRating { get; set; }
        public int CompletedJobs { get; set; }
        public double RatingScore { get; set; }
        public double CostScore { get; set; }
        public double PerformanceScore { get; set; }
        public double TimelineScore { get; set; }
        public double FinalScore { get; set; }
        public int Rank { get; set; }
    }

    public class AssignContractorDto
    {
        [Required]
        public int ProposalId { get; set; }
    }

    // ========================
    // AUDIT DTOs
    // ========================

    public class AuditLogDto
    {
        public int Id { get; set; }
        public string? UserId { get; set; }
        public string? UserName { get; set; }
        public string Action { get; set; } = string.Empty;
        public string Entity { get; set; } = string.Empty;
        public int EntityId { get; set; }
        public string? OldValue { get; set; }
        public string? NewValue { get; set; }
        public DateTime Timestamp { get; set; }
        public string? IpAddress { get; set; }
    }

    // ========================
    // ========================
    // CONTRACTOR DTOs
    // ========================

    public class ContractorSummaryDto
    {
        public string Id { get; set; }
        public string FullName { get; set; }
        public string? CompanyName { get; set; }
        public string? Email { get; set; }
        public double AverageRating { get; set; }
        public int CompletedJobs { get; set; }
        public int ProposalsSubmitted { get; set; }
        public double AverageProposalCost { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class ContractorDetailDto
    {
        public string Id { get; set; }
        public string FullName { get; set; }
        public string? CompanyName { get; set; }
        public string? Email { get; set; }
        public string? PhoneNumber { get; set; }
        public double AverageRating { get; set; }
        public int CompletedJobs { get; set; }
        public int LateCompletions { get; set; }
        public int RatingsCount { get; set; }
        public DateTime CreatedAt { get; set; }
        public List<ContractorProposalDto> Proposals { get; set; } = new();
        public List<ContractorInvoiceDto> Invoices { get; set; } = new();
    }

    public class ContractorProposalDto
    {
        public int Id { get; set; }
        public int IncidentId { get; set; }
        public string? IncidentTitle { get; set; }
        public string? IncidentCategory { get; set; }
        public decimal TotalCost { get; set; }
        public int EstimatedDays { get; set; }
        public string Status { get; set; }
        public decimal Score { get; set; }
        public DateTime SubmittedAt { get; set; }
    }

    public class ContractorInvoiceDto
    {
        public int Id { get; set; }
        public int ProposalId { get; set; }
        public string? IncidentTitle { get; set; }
        public decimal TotalAmount { get; set; }
        public string Status { get; set; }
        public bool DeviationFlagged { get; set; }
        public decimal DeviationPercentage { get; set; }
        public DateTime? SubmittedAt { get; set; }
    }

    public class ContractorPaymentDto
    {
        public int Id { get; set; }
        public int InvoiceId { get; set; }
        public int IncidentId { get; set; }
        public string? IncidentTitle { get; set; }
        public decimal Amount { get; set; }
        public string Status { get; set; }
        public bool CitizenAcknowledged { get; set; }
        public bool AdminApproved { get; set; }
        public DateTime? DisbursedAt { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    // ========================
    // GENERIC API RESPONSE
    // ========================

    public class ApiResponse<T>
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public T? Data { get; set; }
        public List<string>? Errors { get; set; }

        public static ApiResponse<T> SuccessResponse(T data, string message = "Success")
        {
            return new ApiResponse<T> { Success = true, Message = message, Data = data };
        }

        public static ApiResponse<T> ErrorResponse(string message, List<string>? errors = null)
        {
            return new ApiResponse<T> { Success = false, Message = message, Errors = errors };
        }
    }
}
