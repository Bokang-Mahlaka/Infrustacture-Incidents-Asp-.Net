using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MesaMohloane.API.Models
{
    /// <summary>
    /// Audit trail entry capturing Who, What, and When for every key action.
    /// Visible to the Auditor role for transparency and accountability.
    /// </summary>
    public class AuditLog
    {
        public int Id { get; set; }

        /// <summary>
        /// The user who performed the action.
        /// </summary>
        public string? UserId { get; set; }

        /// <summary>
        /// What action was performed (e.g., "StatusChanged", "ProposalSubmitted", "InvoiceApproved").
        /// </summary>
        [Required]
        [MaxLength(100)]
        public string Action { get; set; } = string.Empty;

        /// <summary>
        /// The entity type affected (e.g., "Incident", "Proposal", "Invoice").
        /// </summary>
        [Required]
        [MaxLength(100)]
        public string Entity { get; set; } = string.Empty;

        /// <summary>
        /// The ID of the affected entity.
        /// </summary>
        public int EntityId { get; set; }

        /// <summary>
        /// JSON representation of the old value (before the change).
        /// </summary>
        public string? OldValue { get; set; }

        /// <summary>
        /// JSON representation of the new value (after the change).
        /// </summary>
        public string? NewValue { get; set; }

        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        [MaxLength(50)]
        public string? IpAddress { get; set; }

        // Navigation
        [ForeignKey(nameof(UserId))]
        public ApplicationUser? User { get; set; }
    }
}
