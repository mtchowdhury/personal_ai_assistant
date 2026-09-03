import { Component, OnDestroy, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { Subject, debounceTime, distinctUntilChanged, takeUntil } from 'rxjs';
import { FinanceService, ExpenseListItem, ExpenseItemRow, Category, ExpensePeriod } from '@features/application/finance/services/finance.service';
import { NotificationService } from '@core/services/notification.service';
import { ConfirmDialogService } from '@core/services/confirm-dialog.service';
import { LoadingSpinnerComponent } from '@core/components/loading-spinner/loading-spinner.component';

const MONTH_NAMES = [
  'January', 'February', 'March', 'April', 'May', 'June',
  'July', 'August', 'September', 'October', 'November', 'December'
];

@Component({
  selector: 'app-expense-list',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink, LoadingSpinnerComponent],
  templateUrl: './expense-list.component.html',
  styleUrls: ['./expense-list.component.scss']
})
export class ExpenseListComponent implements OnInit, OnDestroy {
  expenses: ExpenseListItem[] = [];
  items: ExpenseItemRow[] = [];
  categories: Category[] = [];
  periods: ExpensePeriod[] = [];

  /** 'expenses' = grouped receipts, 'items' = flat line-item transactions. */
  view: 'expenses' | 'items' = 'expenses';

  /** Selected category ids; empty means "All". */
  activeCategoryIds: string[] = [];
  /** Selected period as "year-month", or '' for all time. */
  activePeriod = '';
  /** Free-text search over shop/remarks and item raw/canonical names. */
  searchTerm = '';

  isLoading = true;

  private readonly searchInput$ = new Subject<string>();
  private readonly destroy$ = new Subject<void>();

  constructor(
    private finance: FinanceService,
    private notification: NotificationService,
    private confirmDialog: ConfirmDialogService,
    private router: Router
  ) {}

  ngOnInit(): void {
    this.finance.getCategories().subscribe({
      next: (c) => (this.categories = c),
      error: () => {}
    });
    this.finance.getExpensePeriods().subscribe({
      next: (p) => (this.periods = p),
      error: () => {}
    });

    this.searchInput$
      .pipe(debounceTime(300), distinctUntilChanged(), takeUntil(this.destroy$))
      .subscribe(() => this.load());

    this.load();
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  onSearchChange(): void {
    this.searchInput$.next(this.searchTerm);
  }

  clearSearch(): void {
    this.searchTerm = '';
    this.load();
  }

  setView(view: 'expenses' | 'items'): void {
    if (this.view === view) return;
    this.view = view;
    this.load();
  }

  load(): void {
    this.isLoading = true;
    const { year, month } = this.parsePeriod(this.activePeriod);
    const search = this.searchTerm.trim() || undefined;
    const categoryIds = this.activeCategoryIds;

    if (this.view === 'items') {
      this.finance.getExpenseItems(categoryIds, 200, year, month, search).subscribe({
        next: (list) => {
          this.items = list;
          this.isLoading = false;
        },
        error: () => {
          this.isLoading = false;
          this.notification.showError('Could not load items.');
        }
      });
      return;
    }

    this.finance.getExpenses(categoryIds, 100, year, month, search).subscribe({
      next: (list) => {
        this.expenses = list;
        this.isLoading = false;
      },
      error: () => {
        this.isLoading = false;
        this.notification.showError('Could not load expenses.');
      }
    });
  }

  private parsePeriod(value: string): { year?: number; month?: number } {
    if (!value) return {};
    const [year, month] = value.split('-').map(Number);
    return { year, month };
  }

  periodValue(p: ExpensePeriod): string {
    return `${p.year}-${p.month}`;
  }

  periodLabel(p: ExpensePeriod): string {
    return `${MONTH_NAMES[p.month - 1]} ${p.year}`;
  }

  /** Label for the total card, describing the active filters. */
  get totalLabel(): string {
    const period = this.activePeriod
      ? this.periodLabel({
          year: Number(this.activePeriod.split('-')[0]),
          month: Number(this.activePeriod.split('-')[1])
        })
      : 'All time';
    const names = this.categories
      .filter(c => this.activeCategoryIds.includes(c.id))
      .map(c => c.name);
    const parts = [period];
    if (names.length > 0) parts.push(names.join(' + '));
    if (this.searchTerm.trim()) parts.push(`"${this.searchTerm.trim()}"`);
    return parts.join(' · ');
  }

  /** Sum of whatever is currently listed (already filtered), for the active view. */
  get total(): number {
    return this.view === 'items'
      ? this.items.reduce((sum, i) => sum + i.lineTotal, 0)
      : this.expenses.reduce((sum, e) => sum + e.totalAmount, 0);
  }

  get totalCurrency(): string {
    if (this.view === 'items') {
      return this.items.length > 0 ? this.items[0].currency : 'EUR';
    }
    return this.expenses.length > 0 ? this.expenses[0].currency : 'EUR';
  }

  /** Row count for the active view. */
  get resultCount(): number {
    return this.view === 'items' ? this.items.length : this.expenses.length;
  }

  get isEmpty(): boolean {
    return this.resultCount === 0;
  }

  isCategoryActive(categoryId: string): boolean {
    return this.activeCategoryIds.includes(categoryId);
  }

  /** Adds or removes a category from the filter; multiple can be active at once. */
  toggleCategory(categoryId: string): void {
    this.activeCategoryIds = this.isCategoryActive(categoryId)
      ? this.activeCategoryIds.filter(id => id !== categoryId)
      : [...this.activeCategoryIds, categoryId];
    this.load();
  }

  clearCategories(): void {
    if (this.activeCategoryIds.length === 0) return;
    this.activeCategoryIds = [];
    this.load();
  }

  onPeriodChange(): void {
    this.load();
  }

  async delete(expense: ExpenseListItem): Promise<void> {
    const name = expense.shopName || expense.notes || expense.categoryName || 'this expense';
    const ok = await this.confirmDialog.confirm({
      title: 'Delete expense',
      message: `Delete ${name} (${expense.totalAmount.toFixed(2)} ${expense.currency})? This cannot be undone.`,
      confirmLabel: 'Delete',
      danger: true
    });
    if (!ok) return;

    this.finance.deleteExpense(expense.id).subscribe({
      next: () => {
        this.notification.showSuccess('Expense deleted.');
        // Reload the periods too — this may have been the last expense in its month.
        this.finance.getExpensePeriods().subscribe({
          next: (p) => (this.periods = p),
          error: () => {}
        });
        this.load();
      },
      error: () => this.notification.showError('Could not delete this expense.')
    });
  }

  open(id: string): void {
    this.router.navigate(['/finance/expenses', id]);
  }

  goBack(): void {
    this.router.navigate(['/finance']);
  }
}
