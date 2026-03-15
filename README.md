# 🧠 MediMind AI — Real-Time Clinical Intelligence & Multi-Agent Platform

[![.NET 9](https://img.shields.io/badge/.NET-9.0-purple)](https://dotnet.microsoft.com/)
[![Semantic Kernel](https://img.shields.io/badge/Semantic%20Kernel-1.54-blue)](https://github.com/microsoft/semantic-kernel)
[![Anthropic Claude](https://img.shields.io/badge/Claude-Sonnet%204-orange)](https://www.anthropic.com/)
[![Qdrant](https://img.shields.io/badge/Qdrant-Vector%20DB-red)](https://qdrant.tech/)
[![EF Core](https://img.shields.io/badge/EF%20Core-9.0-green)](https://docs.microsoft.com/en-us/ef/core/)
[![Docker](https://img.shields.io/badge/Docker-Compose-blue)](https://docs.docker.com/compose/)
[![License](https://img.shields.io/badge/License-Proprietary-lightgrey)]()

> *"Give every clinician instant access to the world's medical knowledge, grounded in their patient's specific context — in real time."*

An enterprise-grade clinical intelligence platform that combines **multi-agent AI orchestration**, **Retrieval-Augmented Generation (RAG)**, and **real-time streaming** to deliver evidence-based clinical decision support at the point of care.

**Compliance:** HIPAA · HL7 FHIR R4 · GDPR · ISO 27001

---

## Table of Contents

- [Overview](#overview)
- [Architecture](#architecture)
- [Multi-Agent System](#multi-agent-system)
- [RAG Pipeline](#rag-pipeline)
- [Features](#features)
- [Technology Stack](#technology-stack)
- [Project Structure](#project-structure)
- [Database Schema](#database-schema)
- [Prerequisites](#prerequisites)
- [Quick Start](#quick-start)
- [Configuration](#configuration)
- [API Endpoints](#api-endpoints)
- [SignalR Hub](#signalr-hub)
- [Testing](#testing)
- [Docker Compose Services](#docker-compose-services)
- [Observability](#observability)
- [Security & Compliance](#security--compliance)
- [Milestones](#milestones)
- [License](#license)

---

## Overview

MediMind AI uses a **Retrieval-Augmented Generation (RAG)** pipeline to ground every AI response in verified clinical sources — drug formularies, treatment guidelines (WHO, CDC, NICE), EHR records, and research literature — ensuring accurate, hallucination-resistant, cited answers.

### Core Technology Pillars

| Pillar | Technology | Role |
|---|---|---|
| **LLM** | Anthropic Claude Sonnet 4 | Clinical reasoning & response generation |
| **Vector Store** | Qdrant | Semantic medical knowledge retrieval |
| **AI Orchestration** | Microsoft Semantic Kernel | Multi-agent planning, skills, memory, plugins |
| **Relational DB** | SQL Server + EF Core 9 | Patient records, audit logs, structured data |
| **Local Testing** | Docker Compose + Aspire | Full local dev environment with all services |
| **Multi-Agent** | SK Agent Framework | Specialist agents per clinical domain |

---

## Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                    MediMind.BlazorUI                        │
│               Blazor Server · SignalR Client                │
└──────────────────────┬──────────────────────────────────────┘
                       │ SignalR / REST
┌──────────────────────▼──────────────────────────────────────┐
│                     MediMind.API                            │
│  Minimal API · SignalR Hub · PII Middleware · Audit Logging │
└──────────────────────┬──────────────────────────────────────┘
                       │
┌──────────────────────▼──────────────────────────────────────┐
│                    MediMind.Core                            │
│  Agents · Plugins · RAG Pipeline · Domain Models · Interfaces│
│                                                             │
│  ┌─────────┐ ┌──────────┐ ┌─────────┐ ┌─────────────────┐  │
│  │Orchestr-│ │  Drug    │ │Diagnosis│ │   Discharge     │  │
│  │  ator   │→│  Agent   │ │  Agent  │ │     Agent       │  │
│  │  Agent  │ │          │ │         │ │                 │  │
│  └─────────┘ └──────────┘ └─────────┘ └─────────────────┘  │
│                                                             │
│  ┌──────────────────────────────────────────────────────┐   │
│  │ RAG Pipeline: Embed → Search → Rerank → Build Context│   │
│  └──────────────────────────────────────────────────────┘   │
└──────────────────────┬──────────────────────────────────────┘
                       │
┌──────────────────────▼──────────────────────────────────────┐
│                MediMind.Infrastructure                      │
│  EF Core · Qdrant · Anthropic Client · Redis · PII Scrubber│
└─────────┬──────────┬───────────┬───────────┬────────────────┘
          │          │           │           │
     SQL Server   Qdrant     Claude API    Redis
```

---

## Multi-Agent System

MediMind AI uses **Microsoft Semantic Kernel's Agent Framework** with specialist agents that collaborate via an orchestrator.

```
┌──────────────────────────────────────────────────────────────────────┐
│                        USER / CLIENT LAYER                           │
│         Blazor Server UI  ·  REST API  ·  SignalR (Streaming)        │
└───────────────────────────────┬──────────────────────────────────────┘
                                │
┌───────────────────────────────▼──────────────────────────────────────┐
│              SEMANTIC KERNEL — ORCHESTRATOR AGENT                    │
│   • Receives user query                                              │
│   • Builds execution plan via SK Planner (Handlebars)                │
│   • Routes sub-tasks to specialist agents                            │
│   • Aggregates and synthesizes multi-agent responses                 │
└──────┬──────────┬──────────┬──────────┬──────────┬───────────────────┘
       │          │          │          │          │
┌──────▼───┐ ┌───▼────┐ ┌───▼────┐ ┌───▼────┐ ┌───▼──────────┐
│  Drug    │ │  Diag  │ │  EHR   │ │  Lab   │ │  Discharge   │
│  Agent   │ │  Agent │ │  Agent │ │  Agent │ │  Agent       │
│          │ │        │ │        │ │        │ │              │
│Drug DB   │ │Clinical│ │Patient │ │Lab     │ │Post-care     │
│Formulary │ │Guideln │ │Records │ │Results │ │Instructions  │
│Qdrant    │ │Qdrant  │ │SQL Svr │ │SQL Svr │ │Qdrant+SQL    │
└──────────┘ └────────┘ └────────┘ └────────┘ └──────────────┘
```

### Agent Definitions

| Agent | SK Plugin | Data Source | Responsibility |
|---|---|---|---|
| **OrchestratorAgent** | HandlebarsPlanner | All agents | Route, plan, and synthesize |
| **DrugAgent** | DrugInteractionPlugin | Qdrant (drug vectors) | Interactions, dosages, contraindications |
| **DiagnosisAgent** | ClinicalGuidelinePlugin | Qdrant (guideline vectors) | Differential diagnosis, treatment paths |
| **EHRAgent** | PatientRecordPlugin | SQL Server via EF Core | Patient history, allergies, comorbidities |
| **LabAgent** | LabResultPlugin | SQL Server via EF Core | Interpret lab values, flag abnormalities |
| **DischargeAgent** | DischargePlanPlugin | Qdrant + SQL Server | Post-discharge instructions, medication plans |

### Specialist Agent System Prompts

**DrugAgent:**
> You are a clinical pharmacology specialist. Given drug information retrieved from the formulary vector store and the patient's current medication list, identify interactions, contraindications, and safe dosage adjustments. Always cite the drug database source.

**DiagnosisAgent:**
> You are a clinical diagnostician. Using retrieved clinical guidelines and the patient's chief complaint, suggest a ranked differential diagnosis with supporting evidence. Reference the specific guideline and version.

**DischargeAgent:**
> You are a discharge planning specialist. Using the patient's diagnosis, medications, and retrieved post-care protocols, generate clear, patient-friendly discharge instructions. Include medication schedule, warning signs, and follow-up timeline.

---

## RAG Pipeline

```
User Query
    │
    ▼
[PII Scrubbing Middleware]
    │
    ▼
[OrchestratorAgent — SK HandlebarsPlanner]
    │  Decomposes query into sub-tasks
    ▼
[Query Embedder]  ←── Anthropic Embedding API
    │  Generates query vector
    ▼
[Qdrant Semantic Search]  ←── Top-K=8, cosine similarity + metadata filter
    │  Returns relevant chunks
    ▼
[Reranker]  ←── Cross-encoder scoring
    │  Re-orders chunks by relevance
    ▼
[EHR / Lab Agent]  ←── EF Core → SQL Server
    │  Fetches structured patient data
    ▼
[SK ContextBuilder + PromptTemplate]
    │  Builds augmented clinical prompt
    ▼
[Anthropic Claude Sonnet — Messages API]
    │  Streams response tokens
    ▼
[SignalR ChatHub]
    │  Pushes tokens to Blazor UI
    ▼
[Source Citations + Agent Trace Logged to SQL Server]
    │
    ▼
User sees streamed, cited clinical answer
```

---

## Features

- **Multi-Agent Orchestration** — 5 specialized SK agents (Drug, Diagnosis, EHR, Lab, Discharge) orchestrated in parallel with 10-second timeout
- **RAG Pipeline** — Embed → Vector Search (Qdrant) → Rerank → Context Build → LLM Synthesis with source citations
- **Real-Time Streaming** — SignalR hub streams tokens to Blazor UI as they're generated
- **PII Protection** — Regex-based scrubber detects and redacts SSN, email, phone, DOB, MRN from all requests
- **Audit Trail** — Every API request logged as an immutable audit entry with user, IP, and entity details
- **SK Plugins** — Drug Interaction, Clinical Guidelines, Patient Records, Lab Results
- **Synthetic Data Seeder** — 5 patients with encounters, medications, and lab results for development
- **Mock LLM Client** — Deterministic responses for offline development and testing
- **EF Core 9** — Code-First schema with repository pattern, compiled queries, and connection resiliency
- **Session Memory** — Multi-turn conversation history (last 10 turns) via SK ChatHistory + Redis cache
- **Source Citations** — Every clinical response includes document name, page, and confidence score
- **Health Checks** — Liveness and readiness endpoints for all services

---

## Technology Stack

| Component | Technology | Version |
|---|---|---|
| Runtime | .NET | 9.0 |
| Web Framework | ASP.NET Core Minimal API | 9.0 |
| UI | Blazor Server | 9.0 |
| AI Orchestration | Microsoft Semantic Kernel | 1.54 |
| LLM | Anthropic Claude Sonnet 4 | claude-sonnet-4-20250514 |
| Vector Database | Qdrant | 1.9+ |
| ORM | Entity Framework Core | 9.0 |
| Database | SQL Server | 2022 |
| Session Cache | Redis | 7.x |
| Streaming | ASP.NET Core SignalR | 9.0 |
| Auth | Azure AD + Microsoft.Identity.Web | Latest |
| PII Scrubbing | Regex-based middleware | — |
| Local Orchestration | Docker Compose + .NET Aspire | Latest |
| Testing | xUnit + Moq + FluentAssertions + Testcontainers | Latest |
| Observability | OpenTelemetry + Serilog + Prometheus | Latest |

---

## Project Structure

```
MediMindAI/
├── src/
│   ├── MediMind.API/                        # ASP.NET Core entry point
│   │   ├── Endpoints/                       # Minimal API route handlers
│   │   │   ├── QueryEndpoints.cs
│   │   │   ├── IngestionEndpoints.cs
│   │   │   └── PatientEndpoints.cs
│   │   ├── Hubs/
│   │   │   └── ClinicalChatHub.cs           # SignalR streaming hub
│   │   ├── Middleware/
│   │   │   ├── PiiScrubbingMiddleware.cs
│   │   │   └── AuditLoggingMiddleware.cs
│   │   └── Diagnostics/
│   │       ├── InMemoryTraceCollector.cs
│   │       └── InMemoryExportProcessor.cs
│   │
│   ├── MediMind.Core/                       # Domain & application logic
│   │   ├── Agents/
│   │   │   ├── OrchestratorAgent.cs         # SK HandlebarsPlanner orchestrator
│   │   │   ├── DrugAgent.cs
│   │   │   ├── DiagnosisAgent.cs
│   │   │   ├── EhrAgent.cs
│   │   │   ├── LabAgent.cs
│   │   │   └── DischargeAgent.cs
│   │   ├── Plugins/                         # SK Plugin definitions
│   │   │   ├── DrugInteractionPlugin.cs
│   │   │   ├── ClinicalGuidelinePlugin.cs
│   │   │   ├── PatientRecordPlugin.cs
│   │   │   └── LabResultPlugin.cs
│   │   ├── RAG/
│   │   │   ├── RagOrchestrator.cs
│   │   │   ├── QueryEmbedder.cs
│   │   │   ├── ContextBuilder.cs
│   │   │   └── Reranker.cs
│   │   ├── Models/                          # Domain models
│   │   │   ├── ClinicalQuery.cs
│   │   │   ├── AgentResult.cs
│   │   │   └── DocumentChunk.cs
│   │   └── Interfaces/
│   │       ├── IVectorStore.cs
│   │       ├── ILLMClient.cs
│   │       └── IAgentOrchestrator.cs
│   │
│   ├── MediMind.Infrastructure/             # External integrations
│   │   ├── Qdrant/
│   │   │   ├── QdrantVectorStore.cs         # IVectorStore implementation
│   │   │   └── QdrantCollectionSetup.cs
│   │   ├── Anthropic/
│   │   │   ├── AnthropicClient.cs           # ILLMClient implementation
│   │   │   └── AnthropicStreamHandler.cs
│   │   ├── Persistence/
│   │   │   ├── MediMindDbContext.cs          # EF Core DbContext
│   │   │   ├── Configurations/              # EF Core entity type configs
│   │   │   ├── Migrations/                  # EF Core migrations
│   │   │   ├── Repositories/
│   │   │   │   ├── PatientRepository.cs
│   │   │   │   ├── EncounterRepository.cs
│   │   │   │   └── LabResultRepository.cs
│   │   │   └── DataSeeder.cs               # Synthetic test data seeder
│   │   ├── PiiScrubbing/                    # PII detection & redaction
│   │   └── Redis/
│   │       └── RedisSessionStore.cs
│   │
│   └── MediMind.BlazorUI/                   # Blazor Server frontend
│       ├── Pages/
│       │   ├── Chat.razor                   # Main clinical chat interface
│       │   ├── PatientContext.razor         # Patient selector
│       │   └── Admin/
│       │       ├── IngestionDashboard.razor
│       │       └── AuditLog.razor
│       └── Components/
│           ├── SourceCitations.razor
│           └── AgentTrace.razor
│
├── tests/
│   ├── MediMind.UnitTests/                  # xUnit + Moq
│   │   ├── Agents/                          # Agent unit tests
│   │   ├── Plugins/                         # SK plugin unit tests
│   │   ├── RAG/                             # RAG pipeline unit tests
│   │   ├── Services/
│   │   └── Repositories/
│   │
│   └── MediMind.IntegrationTests/
│       ├── AgentPipelineTests.cs            # End-to-end agent flow tests
│       └── SqlServerIntegrationTests.cs     # Testcontainers — SQL Server
│
├── aspire/
│   ├── MediMind.AppHost/                    # .NET Aspire orchestration host
│   └── MediMind.ServiceDefaults/            # Shared service defaults
│
├── docker-compose.yml                       # Full local stack
├── docker-compose.override.yml             # Local dev overrides
├── .env.example                             # Secrets template
└── MediMind_AI_PRD.md                       # Full Product Requirements Document
```

---

## Database Schema

```
Patients
├── PatientId (PK, Guid)
├── FullName, DateOfBirth, Gender
├── BloodGroup, Allergies (JSON)
└── CreatedAt, UpdatedAt, IsActive

Encounters
├── EncounterId (PK, Guid)
├── PatientId (FK → Patients)
├── ClinicianId, EncounterDate
├── ChiefComplaint, Diagnosis (JSON)
└── Notes, DischargeInstructions

Medications
├── MedicationId (PK, Guid)
├── PatientId (FK → Patients)
├── EncounterId (FK → Encounters)
├── DrugName, Dosage, Frequency
└── StartDate, EndDate, IsActive

LabResults
├── LabResultId (PK, Guid)
├── PatientId (FK → Patients)
├── TestName, Value, Unit
├── ReferenceRange, IsAbnormal
└── CollectedAt, ReportedAt

IngestionJobs
├── JobId (PK, Guid)
├── DocumentName, DocumentType
├── Status (Pending/Processing/Done/Failed)
├── ChunksIngested, ErrorMessage
└── StartedAt, CompletedAt

AgentTraces
├── TraceId (PK, Guid)
├── SessionId, UserId
├── OrchestratorPlan (JSON)
├── AgentName, AgentInput, AgentOutput
├── TokensUsed, LatencyMs
└── CreatedAt

AuditLogs
├── LogId (PK, Guid)
├── UserId, Action, EntityType
├── EntityId, OldValue, NewValue
├── IpAddress, UserAgent
└── Timestamp
```

---

## Prerequisites

- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- [Docker Desktop](https://www.docker.com/products/docker-desktop/) (for SQL Server, Qdrant, Redis)
- Anthropic API key (optional — mock client available for offline dev)

---

## Quick Start

### 1. Clone & configure environment

```bash
git clone https://github.com/venkateshTechmates/MediMindAI.git
cd MediMindAI
cp .env.example .env
# Edit .env with your Anthropic API key (or leave ANTHROPIC_USE_MOCK=true)
```

### 2. Start Infrastructure

```bash
docker compose up -d sqlserver qdrant redis
```

### 3. Apply EF Core Migrations & Seed Data

```bash
dotnet ef database update --project src/MediMind.Infrastructure --startup-project src/MediMind.API
```

### 4. Run the API

```bash
cd src/MediMind.API
dotnet run
```

API starts at `https://localhost:5001` — Swagger UI available at `/swagger`.

### 5. Run the Blazor UI

```bash
cd src/MediMind.BlazorUI
dotnet run
```

UI starts at `https://localhost:5002`.

### 6. Or run everything with Docker Compose

```bash
docker compose up --build
```

| Service | URL |
|---|---|
| API | http://localhost:5001 |
| Blazor UI | http://localhost:5002 |
| Qdrant Dashboard | http://localhost:6333/dashboard |
| SQL Server | localhost:1433 |
| Redis | localhost:6379 |

### 7. Or use .NET Aspire (local dev dashboard)

```bash
cd aspire/MediMind.AppHost
dotnet run
```

Opens the Aspire dashboard with distributed traces, health checks, and logs for all services.

---

## Configuration

Copy `.env.example` to `.env` and configure. You can also override via `appsettings.json`:

```env
ANTHROPIC_API_KEY=sk-ant-xxxxxxxxxxxx
ANTHROPIC_USE_MOCK=false
ANTHROPIC_MODEL=claude-sonnet-4-20250514

QDRANT_HOST=localhost
QDRANT_PORT=6334
QDRANT_COLLECTION=medimind_clinical

SQL_SERVER_CONNECTION=Server=localhost,1433;Database=MediMindDb;User=sa;Password=MediMind@Local123;TrustServerCertificate=True

REDIS_CONNECTION=localhost:6379

AZURE_AD_TENANT_ID=your-tenant-id
AZURE_AD_CLIENT_ID=your-client-id
```

### Key Settings Reference

| Setting | Description | Default |
|---|---|---|
| `Anthropic:UseMock` | Use mock LLM client (no API key needed) | `true` |
| `Anthropic:ApiKey` | Anthropic API key | — |
| `Anthropic:Model` | Claude model identifier | `claude-sonnet-4-20250514` |
| `Qdrant:Host` | Qdrant server host | `localhost` |
| `Qdrant:Port` | Qdrant gRPC port | `6334` |
| `Qdrant:Collection` | Qdrant collection name | `medimind_clinical` |
| `ConnectionStrings:Default` | SQL Server connection string | — |
| `Redis:Connection` | Redis connection string | `localhost:6379` |

---

## API Endpoints

### Query

| Method | Path | Description |
|---|---|---|
| `POST` | `/api/v1/query` | Submit a clinical query (non-streaming) |

### Ingestion

| Method | Path | Description |
|---|---|---|
| `POST` | `/api/v1/ingestion` | Create a document ingestion job |
| `GET` | `/api/v1/ingestion/{id}` | Get ingestion job status |
| `GET` | `/api/v1/ingestion` | List all ingestion jobs |

### Patients

| Method | Path | Description |
|---|---|---|
| `GET` | `/api/v1/patients/{id}` | Get patient by ID |
| `GET` | `/api/v1/patients/{id}/profile` | Get full patient profile |
| `GET` | `/api/v1/patients/search?name=` | Search patients by name |
| `GET` | `/api/v1/patients/{id}/medications` | Get patient medications |
| `GET` | `/api/v1/patients/{id}/labs` | Get patient lab results |
| `GET` | `/api/v1/patients/{id}/encounters` | Get patient encounters |

### System

| Method | Path | Description |
|---|---|---|
| `GET` | `/health` | Health check (liveness + readiness) |
| `GET` | `/swagger` | Swagger / OpenAPI UI |
| `GET` | `/api/v1/traces` | In-memory OpenTelemetry trace viewer |

---

## SignalR Hub

Connect to `/hubs/clinical-chat` for real-time token streaming:

```typescript
const connection = new HubConnectionBuilder()
  .withUrl("/hubs/clinical-chat")
  .build();

// Stream response tokens
connection.stream("StreamQuery", query).subscribe({
  next: (token) => appendToken(token),
  complete: () => finalize(),
});

// Set patient context
await connection.invoke("SetActivePatient", sessionId, patientId);

// Clear conversation history
await connection.invoke("ClearSession", sessionId);
```

### Hub Methods

| Method | Parameters | Returns | Description |
|---|---|---|---|
| `StreamQuery` | `ClinicalQuery` | `IAsyncEnumerable<string>` | Streams response tokens |
| `SendQuery` | `ClinicalQuery` | `ClinicalResponse` | Full (non-streaming) response |
| `SetActivePatient` | `sessionId, patientId` | — | Set patient context for session |
| `ClearSession` | `sessionId` | — | Reset conversation history |

---

## Testing

```bash
# Unit tests (no infrastructure needed)
dotnet test tests/MediMind.UnitTests

# Integration tests (requires Docker for Testcontainers)
dotnet test tests/MediMind.IntegrationTests

# All tests with coverage
dotnet test --collect:"XPlat Code Coverage"
```

### Test Coverage Targets

| Area | Target |
|---|---|
| Unit tests (Agents, Plugins, RAG) | ≥ 80% |
| Integration tests (Agent pipelines) | All agent flows |
| SQL Server integration | Testcontainers |

### Mock Mode

Set `ANTHROPIC_USE_MOCK=true` in `.env` to run the full stack with no external API dependencies. The `MockAnthropicClient` returns deterministic responses for all queries.

---

## Docker Compose Services

```yaml
services:
  sqlserver:
    image: mcr.microsoft.com/mssql/server:2022-latest
    ports: ["1433:1433"]
    environment:
      SA_PASSWORD: "MediMind@Local123"
      ACCEPT_EULA: "Y"

  qdrant:
    image: qdrant/qdrant:latest
    ports: ["6333:6333", "6334:6334"]
    volumes: ["qdrant_data:/qdrant/storage"]

  redis:
    image: redis:7-alpine
    ports: ["6379:6379"]

  api:
    build: ./src/MediMind.API
    ports: ["5001:8080"]
    depends_on: [sqlserver, qdrant, redis]
    env_file: .env

  blazorui:
    build: ./src/MediMind.BlazorUI
    ports: ["5002:8080"]
    depends_on: [api]
```

---

## Observability

| Signal | Technology | Destination |
|---|---|---|
| Distributed Traces | OpenTelemetry | Jaeger / Aspire Dashboard |
| Metrics | Prometheus | Grafana |
| Structured Logs | Serilog | Seq / ELK / Console |
| Agent Traces | SQL Server `AgentTraces` table | Query via API |
| Audit Trail | SQL Server `AuditLogs` table | Admin UI |

### In-Memory Trace Viewer

During development, traces are available at `/api/v1/traces` via the `InMemoryTraceCollector`.

---

## Security & Compliance

| Area | Measure |
|---|---|
| **Authentication** | Azure AD / OIDC — JWT Bearer tokens |
| **Authorization** | RBAC: `Clinician`, `Nurse`, `Patient`, `Admin`, `Researcher` |
| **PII Protection** | Regex middleware scrubs SSN, email, phone, DOB, MRN before LLM/vector calls |
| **Audit Logging** | All queries, responses, agent traces logged immutably |
| **Encryption** | AES-256 at rest (SQL Server TDE) · TLS 1.3 in transit |
| **Secrets** | Azure Key Vault in production · `.env` for local only |
| **Data Residency** | HIPAA — no PHI leaves designated cloud region |
| **Token Safety** | Max 4,096 output tokens per query; budget alerts at 80% |

---

## Non-Functional Requirements

| Category | Requirement | Target |
|---|---|---|
| **Performance** | Time-to-first-token (streaming) | < 2 seconds |
| **Performance** | Full RAG pipeline P95 latency | < 8 seconds |
| **Scalability** | Horizontal API scaling | Kubernetes HPA |
| **Scalability** | Qdrant cluster | 3-node minimum (prod) |
| **Availability** | Uptime SLA | 99.9% |
| **Concurrent Users** | Live SignalR sessions | 1,000 simultaneous |
| **Testing** | Unit test coverage | ≥ 80% |

---

## Milestones

| Phase | Milestone | Duration | Deliverable |
|---|---|---|---|
| **Phase 1** | Foundation | Week 1–2 | .NET solution, EF Core schema, Docker Compose, local testing |
| **Phase 2** | Ingestion Pipeline | Week 3–4 | Document ingestion → Qdrant, PDF/DOCX chunking, embedding |
| **Phase 3** | Core RAG | Week 5–6 | Query → embed → Qdrant → Claude → SignalR streaming |
| **Phase 4** | Multi-Agent | Week 7–9 | SK OrchestratorAgent + 5 specialist agents |
| **Phase 5** | EHR Integration | Week 10–11 | EF Core repositories wired to agents; patient context |
| **Phase 6** | UI & Auth | Week 12–13 | Blazor Server UI, Azure AD auth, citation panel |
| **Phase 7** | Testing & Hardening | Week 14–15 | xUnit + Testcontainers; load testing; PII audit |
| **Phase 8** | Production Readiness | Week 16 | Kubernetes, observability dashboards, HIPAA review |

---

## Target Users

| Persona | Role | Primary Use Case |
|---|---|---|
| Dr. Arun | Resident Physician | Differential diagnosis, drug interaction check |
| Nurse Priya | ICU Nurse | Protocol Q&A, dosage lookup |
| Dr. Meera | Radiologist | Cross-reference findings with literature |
| Ravi | Patient (Post-Discharge) | Medication schedule, symptom follow-up |
| Admin Suresh | Hospital Administrator | Policy compliance Q&A, audit trail |
| Research Team | Clinical Researcher | Literature-based queries & summarization |
| Dev / QA Engineer | Internal | Local testing, agent debugging |

---

## License

Proprietary — All rights reserved.

An enterprise-grade clinical intelligence platform that combines **multi-agent AI orchestration**, **Retrieval-Augmented Generation (RAG)**, and **real-time streaming** to deliver evidence-based clinical decision support.

---

## Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                    MediMind.BlazorUI                        │
│               Blazor Server · SignalR Client                │
└──────────────────────┬──────────────────────────────────────┘
                       │ SignalR / REST
┌──────────────────────▼──────────────────────────────────────┐
│                     MediMind.API                            │
│  Minimal API · SignalR Hub · PII Middleware · Audit Logging │
└──────────────────────┬──────────────────────────────────────┘
                       │
┌──────────────────────▼──────────────────────────────────────┐
│                    MediMind.Core                            │
│  Agents · Plugins · RAG Pipeline · Domain Models · Interfaces│
│                                                             │
│  ┌─────────┐ ┌──────────┐ ┌─────────┐ ┌─────────────────┐  │
│  │Orchestr-│ │  Drug    │ │Diagnosis│ │   Discharge     │  │
│  │  ator   │→│  Agent   │ │  Agent  │ │     Agent       │  │
│  │  Agent  │ │          │ │         │ │                 │  │
│  └─────────┘ └──────────┘ └─────────┘ └─────────────────┘  │
│                                                             │
│  ┌──────────────────────────────────────────────────────┐   │
│  │ RAG Pipeline: Embed → Search → Rerank → Build Context│   │
│  └──────────────────────────────────────────────────────┘   │
└──────────────────────┬──────────────────────────────────────┘
                       │
┌──────────────────────▼──────────────────────────────────────┐
│                MediMind.Infrastructure                      │
│  EF Core · Qdrant · Anthropic Client · Redis · PII Scrubber│
└─────────┬──────────┬───────────┬───────────┬────────────────┘
          │          │           │           │
     SQL Server   Qdrant     Claude API    Redis
```

## Features

- **Multi-Agent Orchestration** — 5 specialized SK agents (Drug, Diagnosis, EHR, Lab, Discharge) orchestrated in parallel with 10-second timeout
- **RAG Pipeline** — Embed → Vector Search (Qdrant) → Rerank → Context Build → LLM Synthesis with source citations
- **Real-Time Streaming** — SignalR hub streams tokens to Blazor UI as they're generated
- **PII Protection** — Regex-based scrubber detects and redacts SSN, email, phone, DOB, MRN from all requests
- **Audit Trail** — Every API request logged as an immutable audit entry
- **SK Plugins** — Drug Interaction, Clinical Guidelines, Patient Records, Lab Results
- **Synthetic Data Seeder** — 5 patients with encounters, medications, and lab results for development
- **Mock LLM Client** — Deterministic responses for offline development and testing

## Prerequisites

- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- [Docker Desktop](https://www.docker.com/products/docker-desktop/) (for SQL Server, Qdrant, Redis)
- Anthropic API key (optional — mock client available)

## Quick Start

### 1. Start Infrastructure

```bash
docker compose up -d sqlserver qdrant redis
```

### 2. Run the API

```bash
cd src/MediMind.API
dotnet run
```

The API starts at `https://localhost:5001` with Swagger UI at `/swagger`.

### 3. Run the Blazor UI

```bash
cd src/MediMind.BlazorUI
dotnet run
```

The UI starts at `https://localhost:5002`.

### 4. Or run everything with Docker Compose

```bash
docker compose up --build
```

- API: http://localhost:5001
- UI: http://localhost:5002
- Qdrant Dashboard: http://localhost:6333/dashboard

## Project Structure

```
MediMindAI/
├── src/
│   ├── MediMind.Core/           # Domain models, interfaces, agents, plugins, RAG
│   ├── MediMind.Infrastructure/ # EF Core, Qdrant, Anthropic, Redis, PII
│   ├── MediMind.API/            # Minimal API, SignalR Hub, Middleware
│   └── MediMind.BlazorUI/       # Blazor Server UI
├── tests/
│   ├── MediMind.UnitTests/      # xUnit + Moq + FluentAssertions
│   └── MediMind.IntegrationTests/ # Testcontainers (SQL Server)
├── docker-compose.yml
├── docker-compose.override.yml
└── .env.example
```

## Configuration

Copy `.env.example` to `.env` and set your values, or configure via `appsettings.json`:

| Setting | Description | Default |
|---------|-------------|---------|
| `Anthropic:UseMock` | Use mock LLM client | `true` |
| `Anthropic:ApiKey` | Anthropic API key | — |
| `Anthropic:Model` | Claude model name | `claude-sonnet-4-20250514` |
| `Qdrant:Host` | Qdrant server host | `localhost` |
| `Qdrant:Port` | Qdrant gRPC port | `6334` |

## API Endpoints

| Method | Path | Description |
|--------|------|-------------|
| `POST` | `/api/v1/query` | Submit a clinical query |
| `POST` | `/api/v1/ingestion` | Create ingestion job |
| `GET` | `/api/v1/ingestion/{id}` | Get ingestion job status |
| `GET` | `/api/v1/ingestion` | List ingestion jobs |
| `GET` | `/api/v1/patients/{id}` | Get patient by ID |
| `GET` | `/api/v1/patients/{id}/profile` | Get full patient profile |
| `GET` | `/api/v1/patients/search?name=` | Search patients |
| `GET` | `/api/v1/patients/{id}/medications` | Get patient medications |
| `GET` | `/api/v1/patients/{id}/labs` | Get patient lab results |
| `GET` | `/api/v1/patients/{id}/encounters` | Get patient encounters |
| `GET` | `/health` | Health check |

### SignalR Hub

Connect to `/hubs/clinical-chat` for real-time streaming:

- **`StreamQuery(ClinicalQuery)`** → `IAsyncEnumerable<string>` (token stream)
- **`SendQuery(ClinicalQuery)`** → `ClinicalResponse` (full response)
- **`SetActivePatient(sessionId, patientId)`** — set patient context
- **`ClearSession(sessionId)`** — reset conversation

## Testing

```bash
# Unit tests
dotnet test tests/MediMind.UnitTests

# Integration tests (requires Docker for Testcontainers)
dotnet test tests/MediMind.IntegrationTests
```

## Technology Stack

| Component | Technology |
|-----------|-----------|
| Framework | .NET 9, ASP.NET Core |
| AI Orchestration | Semantic Kernel 1.54 |
| LLM | Anthropic Claude (Sonnet 4) |
| Vector Database | Qdrant |
| Relational DB | SQL Server + EF Core 9 |
| Session Cache | Redis |
| Real-Time | SignalR |
| UI | Blazor Server |
| Testing | xUnit, Moq, FluentAssertions, Testcontainers |
| Containerization | Docker Compose |

## License

Proprietary — All rights reserved.
