import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterLink } from '@angular/router';
import { FinanceService, FinanceDashboard, CategorySpend } from '@features/application/finance/services/finance.service';
import { NotificationService } from '@core/services/notification.service';
import { LoadingSpinnerComponent } from '@core/components/loading-spinner/loading-spinner.component';

@Component({
  selector: 'app-finance-dashboard',
  standalone: true,
  imports: [CommonModule, RouterLink, LoadingSpinnerComponent],
  templateUrl: './finance-dashboard.component.html',
  styleUrls: ['./finance-dashboard.component.scss']
})
export class FinanceDashboardComponent implements OnInit {
  dashboard: FinanceDashboard | null = null;
  isLoading = true;

  private readonly monthNames = [
    'January', 'February', 'March', 'April', 'May', 'June',
    'July', 'August', 'September', 'October', 'November', 'December'
  ];

  constructor(
    private finance: FinanceService,
    private notification: NotificationService,
    private router: Router
  ) {}

  ngOnInit(): void {
    this.load();
  }

  private load(): void {
    this.isLoading = true;
    this.finance.getDashboard().subscribe({
      next: (d) => {
        this.dashboard = d;
        this.isLoading = false;
      },
      error: () => {
        // Don't break the page if finance data can't load.
        this.isLoading = false;
        this.notification.showError('Could not load the finance dashboard.');
      }
    });
  }

  get monthLabel(): string {
    if (!this.dashboard) return '';
    return `${this.monthNames[this.dashboard.month - 1]} ${this.dashboard.year}`;
  }

  /** Percentage of budget used, clamped to 100 for the bar width. */
  usedPercent(c: CategorySpend): number {
    if (!c.budgetLimit || c.budgetLimit <= 0) return 0;
    return Math.min(100, Math.round((c.spent / c.budgetLimit) * 100));
  }

  goBack(): void {
    this.router.navigate(['/dashboard']);
  }
}
