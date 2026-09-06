import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { FormBuilder, FormGroup, FormArray, Validators, ReactiveFormsModule } from '@angular/forms';
import { FinanceService, CreateExpenseRequest, UpdateExpenseRequest, Category } from '@features/application/finance/services/finance.service';
import { NotificationService } from '@core/services/notification.service';
import { LoadingSpinnerComponent } from '@core/components/loading-spinner/loading-spinner.component';

const FALLBACK_CATEGORIES: Category[] = [
  { id: '', name: 'Groceries', partyLabel: 'Shop', showPartyField: true, isProtected: false },
  { id: '', name: 'Dining', partyLabel: 'Restaurant', showPartyField: true, isProtected: false },
  { id: '', name: 'Transport', partyLabel: 'Provider', showPartyField: true, isProtected: false },
  { id: '', name: 'Household', partyLabel: null, showPartyField: false, isProtected: false },
  { id: '', name: 'Health', partyLabel: 'Doctor/Hospital', showPartyField: true, isProtected: false },
  { id: '', name: 'Other', partyLabel: null, showPartyField: false, isProtected: true }
];

@Component({
  selector: 'app-expense-form',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, RouterLink, LoadingSpinnerComponent],
  templateUrl: './expense-form.component.html',
  styleUrls: ['./expense-form.component.scss']
})
export class ExpenseFormComponent implements OnInit {
  form: FormGroup;
  categories: Category[] = [];
  isSaving = false;
  isLoading = false;

  /** Non-null in edit mode: the id of the expense being edited. */
  expenseId: string | null = null;

  get isEditMode(): boolean {
    return this.expenseId !== null;
  }

  constructor(
    private fb: FormBuilder,
    private finance: FinanceService,
    private notification: NotificationService,
    private route: ActivatedRoute,
    private router: Router
  ) {
    this.form = this.fb.group({
      categoryId: [''],
      shopName: [''],
      remarks: [''],
      purchasedOn: [this.today(), Validators.required],
      currency: ['EUR'],
      totalAmount: [null],
      items: this.fb.array([])
    });
  }

  ngOnInit(): void {
    this.expenseId = this.route.snapshot.paramMap.get('id');

    this.finance.getCategories().subscribe({
      next: (c) => {
        this.categories = c;
        if (!this.isEditMode && c.length > 0) this.form.patchValue({ categoryId: c[0].id });
      },
      error: () => (this.categories = FALLBACK_CATEGORIES)
    });

    if (this.isEditMode) {
      this.loadExpense(this.expenseId!);
    } else {
      this.addItem();
    }
  }

  private loadExpense(id: string): void {
    this.isLoading = true;
    this.finance.getExpense(id).subscribe({
      next: (e) => {
        this.form.patchValue({
          categoryId: e.categoryId || '',
          shopName: e.shopName || '',
          remarks: e.shopName ? '' : (e.notes || ''),
          purchasedOn: e.purchasedOn ? e.purchasedOn.slice(0, 10) : this.today(),
          currency: e.currency || 'EUR',
          totalAmount: e.totalAmount ?? null
        });

        this.items.clear();
        for (const item of e.items) {
          this.items.push(this.fb.group({
            rawName: [item.rawName, Validators.required],
            canonicalName: [item.canonicalName || ''],
            quantity: [item.quantity, [Validators.required, Validators.min(0)]],
            unitPrice: [item.unitPrice, [Validators.required, Validators.min(0)]],
            notes: [item.notes || '']
          }));
        }
        if (this.items.length === 0) this.addItem();

        this.isLoading = false;
      },
      error: () => {
        this.isLoading = false;
        this.notification.showError('Could not load this expense.');
        this.router.navigate(['/finance/expenses']);
      }
    });
  }

  private get selectedCategory(): Category | undefined {
    const id = this.form.get('categoryId')?.value;
    return this.categories.find(c => c.id === id);
  }

  /** Label for the shop/provider field for the currently selected category, or null if it should be hidden. */
  get shopFieldLabel(): string | null {
    const c = this.selectedCategory;
    if (!c || !c.showPartyField) return null;
    return c.partyLabel || 'Shop';
  }

  get shopFieldPlaceholder(): string {
    return this.shopFieldLabel ? `e.g. ...` : '';
  }

  get showShopField(): boolean {
    return this.selectedCategory?.showPartyField ?? true;
  }

  get items(): FormArray {
    return this.form.get('items') as FormArray;
  }

  addItem(): void {
    this.items.push(this.fb.group({
      rawName: ['', Validators.required],
      canonicalName: [''],
      quantity: [1, [Validators.required, Validators.min(0)]],
      unitPrice: [0, [Validators.required, Validators.min(0)]],
      notes: ['']
    }));
  }

  removeItem(index: number): void {
    this.items.removeAt(index);
  }

  /**
   * Reads a numeric form value that may have been typed with a decimal comma.
   *
   * A `type="number"` input under a German locale hands back "0,376" / "28,09",
   * which Number() turns into NaN and parseFloat() silently truncates to 28.
   * Both failure modes corrupted line totals and the total override, so every
   * read of a numeric control goes through here.
   */
  private toNumber(value: unknown): number {
    if (typeof value === 'number') return Number.isFinite(value) ? value : 0;
    if (value === null || value === undefined) return 0;

    const raw = String(value).trim();
    if (!raw) return 0;

    // Strip thousands separators, then normalise the decimal mark to a dot.
    // "1.234,56" -> "1234.56";  "1,234.56" -> "1234.56";  "28,09" -> "28.09"
    const lastComma = raw.lastIndexOf(',');
    const lastDot = raw.lastIndexOf('.');
    let normalised: string;

    if (lastComma > -1 && lastDot > -1) {
      normalised = lastComma > lastDot
        ? raw.replace(/\./g, '').replace(',', '.')
        : raw.replace(/,/g, '');
    } else if (lastComma > -1) {
      // A lone comma is a decimal mark, except when it groups exactly three
      // trailing digits ("1,234"). A leading "0," is always a decimal, so a
      // quantity like "0,376" is not mistaken for 376.
      const groups = /^[1-9]\d{0,2}(,\d{3})+$/.test(raw);
      normalised = groups ? raw.replace(/,/g, '') : raw.replace(',', '.');
    } else {
      normalised = raw;
    }

    const parsed = Number(normalised);
    return Number.isFinite(parsed) ? parsed : 0;
  }

  /** Rounds to cents so accumulated float error never reaches the payload. */
  private round2(value: number): number {
    return Math.round((value + Number.EPSILON) * 100) / 100;
  }

  lineTotal(index: number): number {
    const g = this.items.at(index);
    const q = this.toNumber(g.get('quantity')?.value);
    const p = this.toNumber(g.get('unitPrice')?.value);
    return this.round2(q * p);
  }

  get computedTotal(): number {
    let sum = 0;
    for (let i = 0; i < this.items.length; i++) sum += this.lineTotal(i);
    return this.round2(sum);
  }

  /** The override the user typed, or null when the field is blank. */
  get totalOverride(): number | null {
    const raw = this.form.get('totalAmount')?.value;
    if (raw === null || raw === undefined || String(raw).trim() === '') return null;
    return this.round2(this.toNumber(raw));
  }

  /** True when an explicit total is set and disagrees with the item sum. */
  get totalMismatch(): boolean {
    const override = this.totalOverride;
    return override !== null && Math.abs(override - this.computedTotal) >= 0.01;
  }

  /** Signed gap between the typed total and the item sum. */
  get totalDifference(): number {
    const override = this.totalOverride;
    return override === null ? 0 : this.round2(override - this.computedTotal);
  }

  /** Replaces the override with the current item sum. */
  useItemSum(): void {
    this.form.get('totalAmount')?.setValue(this.computedTotal);
  }

  save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      this.notification.showWarning('Please fill in the required fields.');
      return;
    }

    const v = this.form.value;
    const items = (v.items as any[]).map(i => ({
      rawName: i.rawName,
      canonicalName: i.canonicalName || null,
      categoryId: v.categoryId || null,
      quantity: this.toNumber(i.quantity),
      unitPrice: this.toNumber(i.unitPrice),
      lineTotal: this.round2(this.toNumber(i.quantity) * this.toNumber(i.unitPrice)),
      notes: i.notes || null
    }));

    this.isSaving = true;

    if (this.isEditMode) {
      const request: UpdateExpenseRequest = {
        shopName: this.showShopField ? (v.shopName || null) : null,
        purchasedOn: v.purchasedOn ? new Date(v.purchasedOn).toISOString() : null,
        categoryId: v.categoryId || null,
        currency: v.currency || 'EUR',
        totalAmount: this.totalOverride,
        notes: this.showShopField ? null : (v.remarks || null),
        items
      };
      this.finance.updateExpense(this.expenseId!, request).subscribe({
        next: () => {
          this.isSaving = false;
          this.notification.showSuccess('Expense updated.');
          this.router.navigate(['/finance/expenses']);
        },
        error: () => {
          this.isSaving = false;
          this.notification.showError('Could not update the expense.');
        }
      });
    } else {
      const request: CreateExpenseRequest = {
        shopName: this.showShopField ? (v.shopName || null) : null,
        purchasedOn: v.purchasedOn ? new Date(v.purchasedOn).toISOString() : null,
        categoryId: v.categoryId || null,
        currency: v.currency || 'EUR',
        totalAmount: this.totalOverride,
        notes: this.showShopField ? null : (v.remarks || null),
        items
      };
      this.finance.createExpense(request).subscribe({
        next: (created) => {
          this.isSaving = false;
          this.notification.showSuccess('Expense saved.');
          this.router.navigate(['/finance/expenses', created.id]);
        },
        error: () => {
          this.isSaving = false;
          this.notification.showError('Could not save the expense.');
        }
      });
    }
  }

  cancel(): void {
    if (this.isEditMode) {
      this.router.navigate(['/finance/expenses', this.expenseId]);
    } else {
      this.router.navigate(['/finance/expenses']);
    }
  }

  private today(): string {
    return new Date().toISOString().slice(0, 10);
  }
}
