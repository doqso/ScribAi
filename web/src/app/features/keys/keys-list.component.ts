import { Component, inject, signal, OnInit } from '@angular/core';
import { DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ApiService } from '../../core/api.service';
import { KeyDto } from '../../core/models';

@Component({
  selector: 'app-keys-list',
  imports: [DatePipe, FormsModule],
  template: `
    <h1>API Keys</h1>

    <form (ngSubmit)="create()" class="create">
      <input [(ngModel)]="label" name="label" placeholder="Etiqueta" required />
      <input [(ngModel)]="defaultModel" name="defaultModel" placeholder="Modelo default (qwen2.5:7b-instruct)" />
      <label><input type="checkbox" [(ngModel)]="storeOriginals" name="storeOriginals" /> Guardar originales</label>
      <label><input type="checkbox" [(ngModel)]="isAdmin" name="isAdmin" /> Admin</label>
      <button type="submit" [disabled]="!label()">Generar key</button>
    </form>

    @if (newKey()) {
      <div class="notice">
        <strong>Key generada — se muestra una única vez:</strong>
        <code>{{ newKey() }}</code>
        <button (click)="copy(newKey()!)">Copiar</button>
      </div>
    }

    <table>
      <thead><tr><th>Etiqueta</th><th>Prefix</th><th>Admin</th><th>Store</th><th>Modelo</th><th>Creada</th><th>Último uso</th><th>Estado</th><th></th></tr></thead>
      <tbody>
        @for (k of items(); track k.id) {
          <tr>
            <td>{{ k.label }}</td>
            <td><code>{{ k.prefix }}</code></td>
            <td>{{ k.isAdmin ? 'sí' : 'no' }}</td>
            <td>{{ k.storeOriginals ? 'sí' : 'no' }}</td>
            <td>{{ k.defaultModel }}</td>
            <td>{{ k.createdAt | date:'short' }}</td>
            <td>{{ k.lastUsedAt ? (k.lastUsedAt | date:'short') : '—' }}</td>
            <td>{{ k.revokedAt ? 'revocada' : 'activa' }}</td>
            <td>@if (!k.revokedAt) { <button class="danger" (click)="revoke(k.id)">Revocar</button> }</td>
          </tr>
        }
        @empty { <tr><td colspan="9">Sin keys</td></tr> }
      </tbody>
    </table>
  `,
  styles: [`
    .create { display:flex; gap:0.5rem; flex-wrap:wrap; margin-bottom:1rem; align-items:center; }
    input { background:#1c1c1c; color:#eee; border:1px solid #333; padding:0.5rem; border-radius:4px; }
    button { background:#3b82f6; color:#fff; border:none; padding:0.5rem 0.9rem; border-radius:4px; cursor:pointer; }
    button.danger { background:#b91c1c; }
    .notice { background:#064e3b22; border:1px solid #064e3b; padding:1rem; border-radius:6px; margin-bottom:1rem; display:flex; gap:0.75rem; align-items:center; flex-wrap:wrap; }
    code { background:#000; padding:0.25rem 0.5rem; border-radius:3px; font-family:monospace; }
    table { width:100%; border-collapse:collapse; }
    th, td { text-align:left; padding:0.6rem; border-bottom:1px solid #2a2a2e; font-size:0.85rem; }
    th { color:#888; font-weight:500; }
  `]
})
export class KeysListComponent implements OnInit {
  private api = inject(ApiService);
  items = signal<KeyDto[]>([]);
  label = signal('');
  defaultModel = signal('');
  storeOriginals = signal(false);
  isAdmin = signal(false);
  newKey = signal<string | null>(null);

  ngOnInit() { this.load(); }

  load() { this.api.listKeys().subscribe(k => this.items.set(k)); }

  create() {
    this.api.createKey({
      label: this.label(),
      storeOriginals: this.storeOriginals(),
      defaultModel: this.defaultModel() || undefined,
      isAdmin: this.isAdmin()
    }).subscribe(k => {
      this.newKey.set(k.key);
      this.label.set(''); this.defaultModel.set('');
      this.storeOriginals.set(false); this.isAdmin.set(false);
      this.load();
    });
  }

  revoke(id: string) {
    if (!confirm('¿Revocar key?')) return;
    this.api.revokeKey(id).subscribe(() => this.load());
  }

  copy(v: string) { navigator.clipboard.writeText(v); }
}
