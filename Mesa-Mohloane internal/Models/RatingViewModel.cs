using System.ComponentModel.DataAnnotations;

namespace Mesa_Mohloane_internal.Models
{
    public class CreateRatingViewModel
    {
        public int IncidentId { get; set; }

        [Required]
        [Range(1, 5, ErrorMessage = "Rating must be between 1 and 5")]
        public int Rating { get; set; }

        public string? Comment { get; set; }
    }

    public class ProjectSignOffViewModel
    {
        public int IncidentId { get; set; }
        public string? IncidentTitle { get; set; }

        [Required(ErrorMessage = "Please provide a workmanship rating")]
        [Range(1, 5, ErrorMessage = "Rating must be between 1 and 5")]
        public int Rating { get; set; } = 5;

        [Required(ErrorMessage = "Please provide brief feedback on the repair quality")]
        public string Comment { get; set; } = string.Empty;

        [Range(typeof(bool), "true", "true", ErrorMessage = "You must certify the work before sign-off")]
        public bool Certified { get; set; }
    }
}
