# GatherUp

GatherUp is a backend system for managing events — from creation and invitations through participant RSVP, polls, payments, and vendor management. Built as a final project demonstrating layered architecture and clean API design.

---

## Technologies

| Technology | Usage |
|---|---|
| C# / .NET 8 | Runtime and language |
| ASP.NET Core Web API | HTTP layer |
| JWT Authentication | Stateless auth with Bearer tokens |
| Dependency Injection | Built-in .NET DI container |
| Repository Pattern | Data access abstraction |
| XML-based persistence | Flat-file storage via custom `XmlRepository<T>` |
| Swagger / OpenAPI | Interactive API documentation |

---

## Architecture

The solution is divided into five projects with clear separation of concerns:

```
GatherUpSystem.sln
├── GatherUp.API          # HTTP layer: Controllers, DTOs, Middleware, Swagger config
├── GatherUp.BL           # Business logic: EventService, ParticipantService, FinanceService, PollService
├── GatherUp.Core         # Domain: entities, interfaces (IRepository, IEmailService), custom exceptions
├── GatherUp.Infrastructure  # Data access: XmlRepository, MemoryRepository, FileEmailService, ReceiptRepository
└── GatherUp.Tests        # Integration test harness (console runner, tests against real XML files)
```

Dependency direction:

```
API → BL → Core
API → Infrastructure → Core
```

- API uses BL for business logic.
- API uses Infrastructure directly to register and resolve service/repository implementations.
- BL depends on Core interfaces.
- Infrastructure depends on Core interfaces.

---

## Main Features

- **Authentication** — Register and login as event manager or participant; JWT token returned on success.
- **Event management** — Create, update, delete events; track status (Planning → InvitationsSent → Finalized).
- **Participant management** — Add participants to events, confirm attendance (RSVP), manage mailing preferences.
- **Invitations** — Send email invitations to participants who have not yet responded; send special host invitation.
- **Polls** — Create multi-question polls linked to an event; submit and change votes; view results with percentages.
- **Financial tracking** — Record participant payments, add vendors with debt, upload receipts, view financial summary and balance per event.
- **Notifications** — Email notifications on event changes, attendance confirmation, payment, and more (logged to file).
- **Email log** — Outgoing emails are logged locally at runtime to plain text and structured JSONL files. Email logs are generated locally at runtime and are excluded from Git.
- **Swagger UI** — Full interactive documentation available at `/swagger`.

---

## Running the Project

### Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)

### Configuration

The JWT secret is **not stored in source**. Before running, provide your own secret via one of these methods:

**Option A — User secrets (recommended for local development):**
```bash
cd GatherUp.API
dotnet user-secrets set "Jwt:Key" "YourLocalSecretAtLeast32CharsLong!"
```

**Option B — Environment variable:**
```bash
# Windows PowerShell
$env:Jwt__Key = "YourLocalSecretAtLeast32CharsLong!"
dotnet run --project GatherUp.API
```

### Start the API

```bash
dotnet run --project GatherUp.API
```

The API will start and seed initial XML data automatically if no data files exist yet.

> **Note:** On first run the API creates `XMLData/`, `ReceiptsStorage/`, and `EmailsLog/` folders automatically under the project root.

---

## Testing

The `GatherUp.Tests` project is a console-based integration test harness that runs against real XML files.

```bash
dotnet run --project GatherUp.Tests
```

It executes a full end-to-end workflow:
1. Create an event
2. Create a poll and submit votes (including vote change)
3. Add participants and confirm attendance
4. Record payments
5. Add vendors and upload a receipt
6. Verify email log entries
7. Verify XML persistence on disk

Results are printed to the console with `✓ / ✗` per assertion and a final summary.

---

## Swagger

Once the API is running in Development mode, open:

```
https://localhost:<port>/swagger
```

### Authenticated requests

1. `POST /api/auth/login` with a registered email and password.
2. Copy the `token` value from the response.
3. Click **Authorize 🔒** at the top of the Swagger page.
4. Enter: `Bearer <your_token>`
5. All subsequent requests will include the token automatically.

> **Development note:** The current authentication flow is intended for the project's development/demo environment and should not be considered production-ready password authentication.

A full documented flow (login, create event, manage participants, polls, payments) is available in [`SWAGGER_FLOW.md`](./SWAGGER_FLOW.md).

---

## E2E Test (PowerShell)

`e2e_test.ps1` is a PowerShell script that calls the live API and validates HTTP responses end-to-end.

**Prerequisites:** The API must be running locally before executing the script.

```powershell
# Start the API first, then:
.\e2e_test.ps1
```

The script covers: manager login, event creation, participant management, polls, financial operations, and error scenarios (403, 400 expected responses).

---

## Notes

- XML data files (`XMLData/*.xml`) are included as initial project data and are read and updated at runtime.
- Email logs are generated locally at runtime and are excluded from Git.
- Uploaded receipt files (`ReceiptsStorage/`) are saved locally at runtime and are excluded from Git.
- Build artifacts (`bin/`, `obj/`) are excluded from Git.