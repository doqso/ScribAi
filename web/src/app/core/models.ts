export interface SchemaDto {
  id: string;
  name: string;
  version: number;
  jsonSchema: string;
  description?: string;
  apiKeyId?: string | null;
  scope: 'global' | 'personal';
  createdAt: string;
  updatedAt?: string | null;
}

export interface ExtractionDto {
  id: string;
  status: 'queued' | 'running' | 'succeeded' | 'failed' | 'cancelled';
  sourceFilename: string;
  mime: string;
  sizeBytes: number;
  result?: string;
  error?: string;
  model: string;
  extractionMethod: string;
  tokensIn?: number;
  tokensOut?: number;
  extractedText?: string;
  createdAt: string;
  startedAt?: string;
  finishedAt?: string;
}

export interface ExtractionList {
  total: number;
  page: number;
  pageSize: number;
  items: ExtractionDto[];
}

export interface WebhookDto {
  id: string;
  url: string;
  events: string[];
  active: boolean;
  secret?: string;
  apiKeyId?: string | null;
  createdAt: string;
}

export interface KeyDto {
  id: string;
  label: string;
  prefix: string;
  isAdmin: boolean;
  storeOriginals: boolean;
  defaultModel: string;
  createdAt: string;
  lastUsedAt?: string;
  revokedAt?: string;
}

export interface KeyCreatedDto extends KeyDto {
  key: string;
}

export interface MeDto {
  tenantId: string;
  apiKeyId: string;
  isAdmin: boolean;
  storeOriginals: boolean;
  defaultModel: string;
}

export interface ResolvedTenantSettings {
  tenantId: string;
  defaultTextModel: string;
  visionModel: string;
  ollamaTimeoutSeconds: number;
  webhookMaxAttempts: number;
  webhookTimeoutSeconds: number;
  think: boolean | null;
  ocrEnabled: boolean;
}

export interface TenantSettingsDto {
  defaultTextModel: string | null;
  visionModel: string | null;
  ollamaTimeoutSeconds: number | null;
  webhookMaxAttempts: number | null;
  webhookTimeoutSeconds: number | null;
  think: boolean | null;
  ocrEnabled: boolean | null;
  effective: ResolvedTenantSettings;
}

export interface OllamaModelInfo {
  name: string;
  size: number | null;
}

export interface TestModelResult {
  ok: boolean;
  response: string | null;
  error: string | null;
  elapsedMs: number;
}

export interface GlobalSettingsDto {
  seqEnabled: boolean;
  seqUrl: string | null;
  hasSeqApiKey: boolean;
  seqMinimumLevel: string;
  applicationName: string;
  allowedOrigins: string[];
  allowAnyOrigin: boolean;
  updatedAt: string;
}

export interface SeqTestResult {
  ok: boolean;
  error: string | null;
}

export interface AuditEventDto {
  id: string;
  tenantId: string | null;
  apiKeyId: string | null;
  eventType: string;
  target: string | null;
  details: string | null;
  createdAt: string;
}

export interface AuditPage {
  total: number;
  page: number;
  pageSize: number;
  items: AuditEventDto[];
}
