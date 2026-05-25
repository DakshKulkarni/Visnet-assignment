# ViSNET Meta Quest XR Development Task

Unity XR app for the assignment flow:

1. Login
2. Project listing
3. Dynamic floor dropdown
4. Selected project/floor summary panel
5. Back navigation and XR world-space toast notifications

The runtime UI is created by `Assets/Scripts/UI/ViSNETQuestApp.cs`. API integration is isolated in `Assets/Scripts/API/ViSNETApiClient.cs`.

## Test Credentials

- Username: `testuser`
- Password: `123456`

## Setup Instructions

### Unity Setup

1. Install Unity `2022.3.62f2` or a compatible Unity 2022.3 LTS version.
2. Open this folder as a Unity project.
3. Let Unity import packages and scripts completely.
4. Open `Assets/Scenes/SampleScene.unity`.
5. Confirm the API config exists at `Assets/Resources/ViSNETApiConfig.json`:

```json
{ "apiBaseUrl": "https://visnet-quest-task-api.vercel.app" }
```

6. Press Play in the editor or build to Quest.

### Quest Build Setup

1. In Unity, switch platform to Android.
2. Use the existing XR setup from `Packages/manifest.json` and `ProjectSettings`.
3. Build for Meta Quest 2/3/Pro.
4. The Android internet permission is already included in `Assets/Plugins/Android/AndroidManifest.xml`.
5. Oculus system keyboard support is enabled in `Assets/Oculus/OculusProjectConfig.asset`.

### Backend Local Setup

```bash
cd Backend
npm install
npm run dev
```

For local testing, set `Assets/Resources/ViSNETApiConfig.json` to the local Vercel dev URL, for example:

```json
{ "apiBaseUrl": "http://localhost:3000" }
```

Set it back to production before Quest build if the headset should call the deployed API:

```json
{ "apiBaseUrl": "https://visnet-quest-task-api.vercel.app" }
```

### Backend Deployment

The backend is Vercel-compatible and lives in `Backend/`.

```bash
cd Backend
vercel
```

Production API:

```text
https://visnet-quest-task-api.vercel.app
```

## API Documentation

Base URL:

```text
https://visnet-quest-task-api.vercel.app
```

All responses are JSON. CORS is enabled for `GET`, `POST`, and `OPTIONS`.

### POST `/api/login`

Authenticates the user.

Request body:

```json
{
  "username": "testuser",
  "password": "123456"
}
```

Success response, `200`:

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

Invalid credentials response, `401`:

```json
{
  "success": false,
  "error": "Invalid Credentials"
}
```

Example:

```bash
curl -X POST https://visnet-quest-task-api.vercel.app/api/login \
  -H "Content-Type: application/json" \
  -d '{"username":"testuser","password":"123456"}'
```

### GET `/api/projects`

Returns the available inspection projects.

Success response, `200`:

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

Example:

```bash
curl https://visnet-quest-task-api.vercel.app/api/projects
```

### GET `/api/projects/{id}/floors`

Returns floors for a selected project.

Path parameters:

- `id`: project ID from `/api/projects`

Success response, `200`:

```json
{
  "floors": ["Floor 1", "Floor 2", "Floor 3", "Floor 4"]
}
```

Known project floor data:

- Project `1`: `Floor 1`, `Floor 2`, `Floor 3`, `Floor 4`
- Project `2`: `Floor A`, `Floor B`
- Project `3`: `Ground`, `1`, `2`, `3`, `4`, `5`
- Project `4`: `Basement`, `Ground`, `Mezzanine`

Example:

```bash
curl https://visnet-quest-task-api.vercel.app/api/projects/1/floors
```

### Error Responses

Unsupported methods return:

```json
{
  "error": "Method not allowed"
}
```

The Unity client converts login failures into the user-facing message:

```text
Invalid username or password
```

## Notes

- If no API URL is configured, the app can use an embedded mock API with the same response shape.
- Runtime UI, ray interaction, and keyboard handling are created by `ViSNETQuestApp`.
- API base URL is read from `Assets/Resources/ViSNETApiConfig.json`.
