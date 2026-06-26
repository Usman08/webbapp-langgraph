# AI-Native Sales Invoice PoC

An AI-native sales invoice tool where a sales operator submits a natural-language request and a multi-step LangGraph workflow interprets intent, resolves customer and purchase history, adjusts quantities, validates inventory, applies discounts, and produces an invoice draft for human approval — streaming every reasoning step in real time.

## Architecture

- **Backend** (`backend/`): .NET 8 N-layer monolith (Api / Application / Domain / Infrastructure). Owns all business logic and data in PostgreSQL.
- **AI Engine** (`ai-engine/`): Python 3.12 LangGraph workflow service. Calls .NET agent-tool endpoints; streams SSE events back through the .NET relay.
- **Frontend** (`frontend/`): React 18 + Vite SPA. Mobile-first (375 px baseline). Renders workflow progress, invoice preview, and approval gate.
- **Database**: PostgreSQL 16 via EF Core 8 (Npgsql).

```
frontend (React SPA)
       ↕  REST + SSE relay
backend (.NET API)  ←→  PostgreSQL
       ↕  HTTP (internal, X-Engine-Token)
ai-engine (LangGraph)
       ↕  Groq API (openai/gpt-oss-120b)
```

## Quick Start

```bash
cp .env.example .env
# Fill in GROQ_API_KEY in .env
docker compose up
```

Frontend: http://localhost:5173  
Backend API: http://localhost:8080  
AI Engine (internal only): http://localhost:8000

## Development

See `specs/001-ai-sales-invoice-poc/plan.md` for architecture details and `specs/001-ai-sales-invoice-poc/quickstart.md` for validation scenarios.

### Backend

```bash
cd backend
dotnet restore
dotnet build
dotnet test
```

### AI Engine

```bash
cd ai-engine
python -m venv .venv && source .venv/bin/activate  # or .venv\Scripts\activate on Windows
pip install -e ".[dev]"
uvicorn app.main:app --reload
```

### Frontend

```bash
cd frontend
npm install
npm run dev
```
