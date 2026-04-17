import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { AuthService } from '../../core/auth.service';

@Component({
  selector: 'app-login',
  imports: [FormsModule],
  template: `
    <div class="login-wrap">
      <form (ngSubmit)="submit()" class="card">
        <h1>ScribAi</h1>
        <p>Introduce tu API Key para continuar.</p>
        <input type="password" [(ngModel)]="key" name="key" placeholder="sk_..." required autofocus />
        <button type="submit" [disabled]="!key().trim()">Entrar</button>
        @if (error()) { <p class="err">{{ error() }}</p> }
      </form>
    </div>
  `,
  styles: [`
    .login-wrap { display:flex; align-items:center; justify-content:center; min-height:100vh; background:#111; color:#eee; }
    .card { background:#1c1c1c; padding:2rem; border-radius:8px; width:380px; display:flex; flex-direction:column; gap:0.75rem; }
    h1 { margin:0; }
    input { padding:0.6rem; border-radius:4px; border:1px solid #333; background:#0a0a0a; color:#eee; font-family:monospace; }
    button { padding:0.6rem; border:none; border-radius:4px; background:#3b82f6; color:#fff; font-weight:600; cursor:pointer; }
    button:disabled { opacity:0.5; cursor:not-allowed; }
    .err { color:#ef4444; margin:0; }
  `]
})
export class LoginComponent {
  private auth = inject(AuthService);
  private router = inject(Router);
  key = signal('');
  error = signal<string | null>(null);

  submit() {
    const k = this.key().trim();
    if (!k) return;
    this.auth.setKey(k);
    this.router.navigateByUrl('/extractions');
  }
}
