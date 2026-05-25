# ViSNET Quest Task API

Serverless API for the Unity Meta Quest assignment.

Production URL:

```text
https://visnet-quest-task-api.vercel.app
```

## Setup

Install dependencies and run locally:

```bash
npm install
npm run dev
```

Deploy to Vercel from this folder:

```bash
vercel
```

The Unity app reads its base URL from:

```text
Assets/Resources/ViSNETApiConfig.json
```

## API Documentation

All endpoints return JSON and support CORS preflight through `OPTIONS`.

### POST `/api/login`

Request:

```json
{
  "username": "testuser",
  "password": "123456"
}
```

Success, `200`:

```json
{
  "success": true,
  "token": "jwt_token_here",
  "user": {
    "id": 1,
    "name": "Test User"
  }
}
```

Failure, `401`:

```json
{
  "success": false,
  "error": "Invalid Credentials"
}
```

### GET `/api/projects`

Success, `200`:

```json
{
  "projects": [
    { "id": 1, "name": "Project A" },
    { "id": 2, "name": "Project B" },
    { "id": 3, "name": "Project C" },
    { "id": 4, "name": "Project D" }
  ]
}
```

### GET `/api/projects/{id}/floors`

Success, `200`:

```json
{
  "floors": ["Floor 1", "Floor 2", "Floor 3", "Floor 4"]
}
```

Floor data:

- Project `1`: `Floor 1`, `Floor 2`, `Floor 3`, `Floor 4`
- Project `2`: `Floor A`, `Floor B`
- Project `3`: `Ground`, `1`, `2`, `3`, `4`, `5`
- Project `4`: `Basement`, `Ground`, `Mezzanine`

Unsupported methods return `405`:

```json
{
  "error": "Method not allowed"
}
```
