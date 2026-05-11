namespace Mesa_Mohloane_internal.Models
{
    public class ContractorSummaryViewModel
    {
        public string Id { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string? CompanyName { get; set; }
        public string? Email { get; set; }
        public double AverageRating { get; set; }
        public int CompletedJobs { get; set; }
        public int ProposalsSubmitted { get; set; }
        public double AverageProposalCost { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
