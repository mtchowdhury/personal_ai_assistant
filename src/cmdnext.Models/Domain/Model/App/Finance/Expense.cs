using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using CmdNext.Models.Domain.DTOs.Constants;
using CmdNext.Models.Domain.Model.Abstraction;

namespace CmdNext.Models.Domain.Model.App.Finance
{
    /// <summary>
    /// A single purchase / receipt (Rechnung). Holds the header data; line items
    /// are in <see cref="ExpenseItem"/>.
    /// </summary>
    [Table("Expenses", Schema = DBSchema.Finance)]
    public class Expense : BaseEntity<Guid>
    {
        public Guid UserId { get; set; }

        /// <summary>Shop / merchant name, e.g. "REWE".</summary>
        public string? ShopName { get; set; }

        /// <summary>When the purchase happened (from the receipt, not when it was entered).</summary>
        public DateTime PurchasedOn { get; set; }

        public decimal TotalAmount { get; set; }

        public string Currency { get; set; } = "EUR";

        /// <summary>Overall category for the receipt. Items may override per line.</summary>
        public Guid? CategoryId { get; set; }

        public Category? Category { get; set; }

        public string? PaymentMethod { get; set; }

        public string? Notes { get; set; }

        /// <summary>Relative path to the stored receipt image, if any.</summary>
        public string? ImagePath { get; set; }

        /// <summary>"ai" or "manual" — how the record was created.</summary>
        public string Source { get; set; } = "manual";

        public List<ExpenseItem> Items { get; set; } = new();
    }
}
