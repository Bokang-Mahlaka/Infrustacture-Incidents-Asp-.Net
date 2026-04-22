using System.ComponentModel.DataAnnotations;

namespace Mesa_Mohloane_internal.Models
{
    public class IncidentViewModel
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string? PhotoUrl { get; set; }
        public string? CitizenName { get; set; }
        public DateTime CreatedAt { get; set; }
        public int ProposalCount { get; set; }
        public bool IsAcknowledged { get; set; }
    }

    public class CreateIncidentViewModel
    {
        [Required]
        public string Title { get; set; } = string.Empty;

        [Required]
        public string Description { get; set; } = string.Empty;

        [Required]
        public string Category { get; set; } = "Road";

        [Required]
        public string Location { get; set; } = string.Empty;

        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public IFormFile? Photo { get; set; }
    }
}
