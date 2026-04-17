<div align="center">

# ScribAi

**Extractor de datos de documentos 100% local, impulsado por Ollama.**

Sube un documento. Define un JSON Schema. Recibe JSON estructurado. Sin enviar tus datos a ningún servicio externo.

[![.NET](https://img.shields.io/badge/.NET-10-512BD4?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![Angular](https://img.shields.io/badge/Angular-19-DD0031?logo=angular&logoColor=white)](https://angular.dev/)
[![Ollama](https://img.shields.io/badge/Ollama-local-000000?logo=ollama&logoColor=white)](https://ollama.com/)
[![Docker](https://img.shields.io/badge/Docker-Compose-2496ED?logo=docker&logoColor=white)](https://www.docker.com/)
[![PostgreSQL](https://img.shields.io/badge/PostgreSQL-16-4169E1?logo=postgresql&logoColor=white)](https://www.postgresql.org/)
[![License](https://img.shields.io/badge/license-MIT-brightgreen)](LICENSE)

</div>

---

## ¿Qué hace?

ScribAi recibe documentos administrativos heterogéneos (facturas, contratos, albaranes, emails, escaneados…) y los convierte en **JSON estructurado** que cumple exactamente el schema que tú defines. Todo se procesa en tu infraestructura: los modelos LLM corren en Ollama local, los originales se guardan opcionalmente en MinIO, y la API está pensada para integrarse desde cualquier servicio vía API keys + webhooks.

### Casos de uso

- Extraer cabeceras y líneas de **facturas** de proveedores distintos.
- Normalizar **contratos** o **formularios** a un esquema único.
- Pipeline de **onboarding** (DNIs, justificantes, nóminas) sin mover datos a la nube.
- Cualquier flujo interno donde “datos sensibles” = modelo local, obligatorio.

---

## Arquitectura

```
┌────────────┐    ┌──────────────┐     ┌─────────────┐
│  Angular   │───▶│  API .NET 10 │────▶│  PostgreSQL │
│  (Nginx)   │    │  Minimal API │     └─────────────┘
└────────────┘    │              │     ┌─────────────┐
                  │              │────▶│    Redis    │
                  │              │     └─────────────┘
                  │              │     ┌─────────────┐
                  │              │────▶│   MinIO     │
                  │              │     └─────────────┘
                  │              │     ┌─────────────┐
                  │              │────▶│  Ollama ext │
                  │              │     └─────────────┘
                  │              │     ┌─────────────┐
                  │              │────▶│  Seq  ext   │ (logs)
                  └──────┬───────┘     └─────────────┘
                         │
                         ▼
                 ┌──────────────┐
                 │ Worker async │
                 │ Redis Streams│
                 └──────────────┘
```

**Dos servicios en Compose** (`api` + `web`) + stack de dependencias (`postgres`, `redis`, `minio`). **Ollama y Seq corren fuera del stack**: tú decides dónde. ScribAi solo necesita las URLs (Ollama via env, Seq via panel admin).

---

## Pipeline de extracción

```
documento ──▶ detección MIME (magic bytes)
          ├─ PDF nativo       → PdfPig (texto)           ┐
          ├─ PDF escaneado    → PDFtoImage @200dpi       │
          │                     → Tesseract CLI por página│
          ├─ docx / xlsx      → OpenXml                  ├─▶ LLM texto
          ├─ eml / msg        → MimeKit                  │   (qwen2.5:7b-instruct)
          ├─ txt / csv        → lector plano             │         │
          └─ imagen / TIFF    → ImageSharp + Tesseract   │         │
                                   │                              │
                                   ▼ confianza < 0.6              │
                               LLM vision (llama3.2-vision) ──────┤
                                                                  ▼
                                                       JSON ⟶ validación
                                                           NJsonSchema
                                                              │
                                           ✔ match schema ────┘
                                           ✘ retry 1× con feedback del error
```

- **LLM siempre** para construir el JSON final (texto o vision).
- **OCR (Tesseract CLI)** para imágenes y PDFs escaneados (rasterizados con PDFium).
- **Vision model** como fallback si confianza OCR < 0.6.
- **Structured outputs de Ollama** (`format` = JSON Schema) + validación `NJsonSchema` + reintento con el error como feedback: el LLM casi nunca miente el schema.

---

## Características

### Core
- **Multi-tenant** con API keys. Admin ve todo el tenant. Keys no-admin solo ven sus propias extracciones (schemas/webhooks sí son compartidos por tenant).
- **Esquemas versionados** — `v1`, `v2`, `v3` sin romper integraciones.
- **Sync + async** — `<2 MB` en respuesta HTTP; más grandes → cola Redis Streams → webhook al terminar.
- **Webhooks HMAC** — firma `X-Scribai-Signature` (sha256), reintentos exponenciales configurables.
- **Retención configurable** — `store_originals` por API key: guarda en MinIO o descarta en memoria.

### Panel admin (hot-reload, sin reinicio)
- **Configuración por tenant** — modelos default/vision (dropdown con modelos instalados via `/api/tags`), timeout Ollama, webhooks max attempts + delivery timeout.
- **Configuración global** — Seq URL + API key (AES-GCM cifrada) + nivel mínimo + `Application` label; lista de orígenes CORS editable + toggle allow-any.
- **Audit log** — cada mutación (settings, schemas, webhooks, keys) queda registrada con `tenant_id`, `api_key_id`, evento, detalle JSONB.
- **Test de modelo / Seq** — botones "Probar" invocan el backend real.

### Observabilidad
- **Serilog** → stdout + Seq (dinámico, opcional). Logs enriquecidos con `TenantId`, `ApiKeyId`, `ExtractionId`, duraciones por paso, tokens.
- **`Application` tag** en Seq para separar instancias de ScribAi.

### UX
- **Editor Monaco** (lazy-loaded) para crear schemas y ver JSON resultado con syntax highlighting.
- **Comparador doc ↔ JSON** en detail de extracción (PDF embebido / imagen / texto a la izquierda, JSON Monaco readonly a la derecha).
- **Re-extraer sin reupload** — si el original se guardó en MinIO, botón para re-ejecutar con otro schema o modelo (genera nueva extracción, preserva historial).
- **Streaming SSE** — toggle "ver progreso en vivo" durante upload; recibes eventos `started → extracting_text → calling_llm → validating_schema → done` por `text/event-stream`.
- **Export** — descarga JSON de una extracción; descarga CSV agregando múltiples extracciones con mismo schema (dot-notation para campos anidados).

### Plataforma
- **OpenAPI** — `/openapi/v1.json`.
- **Cifrado de secretos** — AES-256-GCM con `SCRIBAI_SECRETS_KEY` master key.
- **EF Core migrations** automáticas al arranque.

---

## Arranque rápido

```bash
# 1. Configura
cp .env.example .env
# genera la clave master de cifrado (obligatoria):
openssl rand -base64 32     # copia en SCRIBAI_SECRETS_KEY del .env

# 2. Levanta Ollama externo y baja los modelos
ollama serve
ollama pull qwen2.5:7b-instruct      # texto
ollama pull llama3.2-vision          # fallback imágenes/escaneados

# 3. (Opcional) Levanta Seq para logs estructurados
docker run -d -p 5341:80 -e ACCEPT_EULA=Y datalust/seq:latest

# 4. Levanta el stack ScribAi
docker compose up -d --build

# 5. Bootstrap inicial (solo funciona la primera vez)
curl -X POST http://localhost:8082/v1/bootstrap \
  -H "Content-Type: application/json" \
  -d '{"tenantName":"Acme","keyLabel":"admin"}'
# copia el campo "key" (sk_...) — no vuelve a mostrarse
```

Listo:

- **UI Web** → http://localhost:8090
- **API** → http://localhost:8082
- **MinIO Console** → http://localhost:9001 (`scribai` / `scribaipass`)
- **Seq (si lo levantaste)** → http://localhost:5341 (config URL en UI → Configuración → Global)

---

## API en 30 segundos

Header obligatorio en toda petición: `X-API-Key: sk_...`

```http
# Extracciones
POST   /v1/extractions                   # multipart: file + (schema | schemaId) + async? + webhookUrl? + model?
POST   /v1/extractions/stream            # igual pero respuesta SSE con progreso
POST   /v1/extractions/{id}/rerun        # { schemaId? | schema?, model? } — reusa original guardado
GET    /v1/extractions/{id}
GET    /v1/extractions?status=&page=&pageSize=
GET    /v1/extractions/{id}/original     # descarga archivo original (si se guardó)
GET    /v1/extractions/{id}/export.json
GET    /v1/extractions/export.csv?schemaId=X

# Schemas (versionados)
POST   /v1/schemas                       # { name, jsonSchema, description? }
GET    /v1/schemas
GET    /v1/schemas/by-name/{name}/latest
DELETE /v1/schemas/{id}

# Webhooks
POST   /v1/webhooks                      # { url, events[] }
POST   /v1/webhooks/{id}/test
GET    /v1/webhooks
DELETE /v1/webhooks/{id}

# Identidad
GET    /v1/me                            # contexto actual (tenantId, apiKeyId, isAdmin)

# Admin-only
POST   /v1/keys                          # genera key nueva; plain devuelto UNA vez
GET    /v1/keys
DELETE /v1/keys/{id}

GET    /v1/settings                      # config tenant (con defaults resueltos)
PUT    /v1/settings
GET    /v1/settings/models               # proxy a Ollama /api/tags
POST   /v1/settings/models/test

GET    /v1/admin/global                  # Seq + CORS + Application
PUT    /v1/admin/global
POST   /v1/admin/global/seq/test

GET    /v1/admin/audit?page=&eventType=

# Bootstrap + salud
POST   /v1/bootstrap                     # crea primer tenant (solo funciona si BD vacía)
GET    /healthz
GET    /readyz
GET    /openapi/v1.json
```

Ejemplo subiendo una factura contra un schema guardado:

```bash
curl -X POST http://localhost:8082/v1/extractions/ \
  -H "X-API-Key: sk_..." \
  -F file=@factura.pdf \
  -F schemaId=01JXYZ...
```

Respuesta:

```json
{
  "id": "…",
  "status": "succeeded",
  "model": "qwen2.5:7b-instruct",
  "extractionMethod": "PdfText",
  "result": "{\"invoice_number\":\"291769\",\"total\":26.00, … }",
  "tokensIn": 1840,
  "tokensOut": 210
}
```

---

## Configuración

### Variables de entorno (infraestructura)

| Variable | Default | Descripción |
|---|---|---|
| `SCRIBAI_SECRETS_KEY` | **(obligatoria)** | Clave master AES-256 base64 (32 bytes) para cifrar secretos en BD |
| `ConnectionStrings__Postgres` | `Host=postgres;...` | Conexión BD |
| `Ollama__BaseUrl` | `http://host.docker.internal:11434` | URL Ollama |
| `Ollama__DefaultModel` | `qwen2.5:7b-instruct` | Modelo texto por defecto (override por tenant en panel) |
| `Ollama__VisionModel` | `llama3.2-vision` | Modelo visión (override por tenant) |
| `Redis__ConnectionString` | `redis:6379` | — |
| `Storage__Endpoint` | `minio:9000` | — |
| `Processing__SyncMaxBytes` | `2097152` | A partir de aquí, async obligatorio |
| `Processing__MaxUploadBytes` | `52428800` | Límite duro de upload |
| `Processing__TesseractLangs` | `spa+eng` | Idiomas OCR |
| `Processing__OcrConfidenceThreshold` | `0.6` | Por debajo → fallback vision |
| `Processing__WebhookMaxAttempts` | `5` | Reintentos webhook (override por tenant) |

Puertos configurables vía `.env`: `API_PORT`, `WEB_PORT`, `POSTGRES_PORT`, `REDIS_PORT`, `MINIO_PORT`.

### Ajustes en runtime (panel admin)

| Setting | Scope | Dónde |
|---|---|---|
| Modelo texto / vision | tenant | `/settings` |
| Timeout Ollama | tenant | `/settings` |
| Webhook max attempts / timeout | tenant | `/settings` |
| Seq URL / API key / nivel mínimo / Application | global | `/settings` (sección Global) |
| CORS: lista orígenes / allow-any | global | `/settings` (sección Global) |
| Audit log readonly | tenant | `/audit` |

---

## Privacidad y roles

| Recurso | Admin (tenant) | No-admin (solo su key) |
|---|---|---|
| Extractions | todas del tenant | solo las subidas con su key |
| Schemas | compartidos | compartidos |
| Webhooks | todos del tenant | solo los propios; dispatcher solo dispara los del ApiKeyId de la extracción |
| API Keys CRUD | sí | — |
| Settings / Audit / Global config | sí | — |

---

## Estructura del repositorio

```
ScribAi/
├── api/                              # .NET 10 Minimal API
│   ├── Auth/                         # ApiKeyMiddleware, hasher, TenantContext
│   ├── Cors/DynamicCorsPolicyProvider.cs   # CORS leído en runtime de GlobalSettings
│   ├── Data/                         # DbContext + entidades + migraciones
│   ├── Endpoints/                    # handlers por recurso + Me, Settings, AdminGlobal, Audit, Export, Streaming
│   ├── Logging/                      # DynamicSeqSink + TenantEnricher
│   ├── Pipeline/
│   │   ├── DocumentRouter.cs
│   │   ├── Extractors/               # Pdf (PDFtoImage + Tesseract) / Image / Office / Email / PlainText
│   │   ├── Ocr/TesseractOcr.cs       # CLI wrapper
│   │   └── Llm/OllamaExtractor.cs    # /api/chat + format=JSON Schema + retry + timeout configurable
│   ├── Jobs/                         # Redis Streams worker + webhook dispatcher
│   ├── Options/                      # config tipada estática
│   ├── Security/SecretsProtector.cs  # AES-256-GCM
│   ├── Services/                     # ExtractionService + TenantSettings + GlobalSettingsProvider + AuditLogger
│   ├── Storage/                      # MinIO blob store
│   ├── Program.cs                    # bootstrap
│   └── Dockerfile                    # runtime con tesseract-ocr + PDFium deps
├── web/                              # Angular 19 SPA
│   ├── src/app/core/                 # ApiService, auth + admin guards, Monaco wrapper
│   ├── src/app/features/             # login, layout, extractions, schemas, webhooks, keys, settings, audit
│   ├── nginx.conf                    # proxy /api → api:8080
│   └── Dockerfile                    # build node → runtime nginx + monaco assets
├── tests/ScribAi.Api.Tests/          # xUnit
├── docker-compose.yml
├── .env.example
└── README.md
```

---

## Tests

```bash
dotnet test
```

Cobertura: hash de API keys, detección de MIME por magic bytes, validación + retry del extractor LLM con mock HTTP, roundtrip del cifrado AES-GCM + resistencia a tampering.

---

## Roadmap

- [ ] **Rate limit por tenant** con límites configurables desde panel.
- [ ] **Log de entregas webhooks** visible en UI (tabla `webhook_deliveries` ya existe).
- [ ] **Importador de plantillas** (factura ES, DNI, nómina) preconfiguradas.
- [ ] **Métricas tenant** — extracciones/día, tokens, tasa éxito, modelos usados.
- [ ] **CI GitHub Actions** — `dotnet test` + `ng build` + docker build on PR.
- [ ] **Testcontainers** — tests integración con postgres/redis/minio reales.
- [ ] **OpenTelemetry traces** hacia Tempo/Jaeger.

---

## Licencia

MIT.
