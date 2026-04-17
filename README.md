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
                  └──────┬───────┘     └─────────────┘
                         │
                         ▼
                 ┌──────────────┐
                 │ Worker async │
                 │ Redis Streams│
                 └──────────────┘
```

**Dos servicios en Compose** (`api` + `web`) + stack de dependencias (`postgres`, `redis`, `minio`). **Ollama corre fuera del stack**: tú decides si en el host, en otro contenedor, o en otra máquina con GPU. ScribAi solo necesita la URL.

---

## Pipeline de extracción

```
documento ──▶ detección MIME (magic bytes)
          ├─ PDF nativo       → PdfPig              ┐
          ├─ docx / xlsx      → OpenXml             │
          ├─ eml / msg        → MimeKit             ├─▶ LLM texto (qwen2.5:7b-instruct)
          ├─ txt / csv        → lector plano        │         │
          └─ imagen / TIFF    → ImageSharp + Tesseract CLI   │
                                   │                          │
                                   ▼ confianza < 0.6          │
                               LLM vision (llama3.2-vision) ──┤
                                                              ▼
                                                   JSON ⟶ validación
                                                       NJsonSchema
                                                          │
                                       ✔ match schema ────┘
                                       ✘ retry 1× con feedback del error
```

- **LLM se usa siempre** para construir el JSON final (texto o vision).
- **OCR (Tesseract CLI)** solo cuando el input es imagen.
- **Vision model** actúa como fallback si Tesseract no se fía de su lectura, o si el PDF es escaneado puro.
- **Structured outputs de Ollama** (`format` = JSON Schema) + validación `NJsonSchema` + reintento con el error como feedback: el LLM casi nunca miente el schema.

---

## Características

- **Multi-tenant** — cada API key pertenece a un tenant, con sus propios schemas, webhooks, extracciones. Aislamiento total en BD.
- **Esquemas versionados** — crear `v1`, `v2`, `v3` sin romper integraciones existentes.
- **Sync + async** — documentos pequeños (`< 2 MB` por defecto) se procesan en la respuesta HTTP; los grandes van a cola Redis Streams y se notifica por webhook cuando acaban.
- **Webhooks con HMAC** — firma `X-Scribai-Signature` (sha256), reintentos exponenciales hasta 5 veces.
- **Retención configurable** — `store_originals` por API key: guarda el fichero en MinIO o descártalo al momento.
- **Privacidad total** — ningún dato sale de tu red. Ollama, BD, storage, API: todo self-hosted.
- **OpenAPI** — `GET /openapi/v1.json` expone el contrato entero.

---

## Arranque rápido

```bash
# 1. Configura
cp .env.example .env

# 2. Levanta Ollama externo y baja los modelos
ollama serve
ollama pull qwen2.5:7b-instruct      # texto
ollama pull llama3.2-vision          # fallback imágenes/escaneados

# 3. Levanta el stack
docker compose up -d --build

# 4. Bootstrap inicial (solo funciona la primera vez)
curl -X POST http://localhost:8082/v1/bootstrap \
  -H "Content-Type: application/json" \
  -d '{"tenantName":"Acme","keyLabel":"admin"}'
# copia el campo "key" (sk_...) — no vuelve a mostrarse
```

Listo:

- **UI Web** → http://localhost:8090
- **API** → http://localhost:8082
- **MinIO Console** → http://localhost:9001 (`scribai` / `scribaipass`)

---

## API en 30 segundos

Header obligatorio en toda petición: `X-API-Key: sk_...`

```http
POST   /v1/extractions          # multipart: file + (schema | schemaId) + async? + webhookUrl? + model?
GET    /v1/extractions/{id}
GET    /v1/extractions?status=&page=&pageSize=

POST   /v1/schemas              # { name, jsonSchema, description? }
GET    /v1/schemas
GET    /v1/schemas/by-name/{name}/latest

POST   /v1/webhooks             # { url, events[] }
POST   /v1/webhooks/{id}/test

POST   /v1/keys                 # admin-only; devuelve la key en claro UNA vez

GET    /healthz   /readyz
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

## Configuración (variables de entorno)

| Variable | Default | Descripción |
|---|---|---|
| `ConnectionStrings__Postgres` | `Host=postgres;Database=scribai;...` | Conexión BD |
| `Ollama__BaseUrl` | `http://host.docker.internal:11434` | URL de tu Ollama |
| `Ollama__DefaultModel` | `qwen2.5:7b-instruct` | Modelo texto por defecto |
| `Ollama__VisionModel` | `llama3.2-vision` | Modelo visión fallback |
| `Redis__ConnectionString` | `redis:6379` | — |
| `Storage__Endpoint` | `minio:9000` | — |
| `Processing__SyncMaxBytes` | `2097152` | A partir de aquí, async obligatorio |
| `Processing__MaxUploadBytes` | `52428800` | Límite duro de upload |
| `Processing__TesseractLangs` | `spa+eng` | Idiomas OCR |
| `Processing__OcrConfidenceThreshold` | `0.6` | Por debajo → fallback vision |
| `Processing__WebhookMaxAttempts` | `5` | Reintentos webhook |

Puertos configurables vía `.env`: `API_PORT`, `WEB_PORT`, `POSTGRES_PORT`, `REDIS_PORT`, `MINIO_PORT`.

---

## Estructura del repositorio

```
ScribAi/
├── api/                              # .NET 10 Minimal API
│   ├── Auth/                         # ApiKeyMiddleware, hasher, tenant context
│   ├── Data/                         # DbContext + entidades + migraciones
│   ├── Endpoints/                    # handlers por recurso
│   ├── Pipeline/
│   │   ├── DocumentRouter.cs         # detección MIME → extractor
│   │   ├── Extractors/               # Pdf / Image / Office / Email / PlainText
│   │   ├── Ocr/TesseractOcr.cs       # wrapper CLI tesseract
│   │   └── Llm/OllamaExtractor.cs    # /api/chat + format=JSON Schema + retry
│   ├── Jobs/                         # Redis Streams worker + webhook dispatcher
│   ├── Options/                      # config tipada
│   ├── Services/                     # ExtractionService (orquestación)
│   ├── Storage/                      # MinIO blob store
│   ├── Program.cs                    # bootstrap
│   └── Dockerfile                    # runtime con tesseract-ocr + spa/eng
├── web/                              # Angular 19 SPA
│   ├── src/app/core/                 # ApiService, interceptor, auth guard
│   ├── src/app/features/             # login, layout, extractions, schemas, webhooks, keys
│   ├── nginx.conf                    # proxy /api → api:8080
│   └── Dockerfile                    # build node → runtime nginx
├── tests/ScribAi.Api.Tests/          # xUnit: hasher, MIME, LLM extractor
├── docker-compose.yml
├── .env.example
└── README.md
```

---

## Tests

```bash
dotnet test
```

Cobertura inicial: hash de API keys, detección de MIME por magic bytes, validación + retry del extractor LLM con mock HTTP.

---

## Roadmap / pendientes

- [ ] **Rasterizar PDFs escaneados** página a página (hoy el fallback manda el PDF entero a vision). Candidato: `Docnet.Core` o `PDFtoImage`.
- [ ] **Editor JSON Schema con Monaco** en la UI (ahora textarea plano).
- [ ] **Diff entre versiones de schema** en UI.
- [ ] **Log de entregas de webhooks** visible en UI.
- [ ] **Rate limit por tenant**.
- [ ] **Importador de plantillas** (factura, DNI, nómina ES) preconfiguradas.

---

## Licencia

MIT.
