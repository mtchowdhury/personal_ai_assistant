using System.Collections.Generic;
using System.Linq;
using CmdNext.Models.Domain.DTOs.Spaces;

namespace CmdNext.Service.Spaces
{
    /// <summary>
    /// Built-in space templates. Each declares the entry types (and their field schema) a new
    /// space of that kind starts with; the schema is copied onto the space itself (Space.SchemaJson)
    /// so it can be edited per-space afterwards without touching this code.
    /// </summary>
    public static class SpaceTemplates
    {
        private static readonly EntryTypeSchema NoteType = new()
        {
            Type = "note",
            Label = "Note",
            Fields = new List<FieldSchema>()
        };

        private static readonly EntryTypeSchema TaskType = new()
        {
            Type = "task",
            Label = "Task",
            Fields = new List<FieldSchema>
            {
                new() { Name = "priority", Type = "select", Options = new List<string> { "low", "medium", "high" } }
            }
        };

        public static readonly List<SpaceTemplateDto> All = new()
        {
            new SpaceTemplateDto
            {
                Kind = "learning",
                Label = "Learning",
                Description = "A course or skill you're learning — lessons, vocab, practice notes.",
                SuggestedNodeKind = "topic",
                EntryTypes = new List<EntryTypeSchema>
                {
                    NoteType,
                    new EntryTypeSchema
                    {
                        Type = "vocab",
                        Label = "Vocab / term",
                        Fields = new List<FieldSchema>
                        {
                            new() { Name = "term", Type = "text", Required = true },
                            new() { Name = "article", Type = "text" },
                            new() { Name = "meaning", Type = "text", Required = true },
                            new() { Name = "note", Type = "text" }
                        }
                    },
                    TaskType
                }
            },
            new SpaceTemplateDto
            {
                Kind = "journal",
                Label = "Journal",
                Description = "Personal experiences, places, day-to-day reflections.",
                SuggestedNodeKind = null,
                EntryTypes = new List<EntryTypeSchema>
                {
                    new EntryTypeSchema
                    {
                        Type = "journal",
                        Label = "Journal entry",
                        Fields = new List<FieldSchema>
                        {
                            new() { Name = "place", Type = "text" },
                            new() { Name = "people", Type = "tags" },
                            new() { Name = "mood", Type = "text" },
                            new() { Name = "rating", Type = "number" }
                        }
                    },
                    TaskType
                }
            },
            new SpaceTemplateDto
            {
                Kind = "people",
                Label = "People",
                Description = "A profile per person.",
                SuggestedNodeKind = "person",
                EntryTypes = new List<EntryTypeSchema>
                {
                    new EntryTypeSchema
                    {
                        Type = "profile",
                        Label = "Profile / character summary",
                        Fields = new List<FieldSchema>
                        {
                            new() { Name = "relation", Type = "text" },
                            new() { Name = "since", Type = "date" }
                        }
                    },
                    new EntryTypeSchema
                    {
                        Type = "event",
                        Label = "Event / interaction",
                        Fields = new List<FieldSchema>
                        {
                            new() { Name = "initiatedBy", Type = "select", Options = new List<string> { "me", "them" } },
                            new() { Name = "context", Type = "select", Options = new List<string> { "social", "help", "work", "family" } },
                            new() { Name = "sentiment", Type = "select", Options = new List<string> { "positive", "neutral", "negative" } }
                        }
                    },
                    TaskType
                }
            },
            new SpaceTemplateDto
            {
                Kind = "process",
                Label = "Process / project",
                Description = "A time-boxed process with contacts, contracts, and a running log (e.g. a house move).",
                SuggestedNodeKind = "topic",
                EntryTypes = new List<EntryTypeSchema>
                {
                    NoteType,
                    new EntryTypeSchema
                    {
                        Type = "contact",
                        Label = "Contact",
                        Fields = new List<FieldSchema>
                        {
                            new() { Name = "org", Type = "text" },
                            new() { Name = "email", Type = "text" },
                            new() { Name = "phone", Type = "text" }
                        }
                    },
                    new EntryTypeSchema
                    {
                        Type = "log",
                        Label = "Update log entry",
                        Fields = new List<FieldSchema>()
                    },
                    TaskType
                }
            },
            new SpaceTemplateDto
            {
                Kind = "custom",
                Label = "Custom",
                Description = "Start empty and define your own entry types as you go.",
                SuggestedNodeKind = null,
                EntryTypes = new List<EntryTypeSchema> { NoteType, TaskType }
            }
        };

        public static SpaceTemplateDto Resolve(string? kind)
        {
            return All.FirstOrDefault(t => t.Kind == kind) ?? All.First(t => t.Kind == "custom");
        }
    }
}
