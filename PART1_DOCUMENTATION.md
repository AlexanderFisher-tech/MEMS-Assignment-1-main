# COMP4977 Assignment 1 — Part 1: ASP.NET Core Web API

**Municipal Elections Companion API** — the backend for the SwiftUI app (Part 2).

| Item | Value |
| --- | --- |
| Project | `src/Mems.Api` (ASP.NET Core Web API, .NET 10) |
| Tests | `tests/Mems.Api.Tests` (xUnit integration tests, real HTTP pipeline + SQLite) |
| Data | EF Core 10 Code-First, SQLite, ASP.NET Core Identity |
| Auth | JWT Bearer (HMAC-SHA256), 7-day tokens |
| Docs UI | Swagger at `/swagger` (the site root `/` redirects there) |
| Azure API URL | `https://<your-app-name>.azurewebsites.net` ← fill in after deploying (section 5) |
| GitHub repository | `https://github.com/AlexanderFisher-tech/MEMS-Assignment-1-main` |

### Why a separate project?

The COMP4976 MVC site (`src/Mems.Web`) is already deployed to Azure by `.github/workflows/main_mems-civic-guide-bcit.yml` and uses cookie auth, anti-forgery tokens and admin areas. Adding a stateless JWT API to it would have meant mixing two auth schemes and risking that deployment. `src/Mems.Api` is a clean, standalone Web API that **reuses** the MVC project's entity design, the 24 fictional candidates and biographies (`Data/DemoCandidates.cs`), and the candidate portraits (`wwwroot/images/demo-candidates`). It deploys as one App Service web app on its own and doesn't touch the MVC site.

Differences from the MVC data, which follow the COMP4977 spec:

- "Councilor" is spelled **Councillor**.
- Province is `BC`.
- `Candidate.Profile` is renamed **`Biography`**.
- Image file names become **`PhotoUrl` / `LogoUrl`**.
- `Candidate` gets the spec's `MunicipalityId` column.
- New PNG municipality logos are in `wwwroot/images/logos`. These are simple original crests, not official logos. They are PNG because SwiftUI's `AsyncImage` can't render SVG.

---

## 1. Architecture

```mermaid
flowchart LR
    subgraph iOS["iPhone — Municipal Elections Companion (SwiftUI, MVVM)"]
        V[Views<br/>Login · Register · Municipalities<br/>Directory · Details · Voting Guide · Profile]
        VM[ViewModels<br/>@Observable / async-await]
        S[APIService<br/>URLSession + Codable]
        K[(Keychain<br/>JWT token)]
        V --> VM --> S
        S <--> K
    end

    subgraph Azure["Azure App Service (Linux, .NET 10)"]
        direction TB
        MW[Middleware<br/>ForwardedHeaders · StaticFiles · CORS<br/>JWT Authentication · Authorization]
        subgraph Controllers
            AC[AuthController<br/>/api/auth]
            BC[Municipalities / Positions /<br/>Candidates controllers]
            SC[SavedCandidatesController<br/>/api/savedcandidates 🔒]
        end
        SV[Services<br/>TokenService · DtoMapper]
        ID[ASP.NET Core Identity<br/>UserManager]
        EF[EF Core ApiDbContext<br/>auto-migrate + seed on startup]
        DB[(SQLite<br/>/home/data/mems-api/mems-api.db)]
        IMG[/wwwroot/images<br/>portraits + logos/]
        SW[Swagger UI /swagger]
        MW --> Controllers
        AC --> ID --> EF
        AC --> SV
        BC --> SV
        SC --> SV
        BC --> EF
        SC --> EF
        EF --> DB
    end

    S -- "HTTPS JSON (camelCase)<br/>Authorization: Bearer &lt;jwt&gt;" --> MW
    V -- "AsyncImage loads absolute photoUrl / logoUrl" --> IMG
```

### Login and voting-guide flow

```mermaid
sequenceDiagram
    participant App as iOS App
    participant API as MEMS API
    participant DB as SQLite
    App->>API: POST /api/auth/register {name,email,password}
    API->>DB: Identity creates AspNetUsers row (hashed password, Name)
    API-->>App: 200 {"message":"Registration successful"}
    App->>API: POST /api/auth/login {email,password}
    API->>DB: verify password hash
    API-->>App: 200 {token, name, email}
    Note over App: store token in Keychain
    App->>API: POST /api/savedcandidates {candidateId}<br/>Authorization: Bearer token
    API->>API: validate signature, issuer, audience, expiry → userId = sub claim
    API->>DB: INSERT SavedCandidates (UserId, CandidateId, DateSaved)
    API-->>App: 201 {savedCandidateId, ..., candidate:{...}}
```

### Request pipeline (Program.cs)

1. **ForwardedHeaders** — App Service terminates TLS, so the app trusts `X-Forwarded-Proto`. That makes generated image URLs `https://…`, which iOS App Transport Security requires.
2. **Exception handler** — returns `{ "message": "An unexpected server error occurred." }` with status 500.
3. **Static files** — `/images/demo-candidates/*.jpg` and `/images/logos/*.png`.
4. **Swagger** and **Swagger UI**.
5. **CORS** — `AllowAnyOrigin`, `AllowAnyMethod`, `AllowAnyHeader`.
6. **Authentication** (JWT Bearer) and **Authorization**.
7. **Controllers**.
8. **Startup**: `Database.MigrateAsync()` and then the idempotent seeder run before the first request. A brand-new App Service needs no manual `dotnet ef` commands.

---

## 2. Database ERD

```mermaid
erDiagram
    AspNetUsers ||--o{ SavedCandidates : "saves"
    Municipalities ||--|{ Positions : "has"
    Municipalities ||--o{ Candidates : "in"
    Positions ||--o{ Candidates : "running for"
    Candidates ||--o{ SavedCandidates : "saved as"

    AspNetUsers {
        string Id PK "GUID (Identity User)"
        string Name "display name from registration"
        string Email UK
        string UserName "same as Email"
        string PasswordHash "never returned by the API"
        string SecurityStamp
    }
    Municipalities {
        int MunicipalityId PK
        string Name UK
        string Province
        datetime ElectionDate
        string Description
        string LogoUrl
    }
    Positions {
        int PositionId PK
        string Name "unique per municipality"
        int SeatsAvailable
        int MunicipalityId FK
    }
    Candidates {
        int CandidateId PK
        string Name
        string Biography
        string PhotoUrl
        string Website
        string Email
        int PositionId FK
        int MunicipalityId FK
    }
    SavedCandidates {
        int SavedCandidateId PK
        string UserId FK "unique with CandidateId"
        int CandidateId FK
        datetime DateSaved "UTC"
    }
```

The other Identity tables (`AspNetRoles`, `AspNetUserClaims`, `AspNetUserLogins`, `AspNetUserRoles`, `AspNetUserTokens`, `AspNetRoleClaims`) are created by `IdentityDbContext` but not used by the API.

**Delete rules:**

- Restrict on Municipality → Position → Candidate, so election data can't be deleted while other rows depend on it.
- Cascade on User/Candidate → SavedCandidate, so voting-guide entries disappear with their user or candidate.

**Seed data** (`Data/SeedData.cs`, runs only when the database is empty):

- **4 municipalities:** Pitt Meadows, Maple Ridge, Burnaby, Surrey. Each has `Province = "BC"`, `ElectionDate = 2026-10-17`, and a PNG logo.
- **12 positions:** each municipality has Mayor (1 seat), Councillor (6 seats) and School Trustee (2 seats).
- **24 fictional candidates:** two per position. Each has an 80–150 word biography, a portrait, a website (`https://example.org/candidates/<key>`) and an email (`first.last@example.org`).

---

## 3. API endpoints

All responses are JSON in camelCase. Every error, including validation failures, has the shape `{ "message": "…" }`. Image URLs are absolute and built from the request: `{scheme}://{host}/images/...`. Set the `App__PublicBaseUrl` setting to force a particular host.

| Method | Route | Auth | Success | Errors |
| --- | --- | --- | --- | --- |
| POST | `/api/auth/register` | public | 200 `{message}` | 400 `{message}` |
| POST | `/api/auth/login` | public | 200 `{token,name,email}` | 400, 401 `{message}` |
| GET | `/api/municipalities` | public | 200 `MunicipalityDto[]` | |
| GET | `/api/municipalities/{id}` | public | 200 `MunicipalityDto` | 404 |
| GET | `/api/positions[?municipalityId=]` | public | 200 `PositionDto[]` | |
| GET | `/api/positions/{id}` | public | 200 `PositionDto` | 404 |
| GET | `/api/candidates[?municipalityId=&positionId=]` | public | 200 `CandidateDto[]` | |
| GET | `/api/candidates/{id}` | public | 200 `CandidateDto` | 404 |
| GET | `/api/candidates/search?name={q}[&municipalityId=&positionId=]` | public | 200 `CandidateDto[]` | |
| GET | `/api/savedcandidates` | 🔒 Bearer | 200 `SavedCandidateDto[]` (newest first) | 401 |
| GET | `/api/savedcandidates/{id}` | 🔒 Bearer | 200 `SavedCandidateDto` | 401, 404 |
| POST | `/api/savedcandidates` | 🔒 Bearer | 201 `SavedCandidateDto` | 400, 401, 404, 409 |
| DELETE | `/api/savedcandidates/{id}` | 🔒 Bearer | 204 No Content | 401, 404 |

Notes:

- `search` does a case-insensitive partial match on the candidate name. If `name` is missing or empty, it returns every candidate.
- `municipalityId` and `positionId` are optional extra filters for the iOS directory screen.
- `DELETE /api/savedcandidates/{id}` first looks for one of the user's entries whose **SavedCandidateId** is `id`. If there isn't one, it looks for an entry whose **CandidateId** is `id`. That way the app can remove an entry using either ID.
- The saved-candidate endpoints only ever read or change the signed-in user's entries. The user ID comes from the token's `sub` claim.

### Sample requests and responses

These were captured from a local run. On Azure, the host part of the URLs becomes `https://<app>.azurewebsites.net`.

**`POST /api/auth/register`**

```json
// request
{ "name": "Jane Doe", "email": "jane@example.com", "password": "password123" }
// 200
{ "message": "Registration successful" }
// 400 (duplicate)
{ "message": "An account with this email already exists." }
// 400 (validation)
{ "message": "Enter a valid email address. Password must be at least 6 characters." }
```

**`POST /api/auth/login`**

```json
// request
{ "email": "jane@example.com", "password": "password123" }
// 200
{ "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJhdWQiOiJNZW1zSW9zQXBwIi…", "name": "Jane Doe", "email": "jane@example.com" }
// 401
{ "message": "Invalid email or password" }
```

The token's claims are `sub` (user ID), `email`, `name`, `jti`, `iss` (`MemsApi`), `aud` (`MemsIosApp`) and `exp` (7 days after login).

**`GET /api/municipalities`** (single item shown)

```json
[
  {
    "municipalityId": 3,
    "name": "Burnaby",
    "province": "BC",
    "electionDate": "2026-10-17T00:00:00",
    "description": "A large, diverse city at the centre of Metro Vancouver with busy town centres, parks, and post-secondary campuses. Sample election data for a student application.",
    "logoUrl": "https://localhost:5090/images/logos/burnaby.png"
  }
]
```

**`GET /api/positions/2`**

```json
{ "positionId": 2, "name": "Councillor", "seatsAvailable": 6, "municipalityId": 1, "municipalityName": "Pitt Meadows" }
```

**`GET /api/candidates/1`**

```json
{
  "candidateId": 1,
  "name": "Mara Ellison",
  "biography": "Mara Ellison is a fictional community planning consultant who has spent her imagined career …",
  "photoUrl": "https://localhost:5090/images/demo-candidates/demo-mara-ellison.jpg",
  "website": "https://example.org/candidates/mara-ellison",
  "email": "mara.ellison@example.org",
  "positionId": 1,
  "positionName": "Mayor",
  "municipalityId": 1,
  "municipalityName": "Pitt Meadows"
}
```

**`GET /api/candidates/search?name=AN`** returns an array of `CandidateDto`:

`Adrian Solano, Vivian Kessler, Mei Tanaka, Daniel Hwang, Priya Nand, Julian Mensah, Hassan Beaumont`

**`POST /api/savedcandidates`** (header `Authorization: Bearer <token>`)

```json
// request
{ "candidateId": 1 }
// 201 Created (Location: /api/savedcandidates/1)
{
  "savedCandidateId": 1,
  "userId": "6acded42-d8a2-4e18-bb30-2f499dee5df6",
  "candidateId": 1,
  "dateSaved": "2026-10-24T07:04:56Z",
  "candidate": {
    "candidateId": 1, "name": "Mara Ellison", "biography": "…",
    "photoUrl": "https://localhost:5090/images/demo-candidates/demo-mara-ellison.jpg",
    "website": "https://example.org/candidates/mara-ellison", "email": "mara.ellison@example.org",
    "positionId": 1, "positionName": "Mayor", "municipalityId": 1, "municipalityName": "Pitt Meadows"
  }
}
// 409 (already saved)
{ "message": "Candidate already in voting guide" }
// 404 (no such candidate)
{ "message": "Candidate not found" }
```

**`GET /api/savedcandidates`** returns an array of the objects above, newest first.

**`DELETE /api/savedcandidates/{id}`** returns `204 No Content`, or `404 {"message":"Saved candidate not found"}`.

**Any 🔒 endpoint called without a valid token**

```
HTTP/1.1 401 Unauthorized
WWW-Authenticate: Bearer
{ "message": "Authentication required. Send a valid bearer token." }
```

### Notes for the SwiftUI client (Part 2)

- Send `Authorization: Bearer \(token)` and `Content-Type: application/json` on protected calls.
- `dateSaved` is ISO 8601 UTC with whole seconds (`2026-10-24T07:04:56Z`), so `.iso8601` decodes it. `electionDate` follows the spec format `2026-10-17T00:00:00`, which has no time zone. Use a decoding strategy that accepts both formats:
  ```swift
  decoder.dateDecodingStrategy = .custom { d in
      let s = try d.singleValueContainer().decode(String.self)
      if let date = ISO8601DateFormatter().date(from: s) { return date }
      let f = DateFormatter(); f.locale = Locale(identifier: "en_US_POSIX"); f.dateFormat = "yyyy-MM-dd'T'HH:mm:ss"
      if let date = f.date(from: s) { return date }
      throw DecodingError.dataCorruptedError(in: try d.singleValueContainer(), debugDescription: "Bad date \(s)")
  }
  ```
- `photoUrl` and `logoUrl` are absolute `https` URLs on Azure, so `AsyncImage(url: URL(string: c.photoUrl ?? ""))` works directly.
- Treat any non-2xx response as an error and show the `message` field to the user.
- For the simulator against a local API, use `http://localhost:5080` and add an ATS exception for `localhost`. The deployed Azure URL is HTTPS-only.

---

## 4. Running and testing locally

```bash
# .NET 10 SDK required
dotnet build MEMS.sln
dotnet test MEMS.sln                                     # 10 API tests + 21 existing MVC tests
dotnet run --project src/Mems.Api --launch-profile http  # http://localhost:5080/swagger
```

On first run the API creates `src/Mems.Api/App_Data/mems-api.db`. Git ignores this file. Delete it to reset the data.

### Configuration

Every setting can be overridden with an environment variable or an App Service app setting. Use `__` in place of `:`.

| Setting | Default | Purpose |
| --- | --- | --- |
| `Jwt__Key` | development key in `appsettings.json` (≥ 32 bytes) | HMAC signing key. **Override it in Azure.** The deploy script generates a random one. |
| `Jwt__Issuer` / `Jwt__Audience` | `MemsApi` / `MemsIosApp` | token validation |
| `Jwt__ExpiryMinutes` | `10080` (7 days) | token lifetime |
| `ConnectionStrings__DefaultConnection` | (computed) | full SQLite connection string override |
| `Storage__Root` | `App_Data` locally, `$HOME/data/mems-api` on App Service | folder that holds the SQLite file |
| `App__PublicBaseUrl` | (request host) | force the host used in image URLs, e.g. a custom domain |

On App Service, the database is kept under `/home/data/mems-api`. `/home` is App Service's persistent storage, so the database survives restarts and redeployments. It is not inside `/home/site/wwwroot`, which each zip deploy replaces.

### Automated tests (`tests/Mems.Api.Tests/ApiTests.cs`)

| Test | Covers |
| --- | --- |
| `Municipalities_return_seeded_data_with_absolute_logo_urls` | exact JSON keys, Pitt Meadows/Maple Ridge/Burnaby seeded, `province`/`electionDate` format, absolute `logoUrl` downloads as `image/png`, 404 shape |
| `Positions_include_required_offices_and_municipality_names` | Mayor/Councillor/School Trustee, DTO keys, by-id and 404 |
| `Candidates_list_details_and_photos_are_public` | at least 10 candidates, DTO keys, biography, absolute `photoUrl` downloads as `image/jpeg`, by-id/404, municipality filter |
| `Candidate_search_is_case_insensitive_partial_and_empty_returns_all` | `ELLI` matches "Mara Ellison", empty or missing `name` returns all, no-match returns `[]` |
| `Register_validates_input_and_rejects_duplicates` | 6-character password accepted, duplicate email (any case) rejected, short password, bad email, missing name → 400 `{message}` |
| `Login_returns_token_name_and_email_or_401` | `{token,name,email}`, JWT issuer/audience/claims/expiry, wrong password or unknown user → 401 `{message}` |
| `Saved_candidates_require_a_valid_jwt` | no token, malformed token and token forged with another key → 401 on GET/POST/DELETE; a valid token → 200 |
| `Saved_candidates_crud_is_scoped_to_the_signed_in_user` | 201 response shape, 409 duplicate, 404 unknown candidate, 400 empty body, per-user isolation, delete by SavedCandidateId and by CandidateId fallback |
| `Swagger_cors_and_root_redirect_are_configured` | swagger.json lists the routes, Swagger UI loads, `/` redirects to `/swagger`, CORS preflight returns `*` |
| `Forwarded_https_scheme_is_used_for_image_urls` | simulates the App Service TLS proxy (`X-Forwarded-Proto: https`) |

**Verification done before submission:**

- `dotnet test MEMS.sln`: 31/31 passed (10 API, 21 MVC), with no build warnings.
- The Release `publish` output was started in **Production** mode against an empty storage folder, with simulated App Service variables (`WEBSITE_SITE_NAME`, `HOME`). It created `$HOME/data/mems-api/mems-api.db`, applied the migration and seeded the data by itself.
- Every endpoint above was exercised with `curl`.
- After a restart, no migration was re-applied, there were still 24 candidates, and the registered user could still log in.

---

## 5. Deploying to Azure App Service

You need the [Azure CLI](https://aka.ms/installazurecli) (`brew install azure-cli` on macOS) and the .NET 10 SDK.

### Option A — one command (recommended)

```bash
az login                                    # opens the browser; choose your Azure for Students subscription
./scripts/deploy-api-azure.sh mems-api-<yourteam>
```

The script:

1. Runs the API tests.
2. Publishes a Release build and zips it.
3. Creates or reuses the resource group `mems-api-rg`, a **Linux B1** plan in `canadacentral`, and the web app with runtime `DOTNETCORE:10.0`.
4. Turns on HTTPS-only and Always On.
5. Sets `ASPNETCORE_ENVIRONMENT=Production` and generates a random `Jwt__Key`. It generates the key only once, so redeploying doesn't log users out.
6. Zip-deploys the build and waits until `/api/municipalities` responds.

At the end it prints the API URL and the Swagger URL.

Optional overrides: `SKU=F1` (free, but sleeps when idle), `LOCATION=westus2`, `RESOURCE_GROUP=…`, `PLAN=…`.
Example: `SKU=F1 ./scripts/deploy-api-azure.sh mems-api-<yourteam>`.

To redeploy after code changes, run the same command again.

### Option B — the same steps by hand

These commands work in bash/zsh. In PowerShell, replace the trailing `\` with a backtick.

```bash
APP=mems-api-<yourteam>; RG=mems-api-rg; PLAN=mems-api-plan; LOC=canadacentral

az login
az group create -n $RG -l $LOC
az appservice plan create -n $PLAN -g $RG -l $LOC --is-linux --sku B1
az webapp create -n $APP -g $RG -p $PLAN --runtime "DOTNETCORE:10.0"
az webapp update -n $APP -g $RG --https-only true
az webapp config set -n $APP -g $RG --always-on true
az webapp config appsettings set -n $APP -g $RG --settings \
    ASPNETCORE_ENVIRONMENT=Production \
    SCM_DO_BUILD_DURING_DEPLOYMENT=false \
    WEBSITES_ENABLE_APP_SERVICE_STORAGE=true \
    Jwt__Key="$(openssl rand -base64 48)"

dotnet publish src/Mems.Api/Mems.Api.csproj -c Release -o artifacts/api-publish
(cd artifacts/api-publish && zip -r ../mems-api.zip .)
az webapp deploy -n $APP -g $RG --src-path artifacts/mems-api.zip --type zip
```

If `az webapp create` says `DOTNETCORE:10.0` isn't available, run `az webapp list-runtimes --os linux` and use the .NET 10 entry it lists.

There is no database step. On its first request after deploying, the app creates the SQLite database under `/home/data/mems-api` and seeds it.

### Verify the deployment (and take the assignment screenshots)

```bash
URL=https://$APP.azurewebsites.net
curl $URL/api/municipalities
curl "$URL/api/candidates/search?name=mara"
curl -X POST $URL/api/auth/register -H "Content-Type: application/json" \
     -d '{"name":"Jane Doe","email":"jane@example.com","password":"password123"}'
TOKEN=$(curl -s -X POST $URL/api/auth/login -H "Content-Type: application/json" \
     -d '{"email":"jane@example.com","password":"password123"}' | python3 -c 'import json,sys;print(json.load(sys.stdin)["token"])')
curl -X POST $URL/api/savedcandidates -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" -d '{"candidateId":1}'
curl $URL/api/savedcandidates -H "Authorization: Bearer $TOKEN"
open $URL/swagger            # Click "Authorize", paste the token, try the endpoints
```

For the "Azure deployment screenshot" deliverable, capture:

- the web app's **Overview** blade in the Azure Portal, showing the default domain and the Running status;
- `$URL/swagger` open in a browser.

### Troubleshooting

- **Logs:** `az webapp log config -n $APP -g $RG --docker-container-logging filesystem`, then `az webapp log tail -n $APP -g $RG`.
- **First request is slow:** this is normal. The first request after a deploy or restart runs the migration and seeding. Free F1 apps also sleep when idle.
- **Reset the data:** open the SSH console (Portal → *SSH*), run `rm /home/data/mems-api/mems-api.db`, then restart the app.
- **Keep the instance count at 1:** SQLite is a single file, so don't scale out.
- **Remove everything when the course is over:** `az group delete -n mems-api-rg`.

### Other deployment notes

- The existing GitHub Actions workflow still deploys only the COMP4976 MVC site (`src/Mems.Web`). It builds the whole solution, so it also compiles the API, but it doesn't deploy it.
- The API is always deployed with the script or commands above.
