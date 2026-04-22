using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;

namespace MesaMohloane.API.Models
{
    /// <summary>
    /// Extends the default IdentityUser with Mesa-Mohloane specific fields.
    /// Roles: Citizen, Contractor, Admin, Auditor
    /// </summary>
    public class ApplicationUser : IdentityUser
    {
        [Required]
        [MaxLength(100)]
        public string FullName { get; set; } = string.Empty;

        /// <summary>
        /// Company name — only relevant for Contractor role.
        /// </summary>
        [MaxLength(200)]
        public string? CompanyName { get; set; }

        /// <summary>
        /// Computed average rating from ContractorRatings (for Contractors).
        /// </summary>
        public double AverageRating { get; set; } = 0;

        /// <summary>
        /// Total number of jobs completed (for Contractors).
        /// </summary>
        public int CompletedJobs { get; set; } = 0;

        /// <summary>
        /// Number of jobs completed late (for Contractors, used in performance scoring).
        /// </summary>
        public int LateCompletions { get; set; } = 0;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation properties
        public ICollection<Incident> ReportedIncidents { get; set; } = new List<Incident>();
        public ICollection<Proposal> Proposals { get; set; } = new List<Proposal>();
        public ICollection<ContractorRating> ReceivedRatings { get; set; } = new List<ContractorRating>();
        public ICollection<ContractorRating> GivenRatings { get; set; } = new List<ContractorRating>();
    }
}
