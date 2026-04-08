import { Routes } from '@angular/router';
import { authGuard, guestGuard } from './core/guards/auth.guard';

export const routes: Routes = [
  { path: 'login', loadComponent: () => import('./features/login/login.component').then(m => m.LoginComponent), canActivate: [guestGuard] },
  {
    path: '',
    loadComponent: () => import('./layout/main-layout.component').then(m => m.MainLayoutComponent),
    canActivate: [authGuard],
    children: [
      { path: '', pathMatch: 'full', redirectTo: 'dashboard' },
      { path: 'dashboard', loadComponent: () => import('./features/dashboard/dashboard.component').then(m => m.DashboardComponent) },
      { path: 'customers', loadComponent: () => import('./features/customers/customers-list.component').then(m => m.CustomersListComponent) },
      { path: 'customers/new', loadComponent: () => import('./features/customers/customer-form.component').then(m => m.CustomerFormComponent) },
      { path: 'customers/:id', loadComponent: () => import('./features/customers/customer-detail.component').then(m => m.CustomerDetailComponent) },
      { path: 'customers/:id/edit', loadComponent: () => import('./features/customers/customer-form.component').then(m => m.CustomerFormComponent) },
      { path: 'kasa', loadComponent: () => import('./features/kasa/kasa.component').then(m => m.KasaComponent) },
      { path: 'currency-exchange', loadComponent: () => import('./features/currency-exchange/currency-exchange.component').then(m => m.CurrencyExchangeComponent) },
      { path: 'linking-history', loadComponent: () => import('./features/linking/linking-history.component').then(m => m.LinkingHistoryComponent) },
      { path: 'transactions', loadComponent: () => import('./features/transactions/transactions-list.component').then(m => m.TransactionsListComponent) },
      { path: 'transactions/new', loadComponent: () => import('./features/transactions/transaction-form.component').then(m => m.TransactionFormComponent) },
      { path: 'transactions/:id/edit', loadComponent: () => import('./features/transactions/transaction-form.component').then(m => m.TransactionFormComponent) },
      { path: 'settings/products', loadComponent: () => import('./features/settings/product-templates-page.component').then(m => m.ProductTemplatesPageComponent) },
    ],
  },
  { path: '**', redirectTo: '' },
];
