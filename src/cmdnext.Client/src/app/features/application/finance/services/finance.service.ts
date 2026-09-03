import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '@environments/environment';

export interface Category {
  id: string;
  name: string;
  partyLabel?: string | null;
  showPartyField: boolean;
  isProtected: boolean;
}

export interface CreateCategoryRequest {
  name: string;
  partyLabel?: string | null;
  showPartyField: boolean;
}

export interface UpdateCategoryRequest {
  name?: string | null;
  partyLabel?: string | null;
  showPartyField?: boolean | null;
}

export interface ExpenseItem {
  id: string;
  rawName: string;
  canonicalName?: string | null;
  categoryId?: string | null;
  categoryName?: string | null;
  quantity: number;
  unitPrice: number;
  lineTotal: number;
  notes?: string | null;
}

export interface Expense {
  id: string;
  shopName?: string | null;
  purchasedOn: string;
  totalAmount: number;
  currency: string;
  categoryId?: string | null;
  categoryName?: string | null;
  paymentMethod?: string | null;
  notes?: string | null;
  imagePath?: string | null;
  source: string;
  createdOn?: string | null;
  items: ExpenseItem[];
}

export interface ExpenseListItem {
  id: string;
  shopName?: string | null;
  purchasedOn: string;
  totalAmount: number;
  currency: string;
  categoryId?: string | null;
  categoryName?: string | null;
  notes?: string | null;
  source: string;
  itemCount: number;
  hasImage: boolean;
}

/** A single line item flattened with its parent expense's context. */
export interface ExpenseItemRow {
  id: string;
  expenseId: string;
  rawName: string;
  canonicalName?: string | null;
  quantity: number;
  unitPrice: number;
  lineTotal: number;
  notes?: string | null;
  purchasedOn: string;
  shopName?: string | null;
  currency: string;
  categoryId?: string | null;
  categoryName?: string | null;
  source: string;
}

export interface ExpensePeriod {
  year: number;
  month: number;
}

export interface CategorySpend {
  categoryId?: string | null;
  categoryName: string;
  spent: number;
  budgetLimit?: number | null;
  overBudget: boolean;
}

export interface FinanceDashboard {
  monthTotal: number;
  currency: string;
  monthExpenseCount: number;
  year: number;
  month: number;
  categories: CategorySpend[];
  totalBudget: number;
  anyOverBudget: boolean;
}

export interface CreateExpenseItemRequest {
  rawName: string;
  canonicalName?: string | null;
  categoryId?: string | null;
  quantity: number;
  unitPrice: number;
  lineTotal?: number | null;
  notes?: string | null;
}

export interface CreateExpenseRequest {
  shopName?: string | null;
  purchasedOn?: string | null;
  totalAmount?: number | null;
  currency?: string | null;
  categoryId?: string | null;
  paymentMethod?: string | null;
  notes?: string | null;
  items: CreateExpenseItemRequest[];
}

export interface Budget {
  id: string;
  categoryId: string;
  categoryName: string;
  monthlyLimit: number;
  currency: string;
}

export interface SetBudgetRequest {
  categoryId: string;
  monthlyLimit: number;
  currency?: string | null;
}

export interface CanonicalSummary {
  canonicalName?: string | null;
  itemCount: number;
  totalSpent: number;
}

export interface MergeCanonicalRequest {
  from: string[];
  to: string;
}

export interface UpdateExpenseItemRequest {
  rawName?: string | null;
  canonicalName?: string | null;
  categoryId?: string | null;
  quantity?: number | null;
  unitPrice?: number | null;
  lineTotal?: number | null;
  notes?: string | null;
}

export interface UpdateExpenseRequest {
  shopName?: string | null;
  purchasedOn?: string | null;
  totalAmount?: number | null;
  currency?: string | null;
  categoryId?: string | null;
  paymentMethod?: string | null;
  notes?: string | null;
  /** When provided, fully replaces the expense's line items. Omit to leave items unchanged. */
  items?: CreateExpenseItemRequest[] | null;
}

@Injectable({ providedIn: 'root' })
export class FinanceService {
  private readonly baseUrl = `${environment.apiUrl}/Finance`;

  constructor(private http: HttpClient) {}

  // ---- Categories ----
  getCategories(): Observable<Category[]> {
    return this.http.get<Category[]>(`${this.baseUrl}/categories`);
  }

  createCategory(request: CreateCategoryRequest): Observable<Category> {
    return this.http.post<Category>(`${this.baseUrl}/categories`, request);
  }

  updateCategory(id: string, request: UpdateCategoryRequest): Observable<Category> {
    return this.http.put<Category>(`${this.baseUrl}/categories/${id}`, request);
  }

  deleteCategory(id: string): Observable<unknown> {
    return this.http.delete(`${this.baseUrl}/categories/${id}`);
  }

  getDashboard(year?: number, month?: number): Observable<FinanceDashboard> {
    const params: string[] = [];
    if (year) params.push(`year=${year}`);
    if (month) params.push(`month=${month}`);
    const q = params.length ? `?${params.join('&')}` : '';
    return this.http.get<FinanceDashboard>(`${this.baseUrl}/dashboard${q}`);
  }

  getExpenses(
    categoryIds?: string[],
    take = 100,
    year?: number,
    month?: number,
    search?: string
  ): Observable<ExpenseListItem[]> {
    const params: string[] = [`take=${take}`];
    for (const id of categoryIds ?? []) params.push(`categoryId=${encodeURIComponent(id)}`);
    if (year) params.push(`year=${year}`);
    if (month) params.push(`month=${month}`);
    if (search) params.push(`search=${encodeURIComponent(search)}`);
    return this.http.get<ExpenseListItem[]>(`${this.baseUrl}/expenses?${params.join('&')}`);
  }

  /** Flat line-item view using the same filters as the expense list. */
  getExpenseItems(
    categoryIds?: string[],
    take = 200,
    year?: number,
    month?: number,
    search?: string
  ): Observable<ExpenseItemRow[]> {
    const params: string[] = [`take=${take}`];
    for (const id of categoryIds ?? []) params.push(`categoryId=${encodeURIComponent(id)}`);
    if (year) params.push(`year=${year}`);
    if (month) params.push(`month=${month}`);
    if (search) params.push(`search=${encodeURIComponent(search)}`);
    return this.http.get<ExpenseItemRow[]>(`${this.baseUrl}/items?${params.join('&')}`);
  }

  /** Year/month periods that actually have expenses, newest first. */
  getExpensePeriods(): Observable<ExpensePeriod[]> {
    return this.http.get<ExpensePeriod[]>(`${this.baseUrl}/expenses/periods`);
  }

  getExpense(id: string): Observable<Expense> {
    return this.http.get<Expense>(`${this.baseUrl}/expenses/${id}`);
  }

  createExpense(request: CreateExpenseRequest): Observable<Expense> {
    return this.http.post<Expense>(`${this.baseUrl}/expenses`, request);
  }

  updateExpense(id: string, request: UpdateExpenseRequest): Observable<Expense> {
    return this.http.put<Expense>(`${this.baseUrl}/expenses/${id}`, request);
  }

  deleteExpense(id: string): Observable<unknown> {
    return this.http.delete(`${this.baseUrl}/expenses/${id}`);
  }

  // ---- Budgets ----
  getBudgets(): Observable<Budget[]> {
    return this.http.get<Budget[]>(`${this.baseUrl}/budgets`);
  }

  setBudget(request: SetBudgetRequest): Observable<Budget> {
    return this.http.put<Budget>(`${this.baseUrl}/budgets`, request);
  }

  deleteBudget(id: string): Observable<unknown> {
    return this.http.delete(`${this.baseUrl}/budgets/${id}`);
  }

  // ---- Canonicals ----
  getCanonicals(): Observable<CanonicalSummary[]> {
    return this.http.get<CanonicalSummary[]>(`${this.baseUrl}/canonicals`);
  }

  mergeCanonicals(request: MergeCanonicalRequest): Observable<{ merged: number }> {
    return this.http.post<{ merged: number }>(`${this.baseUrl}/canonicals/merge`, request);
  }

  // ---- Items ----
  updateItem(id: string, request: UpdateExpenseItemRequest): Observable<ExpenseItem> {
    return this.http.put<ExpenseItem>(`${this.baseUrl}/items/${id}`, request);
  }

  deleteItem(id: string): Observable<unknown> {
    return this.http.delete(`${this.baseUrl}/items/${id}`);
  }

  /** Absolute URL for a receipt image (goes through the authorized endpoint). */
  imageUrl(id: string): string {
    return `${this.baseUrl}/expenses/${id}/image`;
  }
}
