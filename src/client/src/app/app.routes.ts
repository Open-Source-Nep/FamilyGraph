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
        path: 'account',
        title: 'Your account',
        loadComponent: () => import('./features/account/account-page').then((m) => m.AccountPage),
      },
    ],
  },
  { path: '**', redirectTo: '' },
];
