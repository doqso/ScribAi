import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { environment } from '../../environments/environment';
import { ExtractionDto, ExtractionList, KeyCreatedDto, KeyDto, SchemaDto, WebhookDto } from './models';

@Injectable({ providedIn: 'root' })
export class ApiService {
  private http = inject(HttpClient);
  private base = environment.apiBaseUrl;

  listSchemas() { return this.http.get<SchemaDto[]>(`${this.base}/v1/schemas/`); }
  getSchema(id: string) { return this.http.get<SchemaDto>(`${this.base}/v1/schemas/${id}`); }
  createSchema(body: { name: string; jsonSchema: string; description?: string }) {
    return this.http.post<SchemaDto>(`${this.base}/v1/schemas/`, body);
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
}
