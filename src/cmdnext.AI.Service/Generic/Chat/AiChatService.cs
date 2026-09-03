using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using CmdNext.AI.Service.Generic.Configuration;
using CmdNext.AI.Service.Generic.Contracts;

namespace CmdNext.AI.Service.Generic.Chat
{
    public sealed class AiChatService : IAiChatService
    {
        private const string SummarySystemPrompt =
            "You are compacting a conversation so it can continue within a limited context window. " +
            "Write a factual summary that preserves decisions made, facts established, user preferences " +
            "stated, unresolved questions, and any identifiers, file names, or values referenced. " +
            "Omit pleasantries and restatements. Write prose, not a transcript, and do not address the user.";

        private readonly IChatClientFactory _clientFactory;
        private readonly AiOptions _options;

        public AiChatService(IChatClientFactory clientFactory, IOptions<AiOptions> options)
        {
            _clientFactory = clientFactory;
            _options = options.Value;
        }

        public async Task<AiChatResponse> CompleteAsync(
            AiChatRequest request,
            CancellationToken cancellationToken = default)
        {
            var client = _clientFactory.GetClient(request.Credential);
            var messages = BuildMessages(request);
            var chatOptions = BuildOptions(request);

            var response = await client.GetResponseAsync(messages, chatOptions, cancellationToken);

            return new AiChatResponse
            {
                Text = response.Text,
                Usage = MapUsage(response.Usage),
                Provider = request.Credential.Provider,
                Model = response.ModelId ?? request.Credential.Model,
                FinishReason = response.FinishReason?.ToString()
            };
        }

        public async IAsyncEnumerable<AiChatStreamUpdate> StreamAsync(
            AiChatRequest request,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var client = _clientFactory.GetClient(request.Credential);
            var messages = BuildMessages(request);
            var chatOptions = BuildOptions(request);

            UsageDetails? usage = null;
            string? finishReason = null;

            var stream = client.GetStreamingResponseAsync(messages, chatOptions, cancellationToken);

            await foreach (var update in stream.WithCancellation(cancellationToken))
            {
                foreach (var content in update.Contents)
                {
                    if (content is UsageContent usageContent)
                    {
                        usage = usageContent.Details;
                    }
                }

                if (update.FinishReason is not null)
                {
                    finishReason = update.FinishReason.ToString();
                }

                if (!string.IsNullOrEmpty(update.Text))
                {
                    yield return new AiChatStreamUpdate { TextDelta = update.Text };
                }
            }

            yield return new AiChatStreamUpdate
            {
                Phase = AiStreamPhase.Completed,
                IsFinal = true,
                Usage = MapUsage(usage),
                FinishReason = finishReason
            };
        }

        public async Task<string?> SummarizeAsync(
            AiSummarizeRequest request,
            CancellationToken cancellationToken = default)
        {
            if (request.Messages.Count == 0)
            {
                return request.ExistingSummary;
            }

            var client = _clientFactory.GetClient(request.Credential);

            var instruction = string.IsNullOrWhiteSpace(request.ExistingSummary)
                ? SummarySystemPrompt
                : $"{SummarySystemPrompt}\n\nAn earlier part of this conversation was already summarized as follows. Produce a single combined summary covering both it and the new messages.\n\n{request.ExistingSummary}";

            var messages = new List<ChatMessage> { new(ChatRole.System, instruction) };
            messages.AddRange(request.Messages);
            messages.Add(new ChatMessage(
                ChatRole.User,
                "Summarize the conversation above according to your instructions."));

            var options = new ChatOptions
            {
                ModelId = request.Credential.Model,
                Temperature = 0.2f,
                MaxOutputTokens = request.MaxOutputTokens ?? _options.DefaultMaxOutputTokens
            };

            var response = await client.GetResponseAsync(messages, options, cancellationToken);

            return string.IsNullOrWhiteSpace(response.Text)
                ? request.ExistingSummary
                : response.Text;
        }

        private List<ChatMessage> BuildMessages(AiChatRequest request)
        {
            var messages = new List<ChatMessage>();

            if (!string.IsNullOrWhiteSpace(request.SystemPrompt))
            {
                messages.Add(new ChatMessage(ChatRole.System, request.SystemPrompt));
            }

            if (!string.IsNullOrWhiteSpace(request.ConversationSummary))
            {
                messages.Add(new ChatMessage(
                    ChatRole.System,
                    "Summary of the earlier part of this conversation:\n\n" + request.ConversationSummary));
            }

            messages.AddRange(request.Messages);

            return messages;
        }

        private ChatOptions BuildOptions(AiChatRequest request)
        {
            var profile = ResolveProfile(request.ProfileName);

            var options = new ChatOptions
            {
                ModelId = request.Credential.Model,
                Temperature = request.Temperature
                    ?? profile?.Temperature
                    ?? _options.DefaultTemperature,
                MaxOutputTokens = request.MaxOutputTokens
                    ?? profile?.MaxOutputTokens
                    ?? _options.DefaultMaxOutputTokens
            };

            if (request.Tools is { Count: > 0 })
            {
                options.Tools = request.Tools.ToList();
            }

            return options;
        }

        private ProfileOptions? ResolveProfile(string? profileName)
        {
            var name = string.IsNullOrWhiteSpace(profileName)
                ? _options.DefaultProfile
                : profileName;

            if (string.IsNullOrWhiteSpace(name))
            {
                return null;
            }

            return _options.Profiles.TryGetValue(name, out var profile) ? profile : null;
        }

        private static AiUsage? MapUsage(UsageDetails? usage)
        {
            if (usage is null)
            {
                return null;
            }

            var input = (int)(usage.InputTokenCount ?? 0);
            var output = (int)(usage.OutputTokenCount ?? 0);

            return new AiUsage
            {
                InputTokens = input,
                OutputTokens = output,
                TotalTokens = (int)(usage.TotalTokenCount ?? input + output)
            };
        }
    }
}
