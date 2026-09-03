using System;
using System.ComponentModel.DataAnnotations.Schema;
using CmdNext.Models.Domain.DTOs.Constants;
using CmdNext.Models.Domain.Model.Abstraction;

namespace CmdNext.Models.Domain.Model.App.Finance
{
    /// <summary>
    /// A user-defined expense category. Per-user (each user has their own rows).
    /// Expenses/ExpenseItems/Budgets reference this by <see cref="Id"/>, so renaming
    /// a category updates its display everywhere automatically.
    /// </summary>
    [Table("Categories", Schema = DBSchema.Finance)]
    public class Category : BaseEntity<Guid>
    {
        public Guid UserId { get; set; }

        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Label for the shop/party field on the expense form when this category is selected,
        /// e.g. "Restaurant" for Dining, "Provider" for Transport. Null when the field should
        /// be hidden (see <see cref="ShowPartyField"/>).
        /// </summary>
        public string? PartyLabel { get; set; }

        /// <summary>Whether the expense form shows the shop/party field for this category.</summary>
        public bool ShowPartyField { get; set; } = true;

        /// <summary>
        /// True for the permanent "Other" fallback category — cannot be deleted.
        /// </summary>
        public bool IsProtected { get; set; }
    }
}
