using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using CmdNext.Models.Domain.Model.App.Admin;
using CmdNext.Repository.Contracts;
using CmdNext.Service.Contracts;

namespace CmdNext.Service.Services
{
    public class UserService : IUserService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<UserService> _logger;

        public UserService(IUnitOfWork unitOfWork, ILogger<UserService> logger)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
        }

        public async Task<User?> GetByEmailAsync(string email)
        {
            return await _unitOfWork.Repository<User, Guid>()
                .FirstOrDefaultAsync(x => x.Email == email);
        }

        public async Task<User?> GetByIdAsync(Guid userId)
        {
            return await _unitOfWork.Repository<User, Guid>()
                .GetByIdAsync(userId);
        }

        public async Task<User> CreateAsync(User user, string password)
        {
            // Note: the password itself is never logged, only the outcome.
            _logger.LogInformation("Creating user account for {Email}", user.Email);

            // Hash the password
            var (hash, salt) = HashPassword(password);
            user.PasswordHash = hash;
            user.PasswordSalt = salt;
            user.IsActive = true;
            user.IsVerified = true;
            user.CreatedAt = DateTime.UtcNow;

            await _unitOfWork.Repository<User, Guid>().AddAsync(user);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Created user {UserId} ({Email})", user.Id, user.Email);

            return user;
        }

        public Task<bool> ValidatePasswordAsync(User user, string password)
        {
            if (string.IsNullOrEmpty(user.PasswordHash) || string.IsNullOrEmpty(user.PasswordSalt))
            {
                _logger.LogWarning(
                    "User {UserId} has no stored password hash or salt; rejecting sign-in", user.Id);
                return Task.FromResult(false);
            }

            var hash = HashPasswordWithSalt(password, user.PasswordSalt);
            var isValid = hash == user.PasswordHash;

            if (!isValid)
            {
                _logger.LogWarning("Password validation failed for user {UserId}", user.Id);
            }
            else
            {
                _logger.LogInformation("Password validated for user {UserId}", user.Id);
            }

            return Task.FromResult(isValid);
        }

        private static (string hash, string salt) HashPassword(string password)
        {
            var salt = GenerateSalt();
            var hash = HashPasswordWithSalt(password, salt);
            return (hash, salt);
        }

        private static string HashPasswordWithSalt(string password, string salt)
        {
            using var sha256 = SHA256.Create();
            var saltedPassword = Encoding.UTF8.GetBytes(password + salt);
            var hashBytes = sha256.ComputeHash(saltedPassword);
            return Convert.ToBase64String(hashBytes);
        }

        private static string GenerateSalt()
        {
            var random = new byte[16];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(random);
            return Convert.ToBase64String(random);
        }
    }
}
