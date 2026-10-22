import { Routes } from '@angular/router';

import { authGuard } from './core/auth/auth.guard';

export const routes: Routes = [
  {
    path: 'sign-in',
    title: 'Sign in',
    loadComponent: () => import('./features/account/sign-in-page').then((m) => m.SignInPage),
  },
  {
    path: '',
    canActivateChild: [authGuard],
    children: [
      {
        path: '',
        title: 'FamilyGraph',
        loadComponent: () => import('./features/home/home-page').then((m) => m.HomePage),
      },
      {
        path: 'account',
        title: 'Your account',
        loadComponent: () => import('./features/account/account-page').then((m) => m.AccountPage),
      },
    ],
  },
  { path: '**', redirectTo: '' },
];
