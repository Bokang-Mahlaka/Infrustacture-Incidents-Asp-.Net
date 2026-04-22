using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MesaMohloane.API.Models
{
    /// <summary>
    /// Rating given by a Citizen to a Contractor after job completion.
    /// </summary>
    public class ContractorRating
    {
        public int Id { get; set; }

        public int IncidentId { get; set; }

        [Required]
        public string ContractorId { get; set; } = string.Empty;

        [Required]
        public string CitizenId { get; set; } = string.Empty;

        /// <summary>
        /// Rating from 1 to 5.
        /// </summary>
        [Range(1, 5)]
        public int Rating { get; set; }

        [MaxLength(1000)]
        public string? Comment { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation properties
        [ForeignKey(nameof(IncidentId))]
        public Incident Incident { get; set; } = null!;

        [ForeignKey(nameof(ContractorId))]
        public ApplicationUser Contractor { get; set; } = null!;

        [ForeignKey(nameof(CitizenId))]
        public ApplicationUser Citizen { get; set; } = null!;
    }
}
