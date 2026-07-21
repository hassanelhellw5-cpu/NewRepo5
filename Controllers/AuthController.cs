using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using QemmaProject.Models.Payment;
using QemmaProject.Models.Requests;

namespace QemmaProject.Controllers
{
    [Route("api/auth")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly IConfiguration _configuration;

        public AuthController(UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager, IConfiguration configuration)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _configuration = configuration;
        }

        [HttpPost("signup")]
        public async Task<IActionResult> SignUp([FromBody] RegisterRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Email)) return BadRequest(new { message = "Email is required." });
            if (string.IsNullOrWhiteSpace(request.Password)) return BadRequest(new { message = "Password is required." });
            if (request.Password.Length < 6) return BadRequest(new { message = "Password must be at least 6 characters." });

            var normalizedEmail = request.Email.Trim();
            var existing = await _userManager.FindByEmailAsync(normalizedEmail);
            if (existing != null) return Conflict(new { message = "Email is already registered." });

            var user = new ApplicationUser
            {
                UserName = string.IsNullOrWhiteSpace(request.UserName) ? normalizedEmail : request.UserName.Trim(),
                Email = normalizedEmail,
                PhoneNumber = request.PhoneNumber?.Trim(),
                QemmaCoinsBalance = 0m
            };

            var result = await _userManager.CreateAsync(user, request.Password);
            if (!result.Succeeded) return BadRequest(new { message = "Registration failed.", errors = result.Errors.Select(e => e.Description) });

            await EnsureRoleExistsAsync("User");
            await _userManager.AddToRoleAsync(user, "User");

            var response = await BuildAuthResponseAsync(user);
            return Ok(response);
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.EmailOrUserName)) return BadRequest(new { message = "EmailOrUserName is required." });
            if (string.IsNullOrWhiteSpace(request.Password)) return BadRequest(new { message = "Password is required." });

            var identifier = request.EmailOrUserName.Trim();
            var user = await _userManager.FindByEmailAsync(identifier) ?? await _userManager.FindByNameAsync(identifier);
            if (user == null) return Unauthorized(new { message = "Invalid email/username or password." });

            var passwordValid = await _userManager.CheckPasswordAsync(user, request.Password);
            if (!passwordValid) return Unauthorized(new { message = "Invalid email/username or password." });

            var response = await BuildAuthResponseAsync(user);
            return Ok(response);
        }

        [HttpPost("admin/login")]
        public async Task<IActionResult> AdminLogin([FromBody] LoginRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.EmailOrUserName)) return BadRequest(new { message = "EmailOrUserName is required." });
            if (string.IsNullOrWhiteSpace(request.Password)) return BadRequest(new { message = "Password is required." });

            var identifier = request.EmailOrUserName.Trim();
            var user = await _userManager.FindByEmailAsync(identifier) ?? await _userManager.FindByNameAsync(identifier);
            if (user == null) return Unauthorized(new { message = "Invalid admin credentials." });

            var passwordValid = await _userManager.CheckPasswordAsync(user, request.Password);
            if (!passwordValid || !await _userManager.IsInRoleAsync(user, "Admin"))
                return Unauthorized(new { message = "Invalid admin credentials." });

            var response = await BuildAuthResponseAsync(user);
            return Ok(response);
        }

        [Authorize]
        [HttpGet("me")]
        public async Task<IActionResult> Me()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userId)) return Unauthorized(new { message = "Invalid token." });

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) return NotFound(new { message = "User not found." });

            var roles = await _userManager.GetRolesAsync(user);
            return Ok(new
            {
                user.Id,
                user.UserName,
                user.Email,
                user.PhoneNumber,
                roles,
                user.QemmaCoinsBalance,
                user.IsEliteSubscriber,
                user.EliteSubscriptionEndDate,
                user.CustomThemePalette,
                user.ActiveBadgeUrl
            });
        }

        private async Task<object> BuildAuthResponseAsync(ApplicationUser user)
        {
            var roles = await _userManager.GetRolesAsync(user);
            var expiresAt = DateTime.UtcNow.AddMinutes(GetJwtExpiryMinutes());
            var token = CreateJwtToken(user, roles, expiresAt);

            return new
            {
                token,
                tokenType = "Bearer",
                expiresAt,
                user = new
                {
                    user.Id,
                    user.UserName,
                    user.Email,
                    user.PhoneNumber,
                    roles,
                    user.QemmaCoinsBalance,
                    user.IsEliteSubscriber,
                    user.EliteSubscriptionEndDate,
                    user.CustomThemePalette,
                    user.ActiveBadgeUrl
                }
            };
        }

        private string CreateJwtToken(ApplicationUser user, IEnumerable<string> roles, DateTime expiresAt)
        {
            var claims = new List<Claim>
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.Id),
                new Claim(ClaimTypes.NameIdentifier, user.Id),
                new Claim(ClaimTypes.Name, user.UserName ?? string.Empty),
                new Claim(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            };

            claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));

            var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(GetJwtSecret()));
            var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: _configuration["Jwt:Issuer"],
                audience: _configuration["Jwt:Audience"],
                claims: claims,
                expires: expiresAt,
                signingCredentials: credentials);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        private string GetJwtSecret()
        {
            var secret = _configuration["Jwt:Secret"] ?? Environment.GetEnvironmentVariable("JWT_SECRET");
            if (!string.IsNullOrWhiteSpace(secret) && secret.Length >= 32) return secret;
            return "QemmaProjectDevelopmentOnlyJwtSecretKeyChangeMe";
        }

        private int GetJwtExpiryMinutes()
        {
            return int.TryParse(_configuration["Jwt:ExpiryMinutes"], out var minutes) && minutes > 0 ? minutes : 60 * 24 * 7;
        }

        private async Task EnsureRoleExistsAsync(string roleName)
        {
            if (!await _roleManager.RoleExistsAsync(roleName))
            {
                await _roleManager.CreateAsync(new IdentityRole(roleName));
            }
        }
    }
}
