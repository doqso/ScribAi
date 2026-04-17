import { Component, OnInit, OnDestroy, inject, signal } from '@angular/core';
import { DatePipe, DecimalPipe } from '@angular/common';
import { DomSanitizer, SafeResourceUrl } from '@angular/platform-browser';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { ApiService } from '../../core/api.service';
import { ExtractionDto, SchemaDto } from '../../core/models';
import { MonacoComponent } from '../../core/monaco-editor.component';

@Component({
  selector: 'app-extraction-detail',
  imports: [DatePipe, DecimalPipe, RouterLink, FormsModule, MonacoComponent],
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

      <div class="actions">
        <button (click)="showRerun.set(!showRerun())">Re-extraer</button>
        <button class="ghost" (click)="downloadJson(e)" [disabled]="!e.result">Descargar JSON</button>
      </div>

      @if (showRerun()) {
        <div class="card">
          <h3>Re-extraer con otro schema o modelo</h3>
          <div class="grid">
            <label>
              Schema
              <select [(ngModel)]="rerunSchemaId" name="rs">
                <option [value]="''">-- mismo schema que original --</option>
                @for (s of schemas(); track s.id) {
                  <option [value]="s.id">{{ s.name }} v{{ s.version }}</option>
                }
              </select>
            </label>
          </div>
          <div class="actions">
            <button (click)="rerun(e.id)" [disabled]="rerunning()">{{ rerunning() ? 'Procesando...' : 'Ejecutar re-extracción' }}</button>
            <button class="ghost" (click)="showRerun.set(false)">Cancelar</button>
            @if (rerunMsg()) { <span class="msg">{{ rerunMsg() }}</span> }
          </div>
        </div>
      }

      @if (e.error) { <div class="err">{{ e.error }}</div> }

      <div class="split">
        <div class="pane">
          <h2>Documento original</h2>
          @if (previewKind() === 'loading') { <p>Cargando preview...</p> }
          @else if (previewKind() === 'pdf') {
            <iframe [src]="previewUrl()" class="frame"></iframe>
          }
          @else if (previewKind() === 'image') {
            <img [src]="previewSrc()" class="img" alt="preview" />
          }
          @else if (previewKind() === 'text') {
            <pre class="text">{{ previewText() }}</pre>
          }
          @else if (previewKind() === 'unavailable') {
            <p class="msg">Original no disponible (no se guardó).</p>
            @if (e.extractedText) {
              <h3>Texto extraído</h3>
              <pre class="text">{{ e.extractedText }}</pre>
            }
          }
        </div>

        <div class="pane">
          <h2>Resultado JSON</h2>
          @if (e.result) {
            <app-monaco [value]="pretty(e.result)" language="json" [readOnly]="true" height="75vh"></app-monaco>
          }
          @else { <p class="msg">Sin resultado</p> }
        </div>
      </div>
    }
    @else { <p>Cargando...</p> }
  `,
  styles: [`
    a { color:#60a5fa; }
    .meta { display:flex; gap:1rem; flex-wrap:wrap; color:#888; font-size:0.85rem; margin-bottom:1rem; }
    .actions { display:flex; gap:0.5rem; margin:1rem 0; align-items:center; }
    button { background:#3b82f6; color:#fff; border:none; padding:0.5rem 0.9rem; border-radius:4px; cursor:pointer; }
    button.ghost { background:transparent; border:1px solid #333; color:#aaa; }
    .card { background:#161618; border:1px solid #2a2a2e; padding:1rem; border-radius:8px; margin-bottom:1rem; }
    .grid { display:grid; grid-template-columns:1fr 1fr; gap:1rem; margin:0.5rem 0; }
    label { display:flex; flex-direction:column; gap:0.3rem; font-size:0.85rem; color:#bbb; }
    input, select { background:#1c1c1c; color:#eee; border:1px solid #333; padding:0.5rem; border-radius:4px; }
    .status { padding:0.15rem 0.5rem; border-radius:999px; font-size:0.75rem; }
    .status.succeeded { background:#064e3b; color:#34d399; }
    .status.failed { background:#7f1d1d; color:#fca5a5; }
    .status.running { background:#1e3a8a; color:#93c5fd; }
    .status.queued { background:#374151; color:#d1d5db; }
    .err { background:#7f1d1d22; border:1px solid #7f1d1d; color:#fca5a5; padding:0.75rem; border-radius:6px; margin:1rem 0; }
    .split { display:grid; grid-template-columns: 1fr 1fr; gap:1rem; }
    .pane { min-width:0; }
    .frame { width:100%; height:75vh; border:1px solid #333; border-radius:6px; background:#fff; }
    .img { max-width:100%; max-height:75vh; border:1px solid #333; border-radius:6px; background:#fff; }
    .text { background:#1c1c1c; border:1px solid #333; padding:1rem; border-radius:6px; overflow:auto; max-height:75vh; white-space:pre-wrap; font-size:0.85rem; }
    .msg { color:#9ca3af; font-size:0.9rem; }
    h2 { margin:0 0 0.5rem 0; }
  `]
})
export class ExtractionDetailComponent implements OnInit, OnDestroy {
  private api = inject(ApiService);
  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private sanitizer = inject(DomSanitizer);

  item = signal<ExtractionDto | null>(null);
  schemas = signal<SchemaDto[]>([]);
  showRerun = signal(false);
  rerunSchemaId = signal('');
  rerunning = signal(false);
  rerunMsg = signal<string | null>(null);

  previewKind = signal<'loading' | 'pdf' | 'image' | 'text' | 'unavailable'>('loading');
  previewUrl = signal<SafeResourceUrl | null>(null);
  previewSrc = signal<string>('');
  previewText = signal<string>('');
  private blobUrl: string | null = null;
  private timer: ReturnType<typeof setTimeout> | null = null;

  ngOnInit() {
    const id = this.route.snapshot.paramMap.get('id')!;
    this.poll(id);
    this.api.listSchemas().subscribe(s => this.schemas.set(s));
  }

  ngOnDestroy() {
    if (this.timer) clearTimeout(this.timer);
    if (this.blobUrl) URL.revokeObjectURL(this.blobUrl);
  }

  private poll(id: string) {
    this.api.getExtraction(id).subscribe(e => {
      const first = !this.item();
      this.item.set(e);
      if (first) this.loadPreview(e);
      if (e.status === 'queued' || e.status === 'running') {
        this.timer = setTimeout(() => this.poll(id), 2000);
      }
    });
  }

  private loadPreview(e: ExtractionDto) {
    this.previewKind.set('loading');
    this.api.getOriginal(e.id).subscribe({
      next: (b: unknown) => {
        const blob = b as Blob;
        if (this.blobUrl) URL.revokeObjectURL(this.blobUrl);
        const url = URL.createObjectURL(blob);
        this.blobUrl = url;
        const mime = e.mime;
        if (mime === 'application/pdf') {
          this.previewUrl.set(this.sanitizer.bypassSecurityTrustResourceUrl(url));
          this.previewKind.set('pdf');
        } else if (mime.startsWith('image/')) {
          this.previewSrc.set(url);
          this.previewKind.set('image');
        } else if (mime.startsWith('text/') || mime === 'message/rfc822') {
          blob.text().then((t: string) => { this.previewText.set(t); this.previewKind.set('text'); });
        } else {
          this.previewKind.set('unavailable');
        }
      },
      error: () => this.previewKind.set('unavailable')
    });
  }

  rerun(id: string) {
    this.rerunning.set(true);
    this.rerunMsg.set(null);
    this.api.rerunExtraction(id, {
      schemaId: this.rerunSchemaId() || undefined
    }).subscribe({
      next: r => {
        this.rerunning.set(false);
        this.showRerun.set(false);
        this.router.navigate(['/extractions', r.id]);
      },
      error: e => {
        this.rerunning.set(false);
        this.rerunMsg.set('Error: ' + (e.error?.detail ?? e.error?.error ?? e.message));
      }
    });
  }

  pretty(json: string): string {
    try { return JSON.stringify(JSON.parse(json), null, 2); }
    catch { return json; }
  }

  downloadJson(e: ExtractionDto) {
    this.api.exportJson(e.id).subscribe(blob => {
      const url = URL.createObjectURL(blob);
      const a = document.createElement('a');
      a.href = url;
      a.download = e.sourceFilename.replace(/[^\w.\-]+/g, '_') + '.json';
      document.body.appendChild(a); a.click(); a.remove();
      setTimeout(() => URL.revokeObjectURL(url), 1000);
    });
  }
}
