#!/usr/bin/env bash
# Project: Municipal Elections Companion API (MEMS) - COMP4977
# Creates (or reuses) a Linux App Service and deploys src/Mems.Api to it in one command.
#
#   az login
#   ./scripts/deploy-api-azure.sh <globally-unique-app-name>
#
# Optional overrides: RESOURCE_GROUP, LOCATION, PLAN, SKU (B1 default; F1 is free but sleeps when idle).
# Safe to re-run: existing resources are reused and the JWT signing key is only generated once.
set -euo pipefail

APP_NAME="${1:-${APP_NAME:-}}"
if [[ -z "$APP_NAME" ]]; then
  echo "Usage: $0 <globally-unique-app-name>   (e.g. mems-api-yourteam)" >&2
  exit 1
fi
RESOURCE_GROUP="${RESOURCE_GROUP:-mems-api-rg}"
LOCATION="${LOCATION:-canadacentral}"
PLAN="${PLAN:-mems-api-plan}"
SKU="${SKU:-B1}"
RUNTIME="DOTNETCORE:10.0"
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
OUT="$ROOT/artifacts/api-publish"
ZIP="$ROOT/artifacts/mems-api.zip"

command -v az >/dev/null || { echo "Install the Azure CLI first: https://aka.ms/installazurecli" >&2; exit 1; }
command -v dotnet >/dev/null || { echo "Install the .NET 10 SDK first: https://dot.net" >&2; exit 1; }
az account show >/dev/null 2>&1 || { echo "Run 'az login' first." >&2; exit 1; }

echo "==> Testing and publishing src/Mems.Api (Release)"
dotnet test "$ROOT/tests/Mems.Api.Tests" -c Release
rm -rf "$OUT" "$ZIP"
dotnet publish "$ROOT/src/Mems.Api/Mems.Api.csproj" -c Release -o "$OUT"
(cd "$OUT" && zip -qr "$ZIP" .)

echo "==> Ensuring resource group '$RESOURCE_GROUP' and Linux plan '$PLAN' ($SKU) in $LOCATION"
az group create --name "$RESOURCE_GROUP" --location "$LOCATION" --output none
if ! az appservice plan show --name "$PLAN" --resource-group "$RESOURCE_GROUP" --output none 2>/dev/null; then
  az appservice plan create --name "$PLAN" --resource-group "$RESOURCE_GROUP" --location "$LOCATION" --is-linux --sku "$SKU" --output none
fi

echo "==> Ensuring web app '$APP_NAME' ($RUNTIME)"
if ! az webapp show --name "$APP_NAME" --resource-group "$RESOURCE_GROUP" --output none 2>/dev/null; then
  az webapp create --name "$APP_NAME" --resource-group "$RESOURCE_GROUP" --plan "$PLAN" --runtime "$RUNTIME" --output none
fi
az webapp update --name "$APP_NAME" --resource-group "$RESOURCE_GROUP" --https-only true --output none
if [[ "$SKU" != "F1" ]]; then
  az webapp config set --name "$APP_NAME" --resource-group "$RESOURCE_GROUP" --always-on true --output none
fi

# Keep an existing key so re-deploying does not log out every iOS user.
EXISTING_KEY="$(az webapp config appsettings list --name "$APP_NAME" --resource-group "$RESOURCE_GROUP" --query "[?name=='Jwt__Key'].value | [0]" --output tsv)"
SETTINGS=(ASPNETCORE_ENVIRONMENT=Production SCM_DO_BUILD_DURING_DEPLOYMENT=false WEBSITES_ENABLE_APP_SERVICE_STORAGE=true)
if [[ -z "$EXISTING_KEY" || "$EXISTING_KEY" == "None" ]]; then
  SETTINGS+=("Jwt__Key=$(openssl rand -base64 48)")
fi
az webapp config appsettings set --name "$APP_NAME" --resource-group "$RESOURCE_GROUP" --settings "${SETTINGS[@]}" --output none

echo "==> Deploying package (the app migrates and seeds its SQLite database on startup)"
az webapp deploy --name "$APP_NAME" --resource-group "$RESOURCE_GROUP" --src-path "$ZIP" --type zip --output none

URL="https://$(az webapp show --name "$APP_NAME" --resource-group "$RESOURCE_GROUP" --query defaultHostName --output tsv)"
echo "==> Waiting for $URL/api/municipalities"
for _ in $(seq 1 30); do
  if curl -fsS "$URL/api/municipalities" >/dev/null 2>&1; then
    echo "Deployed: $URL"
    echo "Swagger:  $URL/swagger"
    exit 0
  fi
  sleep 10
done
echo "The app did not respond yet. Check logs with: az webapp log tail --name $APP_NAME --resource-group $RESOURCE_GROUP" >&2
exit 1
