namespace MesaMohloane.API.Models
{
    public enum IncidentStatus
    {
        Reported,
        Verified,
        Published,
        Assigned,
        InProgress,
        Completed,
        Closed
    }

    public enum IncidentCategory
    {
        Road,
        Water,
        Electricity,
        Bridge,
        Building,
        Other
    }

    public enum ProposalStatus
    {
        Submitted,
        UnderReview,
        Accepted,
        Rejected
    }

    public enum InvoiceStatus
    {
        Submitted,
        Approved,
        Rejected,
        Flagged
    }

    public enum PaymentStatus
    {
        Pending,
        Disbursed,
        OnHold
    }

    public enum LineItemCategory
    {
        Materials,
        Labor,
        Transport,
        Equipment,
        Other
    }

    public enum RegistrationStatus
    {
        Pending,
        Approved,
        Rejected
    }
}
