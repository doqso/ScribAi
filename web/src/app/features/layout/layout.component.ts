import { Component, inject } from '@angular/core';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { AuthService } from '../../core/auth.service';

@Component({
  selector: 'app-layout',
  imports: [RouterOutlet, RouterLink, RouterLinkActive],
  template: `
    <aside>
      <h2>ScribAi</h2>
      <nav>
        <a routerLink="/extractions" routerLinkActive="active">Extracciones</a>
        <a routerLink="/schemas" routerLinkActive="active">Schemas</a>
        <a routerLink="/webhooks" routerLinkActive="active">Webhooks</a>
        <a routerLink="/keys" routerLinkActive="active">API Keys</a>
      </nav>
      <button class="logout" (click)="logout()">Cerrar sesión</button>
    </aside>
    <main><router-outlet /></main>
  `,
  styles: [`
    :host { display:grid; grid-template-columns:240px 1fr; min-height:100vh; background:#0e0e10; color:#e6e6e6; }
    aside { background:#161618; padding:1.25rem 1rem; display:flex; flex-direction:column; gap:0.75rem; border-right:1px solid #2a2a2e; }
    h2 { margin:0 0 1rem 0; }
    nav { display:flex; flex-direction:column; gap:0.25rem; flex:1; }
    nav a { padding:0.5rem 0.75rem; color:#aaa; text-decoration:none; border-radius:6px; font-size:0.95rem; }
    nav a.active, nav a:hover { background:#24242a; color:#fff; }
    .logout { background:transparent; color:#888; border:1px solid #2a2a2e; padding:0.5rem; border-radius:6px; cursor:pointer; }
    .logout:hover { color:#fff; border-color:#444; }
    main { padding:1.5rem 2rem; overflow:auto; }
  `]
})
export class LayoutComponent {
  private auth = inject(AuthService);
  private router = inject(Router);
  logout() { this.auth.clear(); this.router.navigateByUrl('/login'); }
}
