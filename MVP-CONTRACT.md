# MVP-контракт Manifold (Backend)

| Параметр | Значение |
|---|---|
| Статус | Этап 0 принят; этап 1 выполнен (BE-002 через user secrets, BE-004, BE-016; BE-003 — в объёме закрытого MVP) |
| Дата | 2026-11-07 |
| Область | Только Backend. Frontend, UI и клиентские приложения не рассматриваются |
| Решения владельца | (1) Загрузка книги использует только автоматически извлечённые метаданные, без override-полей `Title`/`Author`/`Isbn`; (2) MVP разворачивается в закрытом/контролируемом окружении |
| Назначение документа | Зафиксировать целевой объём MVP, инварианты данных, контракты API и критерии готовности. Служит входом для задач BE-001…BE-036 |
| Приоритет документов | Этот файл задаёт **целевой** контракт MVP. [`README.md`](README.md) остаётся каноническим описанием архитектуры и текущего поведения. Расхождения перечислены в §11 и устраняются вместе с реализацией соответствующих задач |

---

## 1. Границы MVP

### 1.1. Входит в MVP

| Возможность | Текущее состояние | Требование MVP |
|---|---|---|
| Регистрация, вход, refresh, logout, `/me` | Реализовано | Валидация обязательна на уровне handlers; корректные HTTP-коды; устойчивость к гонкам |
| JWT access-токены | Реализовано | HS256, проверка issuer/audience/lifetime/signature; секрет только из окружения |
| Refresh-токены с ротацией | Реализовано | Атомарная ротация; обнаружение повторного использования; отзыв family |
| Персональный каталог книг | Реализовано | CRUD; стабильная пагинация; изоляция по пользователю |
| Теги (цветные) | Реализовано | CRUD; уникальность имени в рамках пользователя; привязка к книгам |
| Загрузка EPUB, FB2, PDF | Реализовано | Один файл за запрос; проверка типа и размера; контролируемые ошибки |
| Извлечение метаданных | Реализовано | Title, авторы, ISBN, обложка, количество страниц |
| Сохранение `PageCount` в каталоге | **Отсутствует** (регрессия) | Поле проходит весь pipeline: parser → proto → Catalog DB → API |
| Обложки: извлечение и placeholder | Реализовано | Raster-обложка или детерминированный placeholder; повреждённые изображения не сохраняются как есть |
| Presigned URL для скачивания | Реализовано | JSON-ответ с URL; HTTPS вне локального окружения |
| Изоляция данных пользователей | Реализовано | Любая чужая сущность недоступна; ответ 404 без раскрытия существования |
| Ограничение размера загрузки | Частично | Лимит enforced на REST **и** внутри Storage/Metadata |
| Миграции EF Core | Реализовано (dev) | Применяются автоматически в dev; отдельный gate для production |
| Unit-тесты парсеров | Реализовано | Сохраняются и расширяются |
| Integration-тесты Auth/Catalog/Storage | **Отсутствуют** | Обязательны для MVP |
| Health checks | REST-сервисы — только Development; gRPC-сервисы — не подключены | Для закрытого MVP допустимо; до публичного релиза — пересмотреть |

### 1.2. Не входит в MVP (deferred)

| Функция | Причина |
|---|---|
| Publisher, Language, Year | Нет источника данных и требований: парсеры не заполняют поля, proto их не отдаёт, README/PROJECT не заявляют их как пользовательскую функцию |
| Override-поля `Title`/`Author`/`Isbn` при загрузке | Решение владельца (D-01); пример загрузки в README уже приведён в соответствие |
| Password reset, email verification, MFA, OAuth/OIDC | Требуют внешней инфраструктуры (email, identity provider) и не заявлены как функция MVP |
| Роли, админ-панель, модерация | Нет требований |
| Публичные библиотеки, sharing книг | Нет требований |
| Полнотекстовый поиск, рекомендации | Достаточно подстрочного поиска по title/author |
| Полки, коллекции, прогресс чтения, закладки, заметки | Нет требований |
| Импорт из внешних каталогов, конвертация форматов | Нет требований |
| Редактирование и загрузка пользовательских обложек | Нет требований |
| Дополнительные форматы (MOBI, AZW, DOCX, RTF и т. п.) | Вне заявленных EPUB/FB2/PDF |
| Фоновая очередь обработки метаданных | Синхронный upload достаточен для MVP; очередь — оптимизация |
| CDN, кэширование обложек | Не требуется на MVP-нагрузке |
| Soft delete, audit log | Усложнение без требования |
| Удаление аккаунта, экспорт данных | Нет требований |
| Rate limiting для auth | Рекомендуется; обязательно до публичного релиза (D-04), не блокирует закрытый MVP |
| Публичный интернет-релиз | Требует service auth для gRPC (публичная часть BE-003), включённого HTTPS (BE-004) и rate limiting (BE-009) |

---

## 2. Публичная поверхность и trust boundary

**Решение D-04:** MVP — закрытое/контролируемое окружение.

| Компонент | Доступ | Требование |
|---|---|---|
| YARP gateway, порт 3000 | Публичный | Маршрутизирует только `/api/auth/**` и `/api/catalog/**`; префиксы срезаются |
| `Auth.Api` | Через gateway | REST |
| `Catalog.Api` | Через gateway | REST |
| `Storage.Grpc` | **Только внутренняя сеть** | Не публикуется наружу; при публичном релизе — service auth (BE-003) |
| `BookMetadata.Grpc` | **Только внутренняя сеть** | То же |
| SeaweedFS S3 | Доступен клиенту для presigned URL | В локальном окружении `http://localhost:8333`; для внешних клиентов нужен внешний S3 endpoint |
| PgWeb | Только отладка | Не публиковать |

Инварианты:

- Клиент не может напрямую вызвать `Files.*` или `Metadata.*`.
- После BE-002/BE-006/BE-016 приложение не запускается без валидной конфигурации (JWT, PasswordHashing, Seaweed, connection strings).
- Health endpoints в текущем виде доступны только в Development ([`BookTracking.ServiceDefaults/Extensions.cs:113–130`](BookTracking.ServiceDefaults/Extensions.cs#L113-L130)); для закрытого MVP это допустимо.

---

## 3. Доменная модель

### 3.1. Book

| Поле | Тип | Правила |
|---|---|---|
| `Id` | Guid | Генерируется БД |
| `UserId` | Guid | Владелец; устанавливается из JWT, не из запроса |
| `Title` | string | Обязательно, 1…200 символов; при пустом значении из файла — `"Unknown"` |
| `Isbn` | string? | Опционально, ≤20 символов; checksum не проверяется (D-15) |
| `PageCount` | int? | Опционально; `> 0` — известно, иначе `null` (D-12) |
| `FileId` | Guid | Обязательно; ссылка на `StorageFile` |
| `CoverFileId` | Guid? | Опционально; ссылка на `StorageFile` в bucket `covers` |
| `Authors` | 0…N | Каждый 1…64 символа; уникальность в рамках пользователя (BE-022) |
| `Tags` | 0…N | Только теги того же пользователя |

Инварианты:

1. `Book.UserId` совпадает с владельцем всех связанных тегов и авторов.
2. Пока книга существует, `FileId` и `CoverFileId` должны указывать на существующие записи Storage (допускается окно eventual cleanup, см. §9).
3. Удаление книги каскадно удаляет связи `BookTag` и `AuthorBook`; записи `Author`/`Tag` остаются, пока не удалены явно.
4. Чужой `BookId` неотличим от несуществующего: всегда 404.

Текущая реализация: [`Catalog.Api/Database/Entities/Book.cs`](Catalog.Api/Database/Entities/Book.cs), [`Catalog.Api/Database/CatalogDbContext.cs`](Catalog.Api/Database/CatalogDbContext.cs).

### 3.2. Author

| Поле | Тип | Правила |
|---|---|---|
| `Id` | Guid | |
| `UserId` | Guid | Владелец |
| `FullName` | string | 1…64 символа; нормализуется (D-13); уникальность `(UserId, FullName)` — BE-022 |

Текущая реализация: [`Catalog.Api/Database/Entities/Author.cs`](Catalog.Api/Database/Entities/Author.cs).

### 3.3. Tag

| Поле | Тип | Правила |
|---|---|---|
| `Id` | Guid | |
| `UserId` | Guid | Владелец |
| `Name` | string | 1…100 символов; уникальность `(UserId, Name)` |
| `ColorHex` | string | Строго `^#[0-9a-fA-F]{6}$` |

Удаление тега отсоединяет его от книг, но не удаляет книги.

Текущая реализация: [`Catalog.Api/Database/Entities/Tag.cs`](Catalog.Api/Database/Entities/Tag.cs), [`Catalog.Api/Validators/AddTagRequestValidator.cs`](Catalog.Api/Validators/AddTagRequestValidator.cs).

### 3.4. User и RefreshToken

| Сущность | Правила |
|---|---|
| `User` | `Email` нормализуется в lower-case, уникален, ≤254; `UserName` 1…12; `PasswordHash` в формате `iterations.base64(salt).base64(hash)` |
| `RefreshToken` | Хранится только SHA-256 hex (64 символа), уникален; `FamilyId` группирует сессию; `RevokedAt`/`ReplacedByTokenHash` для ротации |

Текущая реализация: [`Auth.Api/Database/Entities/User.cs`](Auth.Api/Database/Entities/User.cs), [`Auth.Api/Database/Entities/RefreshToken.cs`](Auth.Api/Database/Entities/RefreshToken.cs), [`Auth.Api/Services/Pbkdf2PasswordHasher.cs`](Auth.Api/Services/Pbkdf2PasswordHasher.cs).

### 3.5. Нормализация метаданных (D-13)

При загрузке книги применяется детерминированная нормализация; ошибка метаданных не должна приводить к 500:

1. `Title`: trim, схлопывание повторных пробелов; пусто → `"Unknown"`; длиннее 200 — усечение с warning в лог.
2. `Authors`: trim, удаление пустых, `Distinct` (ordinal), каждый длиннее 64 — усечение с warning.
3. `Isbn`: trim; пусто → `null`; длиннее 20 — усечение с warning.
4. `PageCount`: `> 0` сохраняется; `<= 0` → `null`.
5. Cover: допускается только raster (JPEG/PNG/GIF/WebP по фактическому содержимому); иначе placeholder.

Текущее расхождение: [`Catalog.Api/Modules/BookModule.cs:92–104`](Catalog.Api/Modules/BookModule.cs#L92-L104) не применяет limits; задача BE-023.

---

## 4. Форматы, лимиты и обложки

### 4.1. Поддерживаемые форматы

| Формат | Content-Type | Допустимые расширения | Parser | PageCount |
|---|---|---|---|---|
| EPUB | `application/epub+zip` | `.epub` | [`EpubParser`](BookMetadata.Parsers/EpubParser.cs) | Оценка по объёму текста |
| FB2 | `application/x-fictionbook+xml` | `.fb2`, `.fb2.zip` | [`Fb2Parser`](BookMetadata.Parsers/Fb2Parser.cs) | Не определяется (`null`) |
| PDF | `application/pdf` | `.pdf` | [`PdfParser`](BookMetadata.Parsers/PdfParser.cs) | Фактическое число страниц |

Требования:

- MIME сравнивается case-insensitively; MIME и расширение должны быть согласованы (BE-013).
- FB2.ZIP принимается с FB2 Content-Type; распаковка ограничена (BE-012).
- Любой другой формат → 400.

Текущее расхождение: [`BookMetadata.Grpc/Services/MetadataService.cs:19–37`](BookMetadata.Grpc/Services/MetadataService.cs#L19-L37) проверяет только MIME, а parser выбирается по расширению ([`MetadataService.cs:41–45`](BookMetadata.Grpc/Services/MetadataService.cs#L41-L45)).

### 4.2. Лимиты

| Лимит | Значение | Где enforced |
|---|---|---|
| Размер файла книги | 100 MiB ([`Shared/UploadLimits.cs:5`](Shared/UploadLimits.cs#L5)) | REST validation; Storage and Metadata (BE-010, BE-012) |
| Размер multipart-запроса | 110 MiB ([`Shared/UploadLimits.cs:10`](Shared/UploadLimits.cs#L10)) | Kestrel и `FormOptions` в Catalog; gateway env в [`AppHost.cs:47`](BookTracking.AppHost/AppHost.cs#L47) |
| Файлов за запрос | 1 | Контракт `AddBookRequest` |
| Срок presigned URL | 600 секунд | `SeaweedOptions.PresignedUrlExpirySeconds`; конфигурация применяется (BE-016) |
| Размер и число ZIP entries | Конфигурируемый лимит | BE-012 |
| Размеры изображения обложки | Конфигурируемый лимит | BE-014 |
| Параллельный parsing | Конфигурируемый лимит | BE-012 |

Значение gateway (110 MiB) дублирует `UploadLimits.MaxRequestSizeBytes`; рассинхронизация должна быть исключена или проверена тестом (BE-010).

### 4.3. Обложки

- Raster-типы: JPEG, PNG, GIF, WebP ([`CoverMimeDetector.cs:5–15`](BookMetadata.Parsers/CoverMimeDetector.cs#L5-L15)).
- SVG и прочие форматы не сохраняются как обложка — генерируется placeholder ([`EpubParser.cs:82–90`](BookMetadata.Parsers/EpubParser.cs#L82-L90), [`Fb2Parser.cs:97–109`](BookMetadata.Parsers/Fb2Parser.cs#L97-L109)).
- PDF: первая страница рендерится в JPEG с ограничением ширины 800 px ([`PdfParser.cs:10–11`](BookMetadata.Parsers/PdfParser.cs#L10-L11)).
- Placeholder: 600×900 JPEG, детерминирован по title/author ([`PlaceholderCoverGenerator.cs`](BookMetadata.Parsers/PlaceholderCoverGenerator.cs)).
- Повреждённое raster-изображение с валидной сигнатурой не должно попадать в хранилище (BE-014).

---

## 5. Аутентификация и авторизация

### 5.1. Поток

1. `register` → пользователь + access token + refresh token.
2. `login` → access token + refresh token.
3. `refresh` → старый refresh отзывается, выдаётся новая пара в той же `FamilyId`.
4. Повторное использование отозванного refresh → отзыв всей family, 401.
5. `logout` → отзыв переданного refresh.
6. `/me` → профиль из claims.

### 5.2. Параметры

| Параметр | Значение | Источник |
|---|---|---|
| Access token lifetime | 10 минут | `Jwt.AccessTokenLifetimeMinutes` |
| Refresh token lifetime | 30 дней | `Jwt.RefreshTokenLifetimeDays` |
| Алгоритм | HS256 | [`JwtTokenService.cs:19–20`](Auth.Api/Services/JwtTokenService.cs#L19-L20) |
| Проверки | issuer, audience, lifetime, signature, ClockSkew 30 s | [`Shared/Extensions/AuthExtensions.cs:22–32`](Shared/Extensions/AuthExtensions.cs#L22-L32) |
| Хранение refresh | SHA-256 hash | [`Auth.Api/Services/JwtTokenService.cs:53–57`](Auth.Api/Services/JwtTokenService.cs#L53-L57) |

### 5.3. Принятые решения

- **D-05:** `logout` отзывает только refresh token. Уже выданный access token действует до истечения (≤10 минут). Немедленная инвалидизация access token не входит в MVP.
- **D-08:** минимальная длина пароля — 6 символов (текущее правило). Для закрытого MVP принимается как известный риск; до публичного релиза повысить до ≥8 и добавить rate limiting.
- **D-09:** email verification, password reset, MFA отсутствуют в MVP.
- **D-04:** rate limiting не блокирует закрытый MVP; обязателен до публичного релиза.
- **BE-002:** JWT `SecretKey` не хранится в tracked-конфигурации и поступает из окружения/secret store.

### 5.4. Ошибки

| Ситуация | HTTP |
|---|---|
| Некорректный payload | 400 |
| Неверный email/пароль | 401 |
| Невалидный/просроченный/повторно использованный refresh | 401 |
| Дубликат email (включая гонку) | 409 |
| Отсутствующий/невалидный access token на защищённом endpoint | 401 |

---

## 6. REST API

Все пути указаны так, как их видит клиент через gateway. Внутри сервисов префиксы `/api/auth` и `/api/catalog` срезаются.

### 6.1. Auth

| Метод | Путь | Auth | Успех | Ошибки |
|---|---|---|---|---|
| POST | `/api/auth/register` | — | 201 `AuthResponse` | 400, 409 |
| POST | `/api/auth/login` | — | 200 `AuthResponse` | 400, 401 |
| POST | `/api/auth/refresh` | — | 200 `AuthResponse` | 400, 401 |
| POST | `/api/auth/logout` | — | 204 | 400 |
| GET | `/api/auth/me` | Bearer | 200 `{ id, email, userName }` | 401 |

`AuthResponse`: `accessToken`, `refreshToken`, `accessTokenExpiresAt`, `refreshTokenExpiresAt`.

### 6.2. Books

| Метод | Путь | Auth | Успех | Ошибки |
|---|---|---|---|---|
| POST | `/api/catalog/books` | Bearer | 201 `BookResponse` | 400, 401, 404, 502, 503, 500 |
| GET | `/api/catalog/books/my` | Bearer | 200 `Paged<BookResponse>` | 401 |
| GET | `/api/catalog/books/{id}` | Bearer | 200 `BookResponse` | 401, 404 |
| GET | `/api/catalog/books/{id}/file` | Bearer | 200 `BookFileResponse` | 401, 404, 502, 503 |
| PATCH | `/api/catalog/books/{id}` | Bearer | 200 `BookResponse` | 400, 401, 404 |
| DELETE | `/api/catalog/books/{id}` | Bearer | 204 | 401, 404 |

`BookResponse`: `id`, `title`, `authors[]`, `isbn`, `pageCount`, `coverUrl`, `tags[]`, `userId`.

- `pageCount` — **новое аддитивное поле** (BE-024). `null` означает «неизвестно».
- `userId` сохраняется для обратной совместимости (D-14).
- `Paged<BookResponse>`: `items`, `page`, `pageSize`, `totalCount`, `totalPages`, `hasNextPage`.

### 6.3. Upload

`POST /api/catalog/books` — `multipart/form-data`:

| Поле | Обязательно | Формат |
|---|---|---|
| `File` | Да | EPUB / FB2 / PDF, ≤100 MiB |
| `TagIds` | Нет | Список GUID через запятую; повторяющиеся отбрасываются |

**D-01:** поля `Title`, `Author`, `Isbn` **не принимаются**. Источник истины — метаданные файла. Клиент не может переопределить title/authors/isbn при загрузке.

Правила:

- Все переданные `TagIds` должны принадлежать пользователю; иначе 400 (`Book.InvalidTags`).
- При ошибке до сохранения книги загруженные файлы удаляются (rollback).
- При успешном сохранении книги ошибка получения `coverUrl` не откатывает книгу и не удаляет файлы (BE-019).

### 6.4. Tags

| Метод | Путь | Успех | Ошибки |
|---|---|---|---|
| GET | `/api/catalog/tags` | 200 `TagResponse[]` | 401 |
| POST | `/api/catalog/tags` | 201 `TagResponse` | 400, 401, 409 |
| PUT | `/api/catalog/tags/{id}` | 200 `TagResponse` | 400, 401, 404, 409 |
| DELETE | `/api/catalog/tags/{id}` | 204 | 401, 404 |

`TagResponse`: `id`, `name`, `colorHex`.

### 6.5. Pagination, search, filtering

- `page` по умолчанию 1, clamp 1…10 000.
- `pageSize` по умолчанию 20, clamp 1…100.
- Сортировка: `Title`, затем `Id` (стабильная; BE-021).
- `search`: регистронезависимый подстрочный поиск по `Title` или любому автору; введённые `%`, `_`, `\` трактуются буквально (BE-036).
- `tagIds`: книга включается, если имеет **хотя бы один** из указанных тегов (OR).
- `search` и `tagIds` комбинируются через AND.
- Чужие теги в фильтре не раскрывают данные и не дают совпадений.

Текущая реализация: [`CatalogService.GetUserCatalogAsync`](Catalog.Api/Services/CatalogService.cs#L63-L123).

### 6.6. Partial update книги

`PATCH /api/catalog/books/{id}` — JSON:

| Поле | Семантика |
|---|---|
| `title` | `null` — не менять; пустая строка — 400; ≤200 |
| `authors` | `null` — не менять; `[]` — очистить; иначе заменить |
| `isbn` | `null` — не менять; `""` — очистить; ≤20 |
| `tagIds` | `null` — не менять; `[]` — очистить; иначе заменить |

Текущая реализация: [`Catalog.Api/Validators/UpdateBookRequestValidator.cs`](Catalog.Api/Validators/UpdateBookRequestValidator.cs), [`CatalogService.UpdateBookAsync`](Catalog.Api/Services/CatalogService.cs#L161-L225).

---

## 7. Ошибки и ProblemDetails

Формат — `application/problem+json` (RFC 7807).

| Код | Когда |
|---|---|
| 400 | Валидация payload/query; некорректные метаданные; несовместимый формат |
| 401 | Нет/невалиден access token; неверные credentials; невалидный refresh |
| 403 | Недостаточно прав (зарезервировано; в MVP не ожидается) |
| 404 | Сущность не найдена или принадлежит другому пользователю |
| 409 | Дубликат email/tag |
| 429 | Rate limiting (при включении) |
| 500 | Непредвиденная ошибка |
| 502 | Некорректный ответ внутреннего сервиса |
| 503 | Внутренний сервис недоступен |
| 504 | Таймаут внутреннего вызова |

Требования:

- Внутренние exception details не попадают в ответ (BE-015).
- Validation errors содержат имена полей.
- Child-сервисы маппятся в ProblemDetails централизованно ([`RpcExtensions.cs`](Catalog.Api/Extensions/RpcExtensions.cs)).

---

## 8. Конфигурация и секреты

| Настройка | Источник | Требование |
|---|---|---|
| `Jwt:SecretKey` | User secrets (Development) / environment или secret store (deploy) | Не в Git; минимум 32 символа; одинаковое значение в Auth и Catalog (BE-002) |
| `UserSecretsId` | `Auth.Api.csproj`, `Catalog.Api.csproj` | Заданы; секрет хранится вне репозитория |
| `Jwt:Issuer`, `Jwt:Audience`, lifetimes | Конфигурация | Валидируются на startup |
| `PasswordHashing:*` | Конфигурация | Bind + validation (BE-006) |
| `Seaweed:*` | Конфигурация | Bind + validation на startup (BE-016) |
| Connection strings | Aspire service discovery | Не хардкодить production-значения |
| `Kestrel__Limits__MaxRequestBodySize` (gateway) | AppHost | Согласовано с `UploadLimits` |

---

## 9. Согласованность данных и очистка

### 9.1. Источники истины

- **Catalog DB** — перечень книг пользователя и связи с тегами/авторами.
- **Storage DB** — метаданные файлов (bucket, object name, content type, size).
- **SeaweedFS** — байты файлов.

### 9.2. Инварианты

1. Книга не может ссылаться на удалённый StorageFile (иначе — повреждённые данные).
2. StorageFile не должен оставаться без объекта в S3 дольше окна очистки.
3. Операции очистки идемпотентны и не маскируют исходную ошибку.
4. Ошибка очистки не превращает успешную пользовательскую операцию в 500.

### 9.3. Ожидаемое поведение

- **Upload:** файл → metadata → сохранение книги. До commit — компенсационное удаление при ошибке. После commit — файлы не удаляются (BE-019).
- **Delete book:** сначала удаляется запись каталога, затем best-effort удаляются файлы; при сбое Storage допускается временный orphan, который устраняет reconciliation (BE-018).
- **Delete StorageFile:** операция должна быть согласованной или восстанавливаемой; текущая последовательность S3→DB может оставить «висячую» запись (BE-017/018).
- **Reconciliation:** периодическая или ручная процедура сверки Catalog ↔ Storage ↔ S3; обязательна для MVP (BE-018).

### 9.4. Конкурентность

| Сценарий | Требование |
|---|---|
| Два refresh с одним токеном | Успешен ровно один (BE-007) |
| Две регистрации одного email | Одна успешна, вторая — 409 (BE-008) |
| Два создания тега с одним именем | Один успешен, второй — 409 (BE-020) |
| Два создания одного автора | Без дубликата (BE-022) |
| Пагинация при одинаковых title | Без дублей/пропусков (BE-021) |

---

## 10. Нефункциональные требования

| Категория | Требование |
|---|---|
| Производительность | Каталог: count + split-запросы за книгами, авторами и тегами (без N+1 по каждой книге); presigned URL — пакетно |
| Память | Обработка одного файла ограничена лимитами; параллелизм ограничен; отсутствие OOM на 100 MiB-файлах |
| Время | gRPC timeout 10 минут ([`BookTracking.ServiceDefaults/Extensions.cs:29–40`](BookTracking.ServiceDefaults/Extensions.cs#L29-L40)); cancellation пробрасывается |
| Наблюдаемость | Структурированные логи; токены, пароли и presigned URL не логируются; ошибки cleanup видны в логах/метриках |
| Безопасность | Нет секретов в Git; изоляция по `UserId`; gRPC не публикуется; HTTPS вне локального окружения |
| Совместимость | Существующие пути и поля сохраняются; новые поля — аддитивные |
| Тесты | Unit + integration; CI выполняет build и test |
| Документация | README/PROJECT/AGENTS синхронизируются с фактическим поведением (BE-035) |

---

## 11. Расхождения документации и требования к синхронизации

| Расхождение | Где | Решение | Задача |
|---|---|---|---|
| README показывал `-F Title/Author/Isbn` и фразу «missing values are filled from metadata» | [`README.md:135–142`](README.md#L135-L142) | **Исправлено в этапе 0:** оставлены только `File` и `TagIds` (D-01) | — |
| README/PROJECT заявляют page count как пользовательскую функцию, но каталог его не хранит и не отдаёт | [`README.md:8`](README.md#L8), [`PROJECT.md:7`](PROJECT.md#L7) | Вернуть `PageCount` в каталог и `BookResponse` (D-02) | BE-024 |
| PROJECT.md называет хранилище MinIO | [`PROJECT.md:16–17`](PROJECT.md#L16-L17) | Фактически SeaweedFS; привести к README | BE-035 |
| PROJECT.md перечисляет поля CRUD без page count | [`PROJECT.md:62`](PROJECT.md#L62) | Добавить `PageCount` | BE-035 |
| AGENTS.md утверждает, что единственный тестовый проект — parser unit | [`AGENTS.md:8`](AGENTS.md#L8) | Обновить после появления integration-тестов | BE-035 |
| AGENTS.md называет хранилище MinIO | [`AGENTS.md:9`](AGENTS.md#L9) | Фактически SeaweedFS | BE-035 |
| AGENTS.md называет dev-secret закоммиченным как допустимый | [`AGENTS.md:28`](AGENTS.md#L28) | **Исправлено в этапе 1** | — |
| `BookMetadata` содержит Publisher/Language/Year, но они нигде не заполняются | [`BookMetadata.cs:9–16`](BookMetadata.Parsers/BookMetadata.cs#L9-L16) | Оставить как технический долг; не заявлять в API | BE-035 (пометка) |

---

## 12. Принятые решения (Decision log)

| ID | Решение | Обоснование | Последствия |
|---|---|---|---|
| D-01 | Загрузка использует только метаданные файла; override-поля не принимаются | Подтверждено владельцем; минимальный объём; текущий код уже работает так | README исправлен (пример загрузки); override — post-MVP |
| D-02 | `PageCount` входит в MVP | README/PROJECT заявляют page count; proto и парсеры его уже содержат; поле было удалено из Book миграцией `20260922120000` | Новая миграция, изменение `BookResponse`, тесты |
| D-03 | Publisher/Language/Year отложены | Нет требований и источников данных | Поля остаются неиспользуемыми; API их не отдаёт |
| D-04 | MVP — закрытое/контролируемое окружение | Подтверждено владельцем | BE-003 (service auth), BE-004 (HTTPS config) и BE-009 — гейты публичного релиза, не блокеры закрытого MVP |
| D-05 | Logout отзывает только refresh token | Текущая реализация; stateless JWT; 10-минутное окно приемлемо | Немедленная инвалидация access — post-MVP |
| D-06 | Лимит файла 100 MiB, запроса 110 MiB | Текущие значения и документация | Лимиты должны enforced на всех слоях |
| D-07 | Placeholder-обложка обязательна | Уже реализовано; книга без обложки — валидный сценарий | Повреждённые изображения заменяются placeholder |
| D-08 | Минимальная длина пароля 6 символов | Текущее правило; закрытый MVP | Повышение до ≥8 — до публичного релиза |
| D-09 | Нет email verification/reset/MFA | Нет требований и инфраструктуры | Post-MVP |
| D-10 | Hard delete; очистка Storage — eventual с reconciliation | Простота; отсутствие требований на восстановление | Нужна reconciliation-процедура |
| D-11 | Фильтр тегов — OR; поиск и фильтр — AND | Текущая реализация | Задокументировать в README |
| D-12 | `PageCount <= 0` → `null` | «Неизвестно» не должно выглядеть как 0 страниц | Маппинг в BE-024 |
| D-13 | Metadata нормализуется (trim/truncate), а не отклоняется | Валидный файл с длинным названием не должен давать 400/500 | BE-023, логирование усечений |
| D-14 | `userId` остаётся в `BookResponse` | Обратная совместимость | Не удалять без отдельного решения |
| D-15 | ISBN без checksum-валидации | Нет требования; источники metadata неполные | Post-MVP |
| D-16 | `ColorHex` строго `#RRGGBB` | Текущая валидация | Сохраняется |
| D-17 | Сортировка каталога — `Title`, затем `Id` | Стабильная пагинация | BE-021 |
| D-18 | Поиск трактует `%`/`_` буквально | Предсказуемость для пользователя | BE-036 |

Отклонённые альтернативы:

- **Override-поля при загрузке** — отклонено для MVP (D-01).
- **Publisher/Language/Year сейчас** — нет источника и требований (D-03).
- **Soft delete** — усложнение без требования (D-10).
- **Event bus/outbox** — избыточно; достаточно компенсации и reconciliation (D-10).
- **Публичный релиз сразу** — требует security-гейтов (D-04).
- **Фоновая обработка upload** — не требуется на MVP-нагрузке.

---

## 13. Критерии готовности Backend к MVP

### 13.1. Функциональность

- [ ] Register → login → `/me` работает через gateway.
- [ ] Refresh ротирует токены; повторное использование отзывает family.
- [ ] Logout делает refresh непригодным.
- [ ] Пользователь видит только свои книги и теги; чужие ID → 404.
- [ ] CRUD книг и тегов работает; partial update соблюдает семантику.
- [ ] EPUB, FB2 и PDF загружаются; metadata сохраняется, включая `PageCount`.
- [ ] Обложка извлекается или генерируется placeholder.
- [ ] Presigned URL скачивания работает.
- [ ] Удаление книги удаляет каталог и инициирует очистку файлов.
- [ ] Пагинация стабильна при одинаковых title.

### 13.2. Безопасность

- [ ] JWT secret отсутствует в Git и ротирован.
- [ ] Auth validation выполняется на всех auth endpoints.
- [ ] Прямой доступ к gRPC закрыт или задокументирован как внутренний.
- [ ] Ошибки не раскрывают внутренние exception details.
- [ ] Presigned URL используют HTTPS вне локального окружения.
- [ ] Пароли и токены не логируются.

### 13.3. Устойчивость

- [ ] Лимиты размера enforced в Storage и Metadata.
- [ ] ZIP bomb и oversized image не приводят к OOM.
- [ ] Ошибка Storage/Metadata до commit приводит к rollback.
- [ ] Ошибка после commit не удаляет файлы сохранённой книги.
- [ ] Определена и проверена процедура reconciliation.
- [ ] Конкурентные refresh/register/tag-сценарии возвращают корректные коды.

### 13.4. Качество

- [ ] `dotnet build BookTracking.slnx` проходит.
- [ ] `dotnet test BookTracking.slnx` проходит (unit + integration).
- [ ] Есть тесты на изоляцию пользователей, refresh-гонки, upload/rollback, лимиты и ошибки.
- [ ] Миграции применяются на чистой БД.
- [ ] Ручной smoke-тест через Aspire gateway пройден.
- [ ] README/PROJECT/AGENTS соответствуют фактическому поведению.

---

## 14. Трассировка задач

| Область контракта | Задачи | Статус |
|---|---|---|
| §1, §3.5, §12 (решения) | BE-001 | Этот документ |
| §8 (секреты) | BE-002 | Выполнено (этап 1, user secrets) |
| §2 (trust boundary) | BE-003 | Выполнено для закрытого MVP (internal-only wiring + docs); service auth — гейт публичного релиза |
| §4.3, §8 (HTTPS) | BE-004 | Выполнено (этап 1) |
| §5.1, §7 (валидация Auth) | BE-005 | Ожидает |
| §3.4, §8 (PasswordHashing) | BE-006 | Ожидает |
| §5.1, §9.4 (refresh) | BE-007 | Ожидает |
| §5.4, §9.4 (register race) | BE-008 | Ожидает |
| §1.2, §5.3 (rate limiting) | BE-009 | Post-MVP для закрытого окружения |
| §4.2, §8 (Storage limits) | BE-010 | Ожидает |
| §4.2 (RAM buffering) | BE-011 | Ожидает |
| §4.2 (Metadata limits) | BE-012 | Ожидает |
| §4.1 (MIME/extension) | BE-013 | Ожидает |
| §4.3 (cover validation) | BE-014 | Ожидает |
| §7 (скрытие ошибок) | BE-015 | Ожидает |
| §8 (SeaweedOptions) | BE-016 | Выполнено (этап 1) |
| §9.2–9.3 (compensation) | BE-017 | Ожидает |
| §9.3 (delete/reconciliation) | BE-018 | Ожидает |
| §6.3 (post-commit) | BE-019 | Ожидает |
| §3.3, §9.4 (tag race) | BE-020 | Ожидает |
| §6.5, §9.4 (pagination) | BE-021 | Ожидает |
| §3.2 (author uniqueness) | BE-022 | Ожидает |
| §3.5 (normalization) | BE-023 | Ожидает |
| §3.1, §6.2 (PageCount) | BE-024 | Ожидает |
| §4.1 (FB2 stream) | BE-025 | Post-MVP (pipeline использует seekable stream) |
| §13.4 (parser tests) | BE-026 | Ожидает |
| §13.4 (auth tests) | BE-027 | Ожидает |
| §13.4 (catalog tests) | BE-028 | Ожидает |
| §13.4 (auth integration) | BE-029 | Ожидает |
| §13.4 (catalog integration) | BE-030 | Ожидает |
| §13.4 (gRPC integration) | BE-031 | Ожидает |
| §13.4 (migrations) | BE-032 | Ожидает |
| §13.4 (build/tests) | BE-033 | Ожидает |
| §13.4 (smoke) | BE-034 | Ожидает |
| §11 (docs) | BE-035 | Частично: пример загрузки, секреты и trust boundary синхронизированы; остальное — после BE-024 |
| §6.5 (LIKE escaping) | BE-036 | Ожидает |

---

## 15. Post-MVP backlog

1. Password reset и email verification.
2. Rate limiting и усиленная password policy.
3. Публичный релиз: service auth для gRPC, HTTPS, внешний S3 endpoint.
4. Override-поля метаданных при загрузке.
5. Publisher/Language/Year.
6. Soft delete и восстановление книг.
7. Фоновая обработка метаданных и очередь.
8. Полнотекстовый поиск.
9. Пользовательские обложки и редактирование.
10. Дополнительные форматы.
11. Кэширование/CDN обложек.
12. Audit log и удаление аккаунта.
