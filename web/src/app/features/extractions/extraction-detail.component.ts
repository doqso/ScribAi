import { Component, OnInit, OnDestroy, inject, signal } from '@angular/core';
import { DatePipe, DecimalPipe } from '@angular/common';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { ApiService } from '../../core/api.service';
import { ExtractionDto } from '../../core/models';

@Component({
  selector: 'app-extraction-detail',
  imports: [DatePipe, DecimalPipe, RouterLink],
  template: `
    @if (item(); as e) {
      <a routerLink="/extractions">← Volver</a>
      <h1>{{ e.sourceFilename }}</h1>
      <div class="meta">
        <span class="status {{ e.status }}">{{ e.status }}</span>
        <span>{{ e.mime }}</span>
        <span>{{ (e.sizeBytes / 1024) | number:'1.0-1' }} KB</span>
        <span>Modelo: {{ e.model }}</span>
        <span>Método: {{ e.extractionMethod }}</span>
        @if (e.tokensIn) { <span>{{ e.tokensIn }} → {{ e.tokensOut }} tokens</span> }
        <span>{{ e.createdAt | date:'medium' }}</span>
      </div>
      @if (e.error) { <div class="err">{{ e.error }}</div> }
      @if (e.result) {
        <h2>Resultado JSON</h2>
        <pre>{{ pretty(e.result) }}</pre>
      }
    }
    @else { <p>Cargando...</p> }
  `,
  styles: [`
    a { color:#60a5fa; }
    .meta { display:flex; gap:1rem; flex-wrap:wrap; color:#888; font-size:0.85rem; margin-bottom:1rem; }
    .status { padding:0.15rem 0.5rem; border-radius:999px; font-size:0.75rem; }
    .status.succeeded { background:#064e3b; color:#34d399; }
    .status.failed { background:#7f1d1d; color:#fca5a5; }
    .status.running { background:#1e3a8a; color:#93c5fd; }
    .status.queued { background:#374151; color:#d1d5db; }
    .err { background:#7f1d1d22; border:1px solid #7f1d1d; color:#fca5a5; padding:0.75rem; border-radius:6px; margin:1rem 0; }
    pre { background:#1c1c1c; border:1px solid #333; padding:1rem; border-radius:6px; overflow:auto; max-height:60vh; }
  `]
})
export class ExtractionDetailComponent implements OnInit, OnDestroy {
  private api = inject(ApiService);
  private route = inject(ActivatedRoute);
  item = signal<ExtractionDto | null>(null);
  private timer: ReturnType<typeof setTimeout> | null = null;

  ngOnInit() {
    const id = this.route.snapshot.paramMap.get('id')!;
    this.poll(id);
  }

  ngOnDestroy() { if (this.timer) clearTimeout(this.timer); }

  private poll(id: string) {
    this.api.getExtraction(id).subscribe(e => {
      this.item.set(e);
      if (e.status === 'queued' || e.status === 'running') {
        this.timer = setTimeout(() => this.poll(id), 2000);
      }
    });
  }

  pretty(json: string): string {
    try { return JSON.stringify(JSON.parse(json), null, 2); }
    catch { return json; }
  }
}
