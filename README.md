# Online Thumbnail Generator (.NET API + React Frontend)

Modern full-stack web application for image upload to GCS, 100x100 thumbnailing, listing, download links, and an Eventarc-triggered compression job. Deployable to Cloud Run with Cloud SQL (PostgreSQL), Secret Manager, and SSL-only DB connections.

## 🏗️ Architecture

- **Backend**: ASP.NET Core Web API (.NET 8)
- **Frontend**: React 18 + TypeScript
- **Database**: Cloud SQL PostgreSQL (SSL-only)
- **Storage**: Google Cloud Storage (GCS)
- **Authentication**: Cookie-based sessions
- **Encryption**: AES encryption for passwords
- **Deployment**: Cloud Run + Docker

## 🚀 Quick Start

### Prerequisites
- .NET 8 SDK
- Node.js 18+
- GCP project with Cloud SQL, GCS, Secret Manager

### Development Setup

1. **Clone and setup**:
   ```bash
   git clone <repo>
   cd thumbnaildotnet
   ```

2. **Start both servers** (API + React):
   ```bash
   ./start-dev.sh
   ```
   
   Or start manually:
   ```bash
   # Terminal 1 - API
   cd ThumbnailService
   export AES_KEY_BASE64=$(openssl rand -base64 32)
   export AES_IV_BASE64=$(openssl rand -base64 16)
   dotnet run --urls="http://localhost:5001"
   
   # Terminal 2 - React
   cd frontend
   npm start
   ```

3. **Access the application**:
   - Frontend: http://localhost:3000
   - API: http://localhost:5001
   - Swagger: http://localhost:5001/swagger

## 🔧 Configuration

### API Configuration (`ThumbnailService/appsettings.json`)
```json
{
  "ConnectionStrings": {
    "CloudSqlPostgres": "Host=YOUR_CLOUDSQL_HOST;Port=5432;Database=thumbnaildb;Username=appuser;Password=CHANGE_ME;Ssl Mode=Require;Trust Server Certificate=false;Root Certificate=/secrets/sql/ca.pem;Client Certificate=/secrets/sql/client-cert.pem;Client Key=/secrets/sql/client-key.pem;"
  },
  "Gcp": {
    "OriginalsBucket": "your-originals-bucket",
    "ThumbnailsBucket": "your-thumbnails-bucket",
    "CompressedPrefix": "compressed/"
  },
  "Security": {
    "AesKeySecretName": "projects/PROJECT_ID/secrets/AES_KEY/versions/latest",
    "AesIvSecretName": "projects/PROJECT_ID/secrets/AES_IV/versions/latest"
  }
}
```

### Frontend Configuration (`frontend/src/api.ts`)
```typescript
const API_BASE_URL = process.env.REACT_APP_API_URL || 'http://localhost:5001';
```

## 🌐 API Endpoints

### Authentication
- `POST /api/Account/register` - Register new user
- `POST /api/Account/login` - Login user
- `POST /api/Account/logout` - Logout user
- `GET /api/Account/me` - Get current user info

### Images
- `GET /api/Image` - List user's images
- `POST /api/Upload` - Upload image (multipart/form-data)
- `GET /api/Image/{id}/thumbnail` - Get thumbnail download URL

### Compression Job
- `POST /api/Compression/compress` - Trigger compression job (Eventarc)

## 🐳 Docker Deployment

### Build API Image
```bash
cd ThumbnailService
docker build -t gcr.io/PROJECT/thumbnailservice .
```

### Deploy to Cloud Run
```bash
gcloud run deploy thumbnailservice \
  --image gcr.io/PROJECT/thumbnailservice \
  --platform managed \
  --region us-central1 \
  --allow-unauthenticated \
  --set-env-vars ASPNETCORE_ENVIRONMENT=Production
```

## 🔐 GCP Setup

### Cloud SQL (PostgreSQL)
1. Create instance with SSL-only connections
2. Create database `thumbnaildb` and user `appuser`
3. Generate client certificates:
   ```bash
   gcloud sql ssl client-certs create app-cert
   ```
4. Mount certificates to Cloud Run at `/secrets/sql/`

### Google Cloud Storage
1. Create two buckets: `originals` and `thumbnails`
2. Make thumbnails bucket public or use signed URLs
3. Grant service account `Storage Object Admin` role

### Secret Manager
1. Create secrets for AES key/IV (base64 encoded):
   ```bash
   echo -n "$(openssl rand -base64 32)" | gcloud secrets create AES_KEY --data-file=-
   echo -n "$(openssl rand -base64 16)" | gcloud secrets create AES_IV --data-file=-
   ```
2. Grant service account `Secret Manager Secret Accessor` role

### Cloud Scheduler + Eventarc
```bash
# Create scheduler job
gcloud scheduler jobs create http compress-images \
  --schedule="0 * * * *" \
  --http-method=POST \
  --uri=$CLOUD_RUN_URL/api/Compression/compress \
  --oidc-service-account-email=SA@PROJECT.iam.gserviceaccount.com
```

## 🧪 Testing

### API Testing
```bash
# Health check
curl http://localhost:5001/api/Account/healthcheck

# Register
curl -X POST http://localhost:5001/api/Account/register \
  -H "Content-Type: application/json" \
  -d '{"email":"test@example.com","password":"password123"}'

# Login
curl -X POST http://localhost:5001/api/Account/login \
  -H "Content-Type: application/json" \
  -d '{"email":"test@example.com","password":"password123"}' \
  -c cookies.txt

# Upload (with cookie)
curl -X POST http://localhost:5001/api/Upload \
  -b cookies.txt \
  -F "file=@test-image.jpg"
```

### Frontend Testing
- Open http://localhost:3000
- Register/login with test credentials
- Upload PNG/JPG images
- View image list with thumbnails

## 📁 Project Structure

```
thumbnaildotnet/
├── ThumbnailService/           # .NET Web API
│   ├── Controllers/            # API controllers
│   ├── Models/                 # EF Core models
│   ├── Services/               # Business logic
│   ├── Migrations/             # Database migrations
│   └── Dockerfile             # Container config
├── frontend/                   # React TypeScript app
│   ├── src/
│   │   ├── api.ts             # API client
│   │   ├── AuthContext.tsx    # Auth state management
│   │   ├── Login.tsx          # Login/register component
│   │   ├── ImageUploader.tsx  # Upload component
│   │   ├── ImageList.tsx      # Image list component
│   │   └── App.tsx            # Main app component
│   └── package.json
├── start-dev.sh               # Development startup script
└── README.md
```

## 🔒 Security Features

- **Password Encryption**: AES encryption (not hashing per requirements)
- **SSL-Only Database**: Cloud SQL enforces SSL connections
- **Secret Management**: GCP Secret Manager for encryption keys
- **CORS Configuration**: Configured for React frontend
- **File Validation**: PNG/JPG only, size limits

## 🚀 Production Deployment

1. **Build and push container**:
   ```bash
   docker build -t gcr.io/PROJECT/thumbnailservice ./ThumbnailService
   docker push gcr.io/PROJECT/thumbnailservice
   ```

2. **Deploy to Cloud Run**:
   ```bash
   gcloud run deploy thumbnailservice \
     --image gcr.io/PROJECT/thumbnailservice \
     --platform managed \
     --region us-central1 \
     --allow-unauthenticated
   ```

3. **Deploy React frontend**:
   ```bash
   cd frontend
   npm run build
   # Deploy build/ folder to Cloud Storage or CDN
   ```

## 📝 Notes

- Passwords are stored encrypted (AES) per requirements
- In production, consider using salted hashing (PBKDF2/bcrypt)
- Thumbnails are generated synchronously on upload
- Compression job is a stub implementation for Eventarc integration
- All GCP services and configurations are preserved from original MVC version

## 📄 License

MIT