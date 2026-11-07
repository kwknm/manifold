# AGENTS.md

# .NET SDK
- SDK pinned by `global.json`: .NET 10 (`10.0.302`, rollForward latestFeature).

## Commands
- Build: `dotnet build BookTracking.slnx`
- Tests: `dotnet test BookTracking.slnx` — only test project is `BookMetadata.Tests.Unit` (xunit, pure unit tests, no DB/services needed). Single class: `dotnet test BookMetadata.Tests.Unit --filter "FullyQualifiedName~EpubParserTests"`. Test data is built programmatically via `TestData/*Builder.cs`, no fixture files.
- Run stack: `dotnet run --project BookTracking.AppHost` — Aspire starts Postgres (3 DBs), MinIO, all services, gateway on port 3000, PgWeb on 5050. Services depend on Aspire service discovery, so this is the way to run the system.
- EF tooling: `dotnet-ef` is installed globally (no `.config/dotnet-tools.json`). Design-time commands work without a live DB: `dotnet ef dbcontext list --project Catalog.Api --no-build`.
- Windows: while the Aspire stack is running, `dotnet build` emits MSB3026 file-lock warnings on the service exes — harmless, build still succeeds.

## Packages
- Central Package Management in `Directory.Packages.props` with transitive pinning: put the version there, reference without `Version=` in `.csproj`.

## Migrations
- EF Core migrations live inside each service project (`Catalog.Api`, `Auth.Api`, `Storage.Grpc`).
- Applied automatically at startup via `app.ApplyMigrations<TDbContext>()` in each `Program.cs` — don't run `database update` manually in dev.

## Architecture wiring (non-obvious)
- YARP gateway (port 3000) strips `/api/auth` and `/api/catalog` prefixes; routes are defined in `BookTracking.AppHost/AppHost.cs`. Services receive bare paths (e.g. `POST /api/catalog/books` → `/books` in `Catalog.Api/Modules/BookModule.cs`).
- gRPC client addresses are Aspire resource names hardcoded as URIs: `http://storage`, `http://bookmetadata` (in the `AddGrpcClient` calls). Renaming resources in `AppHost.cs` breaks them.
- Proto contracts + generated clients exist only in `Shared.Protos` (`GrpcServices=Both`); all services reference that project. `Storage.Grpc.csproj` contains leftover `None Remove` entries for protos that were moved out — ignore them.
- `Storage.Grpc` Kestrel is HTTP/2-only (`appsettings.json`), which is why gRPC clients use plaintext `http://` URIs.
- `Storage.Grpc` and `BookMetadata.Grpc` are internal-only; `BookTracking.AppHost/AppHost.cs` deliberately does not call `WithExternalHttpEndpoints()` for them. Do not expose these resources externally.
- REST pattern: Carter modules (`Modules/*Module.cs`), FluentValidation validators auto-registered from assembly, `ErrorOr` result types; `RpcException` → HTTP ProblemDetails via `Catalog.Api/Extensions/RpcExtensions.cs`.
- `Shared/Clients/*` (e.g. `FilesClient`) wraps the generated gRPC clients and streams uploads in 32 KB chunks — services depend on `IFilesClient`/`IMetadataClient`, not generated clients directly.
- JWT options (`Jwt` section) use `ValidateOnStart()` with `SecretKey` min 32 chars; any service calling `AddJwtAuthentication` fails fast without it. The signing key is not stored in `appsettings.json`. In Development it is loaded from user secrets (`Jwt:SecretKey`; set the same value in `Auth.Api` and `Catalog.Api`), and `AppHost` propagates its environment to project resources. Deployments must supply `Jwt__SecretKey` via environment or a secret store.

## Docs
- `README.md` (English) and `PROJECT.md` (Russian) describe the same system; README is canonical (architecture diagram, upload flow, example curl).

## Development Rules

- Follow existing architectural patterns.
- Keep changes minimal and scoped to the task.
- Do not introduce unnecessary abstractions.
- Do not modify unrelated files.
- Preserve existing public API contracts unless explicitly requested.
- Explain architectural changes before implementing them.

## Validation

After implementation:

1. Run dotnet build BookTracking.slnx.
2. Run relevant unit tests.
3. Review all modified files.
4. Inspect git diff.
5. Report any tests that could not be executed.

Never claim that build or tests passed without execution.