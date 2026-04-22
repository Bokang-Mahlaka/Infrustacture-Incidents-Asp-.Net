using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MesaMohloane.API.Models
{
    /// <summary>
    /// Payment record linked to an approved Invoice.
    /// Status Guard: Can only be Disbursed if Admin approved AND Citizen acknowledged.
    /// </summary>
    public class Payment
    {
        public int Id { get; set; }

        public int InvoiceId { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal Amount { get; set; }

        public PaymentStatus Status { get; set; } = PaymentStatus.Pending;

        /// <summary>
        /// Has the Citizen acknowledged that the work is complete?
        /// </summary>
        public bool CitizenAcknowledged { get; set; } = false;

        /// <summary>
        /// Has the Admin approved the payment for disbursement?
        /// </summary>
        public bool AdminApproved { get; set; } = false;

        public DateTime? DisbursedAt { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation
        [ForeignKey(nameof(InvoiceId))]
        public Invoice Invoice { get; set; } = null!;
    }
}
