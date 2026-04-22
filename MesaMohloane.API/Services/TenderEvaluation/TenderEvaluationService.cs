using MesaMohloane.API.Data;
using MesaMohloane.API.Models;
using MesaMohloane.API.Models.DTOs;
using Microsoft.EntityFrameworkCore;

namespace MesaMohloane.API.Services.TenderEvaluation
{
    public interface ITenderEvaluationService
    {
        /// <summary>
        /// Evaluates and ranks all proposals for a given incident using the weighted scoring algorithm.
        /// S = (W1 × RatingScore) + (W2 × CostScore) + (W3 × PerformanceScore) + (W4 × TimelineScore)
        /// </summary>
        Task<List<ContractorRankingDto>> EvaluateProposalsAsync(int incidentId);
    }

    public class TenderEvaluationService : ITenderEvaluationService
    {
        private readonly ApplicationDbContext _context;

        // Weighting factors for the scoring algorithm
        private const double W1_Rating = 0.30;       // Contractor average rating
        private const double W2_Cost = 0.30;          // Cost efficiency
        private const double W3_Performance = 0.25;   // Past performance (on-time completion)
        private const double W4_Timeline = 0.15;      // Estimated timeline

        public TenderEvaluationService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<List<ContractorRankingDto>> EvaluateProposalsAsync(int incidentId)
        {
            // Get all submitted proposals for this incident with contractor info
            var proposals = await _context.Proposals
                .Include(p => p.Contractor)
                .Include(p => p.LineItems)
                .Where(p => p.IncidentId == incidentId && p.Status == ProposalStatus.Submitted)
                .ToListAsync();

            if (!proposals.Any())
                return new List<ContractorRankingDto>();

            // Determine min/max values for normalization
            var costs = proposals.Select(p => p.TotalCost).ToList();
            var minCost = costs.Min();
            var maxCost = costs.Max();

            var days = proposals.Select(p => p.EstimatedDays).ToList();
            var minDays = days.Min();
            var maxDays = days.Max();

            var rankings = new List<ContractorRankingDto>();

            foreach (var proposal in proposals)
            {
                var contractor = proposal.Contractor;

                // ========================
                // 1. RATING SCORE (0–100)
                // RatingScore = (AverageRating / 5.0) × 100
                // ========================
                double ratingScore = (contractor.AverageRating / 5.0) * 100;

                // ========================
                // 2. COST SCORE (0–100)
                // CostScore = (1 - (Cost - MinCost) / (MaxCost - MinCost)) × 100
                // If all costs are equal, score is 100
                // ========================
                double costScore;
                if (maxCost == minCost)
                {
                    costScore = 100;
                }
                else
                {
                    costScore = (1 - (double)(proposal.TotalCost - minCost) / (double)(maxCost - minCost)) * 100;
                }

                // ========================
                // 3. PERFORMANCE SCORE (0–100)
                // PerformanceScore = (CompletedOnTime / TotalCompleted) × 100
                // New contractors with no history get a neutral score of 50
                // ========================
                double performanceScore;
                if (contractor.CompletedJobs == 0)
                {
                    performanceScore = 50; // Neutral score for new contractors
                }
                else
                {
                    int completedOnTime = contractor.CompletedJobs - contractor.LateCompletions;
                    performanceScore = ((double)completedOnTime / contractor.CompletedJobs) * 100;
                }

                // ========================
                // 4. TIMELINE SCORE (0–100)
                // TimelineScore = (1 - (Days - MinDays) / (MaxDays - MinDays)) × 100
                // If all timelines are equal, score is 100
                // ========================
                double timelineScore;
                if (maxDays == minDays)
                {
                    timelineScore = 100;
                }
                else
                {
                    timelineScore = (1 - (double)(proposal.EstimatedDays - minDays) / (maxDays - minDays)) * 100;
                }

                // ========================
                // FINAL WEIGHTED SCORE
                // S = (W1 × Rating) + (W2 × Cost) + (W3 × Performance) + (W4 × Timeline)
                // ========================
                double finalScore = (W1_Rating * ratingScore)
                                  + (W2_Cost * costScore)
                                  + (W3_Performance * performanceScore)
                                  + (W4_Timeline * timelineScore);

                // Store score on the proposal entity
                proposal.Score = Math.Round(finalScore, 2);

                rankings.Add(new ContractorRankingDto
                {
                    ProposalId = proposal.Id,
                    ContractorId = contractor.Id,
                    ContractorName = contractor.FullName,
                    CompanyName = contractor.CompanyName,
                    TotalCost = proposal.TotalCost,
                    EstimatedDays = proposal.EstimatedDays,
                    AverageRating = contractor.AverageRating,
                    CompletedJobs = contractor.CompletedJobs,
                    RatingScore = Math.Round(ratingScore, 2),
                    CostScore = Math.Round(costScore, 2),
                    PerformanceScore = Math.Round(performanceScore, 2),
                    TimelineScore = Math.Round(timelineScore, 2),
                    FinalScore = Math.Round(finalScore, 2)
                });
            }

            // Rank by FinalScore descending
            rankings = rankings.OrderByDescending(r => r.FinalScore).ToList();

            // Assign ranks
            for (int i = 0; i < rankings.Count; i++)
            {
                rankings[i].Rank = i + 1;
            }

            // Persist scores to proposals
            await _context.SaveChangesAsync();

            return rankings;
        }
    }
}
