using System.ComponentModel.DataAnnotations;

namespace Mesa_Mohloane_internal.Models
{
    public enum LineItemCategory
    {
        Materials,
        Labor,
        Transport,
        Equipment,
        Other
    }
    public class CreateProposalViewModel
    {
        public int Id { get; set; }
        public int IncidentId { get; set; }

        [Required]
        [Range(1, 365, ErrorMessage = "Please provide realistic day estimation")]
        public int EstimatedDays { get; set; }

        [Required]
        [MaxLength(2000)]
        public string CoverLetter { get; set; } = string.Empty;

        public List<ProposalLineItemViewModel> LineItems { get; set; } = new();
    }

    public class ProposalLineItemViewModel
    {
        public int Category { get; set; } // Matches LineItemCategory enum
        public string Description { get; set; } = string.Empty;
        public int Quantity { get; set; } = 1;
        public decimal UnitPrice { get; set; }
    }

    public class ContractorRankingViewModel
    {
        public int ProposalId { get; set; }
        public string ContractorName { get; set; } = string.Empty;
        public string? CompanyName { get; set; }
        public decimal TotalCost { get; set; }
        public int EstimatedDays { get; set; }
        public double AverageRating { get; set; }
        public int CompletedJobs { get; set; }
        
        public double FinalScore { get; set; }
        public int Rank { get; set; }
    }

    public class ProposalViewModel
    {
        public int Id { get; set; }
        public int IncidentId { get; set; }
        public string IncidentTitle { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public decimal TotalCost { get; set; }
        public int EstimatedDays { get; set; }
        public string CoverLetter { get; set; } = string.Empty;
        public DateTime SubmittedAt { get; set; }
        public string? PaymentStatus { get; set; }
        public bool CitizenAcknowledged { get; set; }
        public List<LineItemViewModel> LineItems { get; set; } = new();
    }
}
