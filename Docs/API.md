# API Documentation

Base URL:

```text
https://visnet-quest-task-api.vercel.app
```

## POST `/api/login`

Request:

```json
{ "username": "testuser", "password": "123456" }
```

Success:

```json
{
  "success": true,
  "token": "jwt_token_here",
  "user": { "id": 1, "name": "Test User" }
}
```

Failure:

```json
{ "success": false, "error": "Invalid Credentials" }
```

## GET `/api/projects`

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

## GET `/api/projects/{id}/floors`

```json
{ "floors": ["Floor 1", "Floor 2", "Floor 3", "Floor 4"] }
```
