# Online Thumbnail Generator (.NET + GCP)

Minimal ASP.NET Core MVC app for image upload to GCS, 100x100 thumbnailing, listing, download links, and an Eventarc-triggered compression job. Deployable to Cloud Run with Cloud SQL (PostgreSQL), Secret Manager, and SSL-only DB connections.

## Local Run
1. Prereqs: .NET 8 SDK.
2. Set AES secrets (base64):
   - `export AES_KEY_BASE64=$(openssl rand -base64 32)`
   - `export AES_IV_BASE64=$(openssl rand -base64 16)`
3. Edit `ThumbnailService/appsettings.json` buckets and connection string placeholders.
4. Run:
   - `cd ThumbnailService`
   - `~/.dotnet/dotnet run`

## Cloud SQL SSL
- Use Npgsql connection string with `Ssl Mode=Require` and mount certs to `/secrets/sql/` in Cloud Run.

## Secret Manager
- Put AES key/iv (base64) in secrets and set names in `appsettings.json` under `Security`.

## Cloud Run
- Build image: `gcloud builds submit --tag gcr.io/PROJECT/thumbnailservice`

`gcloud builds submit --tag asia-southeast1-docker.pkg.dev/hoangassignment/cloud-run-source-deploy/thumbnaildotnet:2.1`

- Deploy: `gcloud run deploy thumbnailservice --image gcr.io/PROJECT/thumbnailservice --region REGION`

`gcloud run deploy thumbnailservice \
  --image asia-southeast1-docker.pkg.dev/hoangassignment/cloud-run-source-deploy/thumbnaildotnet:2.1 \
  --region asia-southeast1 \
  --platform managed \
  --vpc-connector=connector-sql \
  --vpc-egress=all-traffic \
  --service-account=thumbnail-service-account@hoangassignment.iam.gserviceaccount.com \
  --set-env-vars DOTNET_ENVIRONMENT=Production \
  --allow-unauthenticated`

## Scheduler + Eventarc
- Endpoint: `POST /jobs/compress`
- Scheduler (HTTP OIDC) example:
  `gcloud scheduler jobs create http compress-images --schedule="0 * * * *" --http-method=POST --uri=$CLOUD_RUN_URL/jobs/compress --oidc-service-account-email=SA@PROJECT.iam.gserviceaccount.com`

## EF Migrations
- Already added Initial migration. App auto-applies migrations at startup.
