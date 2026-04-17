import { Component, inject, signal, OnInit } from '@angular/core';
import { DatePipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { ApiService } from '../../core/api.service';
import { ExtractionDto } from '../../core/models';

@Component({
  selector: 'app-extractions-list',
  imports: [DatePipe, RouterLink],
  template: `
    <div class="head">
      <h1>Extracciones</h1>
      <a routerLink="/extractions/new" class="btn primary">Nueva extracción</a>
    </div>

    <div class="filters">
      <select (change)="filterStatus($any($event.target).value)">
        <option value="">Todos los estados</option>
        <option value="queued">Queued</option>
        <option value="running">Running</option>
        <option value="succeeded">Succeeded</option>
        <option value="failed">Failed</option>
      </select>
    </div>

    @if (loading()) { <p>Cargando...</p> }
    @else {
      <table>
        <thead>
          <tr><th>Archivo</th><th>Estado</th><th>Método</th><th>Modelo</th><th>Creado</th><th></th></tr>
        </thead>
        <tbody>
          @for (e of items(); track e.id) {
            <tr>
              <td>{{ e.sourceFilename }}</td>
              <td><span class="status {{ e.status }}">{{ e.status }}</span></td>
              <td>{{ e.extractionMethod }}</td>
              <td>{{ e.model }}</td>
              <td>{{ e.createdAt | date:'short' }}</td>
              <td><a [routerLink]="['/extractions', e.id]">Ver</a></td>
            </tr>
          }
          @empty { <tr><td colspan="6">Sin resultados</td></tr> }
        </tbody>
      </table>
      <div class="pager">
        <button [disabled]="page() <= 1" (click)="go(page()-1)">« Anterior</button>
        <span>Página {{ page() }} / {{ totalPages() }}</span>
        <button [disabled]="page() >= totalPages()" (click)="go(page()+1)">Siguiente »</button>
      </div>
    }
  `,
  styles: [`
    .head { display:flex; justify-content:space-between; align-items:center; margin-bottom:1rem; }
    .btn.primary { background:#3b82f6; color:#fff; padding:0.5rem 0.9rem; border-radius:6px; text-decoration:none; }
    .filters { margin-bottom:1rem; }
    select { background:#1c1c1c; color:#eee; border:1px solid #333; padding:0.4rem; border-radius:4px; }
    table { width:100%; border-collapse:collapse; }
    th, td { text-align:left; padding:0.6rem; border-bottom:1px solid #2a2a2e; font-size:0.9rem; }
    th { color:#888; font-weight:500; }
    .status { padding:0.15rem 0.5rem; border-radius:999px; font-size:0.75rem; }
    .status.succeeded { background:#064e3b; color:#34d399; }
    .status.failed { background:#7f1d1d; color:#fca5a5; }
    .status.running { background:#1e3a8a; color:#93c5fd; }
    .status.queued { background:#374151; color:#d1d5db; }
    .pager { display:flex; gap:1rem; align-items:center; margin-top:1rem; }
    .pager button { background:#1c1c1c; color:#eee; border:1px solid #333; padding:0.4rem 0.8rem; border-radius:4px; cursor:pointer; }
    .pager button:disabled { opacity:0.4; cursor:not-allowed; }
    a { color:#60a5fa; }
  `]
})
export class ExtractionsListComponent implements OnInit {
  private api = inject(ApiService);
  items = signal<ExtractionDto[]>([]);
  page = signal(1);
  pageSize = 20;
  total = signal(0);
  status = signal('');
  loading = signal(false);

  ngOnInit() { this.load(); }

  totalPages() { return Math.max(1, Math.ceil(this.total() / this.pageSize)); }

  go(p: number) { this.page.set(p); this.load(); }

  filterStatus(s: string) { this.status.set(s); this.page.set(1); this.load(); }

  load() {
    this.loading.set(true);
    this.api.listExtractions(this.page(), this.pageSize, this.status() || undefined).subscribe({
      next: res => { this.items.set(res.items); this.total.set(res.total); this.loading.set(false); },
      error: () => this.loading.set(false)
    });
  }
}
