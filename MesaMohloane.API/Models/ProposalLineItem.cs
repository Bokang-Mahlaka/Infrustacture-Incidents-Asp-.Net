using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MesaMohloane.API.Models
{
    /// <summary>
    /// A line item in a Proposal's cost breakdown (Materials, Labor, Transport, etc.)
    /// </summary>
    public class ProposalLineItem
    {
        public int Id { get; set; }

        public int ProposalId { get; set; }

        public LineItemCategory Category { get; set; }

        [Required]
        [MaxLength(300)]
        public string Description { get; set; } = string.Empty;

        public int Quantity { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal UnitPrice { get; set; }

        /// <summary>
        /// Computed: Quantity * UnitPrice
        /// </summary>
        [Column(TypeName = "decimal(18,2)")]
        public decimal LineTotal { get; set; }

        // Navigation
        [ForeignKey(nameof(ProposalId))]
        public Proposal Proposal { get; set; } = null!;
    }
}
