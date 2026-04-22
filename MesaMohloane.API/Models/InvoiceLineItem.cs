using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MesaMohloane.API.Models
{
    /// <summary>
    /// A line item in the final Invoice's cost breakdown.
    /// </summary>
    public class InvoiceLineItem
    {
        public int Id { get; set; }

        public int InvoiceId { get; set; }

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
        [ForeignKey(nameof(InvoiceId))]
        public Invoice Invoice { get; set; } = null!;
    }
}
