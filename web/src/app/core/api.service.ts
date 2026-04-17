import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { environment } from '../../environments/environment';
import { AuditPage, ExtractionDto, ExtractionList, GlobalSettingsDto, KeyCreatedDto, KeyDto, MeDto, OllamaModelInfo, SchemaDto, SeqTestResult, TenantSettingsDto, TestModelResult, WebhookDto } from './models';

@Injectable({ providedIn: 'root' })
export class ApiService {
  private http = inject(HttpClient);
  private base = environment.apiBaseUrl;

  listSchemas() { return this.http.get<SchemaDto[]>(`${this.base}/v1/schemas/`); }
  getSchema(id: string) { return this.http.get<SchemaDto>(`${this.base}/v1/schemas/${id}`); }
  createSchema(body: { name: string; jsonSchema: string; description?: string }) {
    return this.http.post<SchemaDto>(`${this.base}/v1/schemas/`, body);
  }
  updateSchema(id: string, body: { jsonSchema: string; description?: string }) {
    return this.http.put<SchemaDto>(`${this.base}/v1/schemas/${id}`, body);
  }
  deleteSchema(id: string) { return this.http.delete(`${this.base}/v1/schemas/${id}`); }

  listExtractions(page = 1, pageSize = 20, status?: string) {
    let q = `page=${page}&pageSize=${pageSize}`;
    if (status) q += `&status=${status}`;
    return this.http.get<ExtractionList>(`${this.base}/v1/extractions/?${q}`);
  }
  getExtraction(id: string) {
    return this.http.get<ExtractionDto>(`${this.base}/v1/extractions/${id}`);
  }
  rerunExtraction(id: string, body: { schemaId?: string; schema?: string; model?: string; async?: boolean }) {
    return this.http.post<ExtractionDto>(`${this.base}/v1/extractions/${id}/rerun`, body);
  }

  exportJson(id: string) {
    return this.http.get(`${this.base}/v1/extractions/${id}/export.json`, { responseType: 'blob' });
  }

  getOriginal(id: string) {
    return this.http.get(`${this.base}/v1/extractions/${id}/original`, { responseType: 'blob' });
  }

  exportCsv(schemaId?: string, schemaName?: string) {
    let q = '';
    if (schemaId) q += `schemaId=${schemaId}&`;
    if (schemaName) q += `schemaName=${encodeURIComponent(schemaName)}&`;
    return this.http.get(`${this.base}/v1/extractions/export.csv?${q}`, { responseType: 'blob' });
  }

  uploadExtraction(file: File, opts: { schemaId?: string; schema?: string; async?: boolean; webhookUrl?: string; model?: string }) {
    const fd = new FormData();
    fd.append('file', file);
    if (opts.schemaId) fd.append('schemaId', opts.schemaId);
    if (opts.schema) fd.append('schema', opts.schema);
    if (opts.async !== undefined) fd.append('async', String(opts.async));
    if (opts.webhookUrl) fd.append('webhookUrl', opts.webhookUrl);
    if (opts.model) fd.append('model', opts.model);
    return this.http.post<ExtractionDto>(`${this.base}/v1/extractions/`, fd);
  }

  listWebhooks() { return this.http.get<WebhookDto[]>(`${this.base}/v1/webhooks/`); }
  createWebhook(body: { url: string; events?: string[] }) {
    return this.http.post<WebhookDto>(`${this.base}/v1/webhooks/`, body);
  }
  deleteWebhook(id: string) { return this.http.delete(`${this.base}/v1/webhooks/${id}`); }
  testWebhook(id: string) { return this.http.post(`${this.base}/v1/webhooks/${id}/test`, {}); }

  listKeys() { return this.http.get<KeyDto[]>(`${this.base}/v1/keys/`); }
  createKey(body: { label: string; storeOriginals: boolean; defaultModel?: string; isAdmin: boolean }) {
    return this.http.post<KeyCreatedDto>(`${this.base}/v1/keys/`, body);
  }
  revokeKey(id: string) { return this.http.delete(`${this.base}/v1/keys/${id}`); }

  me() { return this.http.get<MeDto>(`${this.base}/v1/me`); }

  getSettings() { return this.http.get<TenantSettingsDto>(`${this.base}/v1/settings/`); }
  putSettings(body: Partial<Record<string, unknown>>) {
    return this.http.put<TenantSettingsDto>(`${this.base}/v1/settings/`, body);
  }
  listModels() { return this.http.get<OllamaModelInfo[]>(`${this.base}/v1/settings/models`); }
  testModel(model: string) {
    return this.http.post<TestModelResult>(`${this.base}/v1/settings/models/test`, { model });
  }

  getGlobal() { return this.http.get<GlobalSettingsDto>(`${this.base}/v1/admin/global/`); }
  putGlobal(body: { seqEnabled: boolean; seqUrl: string | null; seqApiKey: string | null; clearSeqApiKey: boolean; seqMinimumLevel: string; applicationName: string; allowedOrigins?: string[]; allowAnyOrigin?: boolean }) {
    return this.http.put<GlobalSettingsDto>(`${this.base}/v1/admin/global/`, body);
  }
  testSeq() { return this.http.post<SeqTestResult>(`${this.base}/v1/admin/global/seq/test`, {}); }

  listAudit(page = 1, pageSize = 50, eventType?: string) {
    let q = `page=${page}&pageSize=${pageSize}`;
    if (eventType) q += `&eventType=${encodeURIComponent(eventType)}`;
    return this.http.get<AuditPage>(`${this.base}/v1/admin/audit?${q}`);
  }
}
