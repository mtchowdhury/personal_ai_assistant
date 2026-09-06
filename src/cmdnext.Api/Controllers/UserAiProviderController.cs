using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CmdNext.Models.Domain.Model.App.Ai;
using CmdNext.Api.Infrastructure;
using CmdNext.Repository.Contracts;
using CmdNext.Service.Contracts;

namespace CmdNext.Api.Controllers
{
    [ApiController]
    [ApiVersion("1.0")]
    [Authorize]
    [Route("api/v{version:apiVersion}/[controller]")]
    public class UserAiProviderController : ApiControllerBase
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ICryptoHelper _cryptoHelper;
        private readonly IAiCredentialResolver _credentialResolver;

        public UserAiProviderController(
            IUnitOfWork unitOfWork,
            IHttpContextAccessor httpContextAccessor,
            ICryptoHelper cryptoHelper,
            IAiCredentialResolver credentialResolver)
        {
            _unitOfWork = unitOfWork;
            _httpContextAccessor = httpContextAccessor;
            _cryptoHelper = cryptoHelper;
            _credentialResolver = credentialResolver;
        }

        [HttpPost("providers")]
        public async Task<ActionResult<UserAiProviderDto>> AddProvider([FromBody] AddAiProviderRequest request)
        {
            // Check if provider already exists for this user
            var existingProvider = await _unitOfWork.Repository<UserAiProvider, Guid>()
                .FirstOrDefaultAsync(x => x.UserId == CurrentUserId && x.Provider == request.Provider);

            // Re-adding a provider that already exists is treated as a key rotation
            // rather than an error: the settings dialog offers no separate "add" and
            // "edit" flow, so a repeated POST is almost always the user replacing a key.
            if (existingProvider != null)
            {
                return await ApplyProviderUpdateAsync(existingProvider, request.ApiKey, request.Endpoint, null);
            }

            // Encrypt the API key
            var encryptedApiKey = _cryptoHelper.Encrypt(request.ApiKey);

            var provider = new UserAiProvider
            {
                UserId = CurrentUserId,
                Provider = request.Provider,
                EncryptedApiKey = encryptedApiKey,
                KeyVersion = 1,
                KeyLastFour = request.ApiKey.Length >= 4 ? request.ApiKey[^4..] : request.ApiKey,
                Endpoint = request.Endpoint,
                IsActive = true
            };

            await _unitOfWork.Repository<UserAiProvider, Guid>().AddAsync(provider);
            await _unitOfWork.SaveChangesAsync();

            return Ok(new UserAiProviderDto
            {
                Id = provider.Id,
                Provider = provider.Provider,
                Endpoint = provider.Endpoint,
                KeyLastFour = provider.KeyLastFour,
                IsActive = provider.IsActive
            });
        }

        [HttpGet("providers")]
        public async Task<ActionResult<UserAiProviderDto[]>> GetProviders()
        {
            var providers = await _unitOfWork.Repository<UserAiProvider, Guid>()
                .FindAsync(x => x.UserId == CurrentUserId);

            var dtos = providers.Select(p => new UserAiProviderDto
            {
                Id = p.Id,
                Provider = p.Provider,
                Endpoint = p.Endpoint,
                KeyLastFour = p.KeyLastFour,
                IsActive = p.IsActive
            }).ToArray();

            return Ok(dtos);
        }

        [HttpPut("providers/{id:guid}")]
        public async Task<ActionResult<UserAiProviderDto>> UpdateProvider(
            Guid id,
            [FromBody] UpdateAiProviderRequest request)
        {
            var provider = await _unitOfWork.Repository<UserAiProvider, Guid>()
                .FirstOrDefaultAsync(x => x.Id == id && x.UserId == CurrentUserId);

            if (provider == null)
            {
                return NotFound(new { message = "Provider not found" });
            }

            return await ApplyProviderUpdateAsync(provider, request.ApiKey, request.Endpoint, request.IsActive);
        }

        [HttpDelete("providers/{id:guid}")]
        public async Task<IActionResult> DeleteProvider(Guid id)
        {
            var provider = await _unitOfWork.Repository<UserAiProvider, Guid>()
                .FirstOrDefaultAsync(x => x.Id == id && x.UserId == CurrentUserId);

            if (provider == null)
            {
                return NotFound(new { message = "Provider not found" });
            }

            _unitOfWork.Repository<UserAiProvider, Guid>().Delete(provider);
            await _unitOfWork.SaveChangesAsync();

            // The removed key may still be sitting in the client cache.
            _credentialResolver.Invalidate(CurrentUserId, provider.Provider);

            return NoContent();
        }

        /// <summary>
        /// Applies a key rotation / endpoint change to an existing provider row. An empty
        /// or whitespace API key means "leave the stored key alone" so the user can edit
        /// only the endpoint or the active flag without re-typing their secret.
        /// </summary>
        private async Task<ActionResult<UserAiProviderDto>> ApplyProviderUpdateAsync(
            UserAiProvider provider,
            string? apiKey,
            string? endpoint,
            bool? isActive)
        {
            var keyChanged = !string.IsNullOrWhiteSpace(apiKey);

            if (keyChanged)
            {
                var trimmed = apiKey!.Trim();

                if (trimmed.Length < 10)
                {
                    return BadRequest(new { message = "The API key looks too short to be valid." });
                }

                provider.EncryptedApiKey = _cryptoHelper.Encrypt(trimmed);
                provider.KeyLastFour = trimmed[^4..];
                provider.KeyVersion += 1;

                // A replaced key invalidates whatever the last validation said.
                provider.LastValidatedAt = null;
                provider.ValidationStatus = null;
                provider.ValidationMessage = null;
            }

            provider.Endpoint = string.IsNullOrWhiteSpace(endpoint) ? null : endpoint.Trim();

            if (isActive.HasValue)
            {
                provider.IsActive = isActive.Value;
            }

            _unitOfWork.Repository<UserAiProvider, Guid>().Update(provider);
            await _unitOfWork.SaveChangesAsync();

            // Chat clients are cached for ClientCacheMinutes, so without this the old
            // key would keep being used until the entry expired.
            _credentialResolver.Invalidate(CurrentUserId, provider.Provider);

            return Ok(new UserAiProviderDto
            {
                Id = provider.Id,
                Provider = provider.Provider,
                Endpoint = provider.Endpoint,
                KeyLastFour = provider.KeyLastFour,
                IsActive = provider.IsActive
            });
        }

        /// <summary>
        /// Token consumption for the current user, aggregated from <c>AiUsageLog</c>.
        /// Covers the trailing <paramref name="days"/> days (default 30) and also
        /// reports all-time totals so the dialog can show both.
        /// </summary>
        [HttpGet("usage")]
        public async Task<ActionResult<AiUsageSummaryDto>> GetUsage([FromQuery] int days = 30)
        {
            if (days < 1 || days > 365)
            {
                return BadRequest(new { message = "days must be between 1 and 365." });
            }

            var since = DateTime.UtcNow.Date.AddDays(-(days - 1));

            var rows = await _unitOfWork.Repository<AiUsageLog, Guid>()
                .Query()
                .AsNoTracking()
                .Where(x => x.UserId == CurrentUserId)
                .Select(x => new
                {
                    x.Provider,
                    x.Model,
                    x.InputTokens,
                    x.OutputTokens,
                    x.TotalTokens,
                    x.DurationMs,
                    x.IsSuccess,
                    CreatedOn = x.CreatedOn ?? DateTime.MinValue
                })
                .ToListAsync();

            var windowed = rows.Where(x => x.CreatedOn >= since).ToList();

            var byModel = windowed
                .GroupBy(x => new
                {
                    Provider = x.Provider ?? "unknown",
                    Model = x.Model ?? "unknown"
                })
                .Select(g => new AiUsageByModelDto
                {
                    Provider = g.Key.Provider,
                    Model = g.Key.Model,
                    Requests = g.Count(),
                    InputTokens = g.Sum(x => (long)x.InputTokens),
                    OutputTokens = g.Sum(x => (long)x.OutputTokens),
                    TotalTokens = g.Sum(x => (long)x.TotalTokens)
                })
                .OrderByDescending(x => x.TotalTokens)
                .ToList();

            // One bucket per day in the window, including days with no activity so the
            // client can render an even sparkline without filling gaps itself.
            var buckets = windowed
                .GroupBy(x => x.CreatedOn.Date)
                .ToDictionary(g => g.Key, g => g);

            var daily = Enumerable.Range(0, days)
                .Select(offset =>
                {
                    var day = since.AddDays(offset);
                    if (!buckets.TryGetValue(day, out var g))
                    {
                        return new AiUsageDailyDto { Date = day, Requests = 0, TotalTokens = 0 };
                    }

                    return new AiUsageDailyDto
                    {
                        Date = day,
                        Requests = g.Count(),
                        InputTokens = g.Sum(x => (long)x.InputTokens),
                        OutputTokens = g.Sum(x => (long)x.OutputTokens),
                        TotalTokens = g.Sum(x => (long)x.TotalTokens)
                    };
                })
                .ToList();

            var failed = windowed.Count(x => !x.IsSuccess);

            return Ok(new AiUsageSummaryDto
            {
                Days = days,
                Since = since,
                Requests = windowed.Count,
                FailedRequests = failed,
                InputTokens = windowed.Sum(x => (long)x.InputTokens),
                OutputTokens = windowed.Sum(x => (long)x.OutputTokens),
                TotalTokens = windowed.Sum(x => (long)x.TotalTokens),
                AverageDurationMs = windowed.Count == 0
                    ? 0
                    : (int)windowed.Average(x => x.DurationMs),
                AllTimeRequests = rows.Count,
                AllTimeTotalTokens = rows.Sum(x => (long)x.TotalTokens),
                LastUsedAt = rows.Count == 0 ? null : rows.Max(x => x.CreatedOn),
                ByModel = byModel,
                Daily = daily
            });
        }

        [HttpGet("settings")]
        public async Task<ActionResult<UserAiSettingsDto>> GetSettings()
        {
            var settings = await _unitOfWork.Repository<UserAiSettings, Guid>()
                .FirstOrDefaultAsync(x => x.UserId == CurrentUserId);

            if (settings == null)
            {
                // Create default settings
                settings = new UserAiSettings
                {
                    UserId = CurrentUserId,
                    DefaultProvider = "mistral",
                    DefaultModel = "mistral-small",
                    Temperature = 0.7f,
                    MaxOutputTokens = 4096,
                    IsChatEnabled = true
                };
                await _unitOfWork.Repository<UserAiSettings, Guid>().AddAsync(settings);
                await _unitOfWork.SaveChangesAsync();
            }

            return Ok(new UserAiSettingsDto
            {
                Id = settings.Id,
                DefaultProvider = settings.DefaultProvider,
                DefaultModel = settings.DefaultModel,
                Temperature = settings.Temperature,
                MaxOutputTokens = settings.MaxOutputTokens,
                IsChatEnabled = settings.IsChatEnabled
            });
        }

        [HttpPut("settings")]
        public async Task<ActionResult<UserAiSettingsDto>> UpdateSettings([FromBody] UpdateAiSettingsRequest request)
        {
            var settings = await _unitOfWork.Repository<UserAiSettings, Guid>()
                .FirstOrDefaultAsync(x => x.UserId == CurrentUserId);

            if (settings == null)
            {
                // Create default settings
                settings = new UserAiSettings
                {
                    UserId = CurrentUserId,
                    DefaultProvider = request.DefaultProvider,
                    DefaultModel = request.DefaultModel,
                    Temperature = request.Temperature,
                    MaxOutputTokens = request.MaxOutputTokens,
                    IsChatEnabled = request.IsChatEnabled
                };
                await _unitOfWork.Repository<UserAiSettings, Guid>().AddAsync(settings);
            }
            else
            {
                settings.DefaultProvider = request.DefaultProvider;
                settings.DefaultModel = request.DefaultModel;
                settings.Temperature = request.Temperature;
                settings.MaxOutputTokens = request.MaxOutputTokens;
                settings.IsChatEnabled = request.IsChatEnabled;
            }

            await _unitOfWork.SaveChangesAsync();

            return Ok(new UserAiSettingsDto
            {
                Id = settings.Id,
                DefaultProvider = settings.DefaultProvider,
                DefaultModel = settings.DefaultModel,
                Temperature = settings.Temperature,
                MaxOutputTokens = settings.MaxOutputTokens,
                IsChatEnabled = settings.IsChatEnabled
            });
        }
    }

    public class AddAiProviderRequest
    {
        public string Provider { get; set; } = "mistral";
        public string ApiKey { get; set; } = string.Empty;
        public string? Endpoint { get; set; }
    }

    public class AiUsageByModelDto
    {
        public string Provider { get; set; } = string.Empty;
        public string Model { get; set; } = string.Empty;
        public int Requests { get; set; }
        public long InputTokens { get; set; }
        public long OutputTokens { get; set; }
        public long TotalTokens { get; set; }
    }

    public class AiUsageDailyDto
    {
        public DateTime Date { get; set; }
        public int Requests { get; set; }
        public long InputTokens { get; set; }
        public long OutputTokens { get; set; }
        public long TotalTokens { get; set; }
    }

    public class AiUsageSummaryDto
    {
        public int Days { get; set; }
        public DateTime Since { get; set; }
        public int Requests { get; set; }
        public int FailedRequests { get; set; }
        public long InputTokens { get; set; }
        public long OutputTokens { get; set; }
        public long TotalTokens { get; set; }
        public int AverageDurationMs { get; set; }
        public int AllTimeRequests { get; set; }
        public long AllTimeTotalTokens { get; set; }
        public DateTime? LastUsedAt { get; set; }
        public List<AiUsageByModelDto> ByModel { get; set; } = new();
        public List<AiUsageDailyDto> Daily { get; set; } = new();
    }

    public class UpdateAiProviderRequest
    {
        /// <summary>Blank leaves the stored key untouched.</summary>
        public string? ApiKey { get; set; }
        public string? Endpoint { get; set; }
        public bool? IsActive { get; set; }
    }

    public class UserAiProviderDto
    {
        public Guid Id { get; set; }
        public string Provider { get; set; } = string.Empty;
        public string? Endpoint { get; set; }
        public string? KeyLastFour { get; set; }
        public bool IsActive { get; set; }
    }

    public class UserAiSettingsDto
    {
        public Guid Id { get; set; }
        public string? DefaultProvider { get; set; }
        public string? DefaultModel { get; set; }
        public float? Temperature { get; set; }
        public int? MaxOutputTokens { get; set; }
        public bool IsChatEnabled { get; set; }
    }

    public class UpdateAiSettingsRequest
    {
        public string? DefaultProvider { get; set; }
        public string? DefaultModel { get; set; }
        public float? Temperature { get; set; }
        public int? MaxOutputTokens { get; set; }
        public bool IsChatEnabled { get; set; } = true;
    }
}
