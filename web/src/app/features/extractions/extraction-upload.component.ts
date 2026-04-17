import { Component, inject, signal, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { ApiService } from '../../core/api.service';
import { SchemaDto } from '../../core/models';

@Component({
  selector: 'app-extraction-upload',
  imports: [FormsModule],
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
          <textarea [(ngModel)]="schemaJson" name="schemaJson" rows="10" placeholder='{"type":"object","properties":{...}}'></textarea>
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
        <input type="checkbox" [(ngModel)]="asyncMode" name="asyncMode" />
        Forzar procesamiento asíncrono
      </label>

      <button type="submit" [disabled]="!file() || submitting()">
        {{ submitting() ? 'Procesando...' : 'Subir y extraer' }}
      </button>

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
  `]
})
export class ExtractionUploadComponent implements OnInit {
  private api = inject(ApiService);
  private router = inject(Router);

  schemas = signal<SchemaDto[]>([]);
  file = signal<File | null>(null);
  schemaId = signal('');
  schemaJson = signal('');
  model = signal('');
  webhookUrl = signal('');
  asyncMode = signal(false);
  submitting = signal(false);
  error = signal<string | null>(null);

  ngOnInit() {
    this.api.listSchemas().subscribe(s => this.schemas.set(s));
  }

  onFile(e: Event) {
    const f = (e.target as HTMLInputElement).files?.[0] ?? null;
    this.file.set(f);
  }

  submit() {
    const f = this.file();
    if (!f) return;
    this.submitting.set(true);
    this.error.set(null);
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
