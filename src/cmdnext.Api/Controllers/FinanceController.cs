using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CmdNext.Api.Infrastructure;
using CmdNext.Models.Domain.DTOs.Finance;
using CmdNext.Service.Contracts;

namespace CmdNext.Api.Controllers
{
    [ApiController]
    [ApiVersion("1.0")]
    [Authorize]
    [Route("api/v{version:apiVersion}/[controller]")]
    public class FinanceController : ApiControllerBase
    {
        private static readonly string[] AllowedImageExtensions = { ".png", ".jpg", ".jpeg", ".gif", ".webp" };
        private const long MaxImageBytes = 10 * 1024 * 1024;

        private readonly IFinanceService _finance;
        private readonly IWebHostEnvironment _environment;

        public FinanceController(IFinanceService finance, IWebHostEnvironment environment)
        {
            _finance = finance;
            _environment = environment;
        }

        // ---- Categories ----

        [HttpGet("categories")]
        public async Task<ActionResult> GetCategories() => Ok(await _finance.GetCategoriesAsync(CurrentUserId));

        [HttpPost("categories")]
        public async Task<ActionResult> CreateCategory([FromBody] CreateCategoryRequest request)
        {
            try { return Ok(await _finance.CreateCategoryAsync(CurrentUserId, request)); }
            catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
            catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
        }

        [HttpPut("categories/{id:guid}")]
        public async Task<ActionResult> UpdateCategory(Guid id, [FromBody] UpdateCategoryRequest request)
        {
            try { return Ok(await _finance.UpdateCategoryAsync(CurrentUserId, id, request)); }
            catch (System.Collections.Generic.KeyNotFoundException) { return NotFound(new { message = "Category not found" }); }
            catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
            catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
        }

        [HttpDelete("categories/{id:guid}")]
        public async Task<ActionResult> DeleteCategory(Guid id)
        {
            try { await _finance.DeleteCategoryAsync(CurrentUserId, id); return Ok(new { message = "Deleted" }); }
            catch (System.Collections.Generic.KeyNotFoundException) { return NotFound(new { message = "Category not found" }); }
            catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
        }

        // ---- Expenses (CRUD) ----

        /// <summary>Repeat <c>categoryId</c> to filter on several categories at once; omit it for all.</summary>
        [HttpGet("expenses")]
        public async Task<ActionResult> GetExpenses(
            [FromQuery(Name = "categoryId")] Guid[]? categoryId,
            [FromQuery] int take = 100,
            [FromQuery] int? year = null,
            [FromQuery] int? month = null,
            [FromQuery] string? search = null)
            => Ok(await _finance.GetExpensesAsync(CurrentUserId, categoryId, take, year, month, search));

        /// <summary>Flat line-item view with the same filters as the expense list.</summary>
        [HttpGet("items")]
        public async Task<ActionResult> GetExpenseItems(
            [FromQuery(Name = "categoryId")] Guid[]? categoryId,
            [FromQuery] int take = 200,
            [FromQuery] int? year = null,
            [FromQuery] int? month = null,
            [FromQuery] string? search = null)
            => Ok(await _finance.GetExpenseItemsAsync(CurrentUserId, categoryId, take, year, month, search));

        /// <summary>Distinct year/month periods the user actually has expenses in, newest first.</summary>
        [HttpGet("expenses/periods")]
        public async Task<ActionResult> GetExpensePeriods()
            => Ok(await _finance.GetExpensePeriodsAsync(CurrentUserId));

        [HttpGet("expenses/{id:guid}")]
        public async Task<ActionResult> GetExpense(Guid id)
        {
            var expense = await _finance.GetExpenseAsync(CurrentUserId, id);
            return expense == null ? NotFound(new { message = "Expense not found" }) : Ok(expense);
        }

        [HttpPost("expenses")]
        public async Task<ActionResult> CreateExpense([FromBody] CreateExpenseRequest request)
        {
            // Manual entry from the UI is always source "manual".
            request.Source = "manual";
            var created = await _finance.CreateExpenseAsync(CurrentUserId, request);
            return Ok(created);
        }

        [HttpPut("expenses/{id:guid}")]
        public async Task<ActionResult> UpdateExpense(Guid id, [FromBody] UpdateExpenseRequest request)
        {
            try { return Ok(await _finance.UpdateExpenseAsync(CurrentUserId, id, request)); }
            catch (System.Collections.Generic.KeyNotFoundException) { return NotFound(new { message = "Expense not found" }); }
        }

        [HttpDelete("expenses/{id:guid}")]
        public async Task<ActionResult> DeleteExpense(Guid id)
        {
            try { await _finance.DeleteExpenseAsync(CurrentUserId, id); return Ok(new { message = "Deleted" }); }
            catch (System.Collections.Generic.KeyNotFoundException) { return NotFound(new { message = "Expense not found" }); }
        }

        // ---- Items ----

        [HttpPut("items/{id:guid}")]
        public async Task<ActionResult> UpdateItem(Guid id, [FromBody] UpdateExpenseItemRequest request)
        {
            try { return Ok(await _finance.UpdateItemAsync(CurrentUserId, id, request)); }
            catch (System.Collections.Generic.KeyNotFoundException) { return NotFound(new { message = "Item not found" }); }
        }

        [HttpDelete("items/{id:guid}")]
        public async Task<ActionResult> DeleteItem(Guid id)
        {
            try { await _finance.DeleteItemAsync(CurrentUserId, id); return Ok(new { message = "Deleted" }); }
            catch (System.Collections.Generic.KeyNotFoundException) { return NotFound(new { message = "Item not found" }); }
        }

        // ---- Canonical names (merge/rename) ----

        [HttpGet("canonicals")]
        public async Task<ActionResult> GetCanonicals()
            => Ok(await _finance.GetCanonicalSummaryAsync(CurrentUserId));

        [HttpPost("canonicals/merge")]
        public async Task<ActionResult> MergeCanonicals([FromBody] MergeCanonicalRequest request)
        {
            try
            {
                var count = await _finance.MergeCanonicalAsync(CurrentUserId, request);
                return Ok(new { merged = count });
            }
            catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
        }

        // ---- Budgets ----

        [HttpGet("budgets")]
        public async Task<ActionResult> GetBudgets() => Ok(await _finance.GetBudgetsAsync(CurrentUserId));

        [HttpPut("budgets")]
        public async Task<ActionResult> SetBudget([FromBody] SetBudgetRequest request)
            => Ok(await _finance.SetBudgetAsync(CurrentUserId, request));

        [HttpDelete("budgets/{id:guid}")]
        public async Task<ActionResult> DeleteBudget(Guid id)
        {
            try { await _finance.DeleteBudgetAsync(CurrentUserId, id); return Ok(new { message = "Deleted" }); }
            catch (System.Collections.Generic.KeyNotFoundException) { return NotFound(new { message = "Budget not found" }); }
        }

        // ---- Dashboard ----

        [HttpGet("dashboard")]
        public async Task<ActionResult> GetDashboard([FromQuery] int? year, [FromQuery] int? month)
            => Ok(await _finance.GetDashboardAsync(CurrentUserId, year, month));

        // ---- Receipt image upload / retrieval ----

        [HttpPost("expenses/{id:guid}/image")]
        [RequestSizeLimit(MaxImageBytes)]
        public async Task<ActionResult> UploadImage(Guid id, IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest(new { message = "No file uploaded" });
            if (file.Length > MaxImageBytes)
                return BadRequest(new { message = "File too large (max 10 MB)" });

            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!AllowedImageExtensions.Contains(extension))
                return BadRequest(new { message = "Unsupported image type" });

            // Confirm ownership before writing anything.
            var expense = await _finance.GetExpenseAsync(CurrentUserId, id);
            if (expense == null)
                return NotFound(new { message = "Expense not found" });

            var relativeDir = Path.Combine("receipts", CurrentUserId.ToString());
            var root = GetStorageRoot();
            var absoluteDir = Path.Combine(root, relativeDir);
            Directory.CreateDirectory(absoluteDir);

            var storedName = $"{id}{extension}";
            var absolutePath = Path.Combine(absoluteDir, storedName);
            await using (var stream = new FileStream(absolutePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            var relativePath = Path.Combine(relativeDir, storedName).Replace('\\', '/');
            _finance.AttachImagePath(CurrentUserId, id, relativePath);

            return Ok(new { imagePath = relativePath });
        }

        [HttpGet("expenses/{id:guid}/image")]
        public async Task<ActionResult> GetImage(Guid id)
        {
            var expense = await _finance.GetExpenseAsync(CurrentUserId, id);
            if (expense?.ImagePath == null)
                return NotFound(new { message = "No image for this expense" });

            var absolutePath = Path.Combine(GetStorageRoot(), expense.ImagePath);
            if (!System.IO.File.Exists(absolutePath))
                return NotFound(new { message = "Image file missing" });

            var contentType = Path.GetExtension(absolutePath).ToLowerInvariant() switch
            {
                ".png" => "image/png",
                ".jpg" or ".jpeg" => "image/jpeg",
                ".gif" => "image/gif",
                ".webp" => "image/webp",
                _ => "application/octet-stream"
            };

            var bytes = await System.IO.File.ReadAllBytesAsync(absolutePath);
            return File(bytes, contentType);
        }

        private string GetStorageRoot()
        {
            // Store under a "storage" folder next to content root; easy to relocate to cloud later.
            var root = Path.Combine(_environment.ContentRootPath, "storage");
            Directory.CreateDirectory(root);
            return root;
        }
    }
}
