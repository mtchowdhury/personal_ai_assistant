using System;
using System.ComponentModel.DataAnnotations.Schema;
using CmdNext.Models.Domain.DTOs.Constants;
using CmdNext.Models.Domain.Model.Abstraction;

namespace CmdNext.Models.Domain.Model.App.Admin
{
    [Table("User", Schema = DBSchema.Identity)]
    public class User : BaseEntity<Guid>
    {
        public string Email { get; set; } = string.Empty;

        public string? FirstName { get; set; }

        public string? LastName { get; set; }

        public string? PasswordHash { get; set; }

        public string? PasswordSalt { get; set; }

        public DateTime? LastLoginAt { get; set; }

        public string? LastLoginIp { get; set; }

        public int LoginAttempts { get; set; }

        public bool IsActive { get; set; } = true;

        public bool IsVerified { get; set; }

        public DateTime? VerifiedAt { get; set; }

        public string? VerificationToken { get; set; }

        public string? ResetToken { get; set; }

        public DateTime? ResetTokenExpires { get; set; }

        public string? ProfilePicture { get; set; }

        public DateTime? DateOfBirth { get; set; }

        public string? PhoneNumber { get; set; }

        public string? Address { get; set; }

        public string? Timezone { get; set; }

        public string? PreferredLanguage { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
