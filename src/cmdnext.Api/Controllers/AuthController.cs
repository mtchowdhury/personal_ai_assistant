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
            var token = GenerateJwtToken(user);

            // Get expiry from JWT settings
            var jwtSettings = _configuration.GetSection("JwtSettings");
            var expiryInMinutes = int.Parse(jwtSettings["ExpiryInMinutes"] ?? "240");

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
            var token = GenerateJwtToken(user);

            _logger.LogInformation("Login succeeded for user {UserId} ({Email})", user.Id, user.Email);

            // Get expiry from JWT settings
            var jwtSettings = _configuration.GetSection("JwtSettings");
            var expiryInMinutes = int.Parse(jwtSettings["ExpiryInMinutes"] ?? "240");

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

        private string GenerateJwtToken(User user)
        {
            var jwtSettings = _configuration.GetSection("JwtSettings");
            var secretKey = jwtSettings["SecretKey"] ?? "CmdNextDefaultSecretKeyChangeInProduction";
            var issuer = jwtSettings["Issuer"] ?? "CmdNextAPI";
            var audience = jwtSettings["Audience"] ?? "CmdNextClient";
            var expiryInMinutes = int.Parse(jwtSettings["ExpiryInMinutes"] ?? "240");

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
    }

    public class LoginRequest
    {
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }

    public class AuthResponse
    {
        public string? token { get; set; }
        public UserDto? user { get; set; }
        public long expiresIn { get; set; }
    }
}
