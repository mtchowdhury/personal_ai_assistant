using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using CmdNext.Models.Domain.DTOs.Finance;
using CmdNext.Service.Contracts;

namespace CmdNext.Service.Tools
{
    /// <summary>
    /// AI-callable finance tools. Deliberately READ + ADD only — there is no
    /// update, delete, or merge tool, so the model can never modify or remove
    /// existing records. Those actions are manual (UI/API) only.
    /// </summary>
    public class FinanceAiTools : IFinanceAiTools
    {
        private readonly IFinanceService _finance;
        private readonly ILogger<FinanceAiTools> _logger;
        private readonly Guid _userId;

        public FinanceAiTools(IFinanceService finance, ILogger<FinanceAiTools> logger, Guid userId)
        {
            _finance = finance;
            _logger = logger;
            _userId = userId;
        }

        public async Task<string> AddExpenseAsync(
            [Description("Shop or merchant name, e.g. 'REWE'.")] string? shopName,
            [Description("Purchase date in ISO format yyyy-MM-dd. Use today if the receipt has none.")] string? purchaseDate,
            [Description("Overall category name — must be one of the user's existing categories (see the tool description for the current list).")] string? category,
            [Description("Total amount as printed on the receipt. If omitted it is summed from the items.")] double? totalAmount,
            [Description("Currency code, default EUR.")] string? currency,
            [Description("The line items on the receipt.")] List<ToolExpenseItem> items)
        {
            DateTime? purchasedOn = null;
            if (!string.IsNullOrWhiteSpace(purchaseDate)
                && DateTime.TryParse(purchaseDate, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed))
            {
                purchasedOn = parsed;
            }

            var request = new CreateExpenseRequest
            {
                ShopName = shopName,
                PurchasedOn = purchasedOn,
                CategoryName = category,
                Currency = currency,
                TotalAmount = totalAmount.HasValue ? (decimal)totalAmount.Value : null,
                Source = "ai",
                Items = (items ?? new List<ToolExpenseItem>()).Select(i => new CreateExpenseItemRequest
                {
                    RawName = i.Name ?? string.Empty,
                    CanonicalName = i.CanonicalName,
                    CategoryName = i.Category,
                    Quantity = i.Quantity.HasValue ? (decimal)i.Quantity.Value : 1,
                    UnitPrice = i.UnitPrice.HasValue ? (decimal)i.UnitPrice.Value : 0,
                    LineTotal = i.LineTotal.HasValue ? (decimal)i.LineTotal.Value : null
                }).ToList()
            };

            var created = await _finance.CreateExpenseAsync(_userId, request);
            _logger.LogInformation("AI tool add_expense created {ExpenseId} for user {UserId}", created.Id, _userId);

            return $"Recorded expense at {created.ShopName ?? "unknown shop"} on {created.PurchasedOn:yyyy-MM-dd} " +
                   $"for {created.TotalAmount.ToString(CultureInfo.InvariantCulture)} {created.Currency} " +
                   $"with {created.Items.Count} item(s). Expense id: {created.Id}.";
        }

        public Task<string> QueryExpensesAsync(
            [Description("Product name to search, matched against both the canonical name and the receipt text, e.g. 'rice' or 'milk'.")] string? product,
            [Description("Shop name filter, e.g. 'REWE'.")] string? shop,
            [Description("Category name filter — one of the user's existing categories.")] string? category,
            [Description("Start date (inclusive) as yyyy-MM-dd.")] string? fromDate,
            [Description("End date (exclusive) as yyyy-MM-dd.")] string? toDate)
        {
            DateTime? from = TryDate(fromDate);
            DateTime? to = TryDate(toDate);
            return _finance.QueryExpensesAsync(_userId, product, shop, category, from, to);
        }

        private static DateTime? TryDate(string? value)
        {
            if (!string.IsNullOrWhiteSpace(value)
                && DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var d))
            {
                return d;
            }
            return null;
        }

        public async Task<IReadOnlyList<AITool>> GetToolsAsync()
        {
            var categories = await _finance.GetCategoriesAsync(_userId);
            var categoryNames = categories.Count > 0
                ? string.Join(", ", categories.Select(c => c.Name))
                : "Other";

            var addExpenseDescription =
                "Record a purchase / receipt (Rechnung) for the current user. Use this after reading a receipt image " +
                "the user attached, or when the user asks to log an expense. Extract the shop name, purchase date, " +
                "each line item (name exactly as printed), quantity, unit price and line total. For every item also set " +
                "'canonicalName' to a short normalised product name in lowercase English (e.g. a receipt line 'MILBONA REIS' " +
                "or 'BASMATI 1KG' both get canonicalName 'rice') so items across shops and brands can be grouped. " +
                $"Assign each item and the overall receipt one of the user's existing categories: {categoryNames}. " +
                "If nothing fits well, use 'Other'. Do not invent a new category name.";

            var queryExpensesDescription =
                "Search the current user's recorded purchases (read-only). Use this to answer questions like " +
                "'what is the cheapest milk I bought and from where' or 'how much did I spend on rice this month'. " +
                "Any argument may be omitted. Results are line items sorted by unit price ascending. " +
                $"The user's existing categories are: {categoryNames}.";

            return new List<AITool>
            {
                AIFunctionFactory.Create(AddExpenseAsync, "add_expense", addExpenseDescription),
                AIFunctionFactory.Create(QueryExpensesAsync, "query_expenses", queryExpensesDescription)
            };
        }
    }

    public class ToolExpenseItem
    {
        [Description("Item name exactly as printed on the receipt, e.g. 'MILBONA REIS'.")]
        public string? Name { get; set; }

        [Description("Short normalised lowercase English product name for grouping, e.g. 'rice'.")]
        public string? CanonicalName { get; set; }

        [Description("Category name — one of the user's existing categories.")]
        public string? Category { get; set; }

        [Description("Quantity purchased. Default 1.")]
        public double? Quantity { get; set; }

        [Description("Price per unit.")]
        public double? UnitPrice { get; set; }

        [Description("Line total. If omitted it is quantity * unit price.")]
        public double? LineTotal { get; set; }
    }
}
