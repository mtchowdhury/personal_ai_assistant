import { Routes } from '@angular/router';

export const routes: Routes = [
  {
    path: '',
    loadComponent: () =>
      import('./components/finance-dashboard/finance-dashboard.component')
        .then(m => m.FinanceDashboardComponent),
    data: { title: 'Finance' }
  },
  {
    path: 'expenses/new',
    loadComponent: () =>
      import('./components/expense-form/expense-form.component')
        .then(m => m.ExpenseFormComponent),
    data: { title: 'Add Expense' }
  },
  {
    path: 'expenses',
    loadComponent: () =>
      import('./components/expense-list/expense-list.component')
        .then(m => m.ExpenseListComponent),
    data: { title: 'Expenses' }
  },
  {
    path: 'items',
    loadComponent: () =>
      import('./components/canonical-manager/canonical-manager.component')
        .then(m => m.CanonicalManagerComponent),
    data: { title: 'Item Names' }
  },
  {
    path: 'categories',
    loadComponent: () =>
      import('./components/category-manager/category-manager.component')
        .then(m => m.CategoryManagerComponent),
    data: { title: 'Categories' }
  },
  {
    path: 'budgets',
    loadComponent: () =>
      import('./components/budget-settings/budget-settings.component')
        .then(m => m.BudgetSettingsComponent),
    data: { title: 'Budgets' }
  },
  {
    path: 'expenses/:id/edit',
    loadComponent: () =>
      import('./components/expense-form/expense-form.component')
        .then(m => m.ExpenseFormComponent),
    data: { title: 'Edit Expense' }
  },
  {
    path: 'expenses/:id',
    loadComponent: () =>
      import('./components/expense-detail/expense-detail.component')
        .then(m => m.ExpenseDetailComponent),
    data: { title: 'Receipt' }
  }
];
