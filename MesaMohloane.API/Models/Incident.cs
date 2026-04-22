using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MesaMohloane.API.Models
{
    /// <summary>
    /// An infrastructure issue reported by a Citizen.
    /// Lifecycle: Reported → Verified → Published → Assigned → InProgress → Completed → Closed
    /// </summary>
    public class Incident
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(200)]
        public string Title { get; set; } = string.Empty;

        [Required]
        [MaxLength(2000)]
        public string Description { get; set; } = string.Empty;

        public IncidentCategory Category { get; set; }

        [Required]
        [MaxLength(300)]
        public string Location { get; set; } = string.Empty;

        public double Latitude { get; set; }
        public double Longitude { get; set; }

        [MaxLength(500)]
        public string? PhotoUrl { get; set; }

        public IncidentStatus Status { get; set; } = IncidentStatus.Reported;

        // Foreign Keys
        [Required]
        public string CitizenId { get; set; } = string.Empty;

        public string? AssignedContractorId { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // Navigation properties
        [ForeignKey(nameof(CitizenId))]
        public ApplicationUser Citizen { get; set; } = null!;

        [ForeignKey(nameof(AssignedContractorId))]
        public ApplicationUser? AssignedContractor { get; set; }

        public ICollection<Proposal> Proposals { get; set; } = new List<Proposal>();
        public ContractorRating? Rating { get; set; }
    }
}
