using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using CmdNext.Models.Domain.Model.App.Admin;
using CmdNext.Service.Contracts;

namespace CmdNext.Api.Controllers
{
    [ApiController]
    [ApiVersion("1.0")]
    [Route("api/v{version:apiVersion}/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IUserService _userService;
        private readonly IConfiguration _configuration;
        private readonly ILogger<AuthController> _logger;

        public AuthController(
            IUserService userService,
            IConfiguration configuration,
            ILogger<AuthController> logger)
        {
            _userService = userService;
            _configuration = configuration;
            _logger = logger;
        }

        [AllowAnonymous]
        [HttpPost("register")]
        public async Task<ActionResult<AuthResponse>> Register([FromBody] RegisterRequest request)
        {
            _logger.LogInformation("Registration attempt for {Email}", request.Email);

            // Check if user already exists
            var existingUser = await _userService.GetByEmailAsync(request.Email);
            if (existingUser != null)
            {
                _logger.LogWarning("Registration rejected: {Email} is already in use", request.Email);
                return BadRequest(new { message = "Email already in use" });
            }

            // Create new user
            var user = new User
            {
                Email = request.Email,
                FirstName = request.FirstName,
                LastName = request.LastName
            };

            await _userService.CreateAsync(user, request.Password);

            _logger.LogInformation("Registration succeeded for {Email} (user {UserId})", user.Email, user.Id);

            // Generate JWT token
            var token = GenerateJwtToken(user, request.Client);

            var expiryInMinutes = ResolveExpiryMinutes(request.Client);

            return Ok(new AuthResponse
            {
                token = token,
                user = new UserDto
                {
                    id = user.Id.ToString(),
                    email = user.Email,
                    firstName = user.FirstName,
                    lastName = user.LastName
                },
                expiresIn = expiryInMinutes * 60 // Convert to seconds
            });
        }

        [AllowAnonymous]
        [HttpPost("login")]
        public async Task<ActionResult<AuthResponse>> Login([FromBody] LoginRequest request)
        {
            _logger.LogInformation("Login attempt for {Email}", request.Email);

            // Find user by email
            var user = await _userService.GetByEmailAsync(request.Email);
            if (user == null)
            {
                _logger.LogWarning("Login failed: no account exists for {Email}", request.Email);
                return Unauthorized(new { message = "Invalid email or password" });
            }

            // Validate password
            var isValidPassword = await _userService.ValidatePasswordAsync(user, request.Password);
            if (!isValidPassword)
            {
                _logger.LogWarning("Login failed: incorrect password for {Email}", request.Email);
                return Unauthorized(new { message = "Invalid email or password" });
            }

            // Check if user is active
            if (!user.IsActive)
            {
                _logger.LogWarning("Login failed: account disabled for {Email}", request.Email);
                return Unauthorized(new { message = "Account is disabled" });
            }

            // Generate JWT token
            var token = GenerateJwtToken(user, request.Client);

            _logger.LogInformation(
                "Login succeeded for user {UserId} ({Email}) on client {Client}",
                user.Id, user.Email, request.Client ?? "web");

            var expiryInMinutes = ResolveExpiryMinutes(request.Client);

            return Ok(new AuthResponse
            {
                token = token,
                user = new UserDto
                {
                    id = user.Id.ToString(),
                    email = user.Email,
                    firstName = user.FirstName,
                    lastName = user.LastName
                },
                expiresIn = expiryInMinutes * 60 // Convert to seconds
            });
        }

        [HttpGet("me")]
        [Authorize]
        public async Task<ActionResult<AuthResponse>> GetCurrentUser()
        {
            var userIdClaim = User.FindFirst("sub")?.Value;
            if (userIdClaim == null)
            {
                return Unauthorized(new { message = "Not authenticated" });
            }

            if (!Guid.TryParse(userIdClaim, out var userId))
            {
                return Unauthorized(new { message = "Invalid user ID" });
            }

            var user = await _userService.GetByIdAsync(userId);
            if (user == null)
            {
                return Unauthorized(new { message = "User not found" });
            }

            // Get expiry from JWT settings
            var jwtSettings = _configuration.GetSection("JwtSettings");
            var expiryInMinutes = int.Parse(jwtSettings["ExpiryInMinutes"] ?? "240");

            return Ok(new AuthResponse
            {
                token = null, // No new token for /me endpoint
                user = new UserDto
                {
                    id = user.Id.ToString(),
                    email = user.Email,
                    firstName = user.FirstName,
                    lastName = user.LastName
                },
                expiresIn = expiryInMinutes * 60
            });
        }

        /// <summary>
        /// Token lifetime for a client. Mobile is deliberately long-lived: it is a personal app,
        /// the token sits in the device keychain, and there is no refresh flow to renew it
        /// silently. Web keeps the short lifetime — a browser session is the riskier place for
        /// a long-lived token.
        /// </summary>
        private int ResolveExpiryMinutes(string? client)
        {
            var jwtSettings = _configuration.GetSection("JwtSettings");
            var isMobile = string.Equals(client, "mobile", StringComparison.OrdinalIgnoreCase);

            var key = isMobile ? "MobileExpiryInMinutes" : "ExpiryInMinutes";
            var fallback = isMobile ? 525600 : 240;

            return int.TryParse(jwtSettings[key], out var minutes) && minutes > 0
                ? minutes
                : fallback;
        }

        private string GenerateJwtToken(User user, string? client)
        {
            var jwtSettings = _configuration.GetSection("JwtSettings");
            var secretKey = jwtSettings["SecretKey"] ?? "CmdNextDefaultSecretKeyChangeInProduction";
            var issuer = jwtSettings["Issuer"] ?? "CmdNextAPI";
            var audience = jwtSettings["Audience"] ?? "CmdNextClient";
            var expiryInMinutes = ResolveExpiryMinutes(client);

            var tokenHandler = new JwtSecurityTokenHandler();
            tokenHandler.OutboundClaimTypeMap.Clear();
            var key = Encoding.UTF8.GetBytes(secretKey);

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(new[]
                {
                    new Claim("sub", user.Id.ToString()),
                    new Claim(ClaimTypes.Email, user.Email),
                    new Claim(ClaimTypes.GivenName, user.FirstName ?? ""),
                    new Claim(ClaimTypes.Surname, user.LastName ?? "")
                }),
                Expires = DateTime.UtcNow.AddMinutes(expiryInMinutes),
                SigningCredentials = new SigningCredentials(
                    new SymmetricSecurityKey(key),
                    SecurityAlgorithms.HmacSha256Signature),
                Issuer = issuer,
                Audience = audience
            };

            var token = tokenHandler.CreateToken(tokenDescriptor);
            return tokenHandler.WriteToken(token);
        }
    }

    public class UserDto
    {
        public string id { get; set; } = string.Empty;
        public string email { get; set; } = string.Empty;
        public string? firstName { get; set; }
        public string? lastName { get; set; }
    }

    public class RegisterRequest
    {
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string? FirstName { get; set; }
        public string? LastName { get; set; }

        /// <summary>See <see cref="LoginRequest.Client"/>.</summary>
        public string? Client { get; set; }
    }

    public class LoginRequest
    {
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;

        /// <summary>
        /// Which client is signing in: "mobile" gets a long-lived token, anything else gets the
        /// short web lifetime. There is no refresh-token flow, so the lifetime is the whole
        /// session — re-authenticating every few hours on a personal phone app is not worth it,
        /// while a browser on a shared machine should still expire quickly.
        /// </summary>
        public string? Client { get; set; }
    }

    public class AuthResponse
    {
        public string? token { get; set; }
        public UserDto? user { get; set; }
        public long expiresIn { get; set; }
    }
}
