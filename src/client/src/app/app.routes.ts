import { Routes } from '@angular/router';

import { authGuard } from './core/auth/auth.guard';
import { signedOutGuard } from './core/auth/signed-out.guard';

export const routes: Routes = [
  {
    path: 'sign-in',
    title: 'Sign in',
    canActivate: [signedOutGuard],
    loadComponent: () => import('./features/account/sign-in-page').then((m) => m.SignInPage),
  },
  {
    // Google's redirect URI; the page passes the result to the API (docs/authentication.md).
    path: 'google-callback',
    title: 'Signing in',
    loadComponent: () =>
      import('./features/account/google-callback-page').then((m) => m.GoogleCallbackPage),
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
        path: 'people',
        title: 'Profiles you maintain',
        loadComponent: () => import('./features/people/people-page').then((m) => m.PeoplePage),
      },
      {
        path: 'people/new',
        title: 'Add a person',
        loadComponent: () =>
          import('./features/people/new-person-page').then((m) => m.NewPersonPage),
      },
      {
        path: 'people/:id',
        title: 'Person',
        loadComponent: () => import('./features/people/person-page').then((m) => m.PersonPage),
      },
      {
        path: 'people/:id/graph',
        title: 'Family graph',
        loadComponent: () => import('./features/graph/graph-page').then((m) => m.GraphPage),
      },
      {
        path: 'approvals',
        title: 'Link approvals',
        loadComponent: () =>
          import('./features/relationships/approvals-page').then((m) => m.ApprovalsPage),
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
