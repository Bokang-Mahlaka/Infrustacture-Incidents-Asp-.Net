using MesaMohloane.API.Models;

namespace MesaMohloane.API.Services.InvoiceValidation
{
    public interface IInvoiceValidationService
    {
        /// <summary>
        /// Validates an invoice against business rules:
        /// 1. Integrity Check: TotalAmount == Sum(LineItems)
        /// 2. Reference Check: Linked proposal must be Accepted
        /// 3. Deviation Check: Flag if > 10% deviation from proposal cost
        /// </summary>
        InvoiceValidationResult Validate(Invoice invoice, Proposal proposal);

        /// <summary>
        /// Status Guard: Payment can only be disbursed if Admin approved AND Citizen acknowledged.
        /// </summary>
        bool CanDisbursePayment(Payment payment);
    }

    public class InvoiceValidationResult
    {
        public bool IsValid { get; set; }
        public bool DeviationFlagged { get; set; }
        public double DeviationPercentage { get; set; }
        public List<string> Errors { get; set; } = new();
    }

    public class InvoiceValidationService : IInvoiceValidationService
    {
        public InvoiceValidationResult Validate(Invoice invoice, Proposal proposal)
        {
            var result = new InvoiceValidationResult { IsValid = true };

            // ========================
            // 1. INTEGRITY CHECK
            // TotalAmount must equal the sum of line items
            // ========================
            var lineItemSum = invoice.LineItems.Sum(li => li.LineTotal);
            if (invoice.TotalAmount != lineItemSum)
            {
                result.IsValid = false;
                result.Errors.Add(
                    $"Integrity Check Failed: Total amount (M{invoice.TotalAmount:N2}) " +
                    $"does not match sum of line items (M{lineItemSum:N2}).");
            }

            // ========================
            // 2. REFERENCE CHECK
            // Invoice must link to an Accepted proposal
            // ========================
            if (proposal.Status != ProposalStatus.Accepted)
            {
                result.IsValid = false;
                result.Errors.Add(
                    $"Reference Check Failed: Linked proposal (#{proposal.Id}) is not in 'Accepted' status. " +
                    $"Current status: {proposal.Status}.");
            }

            // ========================
            // 3. DEVIATION CHECK
            // Flag if deviation > 10% from original proposal cost
            // ========================
            if (proposal.TotalCost > 0)
            {
                var deviation = (double)Math.Abs(invoice.TotalAmount - proposal.TotalCost) / (double)proposal.TotalCost * 100;
                result.DeviationPercentage = Math.Round(deviation, 2);

                if (deviation > 10)
                {
                    result.DeviationFlagged = true;
                    result.Errors.Add(
                        $"Deviation Alert: Invoice deviates by {deviation:F2}% from the original proposal cost " +
                        $"(M{proposal.TotalCost:N2} → M{invoice.TotalAmount:N2}). Flagged for Auditor review.");
                }
            }

            return result;
        }

        /// <summary>
        /// Status Guard: Payment can only be Disbursed if:
        /// - Admin has approved (AdminApproved == true)
        /// - Citizen has acknowledged work completion (CitizenAcknowledged == true)
        /// </summary>
        public bool CanDisbursePayment(Payment payment)
        {
            return payment.AdminApproved && payment.CitizenAcknowledged;
        }
    }
}
