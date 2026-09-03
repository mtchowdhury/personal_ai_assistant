import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { HttpClient } from '@angular/common/http';
import { FinanceService, Expense } from '@features/application/finance/services/finance.service';
import { NotificationService } from '@core/services/notification.service';
import { ConfirmDialogService } from '@core/services/confirm-dialog.service';
import { LoadingSpinnerComponent } from '@core/components/loading-spinner/loading-spinner.component';

@Component({
  selector: 'app-expense-detail',
  standalone: true,
  imports: [CommonModule, RouterLink, LoadingSpinnerComponent],
  templateUrl: './expense-detail.component.html',
  styleUrls: ['./expense-detail.component.scss']
})
export class ExpenseDetailComponent implements OnInit {
  expense: Expense | null = null;
  isLoading = true;
  imageObjectUrl: string | null = null;

  constructor(
    private finance: FinanceService,
    private notification: NotificationService,
    private confirmDialog: ConfirmDialogService,
    private route: ActivatedRoute,
    private router: Router,
    private http: HttpClient
  ) {}

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');
    if (!id) {
      this.router.navigate(['/finance/expenses']);
      return;
    }
    this.finance.getExpense(id).subscribe({
      next: (e) => {
        this.expense = e;
        this.isLoading = false;
        if (e.imagePath) this.loadImage(e.id);
      },
      error: () => {
        this.isLoading = false;
        this.notification.showError('Could not load this expense.');
      }
    });
  }

  /** Fetch the receipt image through the authorized endpoint (interceptor adds the token). */
  private loadImage(id: string): void {
    this.http.get(this.finance.imageUrl(id), { responseType: 'blob' }).subscribe({
      next: (blob) => (this.imageObjectUrl = URL.createObjectURL(blob)),
      error: () => {}
    });
  }

  async delete(): Promise<void> {
    if (!this.expense) return;
    const ok = await this.confirmDialog.confirm({
      title: 'Delete expense',
      message: 'Delete this expense? This cannot be undone.',
      confirmLabel: 'Delete',
      danger: true
    });
    if (!ok) return;

    this.finance.deleteExpense(this.expense.id).subscribe({
      next: () => {
        this.notification.showSuccess('Expense deleted.');
        this.router.navigate(['/finance/expenses']);
      },
      error: () => this.notification.showError('Could not delete this expense.')
    });
  }

  goBack(): void {
    this.router.navigate(['/finance/expenses']);
  }
}
