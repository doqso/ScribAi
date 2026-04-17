import { Component, inject, signal, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { ApiService } from '../../core/api.service';
import { AuthService } from '../../core/auth.service';
import { SchemaDto } from '../../core/models';
import { MonacoComponent } from '../../core/monaco-editor.component';
import { environment } from '../../../environments/environment';

interface StreamEvent { name: string; data: any; }

@Component({
  selector: 'app-extraction-upload',
  imports: [FormsModule, MonacoComponent],
  template: `
    <h1>Nueva extracción</h1>

    <form (ngSubmit)="submit()" class="form">
      <label>
        Archivo
        <input type="file" (change)="onFile($event)" required />
      </label>

      <label>
        Esquema guardado
        <select [(ngModel)]="schemaId" name="schemaId">
          <option value="">-- usar schema inline --</option>
          @for (s of schemas(); track s.id) {
            <option [value]="s.id">{{ s.name }} v{{ s.version }}</option>
          }
        </select>
      </label>

      @if (!schemaId()) {
        <label>
          JSON Schema (inline)
          <app-monaco [value]="schemaJson()" (valueChange)="schemaJson.set($event)" language="json" height="280px"></app-monaco>
        </label>
      }

      <label>
        Modelo (opcional, override)
        <input [(ngModel)]="model" name="model" placeholder="qwen2.5:7b-instruct" />
      </label>

      <label>
        Webhook URL (opcional)
        <input [(ngModel)]="webhookUrl" name="webhookUrl" placeholder="https://..." />
      </label>

      <label class="row">
        <input type="checkbox" [(ngModel)]="asyncMode" name="asyncMode" [disabled]="streamMode()" />
        Forzar procesamiento asíncrono
      </label>

      <label class="row">
        <input type="checkbox" [(ngModel)]="streamMode" name="streamMode" [disabled]="asyncMode()" />
        Ver progreso en vivo (streaming)
      </label>

      <button type="submit" [disabled]="!file() || submitting()">
        {{ submitting() ? 'Procesando...' : 'Subir y extraer' }}
      </button>

      @if (streamEvents().length > 0) {
        <div class="stream-log">
          <h3>Progreso</h3>
          @for (e of streamEvents(); track $index) {
            <div class="evt"><code>{{ e.name }}</code> {{ formatData(e.data) }}</div>
          }
        </div>
      }

      @if (error()) { <p class="err">{{ error() }}</p> }
    </form>
  `,
  styles: [`
    .form { display:flex; flex-direction:column; gap:1rem; max-width:700px; }
    label { display:flex; flex-direction:column; gap:0.3rem; color:#bbb; font-size:0.9rem; }
    label.row { flex-direction:row; align-items:center; gap:0.5rem; }
    input, select, textarea { background:#1c1c1c; color:#eee; border:1px solid #333; padding:0.5rem; border-radius:4px; font-family:inherit; }
    textarea { font-family:monospace; font-size:0.85rem; }
    button { background:#3b82f6; color:#fff; border:none; padding:0.7rem; border-radius:6px; font-weight:600; cursor:pointer; }
    button:disabled { opacity:0.5; cursor:not-allowed; }
    .err { color:#ef4444; }
    .stream-log { background:#161618; border:1px solid #2a2a2e; border-radius:6px; padding:0.75rem; font-size:0.85rem; max-height:300px; overflow:auto; }
    .stream-log h3 { margin:0 0 0.5rem 0; color:#ccc; }
    .evt { padding:0.2rem 0; color:#aaa; font-family:monospace; }
    .evt code { color:#93c5fd; background:#0e0e10; padding:0.1rem 0.3rem; border-radius:3px; margin-right:0.4rem; }
  `]
})
export class ExtractionUploadComponent implements OnInit {
  private api = inject(ApiService);
  private auth = inject(AuthService);
  private router = inject(Router);

  schemas = signal<SchemaDto[]>([]);
  file = signal<File | null>(null);
  schemaId = signal('');
  schemaJson = signal('');
  model = signal('');
  webhookUrl = signal('');
  asyncMode = signal(false);
  streamMode = signal(false);
  submitting = signal(false);
  error = signal<string | null>(null);
  streamEvents = signal<StreamEvent[]>([]);

  ngOnInit() {
    this.api.listSchemas().subscribe(s => this.schemas.set(s));
  }

  onFile(e: Event) {
    const f = (e.target as HTMLInputElement).files?.[0] ?? null;
    this.file.set(f);
  }

  async submit() {
    const f = this.file();
    if (!f) return;
    this.submitting.set(true);
    this.error.set(null);
    this.streamEvents.set([] as StreamEvent[]);

    if (this.streamMode()) {
      await this.submitStreaming(f);
    } else {
      this.api.uploadExtraction(f, {
        schemaId: this.schemaId() || undefined,
        schema: this.schemaId() ? undefined : this.schemaJson() || undefined,
        model: this.model() || undefined,
        webhookUrl: this.webhookUrl() || undefined,
        async: this.asyncMode() || undefined
      }).subscribe({
        next: r => this.router.navigate(['/extractions', r.id]),
        error: err => { this.error.set(err.error?.error ?? err.message); this.submitting.set(false); }
      });
    }
  }

  private async submitStreaming(f: File) {
    const fd = new FormData();
    fd.append('file', f);
    if (this.schemaId()) fd.append('schemaId', this.schemaId());
    else if (this.schemaJson()) fd.append('schema', this.schemaJson());
    if (this.model()) fd.append('model', this.model());
    if (this.webhookUrl()) fd.append('webhookUrl', this.webhookUrl());

    try {
      const resp = await fetch(`${environment.apiBaseUrl}/v1/extractions/stream`, {
        method: 'POST',
        headers: { 'X-API-Key': this.auth.apiKey() ?? '' },
        body: fd
      });

      if (!resp.ok || !resp.body) {
        this.error.set(`HTTP ${resp.status}`);
        this.submitting.set(false);
        return;
      }

      const reader = resp.body.getReader();
      const decoder = new TextDecoder();
      let buffer = '';
      let createdId: string | null = null;

      while (true) {
        const { value, done } = await reader.read();
        if (done) break;
        buffer += decoder.decode(value, { stream: true });
        const parts = buffer.split('\n\n');
        buffer = parts.pop() ?? '';
        for (const part of parts) {
          const evt = parseSse(part);
          if (!evt) continue;
          this.streamEvents.update(arr => [...arr, evt]);
          if (evt.name === 'created') createdId = evt.data?.id ?? null;
          if (evt.name === 'succeeded' || evt.name === 'failed') {
            this.submitting.set(false);
            if (createdId) this.router.navigate(['/extractions', createdId]);
            return;
          }
          if (evt.name === 'error') {
            this.error.set(evt.data?.error ?? 'error');
            this.submitting.set(false);
            return;
          }
        }
      }
      this.submitting.set(false);
    } catch (ex: any) {
      this.error.set(ex?.message ?? 'stream failed');
      this.submitting.set(false);
    }
  }

  formatData(d: any): string {
    if (d == null) return '';
    if (typeof d === 'string') return d;
    return JSON.stringify(d);
  }
}

function parseSse(block: string): StreamEvent | null {
  let name = 'message';
  let data = '';
  for (const line of block.split('\n')) {
    if (line.startsWith('event:')) name = line.substring(6).trim();
    else if (line.startsWith('data:')) data += line.substring(5).trim();
  }
  if (!data) return null;
  try { return { name, data: JSON.parse(data) }; }
  catch { return { name, data }; }
}
