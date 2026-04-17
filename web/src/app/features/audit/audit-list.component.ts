import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { DatePipe } from '@angular/common';
import { ApiService } from '../../core/api.service';
import { AuditEventDto } from '../../core/models';

@Component({
  selector: 'app-audit-list',
  imports: [FormsModule, DatePipe],
  template: `
    <h1>Audit log</h1>
    <div class="filters">
      <input [(ngModel)]="filter" placeholder="filtrar por eventType (vacío = todos)" (keyup.enter)="go(1)" />
      <button (click)="go(1)">Buscar</button>
    </div>

    <table>
      <thead>
        <tr><th>Fecha</th><th>Evento</th><th>Target</th><th>Tenant</th><th>API key</th><th>Detalle</th></tr>
      </thead>
      <tbody>
        @for (a of items(); track a.id) {
          <tr>
            <td>{{ a.createdAt | date:'short' }}</td>
            <td><code>{{ a.eventType }}</code></td>
            <td><code>{{ a.target }}</code></td>
            <td>{{ a.tenantId?.substring(0,8) || '—' }}</td>
            <td>{{ a.apiKeyId?.substring(0,8) || '—' }}</td>
            <td><pre class="det">{{ pretty(a.details) }}</pre></td>
          </tr>
        }
        @empty { <tr><td colspan="6">Sin eventos</td></tr> }
      </tbody>
    </table>

    <div class="pager">
      <button [disabled]="page() <= 1" (click)="go(page()-1)">« Anterior</button>
      <span>{{ page() }} / {{ totalPages() }}</span>
      <button [disabled]="page() >= totalPages()" (click)="go(page()+1)">Siguiente »</button>
    </div>
  `,
  styles: [`
    .filters { display:flex; gap:0.5rem; margin-bottom:1rem; }
    input { flex:1; max-width:400px; background:#1c1c1c; color:#eee; border:1px solid #333; padding:0.5rem; border-radius:4px; }
    button { background:#3b82f6; color:#fff; border:none; padding:0.4rem 0.8rem; border-radius:4px; cursor:pointer; }
    button:disabled { opacity:0.4; cursor:not-allowed; }
    table { width:100%; border-collapse:collapse; }
    th, td { text-align:left; padding:0.5rem; border-bottom:1px solid #2a2a2e; font-size:0.85rem; vertical-align:top; }
    th { color:#888; font-weight:500; }
    code { background:#0e0e10; padding:0.15rem 0.35rem; border-radius:3px; font-size:0.8rem; }
    .det { max-width:400px; max-height:100px; overflow:auto; margin:0; font-size:0.75rem; color:#aaa; white-space:pre-wrap; }
    .pager { display:flex; gap:1rem; margin-top:1rem; align-items:center; }
  `]
})
export class AuditListComponent implements OnInit {
  private api = inject(ApiService);
  items = signal<AuditEventDto[]>([]);
  page = signal(1);
  pageSize = 50;
  total = signal(0);
  filter = signal('');

  ngOnInit() { this.go(1); }

  totalPages() { return Math.max(1, Math.ceil(this.total() / this.pageSize)); }

  go(p: number) {
    this.page.set(p);
    this.api.listAudit(p, this.pageSize, this.filter() || undefined).subscribe(r => {
      this.items.set(r.items);
      this.total.set(r.total);
    });
  }

  pretty(s: string | null): string {
    if (!s) return '';
    try { return JSON.stringify(JSON.parse(s), null, 2); }
    catch { return s; }
  }
}
