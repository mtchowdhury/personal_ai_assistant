using System;
using System.Collections.Generic;

namespace CmdNext.Models.Domain.DTOs.Finance
{
    /// <summary>The starter set of expense categories, seeded per-user on first use.</summary>
    public static class DefaultCategories
    {
        public static readonly (string Name, string? PartyLabel, bool ShowPartyField, bool IsProtected)[] All =
        {
            ("Groceries", "Shop", true, false),
            ("Dining", "Restaurant", true, false),
            ("Transport", "Provider", true, false),
            ("Household", null, false, false),
            ("Health", "Doctor/Hospital", true, false),
            ("Other", null, false, true)
        };
    }

    public class CategoryDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? PartyLabel { get; set; }
        public bool ShowPartyField { get; set; }
        public bool IsProtected { get; set; }
    }

    public class CreateCategoryRequest
    {
        public string Name { get; set; } = string.Empty;
        public string? PartyLabel { get; set; }
        public bool ShowPartyField { get; set; } = true;
    }

    public class UpdateCategoryRequest
    {
        public string? Name { get; set; }
        public string? PartyLabel { get; set; }
        public bool? ShowPartyField { get; set; }
    }

    public class ExpenseItemDto
    {
        public Guid Id { get; set; }
        public string RawName { get; set; } = string.Empty;
        public string? CanonicalName { get; set; }
        public Guid? CategoryId { get; set; }
        public string? CategoryName { get; set; }
        public decimal Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal LineTotal { get; set; }
        public string? Notes { get; set; }
    }

    public class ExpenseDto
    {
        public Guid Id { get; set; }
        public string? ShopName { get; set; }
        public DateTime PurchasedOn { get; set; }
        public decimal TotalAmount { get; set; }
        public string Currency { get; set; } = "EUR";
        public Guid? CategoryId { get; set; }
        public string? CategoryName { get; set; }
        public string? PaymentMethod { get; set; }
        public string? Notes { get; set; }
        public string? ImagePath { get; set; }
        public string Source { get; set; } = "manual";
        public DateTime? CreatedOn { get; set; }
        public List<ExpenseItemDto> Items { get; set; } = new();
    }

    public class ExpenseListItemDto
    {
        public Guid Id { get; set; }
        public string? ShopName { get; set; }
        public DateTime PurchasedOn { get; set; }
        public decimal TotalAmount { get; set; }
        public string Currency { get; set; } = "EUR";
        public Guid? CategoryId { get; set; }
        public string? CategoryName { get; set; }
        public string? Notes { get; set; }
        public string Source { get; set; } = "manual";
        public int ItemCount { get; set; }
        public bool HasImage { get; set; }
    }

    /// <summary>
    /// A single line item flattened with its parent expense's context, for the item-level
    /// ("transactions") list view.
    /// </summary>
    public class ExpenseItemRowDto
    {
        public Guid Id { get; set; }
        public Guid ExpenseId { get; set; }
        public string RawName { get; set; } = string.Empty;
        public string? CanonicalName { get; set; }
        public decimal Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal LineTotal { get; set; }
        public string? Notes { get; set; }

        // Parent expense context
        public DateTime PurchasedOn { get; set; }
        public string? ShopName { get; set; }
        public string Currency { get; set; } = "EUR";
        public Guid? CategoryId { get; set; }
        public string? CategoryName { get; set; }
        public string Source { get; set; } = "manual";
    }

    public class CreateExpenseItemRequest
    {
        public string RawName { get; set; } = string.Empty;
        public string? CanonicalName { get; set; }
        /// <summary>Category id. If omitted, falls back to the expense's own category.</summary>
        public Guid? CategoryId { get; set; }
        /// <summary>Category name as a convenience for the AI tool; resolved to an id server-side.</summary>
        public string? CategoryName { get; set; }
        public decimal Quantity { get; set; } = 1;
        public decimal UnitPrice { get; set; }
        public decimal? LineTotal { get; set; }
        public string? Notes { get; set; }
    }

    public class CreateExpenseRequest
    {
        public string? ShopName { get; set; }
        public DateTime? PurchasedOn { get; set; }
        public decimal? TotalAmount { get; set; }
        public string? Currency { get; set; }
        public Guid? CategoryId { get; set; }
        /// <summary>Category name as a convenience for the AI tool; resolved to an id server-side.</summary>
        public string? CategoryName { get; set; }
        public string? PaymentMethod { get; set; }
        public string? Notes { get; set; }
        /// <summary>"ai" or "manual"; defaults to manual.</summary>
        public string? Source { get; set; }
        public List<CreateExpenseItemRequest> Items { get; set; } = new();
    }

    public class UpdateExpenseRequest
    {
        public string? ShopName { get; set; }
        public DateTime? PurchasedOn { get; set; }
        public decimal? TotalAmount { get; set; }
        public string? Currency { get; set; }
        public Guid? CategoryId { get; set; }
        public string? PaymentMethod { get; set; }
        public string? Notes { get; set; }

        /// <summary>
        /// When provided, fully replaces the expense's line items (existing items are deleted
        /// and these are inserted fresh). Null means "leave items unchanged".
        /// </summary>
        public List<CreateExpenseItemRequest>? Items { get; set; }
    }

    public class UpdateExpenseItemRequest
    {
        public string? RawName { get; set; }
        public string? CanonicalName { get; set; }
        public Guid? CategoryId { get; set; }
        public decimal? Quantity { get; set; }
        public decimal? UnitPrice { get; set; }
        public decimal? LineTotal { get; set; }
        public string? Notes { get; set; }
    }

    /// <summary>Rename/merge a canonical name across all of a user's items.</summary>
    public class MergeCanonicalRequest
    {
        public List<string> From { get; set; } = new();
        public string To { get; set; } = string.Empty;
    }

    /// <summary>A year/month period that has at least one expense.</summary>
    public class ExpensePeriodDto
    {
        public int Year { get; set; }
        public int Month { get; set; }
    }

    public class CanonicalSummaryDto
    {
        public string? CanonicalName { get; set; }
        public int ItemCount { get; set; }
        public decimal TotalSpent { get; set; }
    }

    public class BudgetDto
    {
        public Guid Id { get; set; }
        public Guid CategoryId { get; set; }
        public string CategoryName { get; set; } = string.Empty;
        public decimal MonthlyLimit { get; set; }
        public string Currency { get; set; } = "EUR";
    }

    public class SetBudgetRequest
    {
        public Guid CategoryId { get; set; }
        public decimal MonthlyLimit { get; set; }
        public string? Currency { get; set; }
    }

    public class CategorySpendDto
    {
        public Guid? CategoryId { get; set; }
        public string CategoryName { get; set; } = string.Empty;
        public decimal Spent { get; set; }
        public decimal? BudgetLimit { get; set; }
        public bool OverBudget { get; set; }
    }

    public class FinanceDashboardDto
    {
        public decimal MonthTotal { get; set; }
        public string Currency { get; set; } = "EUR";
        public int MonthExpenseCount { get; set; }
        public int Year { get; set; }
        public int Month { get; set; }
        public List<CategorySpendDto> Categories { get; set; } = new();
        public decimal TotalBudget { get; set; }
        public bool AnyOverBudget { get; set; }
    }
}
