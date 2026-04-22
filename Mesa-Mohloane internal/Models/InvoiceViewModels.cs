using System.ComponentModel.DataAnnotations;

namespace Mesa_Mohloane_internal.Models
{
    public class InvoiceViewModel
    {
        public int Id { get; set; }
        public int ProposalId { get; set; }
        public string ContractorId { get; set; } = string.Empty;
        public string ContractorName { get; set; } = string.Empty;
        public decimal TotalAmount { get; set; }
        public string Status { get; set; } = string.Empty; // Pending, Approved, Rejected, Flagged
        public bool DeviationFlagged { get; set; }
        public double DeviationPercentage { get; set; }
        public string? RejectionReason { get; set; }
        public DateTime SubmittedAt { get; set; }
        public decimal OriginalProposalCost { get; set; }
        
        // Payment tracking
        public int? PaymentId { get; set; }
        public bool CitizenAcknowledged { get; set; }
        public string? ActualPaymentStatus { get; set; } // Pending, Disbursed, etc.

        public List<LineItemViewModel> LineItems { get; set; } = new();
    }

    public class CreateInvoiceViewModel
    {
        public int ProposalId { get; set; }
        
        [Required]
        [Range(0.01, double.MaxValue, ErrorMessage = "Total cost must be greater than zero")]
        public decimal TotalAmount { get; set; }

        public string Description { get; set; } = "Final repair and materials";
    }

    public class LineItemViewModel
    {
        public int Id { get; set; }
        public string Category { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal LineTotal { get; set; }
    }

    public class RejectInvoiceViewModel
    {
        public int InvoiceId { get; set; }
        [Required]
        [MaxLength(1000)]
        public string Reason { get; set; } = string.Empty;
    }
}
