# Архів курсових і дипломних робіт

Веб-застосунок на **C# (ASP.NET Core 10)** для зберігання курсових і дипломних робіт у хмарі.
Викладачі завантажують роботи за особистим кодом доступу, без реєстрації. Будь-хто (або лише ті, хто має код перегляду)
може знайти роботу **за групою та роком** і скачати її.

## Можливості

- **Каталог робіт.** Фільтри «Рік», «Група», «Тип» і пошук за темою, прізвищем студента чи керівника.
  На головній є швидкий вибір «рік → групи» з кількістю робіт.
- **Завантаження робіт викладачами.** Замість акаунтів із паролями кожен викладач отримує особистий код вигляду `7F3K-92QA`.
- **Адміністрування.** Адміністратор створює коди, вимикає їх, видає нові коди та бачить статистику
  (кількість робіт, обсяг файлів, скачування, розподіл за роками).
- **Керування роботами.** Викладач може редагувати та видаляти лише ті роботи, які завантажив сам. Адміністратор може змінювати будь-які.
- **Хмарне сховище файлів.** Підтримується будь-яке S3-сумісне сховище (Cloudflare R2, Backblaze B2, Amazon S3). Для розробки файли можна тримати в локальній папці.
- **База даних.** PostgreSQL у хмарі (Neon, Supabase, Azure) або SQLite локально.
- **Адаптивний інтерфейс.** Сайт українською, працює на телефоні, підтримує темну тему.
- **Захист.** Від підбору кодів, від CSRF, від завантаження небезпечних файлів. Сесії вимкненого викладача завершуються одразу.

## Технології

| Частина | Технологія |
|---|---|
| Мова | C# 14, .NET 10 (LTS) |
| Веб-фреймворк | ASP.NET Core Razor Pages |
| Робота з БД | Entity Framework Core 10 |
| База даних | PostgreSQL (хмара) / SQLite (локально) |
| Сховище файлів | S3 API (AWSSDK.S3) / локальна папка |
| Вхід | Cookie-автентифікація за кодом доступу |
| Тести | xUnit, WebApplicationFactory (54 тести) |
| Інтерфейс | HTML, CSS (без сторонніх бібліотек) |

## Структура проєкту

```
arkhiv-robit/
├── ArkhivRobit.sln
├── Dockerfile
├── docs/ARCHITECTURE.md        ← опис архітектури (для пояснювальної записки)
├── src/ArkhivRobit/
│   ├── Program.cs              ← налаштування застосунку
│   ├── appsettings.json        ← конфігурація за замовчуванням
│   ├── Models/                 ← Work (робота), Teacher (викладач), WorkType
│   ├── Data/AppDbContext.cs    ← таблиці бази даних
│   ├── Services/               ← коди доступу, пошук, перевірка файлів, підключення до БД
│   │   └── Storage/            ← сховища файлів: LocalFileStorage, S3FileStorage
│   ├── Pages/                  ← сторінки (Razor Pages)
│   │   ├── Index               ← каталог і пошук
│   │   ├── Login, Logout       ← вхід за кодом
│   │   ├── Works/              ← Details, Upload, Edit
│   │   └── Admin/              ← керування кодами та статистика
│   └── wwwroot/                ← CSS, іконка
└── tests/ArkhivRobit.Tests/    ← модульні та інтеграційні тести
```

## Запуск на своєму комп'ютері

Потрібен [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) (або Visual Studio 2026 чи JetBrains Rider).

```bash
cd arkhiv-robit/src/ArkhivRobit
dotnet run
```

Відкрийте http://localhost:5068. У режимі розробки код адміністратора — **`admin-dev`**.
База SQLite і файли створюються автоматично в папці `App_Data/`.

У Visual Studio: відкрийте `ArkhivRobit.sln` і натисніть **▶ http**.

Щоб запустити тести:

```bash
cd arkhiv-robit
dotnet test
```

## Налаштування

Усі налаштування задаються у `appsettings.json` або **змінними середовища**. У хмарі використовуйте змінні середовища,
щоб секрети не потрапили в код. Подвійне підкреслення `__` у назві змінної відповідає вкладеності в JSON.

| Змінна середовища | Опис | Приклад |
|---|---|---|
| `Archive__AdminCode` | **Обов'язково.** Код адміністратора (від 10 символів) | `Kafedra-Admin-2026` |
| `Archive__ViewCode` | Код перегляду. Порожній — архів відкритий для всіх | `Student-2026` |
| `Archive__MaxFileSizeMb` | Максимальний розмір файлу, МБ (за замовчуванням 200) | `200` |
| `Archive__LoginAttemptsLimit` | Спроб входу з однієї IP за 5 хв (за замовчуванням 10) | `10` |
| `Database__Provider` | `Sqlite` або `Postgres` | `Postgres` |
| `ConnectionStrings__Default` | Рядок підключення до БД (підходить і формат `postgresql://…`) | `postgresql://user:pass@host/db?sslmode=require` |
| `Storage__Provider` | `Local` або `S3` | `S3` |
| `Storage__ServiceUrl` | Адреса S3 API сховища | `https://s3.eu-central-003.backblazeb2.com` |
| `Storage__Region` | Регіон (`auto` для Cloudflare R2) | `eu-central-003` |
| `Storage__Bucket` | Назва кошика (bucket) | `arkhiv-robit` |
| `Storage__AccessKey` | Ключ доступу до сховища | `0031a2b…` |
| `Storage__SecretKey` | Секретний ключ сховища | `K003…` |

## Розгортання в хмарі (без GitHub)

Рекомендована схема, безкоштовна або майже безкоштовна:

```
Користувач ──► Azure App Service (C# застосунок)
                   │                 │
                   ▼                 ▼
          Neon PostgreSQL     Backblaze B2 / Cloudflare R2
          (опис робіт)        (файли робіт, 10 ГБ безкоштовно)
```

### Крок 1. Сховище файлів (Backblaze B2 або Cloudflare R2)

**Backblaze B2** (10 ГБ безкоштовно, картка не потрібна):
1. Зареєструйтеся на https://www.backblaze.com/cloud-storage.
2. **Buckets → Create a Bucket**: назва, наприклад, `arkhiv-robit-kafedra`; **Files in Bucket: Private**.
3. Запишіть **Endpoint** кошика, наприклад `s3.eu-central-003.backblazeb2.com`. Звідси `Storage__ServiceUrl = https://s3.eu-central-003.backblazeb2.com`, а `Storage__Region = eu-central-003`.
4. **Application Keys → Add a New Application Key** з доступом Read and Write до цього кошика.
   `keyID` → `Storage__AccessKey`, `applicationKey` → `Storage__SecretKey`.

**Cloudflare R2** (10 ГБ безкоштовно, далі ≈ 0,015 $/ГБ на місяць, скачування безкоштовні; потрібна картка):
1. Cloudflare → **R2 → Create bucket**.
2. **R2 → Manage R2 API Tokens → Create API token** (Object Read & Write).
3. `Storage__ServiceUrl = https://<ACCOUNT_ID>.r2.cloudflarestorage.com`, `Storage__Region = auto`.

### Крок 2. База даних (Neon PostgreSQL)

1. Зареєструйтеся на https://neon.tech (безкоштовно, 0,5 ГБ, цього вистачить на десятки тисяч робіт: у базі зберігається лише опис, файли лежать у сховищі).
2. Створіть проєкт, регіон **Europe (Frankfurt)**.
3. Скопіюйте **Connection string** (`postgresql://…`). Це значення `ConnectionStrings__Default`, а `Database__Provider = Postgres`.

Таблиці застосунок створить сам при першому запуску.

### Крок 3. Застосунок (Azure App Service)

Студенти можуть отримати **Azure for Students** (100 $ без картки, за навчальною поштою). Є також безкоштовний тариф **F1**.

1. Створіть **App Service** на https://portal.azure.com: Runtime stack **.NET 10 (LTS)**, OS **Linux**, регіон **West Europe**, тариф **Free F1**.
2. **Settings → Environment variables** — додайте всі змінні з таблиці вище (`Archive__AdminCode`, `Database__Provider`, `ConnectionStrings__Default`, `Storage__*`).
3. Опублікуйте код одним із способів (GitHub не потрібен):
   - **Visual Studio:** правою кнопкою на проєкті → **Publish → Azure → Azure App Service (Linux)** → оберіть створений сервіс → **Publish**.
   - **Командний рядок** ([Azure CLI](https://learn.microsoft.com/cli/azure/install-azure-cli)):
     ```bash
     cd arkhiv-robit/src/ArkhivRobit
     dotnet publish -c Release -o publish
     cd publish && zip -r ../app.zip . && cd ..
     az login
     az webapp deploy --resource-group <група> --name <назва-сервісу> --src-path app.zip --type zip
     ```
4. Відкрийте `https://<назва-сервісу>.azurewebsites.net`.

> **Інші варіанти хостингу.** У проєкті є `Dockerfile`, тому його можна запустити на будь-якому хостингу з Docker:
> Fly.io (`fly launch` і `fly deploy`), Railway (`railway up`), власний VPS (`docker build` і `docker run`).
> Застосунок сам бере порт зі змінної `PORT`, якщо хостинг її передає.

## Як користуватися

1. **Адміністратор** заходить через «Вхід для викладачів» і вводить свій код (`Archive__AdminCode`).
2. У розділі **«Адміністрування»** вводить ПІБ викладача і натискає **«Створити код»**. Код показується **один раз**, його треба передати викладачу.
3. **Викладач** заходить зі своїм кодом (регістр, пробіли й дефіси значення не мають), натискає **«+ Додати роботу»**,
   заповнює групу, рік, ПІБ студента, тему і вибирає файл.
4. **Усі інші** на головній сторінці обирають рік і групу або шукають за прізвищем чи темою та натискають **«Скачати»**.

Якщо викладач загубив код, адміністратор натискає **«Новий код»**: старий код і всі його сесії перестають діяти.
Якщо викладач звільнився, адміністратор натискає **«Вимкнути»**, а його роботи залишаються в архіві.

## Безпека

- Коди зберігаються в базі лише у вигляді хешу SHA-256, самих кодів у базі немає.
- Порівняння кодів виконується за сталий час, тому код не можна вгадати за часом відповіді сервера.
- Кількість спроб входу обмежена: 10 за 5 хвилин з однієї IP-адреси.
- Усі форми захищені від CSRF (антифорджері-токени ASP.NET Core).
- Дозволені лише документи та архіви (`pdf, doc, docx, odt, rtf, ppt, pptx, zip, rar, 7z`), розмір файлу обмежено.
- Файли у сховищі приватні. Для скачування видається тимчасове посилання, яке діє 10 хвилин.
- Ключі шифрування cookie зберігаються в базі, тому вхід не злітає після перезапуску сервера.
