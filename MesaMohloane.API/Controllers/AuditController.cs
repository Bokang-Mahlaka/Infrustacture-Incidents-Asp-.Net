using MesaMohloane.API.Data;
using MesaMohloane.API.Models;
using MesaMohloane.API.Models.DTOs;
using MesaMohloane.API.Services.Auditing;
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
        private readonly IExportService _exportService;

        public AuditController(ApplicationDbContext context, IExportService exportService)
        {
            _context = context;
            _exportService = exportService;
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

        /// <summary>
        /// Export audit logs in CSV or HTML format.
        /// </summary>
        [HttpGet("export")]
        [Authorize(Roles = "Admin,Auditor")]
        public async Task<IActionResult> Export(
            [FromQuery] string format = "csv",
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null,
            [FromQuery] string? entity = null)
        {
            var query = _context.AuditLogs.AsQueryable();

            // Apply filters
            if (startDate.HasValue)
                query = query.Where(a => a.Timestamp >= startDate.Value);

            if (endDate.HasValue)
                query = query.Where(a => a.Timestamp <= endDate.Value);

            if (!string.IsNullOrEmpty(entity))
                query = query.Where(a => a.Entity == entity);

            var logs = await query.OrderByDescending(a => a.Timestamp).ToListAsync();

            if (!logs.Any())
                return BadRequest(new { message = "No audit logs found matching the criteria." });

            var stream = new MemoryStream();
            var fileName = $"audit-logs_{DateTime.UtcNow:yyyyMMdd_HHmmss}";

            if (format.ToLower() == "pdf")
            {
                await _exportService.ExportAuditLogsToPdfAsync(logs, stream);
                stream.Position = 0;
                return File(stream, "text/html", $"{fileName}.html");
            }
            else // CSV is default
            {
                await _exportService.ExportAuditLogsToCsvAsync(logs, stream);
                stream.Position = 0;
                return File(stream, "text/csv", $"{fileName}.csv");
            }
        }
    }
}
