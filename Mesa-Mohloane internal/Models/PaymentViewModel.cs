namespace Mesa_Mohloane_internal.Models
{
    public class PaymentViewModel
    {
        public int Id { get; set; }
        public int InvoiceId { get; set; }
        public decimal Amount { get; set; }
        public string Status { get; set; } = string.Empty; // Pending, Disbursed, OnHold
        public bool CitizenAcknowledged { get; set; }
        public bool AdminApproved { get; set; }
        public DateTime? DisbursedAt { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
