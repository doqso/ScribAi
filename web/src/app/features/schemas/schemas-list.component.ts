import { Component, inject, signal, OnInit } from '@angular/core';
import { DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ApiService } from '../../core/api.service';
import { SchemaDto } from '../../core/models';

@Component({
  selector: 'app-schemas-list',
  imports: [DatePipe, FormsModule],
  template: `
    <h1>Schemas</h1>

    <form (ngSubmit)="create()" class="create">
      <input [(ngModel)]="name" name="name" placeholder="Nombre del schema" required />
      <input [(ngModel)]="description" name="description" placeholder="Descripción" />
      <textarea [(ngModel)]="jsonSchema" name="jsonSchema" rows="8" placeholder='{"type":"object","properties":{"invoice_number":{"type":"string"}},"required":["invoice_number"]}' required></textarea>
      <button type="submit" [disabled]="!name() || !jsonSchema()">Crear versión</button>
      @if (error()) { <p class="err">{{ error() }}</p> }
    </form>

    <h2>Existentes</h2>
    <table>
      <thead><tr><th>Nombre</th><th>Versión</th><th>Creado</th><th></th></tr></thead>
      <tbody>
        @for (s of items(); track s.id) {
          <tr>
            <td>{{ s.name }}</td>
            <td>v{{ s.version }}</td>
            <td>{{ s.createdAt | date:'short' }}</td>
            <td><button (click)="remove(s.id)">Borrar</button></td>
          </tr>
        }
        @empty { <tr><td colspan="4">Sin schemas</td></tr> }
      </tbody>
    </table>
  `,
  styles: [`
    .create { display:flex; flex-direction:column; gap:0.5rem; max-width:700px; margin-bottom:2rem; }
    input, textarea { background:#1c1c1c; color:#eee; border:1px solid #333; padding:0.5rem; border-radius:4px; font-family:inherit; }
    textarea { font-family:monospace; font-size:0.85rem; }
    button { background:#3b82f6; color:#fff; border:none; padding:0.5rem 0.9rem; border-radius:4px; cursor:pointer; }
    button:disabled { opacity:0.5; }
    table { width:100%; border-collapse:collapse; }
    th, td { text-align:left; padding:0.6rem; border-bottom:1px solid #2a2a2e; font-size:0.9rem; }
    th { color:#888; font-weight:500; }
    .err { color:#ef4444; }
  `]
})
export class SchemasListComponent implements OnInit {
  private api = inject(ApiService);
  items = signal<SchemaDto[]>([]);
  name = signal('');
  description = signal('');
  jsonSchema = signal('');
  error = signal<string | null>(null);

  ngOnInit() { this.load(); }

  load() { this.api.listSchemas().subscribe(s => this.items.set(s)); }

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

  remove(id: string) {
    if (!confirm('¿Borrar schema?')) return;
    this.api.deleteSchema(id).subscribe(() => this.load());
  }
}
