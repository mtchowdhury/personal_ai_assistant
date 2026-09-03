using System;
using System.ComponentModel.DataAnnotations.Schema;
using CmdNext.Models.Domain.DTOs.Constants;
using CmdNext.Models.Domain.Model.Abstraction;

namespace CmdNext.Models.Domain.Model.App.Ai
{
    [Table("UserAiProvider", Schema = DBSchema.Ai)]
    public class UserAiProvider : BaseEntity<Guid>
    {
        public Guid UserId { get; set; }

        public string Provider { get; set; } = string.Empty;

        public string? EncryptedApiKey { get; set; }

        public int KeyVersion { get; set; } = 1;

        public string? KeyLastFour { get; set; }

        public string? Endpoint { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTime? LastValidatedAt { get; set; }

        public string? ValidationStatus { get; set; }

        public string? ValidationMessage { get; set; }

        public string? OAuthAccessToken { get; set; }

        public string? OAuthRefreshToken { get; set; }

        public DateTime? OAuthExpiresAt { get; set; }

        public UserAiSettings? Settings { get; set; }

        public Guid? SettingsId { get; set; }
    }
}
