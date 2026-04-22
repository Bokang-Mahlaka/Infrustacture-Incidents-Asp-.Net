using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using MesaMohloane.API.Models;

namespace MesaMohloane.API.Data
{
    /// <summary>
    /// Application database context extending IdentityDbContext for ASP.NET Identity support.
    /// </summary>
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<Incident> Incidents { get; set; }
        public DbSet<Proposal> Proposals { get; set; }
        public DbSet<ProposalLineItem> ProposalLineItems { get; set; }
        public DbSet<Invoice> Invoices { get; set; }
        public DbSet<InvoiceLineItem> InvoiceLineItems { get; set; }
        public DbSet<Payment> Payments { get; set; }
        public DbSet<ContractorRating> ContractorRatings { get; set; }
        public DbSet<AuditLog> AuditLogs { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // ========================
            // INCIDENT Configuration
            // ========================
            builder.Entity<Incident>(entity =>
            {
                entity.HasIndex(i => i.Status);
                entity.HasIndex(i => i.Category);
                entity.HasIndex(i => i.CreatedAt);

                entity.HasOne(i => i.Citizen)
                    .WithMany(u => u.ReportedIncidents)
                    .HasForeignKey(i => i.CitizenId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(i => i.AssignedContractor)
                    .WithMany()
                    .HasForeignKey(i => i.AssignedContractorId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // ========================
            // PROPOSAL Configuration
            // ========================
            builder.Entity<Proposal>(entity =>
            {
                entity.HasIndex(p => new { p.IncidentId, p.ContractorId }).IsUnique();

                entity.HasOne(p => p.Incident)
                    .WithMany(i => i.Proposals)
                    .HasForeignKey(p => p.IncidentId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(p => p.Contractor)
                    .WithMany(u => u.Proposals)
                    .HasForeignKey(p => p.ContractorId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // ========================
            // PROPOSAL LINE ITEM Configuration
            // ========================
            builder.Entity<ProposalLineItem>(entity =>
            {
                entity.HasOne(li => li.Proposal)
                    .WithMany(p => p.LineItems)
                    .HasForeignKey(li => li.ProposalId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // ========================
            // INVOICE Configuration
            // ========================
            builder.Entity<Invoice>(entity =>
            {
                entity.HasIndex(i => i.ProposalId).IsUnique();

                entity.HasOne(inv => inv.Proposal)
                    .WithOne(p => p.Invoice)
                    .HasForeignKey<Invoice>(inv => inv.ProposalId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(inv => inv.Contractor)
                    .WithMany()
                    .HasForeignKey(inv => inv.ContractorId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // ========================
            // INVOICE LINE ITEM Configuration
            // ========================
            builder.Entity<InvoiceLineItem>(entity =>
            {
                entity.HasOne(li => li.Invoice)
                    .WithMany(inv => inv.LineItems)
                    .HasForeignKey(li => li.InvoiceId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // ========================
            // PAYMENT Configuration
            // ========================
            builder.Entity<Payment>(entity =>
            {
                entity.HasIndex(p => p.InvoiceId).IsUnique();

                entity.HasOne(p => p.Invoice)
                    .WithOne(inv => inv.Payment)
                    .HasForeignKey<Payment>(p => p.InvoiceId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // ========================
            // CONTRACTOR RATING Configuration
            // ========================
            builder.Entity<ContractorRating>(entity =>
            {
                // One rating per incident (one citizen rates one contractor per job)
                entity.HasIndex(r => r.IncidentId).IsUnique();

                entity.HasOne(r => r.Incident)
                    .WithOne(i => i.Rating)
                    .HasForeignKey<ContractorRating>(r => r.IncidentId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(r => r.Contractor)
                    .WithMany(u => u.ReceivedRatings)
                    .HasForeignKey(r => r.ContractorId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(r => r.Citizen)
                    .WithMany(u => u.GivenRatings)
                    .HasForeignKey(r => r.CitizenId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // ========================
            // AUDIT LOG Configuration
            // ========================
            builder.Entity<AuditLog>(entity =>
            {
                entity.HasIndex(a => a.Timestamp);
                entity.HasIndex(a => a.Entity);
                entity.HasIndex(a => a.UserId);

                entity.HasOne(a => a.User)
                    .WithMany()
                    .HasForeignKey(a => a.UserId)
                    .OnDelete(DeleteBehavior.SetNull);
            });
        }
    }
}
