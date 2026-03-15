# Product Requirements Document (PRD)

## 🏥 MediMind AI — Real-Time Clinical Intelligence & Multi-Agent Platform
### Domain: Medical / Health

| Field | Detail |
|---|---|
| **Product Name** | MediMind AI |
| **Version** | 1.0.0 |
| **Date** | March 2026 |
| **Status** | Draft |
| **Stack** | .NET 9 · Anthropic Claude · Qdrant · Semantic Kernel · EF Core · SQL Server |
| **Compliance** | HIPAA · HL7 FHIR R4 · GDPR · ISO 27001 |

---

## 1. Executive Summary

**MediMind AI** is a real-time, AI-powered clinical decision support platform that enables clinicians, nurses, and patients to interact with medical knowledge through a conversational multi-agent interface.

The platform uses a **Retrieval-Augmented Generation (RAG)** pipeline to ground every AI response in verified clinical sources — drug formularies, treatment guidelines (WHO, CDC, NICE), EHR records, and research literature — ensuring accurate, hallucination-resistant, cited answers at the point of care.

### Core Technology Pillars

| Pillar | Technology | Role |
|---|---|---|
| **LLM** | Anthropic Claude Sonnet | Clinical reasoning & response generation |
| **Vector Store** | Qdrant | Semantic medical knowledge retrieval |
| **AI Orchestration** | Microsoft Semantic Kernel (SK) | Multi-agent planning, skills, memory, plugins |
| **Relational DB** | SQL Server + EF Core 9 | Patient records, audit logs, structured data |
| **Local Testing** | Docker Compose + Aspire | Full local dev environment with all services |
| **Multi-Agent** | SK Agent Framework | Specialist agents per clinical domain |

---

## 2. Problem Statement

| # | Pain Point | Evidence |
|---|---|---|
| P1 | Clinicians spend 30–40% of shift searching fragmented systems | NEJM Study 2024 |
| P2 | 10–15% of medication errors are preventable with better drug-reference tools | WHO Safety Report |
| P3 | 27% readmission rate due to poor patient discharge understanding | CMS Data 2025 |
| P4 | No unified AI layer reasoning across EHR, labs, and guidelines simultaneously | Internal gap analysis |
| P5 | Single-agent chatbots lack domain-specific expertise for complex clinical queries | Clinical IT survey |
| P6 | No real-time vector search integrated with structured hospital SQL data | Architecture review |

---

## 3. Vision & Goals

> *"Give every clinician instant access to the world's medical knowledge, grounded in their patient's specific context — in real time."*

### Success Metrics

| Goal | KPI | Target |
|---|---|---|
| Reduce clinical lookup time | Avg. retrieval time per query | < 8 seconds |
| Medication safety improvement | Drug interaction alert accuracy | ≥ 97% precision |
| Reduce readmissions | 30-day readmission rate | −25% in 6 months |
| Concurrent users supported | Live sessions | 1,000 simultaneous |
| PII leakage prevention | PII detected in vector store | Zero tolerance |
| Source-cited AI answers | Citation coverage | 100% of clinical responses |
| Local dev parity with prod | Environment reproducibility | 100% via Docker Compose |

---

## 4. Target Users

| Persona | Role | Primary Use Case |
|---|---|---|
| **Dr. Arun** | Resident Physician | Differential diagnosis, drug interaction check |
| **Nurse Priya** | ICU Nurse | Protocol Q&A, dosage lookup |
| **Dr. Meera** | Radiologist | Cross-reference findings with literature |
| **Ravi** | Patient (Post-Discharge) | Medication schedule, symptom follow-up |
| **Admin Suresh** | Hospital Administrator | Policy compliance Q&A, audit trail |
| **Research Team** | Clinical Researcher | Literature-based queries & summarization |
| **Dev / QA Engineer** | Internal | Local testing, agent debugging |

---

## 5. Multi-Agent Architecture

MediMind AI uses **Microsoft Semantic Kernel's Agent Framework** to deploy specialist agents that collaborate via an orchestrator to answer complex clinical queries.

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
                                │
┌───────────────────────────────▼──────────────────────────────────────┐
│                         SHARED SERVICES                              │
│   Anthropic Claude API  ·  Qdrant Vector DB  ·  SQL Server (EF Core) │
│   Redis (Session Cache)  ·  PII Scrubber  ·  Audit Logger            │
└──────────────────────────────────────────────────────────────────────┘
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

---

## 6. Functional Requirements

### 6.1 Ingestion Pipeline

| ID | Requirement |
|---|---|
| FR-01 | Ingest PDF/DOCX clinical guidelines, drug formularies, and research papers via Admin API |
| FR-02 | Chunk documents using sliding-window strategy (512 tokens, 10% overlap) |
| FR-03 | Generate vector embeddings via Anthropic embedding endpoint |
| FR-04 | Store embeddings in **Qdrant** with rich payload metadata (source, category, version, date) |
| FR-05 | Support upsert ingestion — no full re-index required on updates |
| FR-06 | Scheduled sync from external sources (WHO API, CDC RSS, PubMed) every 24 hours |
| FR-07 | Track ingestion status per document in **SQL Server** via EF Core (status, error, timestamp) |

### 6.2 Query & RAG Pipeline

| ID | Requirement |
|---|---|
| FR-08 | Accept natural language clinical queries via REST and SignalR |
| FR-09 | Apply PII scrubbing middleware before any text reaches Qdrant or Anthropic API |
| FR-10 | Embed user query and run semantic search on Qdrant (top-K = 8, cosine similarity) |
| FR-11 | Apply payload filters on Qdrant (category, guideline type, drug class, date range) |
| FR-12 | Rerank retrieved chunks using cross-encoder scoring before context building |
| FR-13 | Construct augmented prompt via SK PromptTemplate and send to **Anthropic Claude Sonnet** |
| FR-14 | Stream response tokens to client via **ASP.NET Core SignalR** |
| FR-15 | Return source citations (document name, page, confidence score) with every response |
| FR-16 | Detect out-of-scope queries and respond with safe fallback + human handoff trigger |

### 6.3 Multi-Agent Orchestration

| ID | Requirement |
|---|---|
| FR-17 | OrchestratorAgent decomposes complex queries into sub-tasks using SK HandlebarsPlanner |
| FR-18 | Each specialist agent executes independently and returns typed result objects |
| FR-19 | OrchestratorAgent synthesizes multi-agent results into a single coherent response |
| FR-20 | Agent execution trace stored in SQL Server for audit and debugging |
| FR-21 | Support agent timeout (10s per agent) with graceful partial-result fallback |
| FR-22 | SK Memory plugin provides session-scoped short-term memory per user |

### 6.4 EF Core & SQL Server

| ID | Requirement |
|---|---|
| FR-23 | Define domain models via EF Core 9 Code-First with migrations |
| FR-24 | Store: Patients, Encounters, LabResults, Medications, AuditLogs, IngestionJobs, AgentTraces |
| FR-25 | Use EF Core Interceptors for automatic audit stamping (CreatedAt, UpdatedAt, CreatedBy) |
| FR-26 | Implement Repository + Unit of Work pattern over EF Core DbContext |
| FR-27 | Use EF Core Compiled Queries for hot-path patient data lookups |
| FR-28 | Connection resiliency via EF Core Retry Policy (SQL Server transient faults) |
| FR-29 | Database seeding for local testing with anonymized synthetic patient data |

### 6.5 Conversation & Session Management

| ID | Requirement |
|---|---|
| FR-30 | Maintain multi-turn conversation history (last 10 turns) per session via SK ChatHistory |
| FR-31 | Persist session metadata in SQL Server; conversation turns cached in Redis (8hr TTL) |
| FR-32 | Allow users to reset/clear conversation history |
| FR-33 | Link conversations to authenticated user and patient context |

### 6.6 Security & Compliance

| ID | Requirement |
|---|---|
| FR-34 | Authentication via Azure AD / OIDC using JWT Bearer tokens |
| FR-35 | Role-based access control: `Clinician`, `Nurse`, `Patient`, `Admin`, `Researcher` |
| FR-36 | PII scrubbing middleware (Microsoft Presidio) applied before LLM and vector calls |
| FR-37 | All queries, responses, and agent traces logged to SQL Server audit table |
| FR-38 | Data encrypted at rest (AES-256, SQL Server TDE) and in transit (TLS 1.3) |
| FR-39 | Anthropic API key managed via Azure Key Vault / environment secrets only |
| FR-40 | HIPAA-compliant data residency — no PHI leaves designated cloud region |

### 6.7 Local Testing Environment

| ID | Requirement |
|---|---|
| FR-41 | Full local stack via **Docker Compose**: SQL Server, Qdrant, Redis, API, Blazor UI |
| FR-42 | **.NET Aspire** dashboard for local service discovery, health checks, and traces |
| FR-43 | Synthetic anonymized patient data seeded via EF Core `DataSeeder` on startup |
| FR-44 | Qdrant local instance pre-loaded with sample clinical guidelines on first run |
| FR-45 | Anthropic API calls mockable via `ILLMClient` interface + `MockAnthropicClient` |
| FR-46 | Integration test project using **Testcontainers** for SQL Server and Qdrant |
| FR-47 | Unit tests for each SK Plugin and Agent using **xUnit + Moq** |
| FR-48 | Local `.env` file support for secrets; `.env.example` committed to repo |

---

## 7. Non-Functional Requirements

| Category | Requirement | Target |
|---|---|---|
| **Performance** | Time-to-first-token (streaming start) | < 2 seconds |
| **Performance** | Full RAG pipeline P95 latency | < 8 seconds |
| **Scalability** | Horizontal API scaling | Kubernetes HPA |
| **Scalability** | Qdrant cluster mode | 3-node minimum prod |
| **Availability** | Uptime SLA | 99.9% |
| **Observability** | Distributed tracing | OpenTelemetry + Jaeger |
| **Observability** | Metrics | Prometheus + Grafana |
| **Observability** | Structured logging | Serilog → Seq / ELK |
| **Token Cost** | Max output tokens per query | 4,096 |
| **Token Cost** | Budget alerts at | 80% monthly threshold |
| **Testing** | Unit test coverage | ≥ 80% |
| **Testing** | Integration test coverage | All agent pipelines |

---

## 8. Technology Stack

| Layer | Technology | Version |
|---|---|---|
| **Runtime** | .NET | 9.0 |
| **Web Framework** | ASP.NET Core Minimal API | 9.0 |
| **UI** | Blazor Server | 9.0 |
| **AI Orchestration** | Microsoft Semantic Kernel | 1.x |
| **LLM** | Anthropic Claude Sonnet | claude-sonnet-4-20250514 |
| **Vector Database** | Qdrant | 1.9+ |
| **ORM** | Entity Framework Core | 9.0 |
| **Database** | SQL Server | 2022 |
| **Session Cache** | Redis | 7.x |
| **Streaming** | ASP.NET Core SignalR | 9.0 |
| **Auth** | Azure AD + Microsoft.Identity.Web | Latest |
| **PII Scrubbing** | Microsoft Presidio (Python sidecar) | Latest |
| **Local Orchestration** | Docker Compose + .NET Aspire | Latest |
| **Testing** | xUnit + Moq + Testcontainers | Latest |
| **Observability** | OpenTelemetry + Serilog + Prometheus | Latest |
| **CI/CD** | GitHub Actions | — |
| **Secrets** | Azure Key Vault / .env (local) | — |

---

## 9. Database Schema (EF Core — SQL Server)

```
Patients
├── PatientId (PK, Guid)
├── FullName, DateOfBirth, Gender
├── BloodGroup, Allergies (JSON)
├── CreatedAt, UpdatedAt, IsActive

Encounters
├── EncounterId (PK, Guid)
├── PatientId (FK → Patients)
├── ClinicianId, EncounterDate
├── ChiefComplaint, Diagnosis (JSON)
├── Notes, DischargeInstructions

Medications
├── MedicationId (PK, Guid)
├── PatientId (FK → Patients)
├── EncounterId (FK → Encounters)
├── DrugName, Dosage, Frequency
├── StartDate, EndDate, IsActive

LabResults
├── LabResultId (PK, Guid)
├── PatientId (FK → Patients)
├── TestName, Value, Unit
├── ReferenceRange, IsAbnormal
├── CollectedAt, ReportedAt

IngestionJobs
├── JobId (PK, Guid)
├── DocumentName, DocumentType
├── Status (Pending/Processing/Done/Failed)
├── ChunksIngested, ErrorMessage
├── StartedAt, CompletedAt

AgentTraces
├── TraceId (PK, Guid)
├── SessionId, UserId
├── OrchestratorPlan (JSON)
├── AgentName, AgentInput, AgentOutput
├── TokensUsed, LatencyMs
├── CreatedAt

AuditLogs
├── LogId (PK, Guid)
├── UserId, Action, EntityType
├── EntityId, OldValue, NewValue
├── IpAddress, UserAgent
├── Timestamp
```

---

## 10. .NET Project Structure

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
│   │   └── Middleware/
│   │       ├── PiiScrubbingMiddleware.cs
│   │       └── AuditLoggingMiddleware.cs
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
│   │   │   ├── Configurations/              # EF Core entity configs
│   │   │   ├── Migrations/
│   │   │   ├── Repositories/
│   │   │   │   ├── PatientRepository.cs
│   │   │   │   ├── EncounterRepository.cs
│   │   │   │   └── LabResultRepository.cs
│   │   │   └── DataSeeder.cs               # Synthetic test data seeder
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
│           ├── MessageStream.razor          # Real-time token streaming
│           ├── SourceCitations.razor
│           └── AgentTrace.razor
│
├── tests/
│   ├── MediMind.UnitTests/
│   │   ├── Agents/                          # xUnit + Moq agent tests
│   │   ├── Plugins/                         # SK plugin unit tests
│   │   └── RAG/                             # RAG pipeline unit tests
│   │
│   └── MediMind.IntegrationTests/
│       ├── QdrantIntegrationTests.cs        # Testcontainers — Qdrant
│       ├── SqlServerIntegrationTests.cs     # Testcontainers — SQL Server
│       └── AgentPipelineTests.cs            # End-to-end agent flow tests
│
├── docker-compose.yml                       # Full local stack
├── docker-compose.override.yml             # Local dev overrides
├── aspire/
│   └── MediMind.AppHost/                    # .NET Aspire host
├── .env.example                             # Local secrets template
└── README.md
```

---

## 11. Local Testing Setup

### Docker Compose Services

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
    ports: ["5000:8080"]
    depends_on: [sqlserver, qdrant, redis]
    env_file: .env

  blazorui:
    build: ./src/MediMind.BlazorUI
    ports: ["5001:8080"]
    depends_on: [api]
```

### Local Test Execution

```bash
# Start all services
docker compose up -d

# Run EF Core migrations + seed synthetic data
dotnet ef database update --project src/MediMind.Infrastructure

# Run unit tests
dotnet test tests/MediMind.UnitTests

# Run integration tests (Testcontainers auto-spins services)
dotnet test tests/MediMind.IntegrationTests

# Mock Anthropic API (set in .env)
ANTHROPIC_USE_MOCK=true
```

### .env.example

```env
ANTHROPIC_API_KEY=sk-ant-xxxxxxxxxxxx
ANTHROPIC_USE_MOCK=false
ANTHROPIC_MODEL=claude-sonnet-4-20250514

QDRANT_HOST=localhost
QDRANT_PORT=6333
QDRANT_COLLECTION=medimind_clinical

SQL_SERVER_CONNECTION=Server=localhost,1433;Database=MediMindDb;User=sa;Password=MediMind@Local123;

REDIS_CONNECTION=localhost:6379

AZURE_AD_TENANT_ID=your-tenant-id
AZURE_AD_CLIENT_ID=your-client-id
```

---

## 12. RAG + Semantic Kernel Flow

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

## 13. Specialist Agent Prompts (SK)

### DrugAgent System Prompt
```
You are a clinical pharmacology specialist. Given drug information retrieved from the 
formulary vector store and the patient's current medication list, identify interactions, 
contraindications, and safe dosage adjustments. Always cite the drug database source.
```

### DiagnosisAgent System Prompt
```
You are a clinical diagnostician. Using retrieved clinical guidelines and the patient's 
chief complaint, suggest a ranked differential diagnosis with supporting evidence. 
Reference the specific guideline and version.
```

### DischargeAgent System Prompt
```
You are a discharge planning specialist. Using the patient's diagnosis, medications, 
and retrieved post-care protocols, generate clear, patient-friendly discharge instructions. 
Include medication schedule, warning signs, and follow-up timeline.
```

---

## 14. Risk Register

| # | Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|---|
| R1 | Hallucinated clinical advice | Medium | Critical | RAG citation enforcement; confidence threshold gate |
| R2 | PHI exposure via LLM | Low | Critical | PII scrubbing middleware; HIPAA audit logs |
| R3 | Qdrant search latency spike | Medium | High | Caching frequent queries; Qdrant cluster mode |
| R4 | Anthropic API rate limiting | Medium | High | Retry policy; token budget management |
| R5 | Agent deadlock in SK planner | Low | Medium | Timeout per agent; fallback to direct RAG |
| R6 | EF Core migration failure in prod | Low | High | Blue-green deployment; migration dry-run in CI |
| R7 | SQL Server performance under load | Medium | Medium | Compiled queries; read replicas; EF Core caching |

---

## 15. Milestones & Delivery Plan

| Phase | Milestone | Duration | Deliverable |
|---|---|---|---|
| **Phase 1** | Foundation | Week 1–2 | .NET solution structure, EF Core schema, Docker Compose, local testing setup |
| **Phase 2** | Ingestion Pipeline | Week 3–4 | Document ingestion → Qdrant, PDF/DOCX chunking, embedding generation |
| **Phase 3** | Core RAG | Week 5–6 | Query → embed → Qdrant search → Claude response → SignalR streaming |
| **Phase 4** | Multi-Agent | Week 7–9 | SK OrchestratorAgent + 5 specialist agents fully operational |
| **Phase 5** | EHR Integration | Week 10–11 | EF Core repositories wired to agents; patient context in responses |
| **Phase 6** | UI & Auth | Week 12–13 | Blazor Server UI, Azure AD auth, source citation panel |
| **Phase 7** | Testing & Hardening | Week 14–15 | xUnit + Testcontainers integration tests; load testing; PII audit |
| **Phase 8** | Production Readiness | Week 16 | Kubernetes deployment, observability dashboards, HIPAA review |

---

## 16. Open Questions

| # | Question | Owner | Due |
|---|---|---|---|
| OQ-1 | Which Anthropic embedding model to use vs OpenAI text-embedding-3-small? | ML Lead | Week 1 |
| OQ-2 | FHIR R4 integration scope — read-only or bidirectional? | Product | Week 2 |
| OQ-3 | On-prem SQL Server vs Azure SQL Managed Instance for production? | Infra | Week 1 |
| OQ-4 | Will Qdrant be self-hosted (AKS) or Qdrant Cloud managed? | Infra | Week 2 |
| OQ-5 | Patient-facing UI — Blazor or separate mobile app (MAUI)? | Product | Week 3 |

---

*End of PRD — MediMind AI v1.0.0*
