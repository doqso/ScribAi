import { Component, inject, signal, OnInit } from '@angular/core';
import { DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ApiService } from '../../core/api.service';
import { WebhookDto } from '../../core/models';

@Component({
  selector: 'app-webhooks-list',
  imports: [DatePipe, FormsModule],
  template: `
    <h1>Webhooks</h1>

    <form (ngSubmit)="create()" class="create">
      <input [(ngModel)]="url" name="url" placeholder="https://mi-servicio/webhook" required />
      <button type="submit" [disabled]="!url()">Crear</button>
    </form>

    @if (newSecret()) {
      <div class="notice">Secreto generado (guárdalo, no se volverá a mostrar): <code>{{ newSecret() }}</code></div>
    }

    <table>
      <thead><tr><th>URL</th><th>Eventos</th><th>Creado</th><th></th></tr></thead>
      <tbody>
        @for (w of items(); track w.id) {
          <tr>
            <td>{{ w.url }}</td>
            <td>{{ w.events.join(', ') }}</td>
            <td>{{ w.createdAt | date:'short' }}</td>
            <td>
              <button (click)="test(w.id)">Probar</button>
              <button class="danger" (click)="remove(w.id)">Borrar</button>
            </td>
          </tr>
        }
        @empty { <tr><td colspan="4">Sin webhooks</td></tr> }
      </tbody>
    </table>
  `,
  styles: [`
    .create { display:flex; gap:0.5rem; margin-bottom:1rem; }
    input { flex:1; background:#1c1c1c; color:#eee; border:1px solid #333; padding:0.5rem; border-radius:4px; }
    button { background:#3b82f6; color:#fff; border:none; padding:0.5rem 0.9rem; border-radius:4px; cursor:pointer; margin-right:0.3rem; }
    button.danger { background:#b91c1c; }
    .notice { background:#1e3a8a22; border:1px solid #1e3a8a; padding:0.75rem; border-radius:6px; margin-bottom:1rem; }
    code { background:#000; padding:0.2rem 0.4rem; border-radius:3px; }
    table { width:100%; border-collapse:collapse; }
    th, td { text-align:left; padding:0.6rem; border-bottom:1px solid #2a2a2e; font-size:0.9rem; }
    th { color:#888; font-weight:500; }
  `]
})
export class WebhooksListComponent implements OnInit {
  private api = inject(ApiService);
  items = signal<WebhookDto[]>([]);
  url = signal('');
  newSecret = signal<string | null>(null);

  ngOnInit() { this.load(); }

  load() { this.api.listWebhooks().subscribe(w => this.items.set(w)); }

  create() {
    this.api.createWebhook({ url: this.url() }).subscribe(w => {
      this.newSecret.set(w.secret ?? null);
      this.url.set('');
      this.load();
    });
  }

  test(id: string) { this.api.testWebhook(id).subscribe(() => alert('Test enviado')); }

  remove(id: string) {
    if (!confirm('¿Borrar webhook?')) return;
    this.api.deleteWebhook(id).subscribe(() => this.load());
  }
}
