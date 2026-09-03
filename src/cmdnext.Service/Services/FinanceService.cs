using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using CmdNext.Models.Domain.DTOs.Finance;
using CmdNext.Models.Domain.Model.App.Finance;
using CmdNext.Repository.Contracts;
using CmdNext.Service.Contracts;

namespace CmdNext.Service.Services
{
    public class FinanceService : IFinanceService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<FinanceService> _logger;

        public FinanceService(IUnitOfWork unitOfWork, ILogger<FinanceService> logger)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
        }

        private IRepository<Category, Guid> Categories => _unitOfWork.Repository<Category, Guid>();
        private IRepository<Expense, Guid> Expenses => _unitOfWork.Repository<Expense, Guid>();
        private IRepository<ExpenseItem, Guid> Items => _unitOfWork.Repository<ExpenseItem, Guid>();
        private IRepository<Budget, Guid> Budgets => _unitOfWork.Repository<Budget, Guid>();

        // ---- Categories ----

        public async Task<List<CategoryDto>> GetCategoriesAsync(Guid userId)
        {
            await EnsureDefaultCategoriesAsync(userId);

            return await Categories.Query()
                .AsNoTracking()
                .Where(c => c.UserId == userId)
                .OrderBy(c => c.Name)
                .Select(c => ToCategoryDto(c))
                .ToListAsync();
        }

        public async Task<CategoryDto> CreateCategoryAsync(Guid userId, CreateCategoryRequest request)
        {
            var name = request.Name?.Trim();
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Category name is required.");

            var exists = await Categories.Query()
                .AnyAsync(c => c.UserId == userId && c.Name.ToLower() == name.ToLower());
            if (exists) throw new InvalidOperationException($"A category named '{name}' already exists.");

            var category = new Category
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Name = name,
                PartyLabel = string.IsNullOrWhiteSpace(request.PartyLabel) ? null : request.PartyLabel!.Trim(),
                ShowPartyField = request.ShowPartyField,
                IsProtected = false,
                CreatedOn = DateTime.UtcNow,
                CreatedBy = userId
            };

            await Categories.AddAsync(category);
            await _unitOfWork.SaveChangesAsync();
            return ToCategoryDto(category);
        }

        public async Task<CategoryDto> UpdateCategoryAsync(Guid userId, Guid categoryId, UpdateCategoryRequest request)
        {
            var category = await Categories.Query()
                .FirstOrDefaultAsync(c => c.Id == categoryId && c.UserId == userId)
                ?? throw new KeyNotFoundException("Category not found.");

            if (request.Name != null)
            {
                var name = request.Name.Trim();
                if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Category name is required.");

                var exists = await Categories.Query()
                    .AnyAsync(c => c.UserId == userId && c.Id != categoryId && c.Name.ToLower() == name.ToLower());
                if (exists) throw new InvalidOperationException($"A category named '{name}' already exists.");

                category.Name = name;
            }

            if (request.PartyLabel != null)
            {
                category.PartyLabel = string.IsNullOrWhiteSpace(request.PartyLabel) ? null : request.PartyLabel.Trim();
            }
            if (request.ShowPartyField is { } show) category.ShowPartyField = show;

            category.UpdatedOn = DateTime.UtcNow;
            category.UpdatedBy = userId;

            Categories.Update(category);
            await _unitOfWork.SaveChangesAsync();
            return ToCategoryDto(category);
        }

        public async Task DeleteCategoryAsync(Guid userId, Guid categoryId)
        {
            var category = await Categories.Query()
                .FirstOrDefaultAsync(c => c.Id == categoryId && c.UserId == userId)
                ?? throw new KeyNotFoundException("Category not found.");

            if (category.IsProtected)
                throw new InvalidOperationException($"'{category.Name}' is a protected category and cannot be deleted.");

            var inUse = await Expenses.Query().AnyAsync(e => e.CategoryId == categoryId)
                || await Items.Query().AnyAsync(i => i.CategoryId == categoryId)
                || await Budgets.Query().AnyAsync(b => b.CategoryId == categoryId);

            if (inUse)
                throw new InvalidOperationException($"'{category.Name}' is still in use by existing expenses or budgets and cannot be deleted.");

            Categories.Delete(category);
            await _unitOfWork.SaveChangesAsync();
        }

        /// <summary>Seeds the starter categories for a user the first time they touch finance data.</summary>
        private async Task EnsureDefaultCategoriesAsync(Guid userId)
        {
            var hasAny = await Categories.Query().AnyAsync(c => c.UserId == userId);
            if (hasAny) return;

            var now = DateTime.UtcNow;
            var seeded = DefaultCategories.All.Select(d => new Category
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Name = d.Name,
                PartyLabel = d.PartyLabel,
                ShowPartyField = d.ShowPartyField,
                IsProtected = d.IsProtected,
                CreatedOn = now,
                CreatedBy = userId
            }).ToList();

            foreach (var c in seeded) await Categories.AddAsync(c);
            await _unitOfWork.SaveChangesAsync();
        }

        private async Task<Guid> ResolveCategoryIdAsync(Guid userId, Guid? categoryId, string? categoryName)
        {
            await EnsureDefaultCategoriesAsync(userId);

            if (categoryId is { } id)
            {
                var byId = await Categories.Query().FirstOrDefaultAsync(c => c.Id == id && c.UserId == userId);
                if (byId != null) return byId.Id;
            }

            if (!string.IsNullOrWhiteSpace(categoryName))
            {
                var name = categoryName.Trim();
                var byName = await Categories.Query()
                    .FirstOrDefaultAsync(c => c.UserId == userId && c.Name.ToLower() == name.ToLower());
                if (byName != null) return byName.Id;
            }

            var other = await Categories.Query().FirstOrDefaultAsync(c => c.UserId == userId && c.IsProtected)
                ?? await Categories.Query().FirstAsync(c => c.UserId == userId);
            return other.Id;
        }

        private static CategoryDto ToCategoryDto(Category c) => new()
        {
            Id = c.Id,
            Name = c.Name,
            PartyLabel = c.PartyLabel,
            ShowPartyField = c.ShowPartyField,
            IsProtected = c.IsProtected
        };

        // ---- Expenses ----

        public async Task<ExpenseDto> CreateExpenseAsync(Guid userId, CreateExpenseRequest request)
        {
            var categoryId = await ResolveCategoryIdAsync(userId, request.CategoryId, request.CategoryName);

            var expense = new Expense
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                ShopName = request.ShopName?.Trim(),
                PurchasedOn = (request.PurchasedOn ?? DateTime.UtcNow).ToUniversalTime(),
                Currency = string.IsNullOrWhiteSpace(request.Currency) ? "EUR" : request.Currency!.Trim().ToUpperInvariant(),
                CategoryId = categoryId,
                PaymentMethod = request.PaymentMethod?.Trim(),
                Notes = request.Notes?.Trim(),
                Source = string.Equals(request.Source, "ai", StringComparison.OrdinalIgnoreCase) ? "ai" : "manual",
                CreatedOn = DateTime.UtcNow,
                CreatedBy = userId
            };

            foreach (var i in request.Items)
            {
                expense.Items.Add(await BuildItemAsync(userId, expense.Id, categoryId, i));
            }

            // If no total was supplied, derive it from the line items.
            if (request.TotalAmount is { } total && total > 0)
            {
                expense.TotalAmount = total;
            }
            else
            {
                expense.TotalAmount = Math.Round(expense.Items.Sum(x => x.LineTotal), 2);
            }

            await Expenses.AddAsync(expense);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation(
                "Created {Source} expense {ExpenseId} for user {UserId}: {Shop} {Total} {Currency}, {ItemCount} item(s)",
                expense.Source, expense.Id, userId, expense.ShopName, expense.TotalAmount, expense.Currency, expense.Items.Count);

            return await ToDtoAsync(expense);
        }

        /// <summary>Builds a new ExpenseItem entity from a create/replace request, resolving its category.</summary>
        private async Task<ExpenseItem> BuildItemAsync(Guid userId, Guid expenseId, Guid fallbackCategoryId, CreateExpenseItemRequest i)
        {
            var quantity = i.Quantity <= 0 ? 1 : i.Quantity;
            var lineTotal = i.LineTotal ?? Math.Round(quantity * i.UnitPrice, 2);
            var itemCategoryId = (i.CategoryId is null && string.IsNullOrWhiteSpace(i.CategoryName))
                ? fallbackCategoryId
                : await ResolveCategoryIdAsync(userId, i.CategoryId, i.CategoryName);

            return new ExpenseItem
            {
                Id = Guid.NewGuid(),
                ExpenseId = expenseId,
                RawName = i.RawName.Trim(),
                CanonicalName = Clean(i.CanonicalName),
                CategoryId = itemCategoryId,
                Quantity = quantity,
                UnitPrice = i.UnitPrice,
                LineTotal = lineTotal,
                Notes = i.Notes?.Trim(),
                CreatedOn = DateTime.UtcNow,
                CreatedBy = userId
            };
        }

        public async Task<List<ExpenseListItemDto>> GetExpensesAsync(
            Guid userId, IReadOnlyCollection<Guid>? categoryIds = null, int take = 100, int? year = null,
            int? month = null, string? search = null)
        {
            var query = Expenses.Query().AsNoTracking().Include(e => e.Category).Where(e => e.UserId == userId);

            // No categories selected means "all"; several means any of them.
            var cids = NormalizeCategoryIds(categoryIds);
            if (cids.Count > 0)
            {
                query = query.Where(e => e.CategoryId != null && cids.Contains(e.CategoryId.Value));
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                // Match the shop/remarks on the expense itself, or any line item's raw or
                // canonical name, so searching "rice" finds receipts containing rice.
                var term = search.Trim().ToLower();
                query = query.Where(e =>
                    (e.ShopName != null && e.ShopName.ToLower().Contains(term)) ||
                    (e.Notes != null && e.Notes.ToLower().Contains(term)) ||
                    e.Items.Any(i =>
                        i.RawName.ToLower().Contains(term) ||
                        (i.CanonicalName != null && i.CanonicalName.ToLower().Contains(term))));
            }

            if (year is { } y)
            {
                // A month without a year is ignored; a year alone filters the whole year.
                var start = new DateTime(y, month ?? 1, 1, 0, 0, 0, DateTimeKind.Utc);
                var end = month.HasValue ? start.AddMonths(1) : start.AddYears(1);
                query = query.Where(e => e.PurchasedOn >= start && e.PurchasedOn < end);
            }

            var rows = await query
                .OrderByDescending(e => e.PurchasedOn)
                .Take(Math.Clamp(take, 1, 500))
                .Select(e => new ExpenseListItemDto
                {
                    Id = e.Id,
                    ShopName = e.ShopName,
                    PurchasedOn = e.PurchasedOn,
                    TotalAmount = e.TotalAmount,
                    Currency = e.Currency,
                    CategoryId = e.CategoryId,
                    CategoryName = e.Category != null ? e.Category.Name : null,
                    Notes = e.Notes,
                    Source = e.Source,
                    ItemCount = e.Items.Count,
                    HasImage = e.ImagePath != null
                })
                .ToListAsync();

            return rows;
        }

        /// <summary>
        /// Flat line-item view with the same filters as the expense list, so a search for
        /// "rice" returns the rice lines themselves rather than the receipts containing them.
        /// </summary>
        public async Task<List<ExpenseItemRowDto>> GetExpenseItemsAsync(
            Guid userId, IReadOnlyCollection<Guid>? categoryIds = null, int take = 200, int? year = null,
            int? month = null, string? search = null)
        {
            var query = Items.Query()
                .AsNoTracking()
                .Include(i => i.Category)
                .Include(i => i.Expense).ThenInclude(e => e!.Category)
                .Where(i => i.Expense!.UserId == userId);

            var cids = NormalizeCategoryIds(categoryIds);
            if (cids.Count > 0)
            {
                // An item inherits the expense's category unless it overrides it.
                query = query.Where(i =>
                    (i.CategoryId != null && cids.Contains(i.CategoryId.Value)) ||
                    (i.CategoryId == null && i.Expense!.CategoryId != null && cids.Contains(i.Expense.CategoryId.Value)));
            }

            if (year is { } y)
            {
                var start = new DateTime(y, month ?? 1, 1, 0, 0, 0, DateTimeKind.Utc);
                var end = month.HasValue ? start.AddMonths(1) : start.AddYears(1);
                query = query.Where(i => i.Expense!.PurchasedOn >= start && i.Expense.PurchasedOn < end);
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim().ToLower();
                query = query.Where(i =>
                    i.RawName.ToLower().Contains(term) ||
                    (i.CanonicalName != null && i.CanonicalName.ToLower().Contains(term)) ||
                    (i.Expense!.ShopName != null && i.Expense.ShopName.ToLower().Contains(term)) ||
                    (i.Expense!.Notes != null && i.Expense.Notes.ToLower().Contains(term)));
            }

            return await query
                .OrderByDescending(i => i.Expense!.PurchasedOn)
                .ThenBy(i => i.RawName)
                .Take(Math.Clamp(take, 1, 1000))
                .Select(i => new ExpenseItemRowDto
                {
                    Id = i.Id,
                    ExpenseId = i.ExpenseId,
                    RawName = i.RawName,
                    CanonicalName = i.CanonicalName,
                    Quantity = i.Quantity,
                    UnitPrice = i.UnitPrice,
                    LineTotal = i.LineTotal,
                    Notes = i.Notes,
                    PurchasedOn = i.Expense!.PurchasedOn,
                    ShopName = i.Expense.ShopName,
                    Currency = i.Expense.Currency,
                    CategoryId = i.CategoryId ?? i.Expense.CategoryId,
                    CategoryName = i.Category != null
                        ? i.Category.Name
                        : (i.Expense.Category != null ? i.Expense.Category.Name : null),
                    Source = i.Expense.Source
                })
                .ToListAsync();
        }

        public async Task<List<ExpensePeriodDto>> GetExpensePeriodsAsync(Guid userId)
        {
            return await Expenses.Query()
                .AsNoTracking()
                .Where(e => e.UserId == userId)
                .GroupBy(e => new { e.PurchasedOn.Year, e.PurchasedOn.Month })
                .Select(g => new ExpensePeriodDto { Year = g.Key.Year, Month = g.Key.Month })
                .OrderByDescending(p => p.Year).ThenByDescending(p => p.Month)
                .ToListAsync();
        }

        public async Task<ExpenseDto?> GetExpenseAsync(Guid userId, Guid expenseId)
        {
            var expense = await Expenses.Query()
                .AsNoTracking()
                .Include(e => e.Category)
                .Include(e => e.Items).ThenInclude(i => i.Category)
                .FirstOrDefaultAsync(e => e.Id == expenseId && e.UserId == userId);

            return expense == null ? null : ToDto(expense);
        }

        public async Task<ExpenseDto> UpdateExpenseAsync(Guid userId, Guid expenseId, UpdateExpenseRequest request)
        {
            var expense = await Expenses.Query()
                .Include(e => e.Items)
                .FirstOrDefaultAsync(e => e.Id == expenseId && e.UserId == userId)
                ?? throw new KeyNotFoundException("Expense not found.");

            expense.ShopName = string.IsNullOrWhiteSpace(request.ShopName) ? null : request.ShopName.Trim();
            if (request.PurchasedOn is { } p) expense.PurchasedOn = p.ToUniversalTime();
            if (!string.IsNullOrWhiteSpace(request.Currency)) expense.Currency = request.Currency.Trim().ToUpperInvariant();
            if (request.CategoryId is { } cid) expense.CategoryId = await ResolveCategoryIdAsync(userId, cid, null);
            expense.PaymentMethod = string.IsNullOrWhiteSpace(request.PaymentMethod) ? null : request.PaymentMethod.Trim();
            expense.Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim();

            List<ExpenseItem>? newItems = null;
            if (request.Items != null)
            {
                // Delete the old items via the repository only — do NOT also call
                // expense.Items.Clear()/Add() on the loaded navigation collection. Expense->Items
                // is a required, cascade-delete relationship, so removing an item from the tracked
                // collection makes EF cascade-delete it as an "orphan" in addition to the explicit
                // DeleteRange below, and the two deletes for the same row collide at SaveChanges
                // ("expected to affect 1 row(s), but actually affected 0"). New items are added
                // directly to the ExpenseItems DbSet instead of the navigation collection so the
                // collection is never touched.
                Items.DeleteRange(expense.Items.ToList());

                newItems = new List<ExpenseItem>();
                foreach (var i in request.Items)
                {
                    newItems.Add(await BuildItemAsync(userId, expense.Id, expense.CategoryId ?? Guid.Empty, i));
                }
                await Items.AddRangeAsync(newItems);
            }

            // If an explicit total was supplied use it; otherwise (re)derive from the new line items.
            if (request.TotalAmount is { } total && total > 0)
            {
                expense.TotalAmount = total;
            }
            else if (newItems != null)
            {
                expense.TotalAmount = Math.Round(newItems.Sum(x => x.LineTotal), 2);
            }

            expense.UpdatedOn = DateTime.UtcNow;
            expense.UpdatedBy = userId;

            // expense was loaded tracked (not AsNoTracking), so EF already observes the property
            // changes above and the item Add/DeleteRange calls. Calling Expenses.Update(expense)
            // here would re-walk the whole graph and force the newly-added items from Added back
            // to Modified, causing a spurious DbUpdateConcurrencyException on save.
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Updated expense {ExpenseId} for user {UserId}", expenseId, userId);

            if (newItems != null)
            {
                // expense.Items is stale (new items were added to the DbSet directly, not the
                // navigation collection, to avoid the cascade-delete collision above) — reload
                // so the returned DTO reflects the actual current items.
                expense = await Expenses.Query()
                    .Include(e => e.Items)
                    .FirstAsync(e => e.Id == expenseId);
            }
            return await ToDtoAsync(expense);
        }

        public async Task DeleteExpenseAsync(Guid userId, Guid expenseId)
        {
            var expense = await Expenses.Query()
                .FirstOrDefaultAsync(e => e.Id == expenseId && e.UserId == userId)
                ?? throw new KeyNotFoundException("Expense not found.");

            Expenses.Delete(expense); // items cascade
            await _unitOfWork.SaveChangesAsync();
            _logger.LogInformation("Deleted expense {ExpenseId} for user {UserId}", expenseId, userId);
        }

        // ---- Items ----

        public async Task<ExpenseItemDto> UpdateItemAsync(Guid userId, Guid itemId, UpdateExpenseItemRequest request)
        {
            var item = await Items.Query()
                .Include(i => i.Expense)
                .FirstOrDefaultAsync(i => i.Id == itemId && i.Expense!.UserId == userId)
                ?? throw new KeyNotFoundException("Item not found.");

            if (request.RawName != null) item.RawName = request.RawName.Trim();
            if (request.CanonicalName != null) item.CanonicalName = Clean(request.CanonicalName);
            if (request.CategoryId is { } cid) item.CategoryId = await ResolveCategoryIdAsync(userId, cid, null);
            if (request.Quantity is { } q) item.Quantity = q;
            if (request.UnitPrice is { } up) item.UnitPrice = up;
            if (request.LineTotal is { } lt) item.LineTotal = lt;
            if (request.Notes != null) item.Notes = request.Notes.Trim();
            item.UpdatedOn = DateTime.UtcNow;
            item.UpdatedBy = userId;

            Items.Update(item);
            await _unitOfWork.SaveChangesAsync();
            return await ToItemDtoAsync(item);
        }

        public async Task DeleteItemAsync(Guid userId, Guid itemId)
        {
            var item = await Items.Query()
                .Include(i => i.Expense)
                .FirstOrDefaultAsync(i => i.Id == itemId && i.Expense!.UserId == userId)
                ?? throw new KeyNotFoundException("Item not found.");

            Items.Delete(item);
            await _unitOfWork.SaveChangesAsync();
        }

        // ---- Canonical names ----

        public async Task<List<CanonicalSummaryDto>> GetCanonicalSummaryAsync(Guid userId)
        {
            return await Items.Query()
                .AsNoTracking()
                .Where(i => i.Expense!.UserId == userId)
                .GroupBy(i => i.CanonicalName)
                .Select(g => new CanonicalSummaryDto
                {
                    CanonicalName = g.Key,
                    ItemCount = g.Count(),
                    TotalSpent = g.Sum(x => x.LineTotal)
                })
                .OrderByDescending(x => x.TotalSpent)
                .ToListAsync();
        }

        public async Task<int> MergeCanonicalAsync(Guid userId, MergeCanonicalRequest request)
        {
            var to = Clean(request.To);
            if (to == null) throw new ArgumentException("Target canonical name is required.");

            var from = request.From
                .Select(Clean)
                .Where(x => x != null)
                .Select(x => x!)
                .ToList();

            if (from.Count == 0) throw new ArgumentException("At least one source name is required.");

            var affected = await Items.Query()
                .Where(i => i.Expense!.UserId == userId && i.CanonicalName != null && from.Contains(i.CanonicalName))
                .ToListAsync();

            foreach (var item in affected)
            {
                item.CanonicalName = to;
                item.UpdatedOn = DateTime.UtcNow;
                item.UpdatedBy = userId;
            }

            if (affected.Count > 0)
            {
                Items.UpdateRange(affected);
                await _unitOfWork.SaveChangesAsync();
            }

            _logger.LogInformation(
                "Merged canonical {From} -> {To} for user {UserId}, {Count} item(s)",
                string.Join(",", from), to, userId, affected.Count);

            return affected.Count;
        }

        // ---- Budgets ----

        public async Task<List<BudgetDto>> GetBudgetsAsync(Guid userId)
        {
            return await Budgets.Query()
                .AsNoTracking()
                .Include(b => b.Category)
                .Where(b => b.UserId == userId)
                .Select(b => new BudgetDto
                {
                    Id = b.Id,
                    CategoryId = b.CategoryId,
                    CategoryName = b.Category != null ? b.Category.Name : string.Empty,
                    MonthlyLimit = b.MonthlyLimit,
                    Currency = b.Currency
                })
                .ToListAsync();
        }

        public async Task<BudgetDto> SetBudgetAsync(Guid userId, SetBudgetRequest request)
        {
            var categoryId = await ResolveCategoryIdAsync(userId, request.CategoryId, null);
            var budget = await Budgets.Query()
                .FirstOrDefaultAsync(b => b.UserId == userId && b.CategoryId == categoryId);

            if (budget == null)
            {
                budget = new Budget
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    CategoryId = categoryId,
                    MonthlyLimit = request.MonthlyLimit,
                    Currency = string.IsNullOrWhiteSpace(request.Currency) ? "EUR" : request.Currency!.Trim().ToUpperInvariant(),
                    CreatedOn = DateTime.UtcNow,
                    CreatedBy = userId
                };
                await Budgets.AddAsync(budget);
            }
            else
            {
                budget.MonthlyLimit = request.MonthlyLimit;
                if (!string.IsNullOrWhiteSpace(request.Currency)) budget.Currency = request.Currency!.Trim().ToUpperInvariant();
                budget.UpdatedOn = DateTime.UtcNow;
                budget.UpdatedBy = userId;
                Budgets.Update(budget);
            }

            await _unitOfWork.SaveChangesAsync();

            var category = await Categories.Query().AsNoTracking().FirstOrDefaultAsync(c => c.Id == categoryId);
            return new BudgetDto
            {
                Id = budget.Id,
                CategoryId = budget.CategoryId,
                CategoryName = category?.Name ?? string.Empty,
                MonthlyLimit = budget.MonthlyLimit,
                Currency = budget.Currency
            };
        }

        public async Task DeleteBudgetAsync(Guid userId, Guid budgetId)
        {
            var budget = await Budgets.Query()
                .FirstOrDefaultAsync(b => b.Id == budgetId && b.UserId == userId)
                ?? throw new KeyNotFoundException("Budget not found.");
            Budgets.Delete(budget);
            await _unitOfWork.SaveChangesAsync();
        }

        // ---- Dashboard ----

        public async Task<FinanceDashboardDto> GetDashboardAsync(Guid userId, int? year = null, int? month = null)
        {
            await EnsureDefaultCategoriesAsync(userId);

            var now = DateTime.UtcNow;
            var y = year ?? now.Year;
            var m = month ?? now.Month;
            var start = new DateTime(y, m, 1, 0, 0, 0, DateTimeKind.Utc);
            var end = start.AddMonths(1);

            var monthExpenses = await Expenses.Query()
                .AsNoTracking()
                .Where(e => e.UserId == userId && e.PurchasedOn >= start && e.PurchasedOn < end)
                .Select(e => new { e.TotalAmount, e.CategoryId, e.Currency })
                .ToListAsync();

            var budgets = await Budgets.Query()
                .AsNoTracking()
                .Where(b => b.UserId == userId)
                .ToListAsync();

            var categoryLookup = await Categories.Query()
                .AsNoTracking()
                .Where(c => c.UserId == userId)
                .ToDictionaryAsync(c => c.Id, c => c.Name);

            var currency = monthExpenses.FirstOrDefault()?.Currency
                           ?? budgets.FirstOrDefault()?.Currency
                           ?? "EUR";

            var byCategory = monthExpenses
                .GroupBy(e => e.CategoryId)
                .ToDictionary(g => g.Key, g => g.Sum(x => x.TotalAmount));

            var categories = new List<CategorySpendDto>();
            // Include every category that has spend or a budget so the dashboard is complete.
            var keys = byCategory.Keys
                .Union(budgets.Select(b => (Guid?)b.CategoryId))
                .Where(k => k.HasValue)
                .Distinct()
                .OrderBy(k => categoryLookup.GetValueOrDefault(k!.Value, string.Empty));

            foreach (var key in keys)
            {
                byCategory.TryGetValue(key, out var spent);
                var limit = budgets.FirstOrDefault(b => b.CategoryId == key)?.MonthlyLimit;
                categories.Add(new CategorySpendDto
                {
                    CategoryId = key,
                    CategoryName = key.HasValue ? categoryLookup.GetValueOrDefault(key.Value, "Unknown") : "Unknown",
                    Spent = spent,
                    BudgetLimit = limit,
                    OverBudget = limit is { } l && l > 0 && spent > l
                });
            }

            return new FinanceDashboardDto
            {
                MonthTotal = Math.Round(monthExpenses.Sum(x => x.TotalAmount), 2),
                Currency = currency,
                MonthExpenseCount = monthExpenses.Count,
                Year = y,
                Month = m,
                Categories = categories,
                TotalBudget = budgets.Sum(b => b.MonthlyLimit),
                AnyOverBudget = categories.Any(c => c.OverBudget)
            };
        }

        // ---- AI read-only query ----

        public async Task<string> QueryExpensesAsync(Guid userId, string? canonicalOrName, string? shop, string? categoryName, DateTime? from, DateTime? to)
        {
            var query = Items.Query().AsNoTracking().Include(i => i.Category).Where(i => i.Expense!.UserId == userId);

            if (!string.IsNullOrWhiteSpace(canonicalOrName))
            {
                var term = canonicalOrName.Trim().ToLower();
                query = query.Where(i =>
                    (i.CanonicalName != null && i.CanonicalName.ToLower().Contains(term)) ||
                    i.RawName.ToLower().Contains(term));
            }
            if (!string.IsNullOrWhiteSpace(shop))
            {
                var s = shop.Trim().ToLower();
                query = query.Where(i => i.Expense!.ShopName != null && i.Expense.ShopName.ToLower().Contains(s));
            }
            if (!string.IsNullOrWhiteSpace(categoryName))
            {
                var c = categoryName.Trim().ToLower();
                query = query.Where(i =>
                    (i.Category != null && i.Category.Name.ToLower() == c) ||
                    (i.Expense!.Category != null && i.Expense.Category.Name.ToLower() == c));
            }
            if (from is { } f) query = query.Where(i => i.Expense!.PurchasedOn >= f.ToUniversalTime());
            if (to is { } t) query = query.Where(i => i.Expense!.PurchasedOn < t.ToUniversalTime());

            var results = await query
                .OrderBy(i => i.UnitPrice)
                .Take(200)
                .Select(i => new
                {
                    i.RawName,
                    i.CanonicalName,
                    Shop = i.Expense!.ShopName,
                    i.Expense.PurchasedOn,
                    i.UnitPrice,
                    i.Quantity,
                    i.LineTotal,
                    i.Expense.Currency
                })
                .ToListAsync();

            if (results.Count == 0)
            {
                return "No matching purchases found.";
            }

            var sb = new StringBuilder();
            sb.AppendLine($"Found {results.Count} matching line item(s):");
            foreach (var r in results)
            {
                var name = string.IsNullOrWhiteSpace(r.CanonicalName) ? r.RawName : $"{r.CanonicalName} ({r.RawName})";
                sb.AppendLine(
                    $"- {name} @ {r.UnitPrice.ToString(CultureInfo.InvariantCulture)} {r.Currency} " +
                    $"x{r.Quantity.ToString(CultureInfo.InvariantCulture)} = {r.LineTotal.ToString(CultureInfo.InvariantCulture)} {r.Currency}, " +
                    $"{r.Shop ?? "unknown shop"}, {r.PurchasedOn:yyyy-MM-dd}");
            }
            return sb.ToString();
        }

        public void AttachImagePath(Guid userId, Guid expenseId, string relativePath)
        {
            var expense = Expenses.Query().FirstOrDefault(e => e.Id == expenseId && e.UserId == userId);
            if (expense == null) return;
            expense.ImagePath = relativePath;
            Expenses.Update(expense);
            _unitOfWork.SaveChanges();
        }

        // ---- Mapping helpers ----

        /// <summary>Drops empty ids and duplicates so the filter stays a simple "any of these" set.</summary>
        private static IReadOnlyList<Guid> NormalizeCategoryIds(IReadOnlyCollection<Guid>? categoryIds)
        {
            if (categoryIds == null || categoryIds.Count == 0) return Array.Empty<Guid>();
            return categoryIds.Where(id => id != Guid.Empty).Distinct().ToList();
        }

        private static string? Clean(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;
            return value.Trim().ToLowerInvariant();
        }

        private async Task<ExpenseDto> ToDtoAsync(Expense e)
        {
            var categoryIds = new[] { e.CategoryId }.Concat(e.Items.Select(i => i.CategoryId))
                .Where(id => id.HasValue).Select(id => id!.Value).Distinct().ToList();
            var names = await Categories.Query().AsNoTracking()
                .Where(c => categoryIds.Contains(c.Id))
                .ToDictionaryAsync(c => c.Id, c => c.Name);

            return new ExpenseDto
            {
                Id = e.Id,
                ShopName = e.ShopName,
                PurchasedOn = e.PurchasedOn,
                TotalAmount = e.TotalAmount,
                Currency = e.Currency,
                CategoryId = e.CategoryId,
                CategoryName = e.CategoryId.HasValue ? names.GetValueOrDefault(e.CategoryId.Value) : null,
                PaymentMethod = e.PaymentMethod,
                Notes = e.Notes,
                ImagePath = e.ImagePath,
                Source = e.Source,
                CreatedOn = e.CreatedOn,
                Items = e.Items.OrderBy(i => i.CreatedOn).Select(i => new ExpenseItemDto
                {
                    Id = i.Id,
                    RawName = i.RawName,
                    CanonicalName = i.CanonicalName,
                    CategoryId = i.CategoryId,
                    CategoryName = i.CategoryId.HasValue ? names.GetValueOrDefault(i.CategoryId.Value) : null,
                    Quantity = i.Quantity,
                    UnitPrice = i.UnitPrice,
                    LineTotal = i.LineTotal,
                    Notes = i.Notes
                }).ToList()
            };
        }

        /// <summary>Maps an Expense already loaded with Category/Items.Category included.</summary>
        private static ExpenseDto ToDto(Expense e) => new()
        {
            Id = e.Id,
            ShopName = e.ShopName,
            PurchasedOn = e.PurchasedOn,
            TotalAmount = e.TotalAmount,
            Currency = e.Currency,
            CategoryId = e.CategoryId,
            CategoryName = e.Category?.Name,
            PaymentMethod = e.PaymentMethod,
            Notes = e.Notes,
            ImagePath = e.ImagePath,
            Source = e.Source,
            CreatedOn = e.CreatedOn,
            Items = e.Items.OrderBy(i => i.CreatedOn).Select(ToItemDto).ToList()
        };

        private static ExpenseItemDto ToItemDto(ExpenseItem i) => new()
        {
            Id = i.Id,
            RawName = i.RawName,
            CanonicalName = i.CanonicalName,
            CategoryId = i.CategoryId,
            CategoryName = i.Category?.Name,
            Quantity = i.Quantity,
            UnitPrice = i.UnitPrice,
            LineTotal = i.LineTotal,
            Notes = i.Notes
        };

        private async Task<ExpenseItemDto> ToItemDtoAsync(ExpenseItem i)
        {
            string? categoryName = null;
            if (i.CategoryId is { } cid)
            {
                categoryName = (await Categories.Query().AsNoTracking().FirstOrDefaultAsync(c => c.Id == cid))?.Name;
            }
            return new ExpenseItemDto
            {
                Id = i.Id,
                RawName = i.RawName,
                CanonicalName = i.CanonicalName,
                CategoryId = i.CategoryId,
                CategoryName = categoryName,
                Quantity = i.Quantity,
                UnitPrice = i.UnitPrice,
                LineTotal = i.LineTotal,
                Notes = i.Notes
            };
        }
    }
}
