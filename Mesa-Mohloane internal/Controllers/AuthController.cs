using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Mesa_Mohloane_internal.Models;
using Mesa_Mohloane_internal.Services;

namespace Mesa_Mohloane_internal.Controllers
{
    public class AuthController : Controller
    {
        private readonly IApiClient _apiClient;

        public AuthController(IApiClient apiClient)
        {
            _apiClient = apiClient;
        }

        [HttpGet]
        public IActionResult Login()
        {
            if (User.Identity != null && User.Identity.IsAuthenticated)
            {
                return RedirectToDashboard();
            }
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            var response = await _apiClient.PostAsync<LoginViewModel, ApiResponse<AuthResponse>>("/api/Auth/login", model);

            if (response != null && response.Success && response.Data != null)
            {
                await SignInUser(response.Data.Token);
                return RedirectToDashboard();
            }

            ModelState.AddModelError(string.Empty, response?.Message ?? "Invalid login attempt.");
            return View(model);
        }

        [HttpGet]
        public IActionResult Register()
        {
            if (User.Identity != null && User.Identity.IsAuthenticated)
            {
                return RedirectToDashboard();
            }
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            // Using the same endpoint structure as API
            var response = await _apiClient.PostAsync<RegisterViewModel, ApiResponse<AuthResponse>>("/api/Auth/register", model);

            if (response != null && response.Success)
            {
                TempData["SuccessMessage"] = "Registration successful. Please log in.";
                return RedirectToAction("Login");
            }

            ModelState.AddModelError(string.Empty, response?.Message ?? "Registration failed.");
            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            Response.Cookies.Delete("JwtToken");
            return RedirectToAction("Index", "Home");
        }

        private async Task SignInUser(string token)
        {
            var handler = new JwtSecurityTokenHandler();
            var jwtToken = handler.ReadJwtToken(token);

            // Extract claims from JWT
            var claims = jwtToken.Claims.ToList();

            var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var authProperties = new AuthenticationProperties
            {
                IsPersistent = true,
                ExpiresUtc = jwtToken.ValidTo
            };

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(claimsIdentity),
                authProperties);

            // Store token in cookie for the ApiClient to use in future requests
            Response.Cookies.Append("JwtToken", token, new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Strict,
                Expires = jwtToken.ValidTo
            });
        }

        private IActionResult RedirectToDashboard()
        {
            if (User.IsInRole("Admin")) return RedirectToAction("Dashboard", "Admin");
            if (User.IsInRole("Contractor")) return RedirectToAction("Dashboard", "Contractor");
            if (User.IsInRole("Citizen")) return RedirectToAction("Dashboard", "Citizen");
            if (User.IsInRole("Auditor")) return RedirectToAction("Dashboard", "Auditor");
            
            return RedirectToAction("Index", "Home");
        }
    }
}
