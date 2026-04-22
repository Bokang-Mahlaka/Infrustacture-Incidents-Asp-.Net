using MesaMohloane.API.Data;
using MesaMohloane.API.Models;

namespace MesaMohloane.API.Services.Auditing
{
    public interface IAuditService
    {
        Task LogAsync(string? userId, string action, string entity, int entityId,
            string? oldValue = null, string? newValue = null, string? ipAddress = null);
    }

    public class AuditService : IAuditService
    {
        private readonly ApplicationDbContext _context;

        public AuditService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task LogAsync(string? userId, string action, string entity, int entityId,
            string? oldValue = null, string? newValue = null, string? ipAddress = null)
        {
            var auditLog = new AuditLog
            {
                UserId = userId,
                Action = action,
                Entity = entity,
                EntityId = entityId,
                OldValue = oldValue,
                NewValue = newValue,
                Timestamp = DateTime.UtcNow,
                IpAddress = ipAddress
            };

            _context.AuditLogs.Add(auditLog);
            await _context.SaveChangesAsync();
        }
    }
}
