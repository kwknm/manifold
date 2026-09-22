# Manifold — библиотека электронных книг

Проект для загрузки, чтения и отслеживания библиотеки электронных книг.

## Обзор

Manifold — бэкенд-решение, построенное по микросервисной архитектуре. Пользователи могут загружать книги (EPUB, FB2, PDF), автоматически извлекать метаданные (заголовок, автор, ISBN, количество страниц, обложка), хранить файлы в объектном хранилище и вести личный каталог книг с тегами.

## Технологический стек

- **.NET 10** (C#) — все сервисы
- **.NET Aspire** — оркестрация, service discovery, health checks, OpenTelemetry
- **ASP.NET Core (Minimal APIs + Carter)** — REST API
- **gRPC** — межсервисное взаимодействие (proto-контракты в `Shared.Protos`)
- **PostgreSQL + EF Core 10** — хранение данных (миграции применяются автоматически при запуске)
- **MinIO** — объектное хранилище файлов книг
- **YARP** — API-шлюз с маршрутизацией по префиксам
- **JWT (Bearer)** — аутентификация, refresh-токены
- **FluentValidation** — валидация запросов
- **ErrorOr** — результатные типы для обработки ошибок
- **VersOne.Epub**, **PdfPig** — парсинг EPUB и PDF

## Архитектура

```
                    ┌──────────┐
  Клиент ─────────▶ │  Gateway │  (YARP, порт 3000)
                    └────┬─────┘
                         │
        ┌────────────────┼─────────────────┐
        │                │                 │
 ┌──────▼─────┐   ┌──────▼──────┐   ┌──────▼─────────┐
 │  Auth.Api  │   │ Catalog.Api │   │  BookMetadata  │
 │  (REST)    │   │  (REST)     │   │  .Grpc        │
 └──────┬─────┘   └──────┬──────┘   └──────┬─────────┘
        │                │                 │
        │                │         ┌──────▼─────┐
        │                └────────▶│ Storage.Grpc│
        │                          └──────┬─────┘
        │                                 │
   ┌────▼─────┐   ┌──────────┐   ┌────────▼────────┐
   │  users-db│   │ catalog- │   │ storage-db      │
   │ (Postgres)│   │ db       │   │ (Postgres)      │
   └──────────┘   │(Postgres) │   └────────┬────────┘
                  └──────────┘            │
                                    ┌─────▼─────┐
                                    │  MinIO    │
                                    └───────────┘
```

## Сервисы

### Auth.Api (REST, `/api/auth`)
Аутентификация и регистрация пользователей:
- Регистрация, вход, выход, обновление токенов
- Пароли хешируются через PBKDF2
- JWT access-токены + refresh-токены в БД
- Использует Carter-модули (`Modules/AuthModule.cs`)

### Catalog.Api (REST, `/api/catalog`)
Личный каталог книг и тегов:
- CRUD книг: заголовок, автор, ISBN, количество страниц, файл, обложка
- CRUD тегов (с цветом `ColorHex`)
- Книги и теги привязаны к `UserId` — изоляция данных пользователей

### BookMetadata.Grpc (gRPC)
Извлечение метаданных из загруженных книг:
- Принимает файл потоком (`stream FileChunk`)
- Определяет формат и парсит метаданные (EPUB, FB2, PDF)
- Возвращает заголовок, автора, ISBN, количество страниц и обложку
- Обложка: EPUB/FB2 — извлекается из файла, при отсутствии генерируется плейсхолдер (SkiaSharp);
  PDF — рендер первой страницы (PDFtoImage/PDFium). Обложка загружается в Storage.Grpc (бакет `covers`) и возвращается как `cover_file_id`

### Storage.Grpc (gRPC)
Хранение файлов в MinIO:
- Потоковая загрузка файлов книги (бакет `books`) и обложек (`UploadCover`, бакет `covers`)
- Запись метаданных файла (bucket, object name, content type, размер) в PostgreSQL

### BookTracking.AppHost
Aspire-оркестратор: PostgreSQL (3 базы), MinIO, gateway, все сервисы с зависимостями и health checks.

## Данные

- `users-db` — пользователи и refresh-токены
- `catalog-db` — книги, теги
- `storage-db` — метаданные файлов в MinIO

## Как запустить

1. Установите .NET SDK 10 (`global.json` — версия `10.0.302`).
2. Запустите оркестратор:

   ```
   dotnet run --project BookTracking.AppHost
   ```

3. Aspire поднимет PostgreSQL, MinIO и все сервисы.
4. API доступен через gateway на порту 3000:
   - `/api/auth/**` — Auth.Api
   - `/api/catalog/**` — Catalog.Api
   - MinIO/PgWeb (порт 5050) доступны для отладки.

## gRPC-контракты (`Shared.Protos`)

- `files.proto` — сервис `Files.UploadBook` (потоковая загрузка файла)
- `metadata.proto` — сервис `Metadata.FetchBookMetadata` (извлечение метаданных)

## Структура репозитория

```
Auth.Api/               # REST-сервис аутентификации
Catalog.Api/            # REST-сервис каталога книг
BookMetadata.Grpc/      # gRPC-сервис извлечения метаданных
BookMetadata.Parsers/   # Библиотека парсеров (EPUB, FB2, PDF)
Storage.Grpc/           # gRPC-сервис хранения файлов (MinIO)
BookTracking.AppHost/   # Aspire-оркестратор
BookTracking.ServiceDefaults/  # Общие настройки Aspire (telemetry, health checks)
Shared/                 # Общие компоненты: клиенты gRPC, опции JWT, расширения
Shared.Protos/          # proto-контракты и сгенерированные клиенты
```