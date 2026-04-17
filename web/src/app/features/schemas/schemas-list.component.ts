import { Component, inject, signal, OnInit } from '@angular/core';
import { DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ApiService } from '../../core/api.service';
import { MeDto, SchemaDto } from '../../core/models';
import { MonacoComponent } from '../../core/monaco-editor.component';

@Component({
  selector: 'app-schemas-list',
  imports: [DatePipe, FormsModule, MonacoComponent],
  template: `
    <h1>Schemas</h1>

    <form (ngSubmit)="create()" class="create">
      <input [(ngModel)]="name" name="name" placeholder="Nombre del schema" required />
      <input [(ngModel)]="description" name="description" placeholder="Descripción" />
      <app-monaco [value]="jsonSchema()" (valueChange)="jsonSchema.set($event)" language="json" height="320px"></app-monaco>
      <button type="submit" [disabled]="!name() || !jsonSchema()">Crear versión</button>
      @if (error()) { <p class="err">{{ error() }}</p> }
    </form>

    <h2>Existentes</h2>
    <table>
      <thead><tr><th>Nombre</th><th>Scope</th><th>Versión</th><th>Creado</th><th>Editado</th><th></th></tr></thead>
      <tbody>
        @for (s of items(); track s.id) {
          <tr>
            <td>{{ s.name }}</td>
            <td><span class="badge {{ s.scope }}">{{ s.scope }}</span></td>
            <td>v{{ s.version }}</td>
            <td>{{ s.createdAt | date:'short' }}</td>
            <td>{{ s.updatedAt ? (s.updatedAt | date:'short') : '—' }}</td>
            <td class="actions">
              <button (click)="open(s, true)">Ver</button>
              @if (canMutate(s)) {
                <button (click)="open(s, false)">Editar</button>
                <button class="danger" (click)="remove(s.id)">Borrar</button>
              }
            </td>
          </tr>
        }
        @empty { <tr><td colspan="6">Sin schemas</td></tr> }
      </tbody>
    </table>

    @if (editing(); as e) {
      <div class="modal-backdrop" (click)="close()">
        <div class="modal" (click)="$event.stopPropagation()">
          <div class="modal-head">
            <h3>
              {{ readOnly() ? 'Ver' : 'Editar' }} schema
              — {{ e.name }} v{{ e.version }}
              <span class="badge {{ e.scope }}">{{ e.scope }}</span>
            </h3>
            <button class="ghost" (click)="close()">✕</button>
          </div>
          <label>
            Descripción
            <input [(ngModel)]="editDesc" name="ed" [disabled]="readOnly()" />
          </label>
          <app-monaco [value]="editJson()" (valueChange)="editJson.set($event)" [readOnly]="readOnly()" language="json" height="480px"></app-monaco>
          <div class="modal-foot">
            @if (!readOnly()) {
              <button (click)="save()" [disabled]="saving()">{{ saving() ? 'Guardando...' : 'Guardar' }}</button>
            }
            <button class="ghost" (click)="close()">Cerrar</button>
            @if (editMsg()) { <span class="msg">{{ editMsg() }}</span> }
          </div>
        </div>
      </div>
    }
  `,
  styles: [`
    .create { display:flex; flex-direction:column; gap:0.5rem; max-width:900px; margin-bottom:2rem; }
    input { background:#1c1c1c; color:#eee; border:1px solid #333; padding:0.5rem; border-radius:4px; font-family:inherit; }
    button { background:#3b82f6; color:#fff; border:none; padding:0.4rem 0.8rem; border-radius:4px; cursor:pointer; }
    button.danger { background:#b91c1c; }
    button.ghost { background:transparent; color:#aaa; border:1px solid #333; }
    button:disabled { opacity:0.5; }
    table { width:100%; border-collapse:collapse; }
    th, td { text-align:left; padding:0.6rem; border-bottom:1px solid #2a2a2e; font-size:0.9rem; }
    th { color:#888; font-weight:500; }
    .badge { padding:0.15rem 0.5rem; border-radius:999px; font-size:0.7rem; text-transform:uppercase; letter-spacing:0.05em; }
    .badge.global { background:#1e3a8a; color:#93c5fd; }
    .badge.personal { background:#374151; color:#d1d5db; }
    .actions { display:flex; gap:0.35rem; }
    .err { color:#ef4444; }
    .modal-backdrop { position:fixed; inset:0; background:rgba(0,0,0,0.7); display:flex; align-items:center; justify-content:center; z-index:1000; padding:2rem; }
    .modal { background:#161618; border:1px solid #2a2a2e; border-radius:10px; width:min(1100px, 100%); max-height:90vh; overflow:auto; padding:1.25rem; display:flex; flex-direction:column; gap:0.75rem; }
    .modal-head { display:flex; align-items:center; justify-content:space-between; }
    .modal-head h3 { margin:0; display:flex; gap:0.5rem; align-items:center; }
    .modal-foot { display:flex; gap:0.5rem; align-items:center; margin-top:0.5rem; }
    label { display:flex; flex-direction:column; gap:0.3rem; color:#bbb; font-size:0.85rem; }
    .msg { color:#9ca3af; font-size:0.85rem; }
  `]
})
export class SchemasListComponent implements OnInit {
  private api = inject(ApiService);
  items = signal<SchemaDto[]>([]);
  name = signal('');
  description = signal('');
  jsonSchema = signal('');
  error = signal<string | null>(null);

  me = signal<MeDto | null>(null);
  editing = signal<SchemaDto | null>(null);
  readOnly = signal(false);
  editJson = signal('');
  editDesc = signal('');
  saving = signal(false);
  editMsg = signal<string | null>(null);

  ngOnInit() {
    this.api.me().subscribe(m => this.me.set(m));
    this.load();
  }

  load() { this.api.listSchemas().subscribe(s => this.items.set(s)); }

  canMutate(s: SchemaDto): boolean {
    const m = this.me();
    if (!m) return false;
    if (s.scope === 'global') return m.isAdmin;
    return s.apiKeyId === m.apiKeyId || m.isAdmin;
  }

  create() {
    this.error.set(null);
    this.api.createSchema({ name: this.name(), jsonSchema: this.jsonSchema(), description: this.description() || undefined })
      .subscribe({
        next: () => {
          this.name.set(''); this.jsonSchema.set(''); this.description.set('');
          this.load();
        },
        error: err => this.error.set(err.error?.detail ?? err.error?.error ?? err.message)
      });
  }

  open(s: SchemaDto, viewOnly: boolean) {
    this.editing.set(s);
    this.readOnly.set(viewOnly || !this.canMutate(s));
    this.editJson.set(this.pretty(s.jsonSchema));
    this.editDesc.set(s.description ?? '');
    this.editMsg.set(null);
  }

  close() { this.editing.set(null); this.editJson.set(''); this.editMsg.set(null); }

  save() {
    const e = this.editing();
    if (!e) return;
    this.saving.set(true);
    this.editMsg.set(null);
    this.api.updateSchema(e.id, { jsonSchema: this.editJson(), description: this.editDesc() || undefined })
      .subscribe({
        next: () => { this.saving.set(false); this.close(); this.load(); },
        error: err => {
          this.saving.set(false);
          this.editMsg.set('Error: ' + (err.error?.detail ?? err.error?.error ?? err.message));
        }
      });
  }

  remove(id: string) {
    if (!confirm('¿Borrar schema?')) return;
    this.api.deleteSchema(id).subscribe(() => this.load());
  }

  private pretty(json: string): string {
    try { return JSON.stringify(JSON.parse(json), null, 2); }
    catch { return json; }
  }
}
