using System;
using System.ComponentModel.DataAnnotations.Schema;
using CmdNext.Models.Domain.DTOs.Constants;
using CmdNext.Models.Domain.Model.Abstraction;

namespace CmdNext.Models.Domain.Model.App.Finance
{
    /// <summary>
    /// A single line item on a receipt.
    /// </summary>
    [Table("ExpenseItems", Schema = DBSchema.Finance)]
    public class ExpenseItem : BaseEntity<Guid>
    {
        public Guid ExpenseId { get; set; }

        public Expense? Expense { get; set; }

        /// <summary>The name exactly as it appears on the receipt, e.g. "MILBONA REIS".</summary>
        public string RawName { get; set; } = string.Empty;

        /// <summary>
        /// Normalised product name the AI fills in so items across shops/brands group,
        /// e.g. "rice". Nullable; editable/mergeable from the UI.
        /// </summary>
        public string? CanonicalName { get; set; }

        public Guid? CategoryId { get; set; }

        public Category? Category { get; set; }

        public decimal Quantity { get; set; } = 1;

        public decimal UnitPrice { get; set; }

        public decimal LineTotal { get; set; }

        /// <summary>Optional free-text note on this line item, e.g. for future reference.</summary>
        public string? Notes { get; set; }
    }
}
