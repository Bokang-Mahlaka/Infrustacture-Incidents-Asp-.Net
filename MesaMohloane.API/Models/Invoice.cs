using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MesaMohloane.API.Models
{
    /// <summary>
    /// Final invoice submitted by a Contractor after completing the work.
    /// Must be validated against the original Proposal before payment.
    /// </summary>
    public class Invoice
    {
        public int Id { get; set; }

        public int ProposalId { get; set; }

        [Required]
        public string ContractorId { get; set; } = string.Empty;

        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalAmount { get; set; }

        public InvoiceStatus Status { get; set; } = InvoiceStatus.Submitted;

        /// <summary>
        /// True if the deviation from the original proposal cost exceeds 10%.
        /// </summary>
        public bool DeviationFlagged { get; set; } = false;

        /// <summary>
        /// The percentage deviation from the original proposal cost.
        /// </summary>
        public double DeviationPercentage { get; set; } = 0;

        [MaxLength(1000)]
        public string? RejectionReason { get; set; }

        [MaxLength(2000)]
        public string? ProofOfWorkImageUrls { get; set; }

        public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;
        public DateTime? ApprovedAt { get; set; }

        // Navigation properties
        [ForeignKey(nameof(ProposalId))]
        public Proposal Proposal { get; set; } = null!;

        [ForeignKey(nameof(ContractorId))]
        public ApplicationUser Contractor { get; set; } = null!;

        public ICollection<InvoiceLineItem> LineItems { get; set; } = new List<InvoiceLineItem>();

        public Payment? Payment { get; set; }
    }
}
