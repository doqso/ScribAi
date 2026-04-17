import { Routes } from '@angular/router';
import { authGuard } from './core/auth.guard';
import { adminGuard } from './core/admin.guard';

export const routes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'extractions' },
  {
    path: 'login',
    loadComponent: () => import('./features/login/login.component').then(m => m.LoginComponent)
  },
  {
    path: '',
    canActivate: [authGuard],
    loadComponent: () => import('./features/layout/layout.component').then(m => m.LayoutComponent),
    children: [
      {
        path: 'extractions',
        loadComponent: () => import('./features/extractions/extractions-list.component').then(m => m.ExtractionsListComponent)
      },
      {
        path: 'extractions/new',
        loadComponent: () => import('./features/extractions/extraction-upload.component').then(m => m.ExtractionUploadComponent)
      },
      {
        path: 'extractions/:id',
        loadComponent: () => import('./features/extractions/extraction-detail.component').then(m => m.ExtractionDetailComponent)
      },
      {
        path: 'schemas',
        loadComponent: () => import('./features/schemas/schemas-list.component').then(m => m.SchemasListComponent)
      },
      {
        path: 'webhooks',
        loadComponent: () => import('./features/webhooks/webhooks-list.component').then(m => m.WebhooksListComponent)
      },
      {
        path: 'keys',
        loadComponent: () => import('./features/keys/keys-list.component').then(m => m.KeysListComponent)
      },
      {
        path: 'settings',
        canActivate: [adminGuard],
        loadComponent: () => import('./features/settings/settings.component').then(m => m.SettingsComponent)
      }
    ]
  },
  { path: '**', redirectTo: 'extractions' }
];
