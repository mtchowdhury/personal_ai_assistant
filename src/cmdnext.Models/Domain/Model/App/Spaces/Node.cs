using System;
using System.ComponentModel.DataAnnotations.Schema;
using CmdNext.Models.Domain.DTOs.Constants;
using CmdNext.Models.Domain.Model.Abstraction;

namespace CmdNext.Models.Domain.Model.App.Spaces
{
    /// <summary>
    /// A folder in a space's user-designed hierarchy (e.g. level/class, lecture/topic, a person).
    /// </summary>
    [Table("Nodes", Schema = DBSchema.Spaces)]
    public class Node : BaseEntity<Guid>
    {
        public Guid SpaceId { get; set; }

        public Space? Space { get; set; }

        public Guid? ParentId { get; set; }

        public Node? Parent { get; set; }

        public string Name { get; set; } = string.Empty;

        /// <summary>Materialized path of slugs, e.g. "a2-2/class-konnektoren". Unique per space.</summary>
        public string Path { get; set; } = string.Empty;

        public int SortOrder { get; set; }

        /// <summary>Optional label from the space's template, e.g. "level", "class", "person".</summary>
        public string? Kind { get; set; }

        /// <summary>Short markdown shown at the top of the node.</summary>
        public string? Summary { get; set; }
    }
}
