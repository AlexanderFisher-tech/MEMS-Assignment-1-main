# Azure App Service preparation

Deployment and resource creation require the owner's explicit approval. No Azure operations are performed by the setup, packaging, or test scripts.

## Deployment choice

Use one Linux App Service instance with the supplied Dockerfile (.NET 10 runtime and native Linux image codec). A container avoids depending on the site's managed .NET runtime version. Before provisioning, choose the subscription, region, resource group, registry/image name, unique app name, and an approved cost tier. Confirm registry access and the service's image architecture. No production password is stored in the Dockerfile or repository.

Local reviewable build/publish commands:

```powershell
dotnet test MEMS.sln -c Release
dotnet publish src/Mems.Web -c Release -o artifacts/publish
# Optional if Docker is installed; builds locally and does not push:
docker build -f deploy/Dockerfile -t mems:review .
```

Do not run a registry push or az deployment command until approval. After approval, configure the Web App to pull the reviewed image, configure port 8080, enable HTTPS Only, and apply the settings below using the portal or a secure account-scoped deployment process. URLs remain [AZURE_APP_URL] and [GITHUB_REPOSITORY_URL] until actually created.

## App settings (Linux container)

| Setting | Value |
| --- | --- |
| ASPNETCORE_ENVIRONMENT | Production |
| ASPNETCORE_FORWARDEDHEADERS_ENABLED | true (behind the App Service reverse proxy) |
| WEBSITES_PORT | 8080 |
| WEBSITES_ENABLE_APP_SERVICE_STORAGE | true |
| Storage__Root | /home/mems |
| ConnectionStrings__DefaultConnection | Data Source=/home/mems/mems.db;Default Timeout=30;Pooling=False |
| Seed__CreateUsers | true only for the initial approved administrator setup |
| Seed__Password | unique strong secret supplied through App Service settings/Key Vault |

The container defaults to Production. Startup creates the storage directories, applies migrations, and seeds sample domain data/groups. Production refuses the assignment demo password. With explicitly enabled Seed__CreateUsers and a unique Seed__Password, startup creates super and pm if absent, assigning their required roles. Remove Seed__CreateUsers and Seed__Password after successful initial setup; existing users are not deleted or reset. Do not reuse or upload the local Development database. All candidates are fictional demonstration content.

## Persistence and limits

The database, generated uploads, and Data Protection keys live beneath /home/mems. The App Service persistent /home mount must be enabled and writable by the running process. This image runs with the base image's default user to support that mounted directory; further hardening can use a separately permissioned volume and a non-root user.

SQLite is suitable here for a small, single-instance assignment demonstration. Keep instance count at one; do not enable autoscale or share the same database between slots/instances. SQLite file locking and journal behavior on Azure's network-backed persistent storage require a real deployment test; persistent storage alone does not guarantee safe concurrent database access. The app switches to rollback journaling (DELETE), which avoids WAL shared-memory requirements but is not a guarantee for every file share. For a larger or multi-instance service, the mandated SQLite design needs reconsideration. Back up the database and uploads consistently with the app stopped; never copy an actively written SQLite file by itself. Keys must persist for authentication cookies; restrict access to the key directory and use platform/Key Vault encryption in a hardened deployment.

Anonymous guides use process memory and expire after eight idle hours; a restart clears guides. This is deliberate for the single-instance assignment and must be explained during demonstration. Use a distributed cache if persistence across app restarts becomes necessary.

## Acceptance after deployment

Confirm HTTPS, public search/profiles, production admin sign-in, pm isolation, CRUD, logo/photo uploads, and the printable guide. Restart the app and verify database records, images, and administrator sign-in survive. Verify /App_Data and settings files are not served. Inspect logs for migration/locking/native-codec errors. A working Azure URL, GitHub publication, and D2L submission remain unverified until these owner-approved steps occur.

References: [Microsoft App Service persistent storage](https://learn.microsoft.com/en-us/azure/app-service/configure-custom-container#use-persistent-shared-storage), [ASP.NET Core configuration on App Service](https://learn.microsoft.com/en-us/azure/app-service/configure-language-dotnetcore).
