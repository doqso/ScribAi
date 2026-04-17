export interface SchemaDto {
  id: string;
  name: string;
  version: number;
  jsonSchema: string;
  description?: string;
  createdAt: string;
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
