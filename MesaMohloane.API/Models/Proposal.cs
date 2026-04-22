using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MesaMohloane.API.Models
{
    /// <summary>
    /// A structured Digital Tender submitted by a Contractor for an Incident.
    /// Contains a cover letter, estimated timeline, and line-item cost breakdown.
    /// </summary>
    public class Proposal
    {
        public int Id { get; set; }

        public int IncidentId { get; set; }

        [Required]
        public string ContractorId { get; set; } = string.Empty;

        [MaxLength(3000)]
        public string CoverLetter { get; set; } = string.Empty;

        /// <summary>
        /// Estimated number of days to complete the work.
        /// </summary>
        public int EstimatedDays { get; set; }

        /// <summary>
        /// Total cost computed from the sum of ProposalLineItems.
        /// </summary>
        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalCost { get; set; }

        public ProposalStatus Status { get; set; } = ProposalStatus.Submitted;

        /// <summary>
        /// Score computed by the Smart Tender Evaluation Algorithm.
        /// </summary>
        public double Score { get; set; } = 0;

        public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;

        // Navigation properties
        [ForeignKey(nameof(IncidentId))]
        public Incident Incident { get; set; } = null!;

        [ForeignKey(nameof(ContractorId))]
        public ApplicationUser Contractor { get; set; } = null!;

        public ICollection<ProposalLineItem> LineItems { get; set; } = new List<ProposalLineItem>();

        public Invoice? Invoice { get; set; }
    }
}
