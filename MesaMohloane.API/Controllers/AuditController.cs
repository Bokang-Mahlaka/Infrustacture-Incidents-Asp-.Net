using MesaMohloane.API.Data;
using MesaMohloane.API.Models.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MesaMohloane.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "Auditor,Admin")]
    public class AuditController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public AuditController(ApplicationDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Get all audit logs with optional filtering by entity, user, and date range.
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<ApiResponse<List<AuditLogDto>>>> GetAll(
            [FromQuery] string? entity = null,
            [FromQuery] string? userId = null,
            [FromQuery] DateTime? from = null,
            [FromQuery] DateTime? to = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 50)
        {
            var query = _context.AuditLogs
                .Include(a => a.User)
                .AsQueryable();

            if (!string.IsNullOrEmpty(entity))
                query = query.Where(a => a.Entity == entity);

            if (!string.IsNullOrEmpty(userId))
                query = query.Where(a => a.UserId == userId);

            if (from.HasValue)
                query = query.Where(a => a.Timestamp >= from.Value);

            if (to.HasValue)
                query = query.Where(a => a.Timestamp <= to.Value);

            var total = await query.CountAsync();

            var logs = await query
                .OrderByDescending(a => a.Timestamp)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var dtos = logs.Select(a => new AuditLogDto
            {
                Id = a.Id,
                UserId = a.UserId,
                UserName = a.User?.FullName,
                Action = a.Action,
                Entity = a.Entity,
                EntityId = a.EntityId,
                OldValue = a.OldValue,
                NewValue = a.NewValue,
                Timestamp = a.Timestamp,
                IpAddress = a.IpAddress
            }).ToList();

            return Ok(ApiResponse<List<AuditLogDto>>.SuccessResponse(dtos,
                $"Page {page} of audit logs. Total: {total} entries."));
        }

        /// <summary>
        /// Get audit logs for a specific entity and entity ID.
        /// </summary>
        [HttpGet("{entity}/{entityId}")]
        public async Task<ActionResult<ApiResponse<List<AuditLogDto>>>> GetByEntity(string entity, int entityId)
        {
            var logs = await _context.AuditLogs
                .Include(a => a.User)
                .Where(a => a.Entity == entity && a.EntityId == entityId)
                .OrderByDescending(a => a.Timestamp)
                .ToListAsync();

            var dtos = logs.Select(a => new AuditLogDto
            {
                Id = a.Id,
                UserId = a.UserId,
                UserName = a.User?.FullName,
                Action = a.Action,
                Entity = a.Entity,
                EntityId = a.EntityId,
                OldValue = a.OldValue,
                NewValue = a.NewValue,
                Timestamp = a.Timestamp,
                IpAddress = a.IpAddress
            }).ToList();

            return Ok(ApiResponse<List<AuditLogDto>>.SuccessResponse(dtos));
        }
    }
}
