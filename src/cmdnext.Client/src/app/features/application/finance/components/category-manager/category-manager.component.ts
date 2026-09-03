import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, Validators, ReactiveFormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { FinanceService, Category } from '@features/application/finance/services/finance.service';
import { NotificationService } from '@core/services/notification.service';
import { ConfirmDialogService } from '@core/services/confirm-dialog.service';
import { LoadingSpinnerComponent } from '@core/components/loading-spinner/loading-spinner.component';

@Component({
  selector: 'app-category-manager',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, LoadingSpinnerComponent],
  templateUrl: './category-manager.component.html',
  styleUrls: ['./category-manager.component.scss']
})
export class CategoryManagerComponent implements OnInit {
  categories: Category[] = [];
  isLoading = true;
  isSaving = false;

  showAddForm = false;
  addForm: FormGroup;

  editingId: string | null = null;
  editForm: FormGroup | null = null;

  constructor(
    private fb: FormBuilder,
    private finance: FinanceService,
    private notification: NotificationService,
    private confirmDialog: ConfirmDialogService,
    private router: Router
  ) {
    this.addForm = this.fb.group({
      name: ['', Validators.required],
      partyLabel: [''],
      showPartyField: [true]
    });
  }

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.isLoading = true;
    this.finance.getCategories().subscribe({
      next: (list) => {
        this.categories = list;
        this.isLoading = false;
      },
      error: () => {
        this.isLoading = false;
        this.notification.showError('Could not load categories.');
      }
    });
  }

  openAddForm(): void {
    this.showAddForm = true;
    this.addForm.reset({ name: '', partyLabel: '', showPartyField: true });
  }

  cancelAdd(): void {
    this.showAddForm = false;
  }

  addCategory(): void {
    if (this.addForm.invalid) {
      this.addForm.markAllAsTouched();
      return;
    }
    const v = this.addForm.value;
    this.isSaving = true;
    this.finance.createCategory({
      name: v.name.trim(),
      partyLabel: v.showPartyField ? (v.partyLabel || null) : null,
      showPartyField: v.showPartyField
    }).subscribe({
      next: () => {
        this.isSaving = false;
        this.showAddForm = false;
        this.notification.showSuccess('Category added.');
        this.load();
      },
      error: (err) => {
        this.isSaving = false;
        this.notification.showError(err?.error?.message || 'Could not add the category.');
      }
    });
  }

  startEdit(c: Category): void {
    this.editingId = c.id;
    this.editForm = this.fb.group({
      name: [c.name, Validators.required],
      partyLabel: [c.partyLabel || ''],
      showPartyField: [c.showPartyField]
    });
  }

  cancelEdit(): void {
    this.editingId = null;
    this.editForm = null;
  }

  saveEdit(c: Category): void {
    if (!this.editForm || this.editForm.invalid) {
      this.editForm?.markAllAsTouched();
      return;
    }
    const v = this.editForm.value;
    this.isSaving = true;
    this.finance.updateCategory(c.id, {
      name: v.name.trim(),
      partyLabel: v.showPartyField ? (v.partyLabel || null) : null,
      showPartyField: v.showPartyField
    }).subscribe({
      next: () => {
        this.isSaving = false;
        this.cancelEdit();
        this.notification.showSuccess('Category updated.');
        this.load();
      },
      error: (err) => {
        this.isSaving = false;
        this.notification.showError(err?.error?.message || 'Could not update the category.');
      }
    });
  }

  async deleteCategory(c: Category): Promise<void> {
    if (c.isProtected) return;
    const ok = await this.confirmDialog.confirm({
      title: 'Delete category',
      message: `Delete category "${c.name}"? This only works if no expenses use it.`,
      confirmLabel: 'Delete',
      danger: true
    });
    if (!ok) return;

    this.finance.deleteCategory(c.id).subscribe({
      next: () => {
        this.notification.showSuccess(`"${c.name}" deleted.`);
        this.load();
      },
      error: (err) => {
        this.notification.showError(err?.error?.message || 'Could not delete the category.');
      }
    });
  }

  goBack(): void {
    this.router.navigate(['/finance']);
  }
}
