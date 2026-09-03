using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CmdNext.Models.Domain.DTOs.Finance;

namespace CmdNext.Service.Contracts
{
    public interface IFinanceService
    {
        Task<List<CategoryDto>> GetCategoriesAsync(Guid userId);
        Task<CategoryDto> CreateCategoryAsync(Guid userId, CreateCategoryRequest request);
        Task<CategoryDto> UpdateCategoryAsync(Guid userId, Guid categoryId, UpdateCategoryRequest request);
        Task DeleteCategoryAsync(Guid userId, Guid categoryId);

        Task<ExpenseDto> CreateExpenseAsync(Guid userId, CreateExpenseRequest request);
        /// <summary>Lists expenses; <paramref name="categoryIds"/> matches any of the given categories, or all when empty.</summary>
        Task<List<ExpenseListItemDto>> GetExpensesAsync(
            Guid userId, IReadOnlyCollection<Guid>? categoryIds = null, int take = 100, int? year = null,
            int? month = null, string? search = null);
        /// <summary>Lists line items; <paramref name="categoryIds"/> matches any of the given categories, or all when empty.</summary>
        Task<List<ExpenseItemRowDto>> GetExpenseItemsAsync(
            Guid userId, IReadOnlyCollection<Guid>? categoryIds = null, int take = 200, int? year = null,
            int? month = null, string? search = null);
        Task<List<ExpensePeriodDto>> GetExpensePeriodsAsync(Guid userId);
        Task<ExpenseDto?> GetExpenseAsync(Guid userId, Guid expenseId);
        Task<ExpenseDto> UpdateExpenseAsync(Guid userId, Guid expenseId, UpdateExpenseRequest request);
        Task DeleteExpenseAsync(Guid userId, Guid expenseId);

        Task<ExpenseItemDto> UpdateItemAsync(Guid userId, Guid itemId, UpdateExpenseItemRequest request);
        Task DeleteItemAsync(Guid userId, Guid itemId);

        Task<List<CanonicalSummaryDto>> GetCanonicalSummaryAsync(Guid userId);
        Task<int> MergeCanonicalAsync(Guid userId, MergeCanonicalRequest request);

        Task<List<BudgetDto>> GetBudgetsAsync(Guid userId);
        Task<BudgetDto> SetBudgetAsync(Guid userId, SetBudgetRequest request);
        Task DeleteBudgetAsync(Guid userId, Guid budgetId);

        Task<FinanceDashboardDto> GetDashboardAsync(Guid userId, int? year = null, int? month = null);

        /// <summary>Read-only query used by the AI tool and by ad-hoc questions.</summary>
        Task<string> QueryExpensesAsync(Guid userId, string? canonicalOrName, string? shop, string? categoryName, DateTime? from, DateTime? to);

        void AttachImagePath(Guid userId, Guid expenseId, string relativePath);
    }
}
