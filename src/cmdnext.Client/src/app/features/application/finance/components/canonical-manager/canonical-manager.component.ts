import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { FinanceService, CanonicalSummary } from '@features/application/finance/services/finance.service';
import { NotificationService } from '@core/services/notification.service';
import { ConfirmDialogService } from '@core/services/confirm-dialog.service';
import { LoadingSpinnerComponent } from '@core/components/loading-spinner/loading-spinner.component';

@Component({
  selector: 'app-canonical-manager',
  standalone: true,
  imports: [CommonModule, FormsModule, LoadingSpinnerComponent],
  templateUrl: './canonical-manager.component.html',
  styleUrls: ['./canonical-manager.component.scss']
})
export class CanonicalManagerComponent implements OnInit {
  canonicals: CanonicalSummary[] = [];
  isLoading = true;
  isSaving = false;

  selected = new Set<string>();
  mergeTarget = '';

  constructor(
    private finance: FinanceService,
    private notification: NotificationService,
    private confirmDialog: ConfirmDialogService,
    private router: Router
  ) {}

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.isLoading = true;
    this.finance.getCanonicals().subscribe({
      next: (list) => {
        this.canonicals = list;
        this.isLoading = false;
      },
      error: () => {
        this.isLoading = false;
        this.notification.showError('Could not load item names.');
      }
    });
  }

  /** Only rows that actually have a canonical name can be merged/renamed. */
  named(c: CanonicalSummary): string | null {
    return c.canonicalName && c.canonicalName.trim() ? c.canonicalName : null;
  }

  toggle(name: string): void {
    if (this.selected.has(name)) this.selected.delete(name);
    else this.selected.add(name);
  }

  merge(): void {
    const from = Array.from(this.selected);
    const to = this.mergeTarget.trim().toLowerCase();

    if (from.length === 0) {
      this.notification.showWarning('Select at least one name to merge.');
      return;
    }
    if (!to) {
      this.notification.showWarning('Enter the target name to merge into.');
      return;
    }

    this.isSaving = true;
    this.finance.mergeCanonicals({ from, to }).subscribe({
      next: (res) => {
        this.isSaving = false;
        this.notification.showSuccess(`Merged ${res.merged} item(s) into "${to}".`);
        this.selected.clear();
        this.mergeTarget = '';
        this.load();
      },
      error: () => {
        this.isSaving = false;
        this.notification.showError('Could not merge the selected names.');
      }
    });
  }

  /** Rename a single canonical = merge one source into a new name. */
  async rename(current: string): Promise<void> {
    const to = await this.confirmDialog.prompt({
      title: 'Rename item',
      message: `Rename "${current}" to:`,
      initialValue: current,
      confirmLabel: 'Rename'
    });
    if (to == null) return;
    const target = to.trim().toLowerCase();
    if (!target || target === current) return;

    this.finance.mergeCanonicals({ from: [current], to: target }).subscribe({
      next: () => {
        this.notification.showSuccess(`Renamed "${current}" to "${target}".`);
        this.load();
      },
      error: () => this.notification.showError('Could not rename.')
    });
  }

  goBack(): void {
    this.router.navigate(['/finance']);
  }
}
