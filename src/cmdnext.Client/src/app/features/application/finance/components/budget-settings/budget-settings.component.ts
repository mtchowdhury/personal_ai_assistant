import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { FinanceService, Budget, Category } from '@features/application/finance/services/finance.service';
import { NotificationService } from '@core/services/notification.service';
import { LoadingSpinnerComponent } from '@core/components/loading-spinner/loading-spinner.component';
import { forkJoin } from 'rxjs';

interface CategoryBudgetRow {
  category: Category;
  budget: Budget | null;
  limit: number | null;
}

@Component({
  selector: 'app-budget-settings',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink, LoadingSpinnerComponent],
  templateUrl: './budget-settings.component.html',
  styleUrls: ['./budget-settings.component.scss']
})
export class BudgetSettingsComponent implements OnInit {
  rows: CategoryBudgetRow[] = [];
  currency = 'EUR';
  isLoading = true;
  savingCategoryId: string | null = null;

  constructor(
    private finance: FinanceService,
    private notification: NotificationService,
    private router: Router
  ) {}

  ngOnInit(): void {
    this.isLoading = true;
    forkJoin({
      categories: this.finance.getCategories(),
      budgets: this.finance.getBudgets()
    }).subscribe({
      next: ({ categories, budgets }) => {
        if (budgets.length > 0) this.currency = budgets[0].currency;
        this.rows = categories.map(cat => {
          const budget = budgets.find(b => b.categoryId === cat.id) ?? null;
          return { category: cat, budget, limit: budget ? budget.monthlyLimit : null };
        });
        this.isLoading = false;
      },
      error: () => {
        this.isLoading = false;
        this.notification.showError('Could not load budgets.');
      }
    });
  }

  save(row: CategoryBudgetRow): void {
    if (row.limit == null || row.limit < 0) {
      this.notification.showWarning('Enter a valid amount.');
      return;
    }
    this.savingCategoryId = row.category.id;
    this.finance.setBudget({ categoryId: row.category.id, monthlyLimit: Number(row.limit), currency: this.currency }).subscribe({
      next: (b) => {
        row.budget = b;
        this.savingCategoryId = null;
        this.notification.showSuccess(`${row.category.name} budget saved.`);
      },
      error: () => {
        this.savingCategoryId = null;
        this.notification.showError('Could not save the budget.');
      }
    });
  }

  clear(row: CategoryBudgetRow): void {
    if (!row.budget) { row.limit = null; return; }
    this.finance.deleteBudget(row.budget.id).subscribe({
      next: () => {
        row.budget = null;
        row.limit = null;
        this.notification.showSuccess(`${row.category.name} budget removed.`);
      },
      error: () => this.notification.showError('Could not remove the budget.')
    });
  }

  goBack(): void {
    this.router.navigate(['/finance']);
  }
}
