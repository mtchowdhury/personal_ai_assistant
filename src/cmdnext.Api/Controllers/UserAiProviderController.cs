using System;
using System.Linq;
using System.Threading.Tasks;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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

        public UserAiProviderController(
            IUnitOfWork unitOfWork,
            IHttpContextAccessor httpContextAccessor,
            ICryptoHelper cryptoHelper)
        {
            _unitOfWork = unitOfWork;
            _httpContextAccessor = httpContextAccessor;
            _cryptoHelper = cryptoHelper;
        }

        [HttpPost("providers")]
        public async Task<ActionResult<UserAiProviderDto>> AddProvider([FromBody] AddAiProviderRequest request)
        {
            // Check if provider already exists for this user
            var existingProvider = await _unitOfWork.Repository<UserAiProvider, Guid>()
                .FirstOrDefaultAsync(x => x.UserId == CurrentUserId && x.Provider == request.Provider);

            if (existingProvider != null)
            {
                return BadRequest(new { message = "Provider already configured" });
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
