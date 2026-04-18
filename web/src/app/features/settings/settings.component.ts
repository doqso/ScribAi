import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { DatePipe } from '@angular/common';
import { ApiService } from '../../core/api.service';
import { GlobalSettingsDto, OllamaModelInfo, TenantSettingsDto } from '../../core/models';

@Component({
  selector: 'app-settings',
  imports: [FormsModule, DatePipe],
  template: `
    <h1>Configuración</h1>

    <section class="card">
      <h2>Tenant — modelos y webhooks</h2>
      @if (settings(); as s) {
        <div class="grid">
          <label>
            Modelo texto default
            <div class="row">
              <select [(ngModel)]="textModel" name="tm">
                <option value="">-- usar default global ({{ s.effective.defaultTextModel }}) --</option>
                @for (m of models(); track m.name) { <option [value]="m.name">{{ m.name }}</option> }
                <option value="__custom__">Otro (escribir)…</option>
              </select>
              @if (textModel() === '__custom__') {
                <input [(ngModel)]="textModelCustom" name="tmc" placeholder="nombre-modelo:tag" />
              }
              <button type="button" (click)="testM(effectiveTextModel())" [disabled]="testing()">Probar</button>
            </div>
          </label>

          <label>
            Modelo vision
            <div class="row">
              <select [(ngModel)]="visionModel" name="vm">
                <option value="">-- usar default global ({{ s.effective.visionModel }}) --</option>
                @for (m of models(); track m.name) { <option [value]="m.name">{{ m.name }}</option> }
                <option value="__custom__">Otro (escribir)…</option>
              </select>
              @if (visionModel() === '__custom__') {
                <input [(ngModel)]="visionModelCustom" name="vmc" placeholder="llama3.2-vision" />
              }
              <button type="button" (click)="testM(effectiveVisionModel())" [disabled]="testing()">Probar</button>
            </div>
          </label>

          <label>
            Timeout Ollama (segundos)
            <input type="number" [(ngModel)]="ollamaTimeout" name="ot" [placeholder]="'default: ' + s.effective.ollamaTimeoutSeconds" min="5" max="3600" />
          </label>

          <label>
            Webhook reintentos máximos
            <input type="number" [(ngModel)]="webhookAttempts" name="wa" [placeholder]="'default: ' + s.effective.webhookMaxAttempts" min="1" max="20" />
          </label>

          <label>
            Webhook timeout entrega (segundos)
            <input type="number" [(ngModel)]="webhookTimeout" name="wt" [placeholder]="'default: ' + s.effective.webhookTimeoutSeconds" min="1" max="300" />
          </label>

          <label>
            Thinking (modelos reasoning)
            <select [(ngModel)]="think" name="th">
              <option value="">Heredar default (no enviar)</option>
              <option value="on">Forzar ON (think: true)</option>
              <option value="off">Forzar OFF (think: false)</option>
            </select>
          </label>

          <label class="row">
            <input type="checkbox" [(ngModel)]="ocrEnabled" name="ocr" />
            Activar OCR (Tesseract). Si desactivado, PDFs escaneados e imágenes van directos al modelo visión.
          </label>
        </div>
        <div class="actions">
          <button (click)="saveTenant()" [disabled]="saving()">Guardar cambios</button>
          @if (tenantMsg()) { <span class="msg">{{ tenantMsg() }}</span> }
        </div>
        @if (testMsg()) { <div class="msg">{{ testMsg() }}</div> }
      }
      @else { <p>Cargando...</p> }
    </section>

    <section class="card">
      <h2>Global — CORS</h2>
      @if (globalCfg(); as g) {
        <div class="grid">
          <label class="row">
            <input type="checkbox" [(ngModel)]="allowAny" name="aao" />
            Permitir cualquier origen (no recomendado en producción)
          </label>
          <label>
            Orígenes permitidos (uno por línea)
            <textarea [(ngModel)]="originsText" name="ao" rows="4" placeholder="http://localhost:8090&#10;https://miapp.com"></textarea>
          </label>
        </div>
        <div class="actions">
          <button (click)="saveGlobal()" [disabled]="saving()">Guardar CORS + Seq</button>
          @if (corsMsg()) { <span class="msg">{{ corsMsg() }}</span> }
        </div>
      }
    </section>

    <section class="card">
      <h2>Global — Seq (logging)</h2>
      @if (globalCfg(); as g) {
        <div class="grid">
          <label class="row">
            <input type="checkbox" [(ngModel)]="seqEnabled" name="se" />
            Habilitar envío a Seq
          </label>

          <label>
            Application (etiqueta del proyecto en Seq)
            <input [(ngModel)]="appName" name="an" placeholder="ScribAi" />
          </label>

          <label>
            URL Seq
            <input [(ngModel)]="seqUrl" name="su" placeholder="http://host.docker.internal:5341" />
          </label>

          <label>
            API key Seq (opcional)
            @if (g.hasSeqApiKey && !changingKey()) {
              <div class="row">
                <input type="text" value="●●●●●●●● (configurada)" disabled />
                <button type="button" (click)="changingKey.set(true); seqKey.set('')">Cambiar</button>
                <button type="button" class="danger" (click)="clearKey.set(true); changingKey.set(true); seqKey.set('')">Borrar</button>
              </div>
            }
            @else {
              <input [(ngModel)]="seqKey" name="sk" type="password" placeholder="vacío = sin auth" />
            }
          </label>

          <label>
            Nivel mínimo
            <select [(ngModel)]="seqLevel" name="sl">
              <option>Verbose</option>
              <option>Debug</option>
              <option>Information</option>
              <option>Warning</option>
              <option>Error</option>
              <option>Fatal</option>
            </select>
          </label>

          <div>Actualizado: {{ g.updatedAt | date:'medium' }}</div>
        </div>
        <div class="actions">
          <button (click)="saveGlobal()" [disabled]="saving()">Guardar</button>
          <button type="button" (click)="testSeq()" [disabled]="!g.seqEnabled">Enviar log de prueba</button>
          @if (globalMsg()) { <span class="msg">{{ globalMsg() }}</span> }
        </div>
      }
      @else { <p>Cargando...</p> }
    </section>
  `,
  styles: [`
    .card { background:#161618; border:1px solid #2a2a2e; border-radius:8px; padding:1.25rem; margin-bottom:1.5rem; }
    .grid { display:grid; grid-template-columns:1fr 1fr; gap:1rem; }
    label { display:flex; flex-direction:column; gap:0.3rem; color:#bbb; font-size:0.9rem; }
    label.row { flex-direction:row; align-items:center; gap:0.5rem; }
    .row { display:flex; gap:0.5rem; align-items:center; }
    .row input, .row select { flex:1; }
    input, select { background:#1c1c1c; color:#eee; border:1px solid #333; padding:0.5rem; border-radius:4px; font-family:inherit; }
    button { background:#3b82f6; color:#fff; border:none; padding:0.5rem 0.9rem; border-radius:4px; cursor:pointer; }
    button.danger { background:#b91c1c; }
    button:disabled { opacity:0.5; cursor:not-allowed; }
    .actions { margin-top:1rem; display:flex; gap:0.75rem; align-items:center; }
    .msg { color:#9ca3af; font-size:0.85rem; }
  `]
})
export class SettingsComponent implements OnInit {
  private api = inject(ApiService);

  settings = signal<TenantSettingsDto | null>(null);
  globalCfg = signal<GlobalSettingsDto | null>(null);
  models = signal<OllamaModelInfo[]>([]);

  textModel = signal('');
  textModelCustom = signal('');
  visionModel = signal('');
  visionModelCustom = signal('');
  ollamaTimeout = signal<number | null>(null);
  webhookAttempts = signal<number | null>(null);
  webhookTimeout = signal<number | null>(null);
  think = signal<'' | 'on' | 'off'>('');
  ocrEnabled = signal(true);

  seqEnabled = signal(false);
  seqUrl = signal('');
  seqKey = signal('');
  seqLevel = signal('Information');
  appName = signal('ScribAi');
  allowAny = signal(false);
  originsText = signal('');
  changingKey = signal(false);
  clearKey = signal(false);
  corsMsg = signal<string | null>(null);

  saving = signal(false);
  testing = signal(false);
  tenantMsg = signal<string | null>(null);
  globalMsg = signal<string | null>(null);
  testMsg = signal<string | null>(null);

  ngOnInit() {
    this.loadAll();
  }

  loadAll() {
    this.api.getSettings().subscribe(s => {
      this.settings.set(s);
      this.textModel.set(s.defaultTextModel ?? '');
      this.visionModel.set(s.visionModel ?? '');
      this.ollamaTimeout.set(s.ollamaTimeoutSeconds);
      this.webhookAttempts.set(s.webhookMaxAttempts);
      this.webhookTimeout.set(s.webhookTimeoutSeconds);
      this.think.set(s.think === true ? 'on' : s.think === false ? 'off' : '');
      this.ocrEnabled.set(s.ocrEnabled ?? true);
    });
    this.api.listModels().subscribe({
      next: m => this.models.set(m),
      error: () => this.models.set([])
    });
    this.api.getGlobal().subscribe(g => {
      this.globalCfg.set(g);
      this.seqEnabled.set(g.seqEnabled);
      this.seqUrl.set(g.seqUrl ?? '');
      this.seqLevel.set(g.seqMinimumLevel);
      this.appName.set(g.applicationName ?? 'ScribAi');
      this.allowAny.set(g.allowAnyOrigin);
      this.originsText.set((g.allowedOrigins ?? []).join('\n'));
    });
  }

  effectiveTextModel(): string {
    return this.textModel() === '__custom__' ? this.textModelCustom() : (this.textModel() || this.settings()?.effective.defaultTextModel || '');
  }
  effectiveVisionModel(): string {
    return this.visionModel() === '__custom__' ? this.visionModelCustom() : (this.visionModel() || this.settings()?.effective.visionModel || '');
  }

  saveTenant() {
    this.saving.set(true);
    this.tenantMsg.set(null);
    const tm = this.textModel(), vm = this.visionModel(), th = this.think();
    const body = {
      defaultTextModel: tm === '__custom__' ? this.textModelCustom() : (tm || null),
      clearDefaultTextModel: tm === '' || (tm === '__custom__' && !this.textModelCustom()),
      visionModel: vm === '__custom__' ? this.visionModelCustom() : (vm || null),
      clearVisionModel: vm === '' || (vm === '__custom__' && !this.visionModelCustom()),
      ollamaTimeoutSeconds: this.ollamaTimeout(),
      clearOllamaTimeoutSeconds: this.ollamaTimeout() === null,
      webhookMaxAttempts: this.webhookAttempts(),
      clearWebhookMaxAttempts: this.webhookAttempts() === null,
      webhookTimeoutSeconds: this.webhookTimeout(),
      clearWebhookTimeoutSeconds: this.webhookTimeout() === null,
      think: th === 'on' ? true : th === 'off' ? false : null,
      clearThink: th === '',
      ocrEnabled: this.ocrEnabled(),
    };
    this.api.putSettings(body).subscribe({
      next: s => { this.settings.set(s); this.tenantMsg.set('Guardado'); this.saving.set(false); },
      error: e => { this.tenantMsg.set('Error: ' + (e.error?.error ?? e.message)); this.saving.set(false); }
    });
  }

  saveGlobal() {
    this.saving.set(true);
    this.globalMsg.set(null);
    const origins = this.originsText().split('\n').map(s => s.trim()).filter(s => s.length > 0);
    this.api.putGlobal({
      seqEnabled: this.seqEnabled(),
      seqUrl: this.seqUrl() || null,
      seqApiKey: this.seqKey() || null,
      clearSeqApiKey: this.clearKey(),
      seqMinimumLevel: this.seqLevel(),
      applicationName: this.appName() || 'ScribAi',
      allowedOrigins: origins,
      allowAnyOrigin: this.allowAny()
    }).subscribe({
      next: g => {
        this.globalCfg.set(g);
        this.globalMsg.set('Guardado');
        this.saving.set(false);
        this.changingKey.set(false);
        this.clearKey.set(false);
        this.seqKey.set('');
      },
      error: e => { this.globalMsg.set('Error: ' + (e.error?.error ?? e.message)); this.saving.set(false); }
    });
  }

  testM(name: string) {
    if (!name) return;
    this.testing.set(true);
    this.testMsg.set('Probando ' + name + '...');
    this.api.testModel(name).subscribe({
      next: r => {
        this.testing.set(false);
        this.testMsg.set(r.ok
          ? `✔ ${name} respondió en ${r.elapsedMs}ms: "${r.response?.trim().slice(0, 80)}"`
          : `✘ ${name} falló: ${r.error}`);
      },
      error: e => { this.testing.set(false); this.testMsg.set('Error: ' + e.message); }
    });
  }

  testSeq() {
    this.globalMsg.set('Enviando log de prueba...');
    this.api.testSeq().subscribe({
      next: r => this.globalMsg.set(r.ok ? '✔ Log enviado a Seq' : '✘ ' + r.error),
      error: e => this.globalMsg.set('Error: ' + (e.error?.error ?? e.message))
    });
  }
}
