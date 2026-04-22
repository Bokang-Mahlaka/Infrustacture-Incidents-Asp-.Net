using MesaMohloane.API.Models;
using MesaMohloane.API.Models.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace MesaMohloane.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly IConfiguration _configuration;

        public AuthController(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            IConfiguration configuration)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _configuration = configuration;
        }

        /// <summary>
        /// Register a new user with a specific role (Citizen, Contractor, Admin, Auditor).
        /// </summary>
        [HttpPost("register")]
        public async Task<ActionResult<ApiResponse<AuthResponseDto>>> Register([FromBody] RegisterDto dto)
        {
            // Validate role
            var validRoles = new[] { "Citizen", "Contractor", "Admin", "Auditor" };
            if (!validRoles.Contains(dto.Role))
            {
                return BadRequest(ApiResponse<AuthResponseDto>.ErrorResponse(
                    "Invalid role. Must be one of: Citizen, Contractor, Admin, Auditor."));
            }

            // Contractors must provide a company name
            if (dto.Role == "Contractor" && string.IsNullOrWhiteSpace(dto.CompanyName))
            {
                return BadRequest(ApiResponse<AuthResponseDto>.ErrorResponse(
                    "Company name is required for Contractor registration."));
            }

            var user = new ApplicationUser
            {
                UserName = dto.Email,
                Email = dto.Email,
                FullName = dto.FullName,
                PhoneNumber = dto.PhoneNumber,
                CompanyName = dto.Role == "Contractor" ? dto.CompanyName : null
            };

            var result = await _userManager.CreateAsync(user, dto.Password);

            if (!result.Succeeded)
            {
                var errors = result.Errors.Select(e => e.Description).ToList();
                return BadRequest(ApiResponse<AuthResponseDto>.ErrorResponse("Registration failed.", errors));
            }

            await _userManager.AddToRoleAsync(user, dto.Role);

            var token = await GenerateJwtToken(user);

            var response = new AuthResponseDto
            {
                Token = token.Token,
                UserId = user.Id,
                FullName = user.FullName,
                Email = user.Email!,
                Role = dto.Role,
                Expiration = token.Expiration
            };

            return Ok(ApiResponse<AuthResponseDto>.SuccessResponse(response, "Registration successful."));
        }

        /// <summary>
        /// Login with email and password, returns JWT token.
        /// </summary>
        [HttpPost("login")]
        public async Task<ActionResult<ApiResponse<AuthResponseDto>>> Login([FromBody] LoginDto dto)
        {
            var user = await _userManager.FindByEmailAsync(dto.Email);
            if (user == null)
            {
                return Unauthorized(ApiResponse<AuthResponseDto>.ErrorResponse("Invalid email or password."));
            }

            var result = await _signInManager.CheckPasswordSignInAsync(user, dto.Password, false);
            if (!result.Succeeded)
            {
                return Unauthorized(ApiResponse<AuthResponseDto>.ErrorResponse("Invalid email or password."));
            }

            var roles = await _userManager.GetRolesAsync(user);
            var token = await GenerateJwtToken(user);

            var response = new AuthResponseDto
            {
                Token = token.Token,
                UserId = user.Id,
                FullName = user.FullName,
                Email = user.Email!,
                Role = roles.FirstOrDefault() ?? "Unknown",
                Expiration = token.Expiration
            };

            return Ok(ApiResponse<AuthResponseDto>.SuccessResponse(response, "Login successful."));
        }

        /// <summary>
        /// Get the current authenticated user's profile.
        /// </summary>
        [HttpGet("profile")]
        [Authorize]
        public async Task<ActionResult<ApiResponse<UserProfileDto>>> GetProfile()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var user = await _userManager.FindByIdAsync(userId!);

            if (user == null)
                return NotFound(ApiResponse<UserProfileDto>.ErrorResponse("User not found."));

            var roles = await _userManager.GetRolesAsync(user);

            var profile = new UserProfileDto
            {
                Id = user.Id,
                FullName = user.FullName,
                Email = user.Email!,
                Role = roles.FirstOrDefault() ?? "Unknown",
                PhoneNumber = user.PhoneNumber,
                CompanyName = user.CompanyName,
                AverageRating = user.AverageRating,
                CompletedJobs = user.CompletedJobs,
                LateCompletions = user.LateCompletions
            };

            return Ok(ApiResponse<UserProfileDto>.SuccessResponse(profile));
        }

        private async Task<(string Token, DateTime Expiration)> GenerateJwtToken(ApplicationUser user)
        {
            var roles = await _userManager.GetRolesAsync(user);
            var jwtSettings = _configuration.GetSection("JwtSettings");
            var secretKey = jwtSettings["SecretKey"] ?? "MesaMohloaneSuperSecretKey2024LesothoInfra!@#$";
            var expirationHours = int.Parse(jwtSettings["ExpirationInHours"] ?? "24");

            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, user.Id),
                new(ClaimTypes.Email, user.Email!),
                new(ClaimTypes.Name, user.FullName),
                new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            };

            foreach (var role in roles)
            {
                claims.Add(new Claim(ClaimTypes.Role, role));
            }

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
            var expiration = DateTime.UtcNow.AddHours(expirationHours);

            var token = new JwtSecurityToken(
                issuer: jwtSettings["Issuer"] ?? "MesaMohloane.API",
                audience: jwtSettings["Audience"] ?? "MesaMohloane.Client",
                claims: claims,
                expires: expiration,
                signingCredentials: creds
            );

            return (new JwtSecurityTokenHandler().WriteToken(token), expiration);
        }
    }
}
