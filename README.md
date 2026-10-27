# DocQuery — AI Document Q&A (RAG)

Upload PDFs, ask questions in plain English, get streamed answers with page citations.

**Stack:** ASP.NET Core Web API (.NET 10) · React (Vite) · PostgreSQL + pgvector · Azure OpenAI

## How it works

```
Upload PDF -> PdfPig extracts text per page -> split into 200-word chunks (40 overlap)
           -> Azure OpenAI embeddings -> saved in Postgres (vector column)

Question   -> embed question -> pgvector finds 5 closest chunks (user's docs only)
           -> chunks + question sent to GPT -> answer streamed back via SSE with [1][2] citations
```

## Build phases

| Phase | What | Files |
|---|---|---|
| 1. Setup + DB | Postgres/pgvector in Docker, tables created on startup | `docker-compose.yml`, `Data/Database.cs` |
| 2. Auth | Register/login, BCrypt hashing, JWT tokens | `AuthController.cs`, `JwtService.cs`, `Program.cs` |
| 3. Ingestion | PDF text extraction, chunking, embeddings, vector storage | `PdfService.cs`, `DocumentService.IngestAsync`, `DocumentsController.cs` |
| 4. Q&A + SSE | Vector search, prompt with sources, streamed answer | `DocumentService.SearchAsync`, `AzureOpenAiService.cs`, `ChatController.cs` |
| 5. React UI | Login, document list/upload, streaming chat with sources | `frontend/src/*` |
| 6. Tests | Chunking + prompt unit tests (xUnit) | `backend/DocQuery.Tests` |

## Run it

1. Start the database: `docker compose up -d` (runs on port **5433**)
2. Put your Azure OpenAI values in `backend/DocQuery.Api/appsettings.json`
   (endpoint, key, chat deployment, embedding deployment = `text-embedding-3-small`).
   Also change `Jwt:Key` to a long random string.
3. Backend: `dotnet run --project backend/DocQuery.Api` -> http://localhost:5016
4. Frontend: `cd frontend && npm install && npm run dev` -> http://localhost:5173
5. Tests: `dotnet test`

## API

| Method | Route | Auth |
|---|---|---|
| POST | `/api/auth/register` `{email,password}` | no |
| POST | `/api/auth/login` `{email,password}` | no |
| GET | `/api/documents` | JWT |
| POST | `/api/documents/upload` (form field `file`) | JWT |
| DELETE | `/api/documents/{id}` | JWT |
| POST | `/api/chat/ask` `{question, documentId?}` -> SSE stream | JWT |

## Debugging tips

- Breakpoint in `DocumentService.IngestAsync` to watch each ingestion step.
- Breakpoint in `ChatController.Ask` to see which chunks were found (`sources`) before GPT is called.
- Azure errors are logged with their full response body (see `AzureOpenAiService`).
- Look at data directly: `docker exec -it docquery-db-1 psql -U postgres -d docquery -c "select id, page_number, left(content,60) from chunks;"`
