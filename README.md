# Manifold – upload, read, and track your e-book library

Manifold is a backend for an e-book library built with a microservice architecture. Users can upload books (EPUB, FB2, PDF), automatically extract metadata (title, author, ISBN, page count, cover), store files in object storage, and maintain a personal catalog with tags.

## Features

- **Upload books** in EPUB, FB2, and PDF formats via streaming multipart requests
- **Automatic metadata extraction** — title, author, ISBN, page count, and cover
- **Cover handling**:
  - PDF — first page is rendered to a JPEG (PDFium)
  - EPUB / FB2 — cover image is extracted from the file; broken EPUB2 cover metadata (href instead of manifest ID) is repaired automatically
  - If no cover is found or it is not a raster image — a deterministic abstract placeholder is generated (SkiaSharp)
  - Covers are stored in a dedicated MinIO bucket `covers`
- **Personal catalog** with tags (colored) and per-user data isolation
- **JWT authentication** with refresh tokens and PBKDF2 password hashing

## Tech stack

- **.NET 10** (C#) — all services
- **.NET Aspire** — orchestration, service discovery, health checks, OpenTelemetry
- **ASP.NET Core (Minimal APIs + Carter)** — REST API
- **gRPC** — inter-service communication (proto contracts in `Shared.Protos`)
- **PostgreSQL + EF Core 10** — data storage (migrations are applied automatically on startup)
- **MinIO** — object storage for book files and covers
- **YARP** — API gateway with prefix-based routing
- **FluentValidation**, **ErrorOr** — request validation and result types
- **VersOne.Epub**, **PdfPig**, **PDFtoImage**, **SkiaSharp** — book parsing and cover rendering

## Architecture

```
                    ┌──────────┐
  Client ─────────▶ │  Gateway │  (YARP, port 3000)
                    └────┬─────┘
                         │
         ┌───────────────┼────────────────┐
         │               │                │
  ┌──────▼─────┐  ┌──────▼──────┐  ┌──────▼─────────┐
  │  Auth.Api  │  │ Catalog.Api │  │ BookMetadata   │
  │  (REST)    │  │  (REST)     │  │ .Grpc          │
  └──────┬─────┘  └──────┬──────┘  └──────┬─────────┘
         │               │         ┌──────▼─────┐
         │               └────────▶│ Storage.Grpc│
         │                         └──────┬─────┘
         │                                │
   ┌────▼─────┐   ┌──────────┐   ┌───────▼─────────┐
   │ users-db │   │ catalog- │   │ storage-db      │
   │ (Postgres│   │ db       │   │ (Postgres)      │
   └──────────┘   └──────────┘   └────────┬────────┘
                                          │
                                   ┌──────▼─────┐
                                   │   MinIO    │
                                   │ books/covers│
                                   └────────────┘
```

### Book upload flow

1. Client sends a multipart request to `POST /api/catalog/books`
2. `Catalog.Api` streams the file to `Storage.Grpc` → MinIO bucket `books`, getting a `file_id` back
3. `Catalog.Api` requests metadata from `BookMetadata.Grpc`, passing only `file_id` (the file itself is not re-transferred):
   - `BookMetadata.Grpc` downloads the book from `Storage.Grpc` (`DownloadFile`)
   - the book is parsed, the cover is extracted (or generated as a placeholder)
   - the cover is uploaded to `Storage.Grpc` → MinIO bucket `covers`
   - `cover_file_id` is returned together with the rest of the metadata
4. `Catalog.Api` stores the book in the personal catalog (`catalog-db`)

## Services

| Service | Type | Purpose |
|---|---|---|
| **Auth.Api** | REST `/api/auth` | Registration, login, logout, token refresh |
| **Catalog.Api** | REST `/api/catalog` | Personal book catalog and tags (CRUD) |
| **BookMetadata.Grpc** | gRPC | Metadata and cover extraction (EPUB, FB2, PDF) |
| **Storage.Grpc** | gRPC | Streaming upload of books (`books` bucket) and covers (`covers` bucket) to MinIO, file metadata in PostgreSQL |
| **BookTracking.AppHost** | Aspire | Orchestration: PostgreSQL ×3, MinIO, gateway, all services with dependencies and health checks |

## gRPC contracts (`Shared.Protos`)

- `files.proto` — service `Files`: `UploadBook`, `UploadCover` (streaming upload), `DownloadFile` (streaming download), `DeleteFile`
- `metadata.proto` — service `Metadata`: `FetchBookMetadata` (metadata + cover extraction)

## Getting started

1. Install the .NET SDK 10 (`global.json` pins `10.0.302`).
2. Run the orchestrator:

   ```
   dotnet run --project BookTracking.AppHost
   ```

3. Aspire starts PostgreSQL, MinIO, and all services.
4. The API is available through the gateway on port 3000:
   - `/api/auth/**` — Auth.Api
   - `/api/catalog/**` — Catalog.Api
   - MinIO console and PgWeb (port 5050) are available for debugging

### Example: adding a book

```bash
curl -X POST http://localhost:3000/api/catalog/books \
  -H "Authorization: Bearer <token>" \
  -F "Title=The Book" \
  -F "Author=Author Name" \
  -F "Isbn=978-5-1" \
  -F "TagIds=<tag-guid>" \
  -F "File=@book.epub;type=application/epub+zip"
```

`Title` and `File` are required; `Author`, `Isbn`, and `TagIds` are optional (missing values are filled from the book's metadata). Max file size is 100 MB.

## Project structure

```
Auth.Api/               # REST authentication service
Catalog.Api/            # REST catalog service (books, tags)
BookMetadata.Grpc/      # gRPC metadata extraction service
BookMetadata.Parsers/   # Parser library (EPUB, FB2, PDF) + cover generation
Storage.Grpc/           # gRPC file storage service (MinIO)
BookTracking.AppHost/   # Aspire orchestrator
BookTracking.ServiceDefaults/  # Shared Aspire settings (telemetry, health checks)
Shared/                 # Common components: gRPC clients, JWT options, content types
Shared.Protos/          # Proto contracts and generated clients
BookMetadata.Tests.Unit/  # Parser unit tests
```