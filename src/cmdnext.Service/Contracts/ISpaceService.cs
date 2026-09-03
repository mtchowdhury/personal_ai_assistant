using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CmdNext.Models.Domain.DTOs.Spaces;

namespace CmdNext.Service.Contracts
{
    public interface ISpaceService
    {
        List<SpaceTemplateDto> GetTemplates();

        Task<List<SpaceDto>> GetSpacesAsync(Guid userId, bool includeArchived = false);
        Task<SpaceDto> GetSpaceAsync(Guid userId, Guid spaceId);
        Task<SpaceDto> CreateSpaceAsync(Guid userId, CreateSpaceRequest request);
        Task<SpaceDto> UpdateSpaceAsync(Guid userId, Guid spaceId, UpdateSpaceRequest request);
        Task<SpaceDto> UpdateSpaceStateAsync(Guid userId, Guid spaceId, UpdateSpaceStateRequest request);
        Task<SpaceDto> UpdateSpaceSchemaAsync(Guid userId, Guid spaceId, UpdateSpaceSchemaRequest request);
        Task ArchiveSpaceAsync(Guid userId, Guid spaceId);
        Task DeleteSpaceAsync(Guid userId, Guid spaceId);

        Task<List<NodeDto>> GetNodesAsync(Guid userId, Guid spaceId);
        Task<NodeDto> CreateNodeAsync(Guid userId, Guid spaceId, CreateNodeRequest request);
        Task<NodeDto> UpdateNodeAsync(Guid userId, Guid spaceId, Guid nodeId, UpdateNodeRequest request);
        Task DeleteNodeAsync(Guid userId, Guid spaceId, Guid nodeId);

        Task<List<EntryListItemDto>> GetEntriesAsync(Guid userId, Guid spaceId, EntryQuery query);
        Task<EntryDto> GetEntryAsync(Guid userId, Guid spaceId, Guid entryId);
        Task<EntryDto> CreateEntryAsync(Guid userId, Guid spaceId, CreateEntryRequest request);
        Task<EntryDto> UpdateEntryAsync(Guid userId, Guid spaceId, Guid entryId, UpdateEntryRequest request);
        Task<EntryDto> AppendToEntryAsync(Guid userId, Guid spaceId, Guid entryId, AppendToEntryRequest request);
        Task DeleteEntryAsync(Guid userId, Guid spaceId, Guid entryId);

        Task<List<SearchResultDto>> SearchAsync(Guid userId, SearchEntriesRequest request);

        Task<AttachmentDto> AddAttachmentAsync(
            Guid userId, Guid spaceId, Guid? nodeId, Guid? entryId,
            string fileName, string contentType, System.IO.Stream content, string? extractedText);
        Task<List<AttachmentDto>> GetAttachmentsAsync(Guid userId, Guid spaceId, Guid? nodeId, Guid? entryId);
        Task<(System.IO.Stream Stream, string ContentType, string FileName)> OpenAttachmentAsync(Guid userId, Guid spaceId, Guid attachmentId);
        Task DeleteAttachmentAsync(Guid userId, Guid spaceId, Guid attachmentId);
    }
}
