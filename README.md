# RaceDay - Event Management System

RaceDay is a full-stack, API-driven event management platform for the South African road running, walking and cycling community. Event Organisers create and manage events, categories and results, while Participants browse events, enter races and track their personal performance.

This repository is built in three parts. **Part 1** (planning and database) and **Part 2** (RESTful API) and **Part 3** (MVC web application) are complete and documented below.

## User roles

- **Organiser** - can create, edit and delete their own events, manage the categories of those events, capture participant results, and view all enrolments for their events.
- **Participant** - can create an account, browse events and categories, enter an event by selecting a category, view and cancel their own enrolments, pay entry fees, and view their own results.

Role-based access is enforced at the API level. A Participant cannot use Organiser endpoints (403 Forbidden), and an Organiser cannot use Participant endpoints.

## Repository structure

```
/docs                         Part 1 - ERD, endpoint plan and SQL script
/Part2
  /RaceDay.Api                Part 2 - ASP.NET Core Web API (.NET 10)
    /Controllers              One controller per resource
    /Data                     EF Core DbContext (matches the Part 1 ERD)
    /Dtos                     Request/response models with validation
    /Helpers                  Role constants and session helpers
    /Migrations               EF Core migrations
    /Models                   Entity classes
    /Services                 Session authentication handler
  /RaceDay.Tests              xUnit tests (71 tests)
/Part3
  /RaceDay.Web                Part 3 - ASP.NET Core MVC web app (with Dockerfile)
  docker-compose.yml          Starts the web app in Docker with one command
/.github/workflows            CI workflows (Part 1 structure check, build, tests, MVC build and Docker image)
```

---

## Part 1 - System planning and database

The planning documents are in `/docs`: `erd.png`, `endpoint-plan.pdf` and `raceday-schema.sql` (SQL Server script that creates and seeds the database).

![Part 1 CI build passing](docs/ci-success.png)

**Part 1 video:** PASTE_PART1_VIDEO_LINK_HERE

---

## Part 2 - RESTful API

### Technology

ASP.NET Core Web API (.NET 10), Entity Framework Core (Code-First) with SQL Server, server-side sessions, BCrypt password hashing, Swagger (Swashbuckle) and xUnit.

### Prerequisites

- .NET 10 SDK
- SQL Server Express (instance `localhost\SQLEXPRESS`) and SQL Server Management Studio
- Visual Studio 2026 (optional)

### Setup and run instructions

1. Clone the repository:
   ```
   git clone https://github.com/Kamva-Setheni/RaceDay_Repo.git
   cd RaceDay_Repo/Part2/RaceDay.Api
   ```
2. Check the connection string in `Part2/RaceDay.Api/appsettings.json`. The default targets the local SQL Express instance:
   ```
   Server=localhost\SQLEXPRESS;Database=RaceDay;Trusted_Connection=True;TrustServerCertificate=True;
   ```
3. Create the database from the committed migration (install the EF tool once with `dotnet tool install --global dotnet-ef`):
   ```
   dotnet ef database update
   ```
   In Visual Studio you can run `Update-Database` in the Package Manager Console instead.
4. Run the API:
   ```
   dotnet run --launch-profile https
   ```
   If the browser warns about the development certificate, run `dotnet dev-certs https --trust` once.
5. Open Swagger in a browser at `https://localhost:<port>/swagger`. The port is printed in the console as `Now listening on`.
6. Register two users (one Organiser, one Participant), log in, and try the endpoints. Registering as an Organiser needs the organiser sign-up code `RACEDAY-ORG-2026` (set in `appsettings.json`).

### Authentication and sessions

- **Register** hashes the password with BCrypt. The original password is never stored.
- **Login** starts a **server-side session** that stores the user's ID and role. The browser only receives an HttpOnly session cookie (`.RaceDay.Session`).
- A small authentication handler (`SessionAuthenticationHandler`) reads the ID and role from the session on every request, so `[Authorize]` and `[Authorize(Roles = "...")]` protect the endpoints. Swagger UI sends the cookie automatically after login.
- **Logout** clears the session. Without a valid session, protected endpoints return **401 Unauthorized**; the wrong role returns **403 Forbidden**.
- **Organiser sign-up code:** anyone can register as a Participant, but registering as an Organiser needs the sign-up code, so users cannot simply pick the Organiser role.

### API endpoints (27)

| Method | Route | Role |
|---|---|---|
| POST | /api/auth/register | Public |
| POST | /api/auth/login | Public |
| POST | /api/auth/logout | Any logged in |
| GET | /api/users/me | Any logged in |
| PUT | /api/users/me | Any logged in |
| GET | /api/events | Public |
| GET | /api/events/{id} | Public |
| POST | /api/events | Organiser |
| PUT | /api/events/{id} | Organiser (owner) |
| DELETE | /api/events/{id} | Organiser (owner) |
| GET | /api/events/{id}/categories | Public |
| POST | /api/events/{id}/categories | Organiser (owner) |
| PUT | /api/categories/{id} | Organiser (owner) |
| DELETE | /api/categories/{id} | Organiser (owner) |
| POST | /api/categories/{id}/enrol | Participant |
| GET | /api/users/me/enrolments | Participant |
| GET | /api/events/{id}/enrolments | Organiser (owner) |
| DELETE | /api/enrolments/{id} | Participant (owner) |
| POST | /api/enrolments/{id}/results | Organiser (owner) |
| PUT | /api/results/{id} | Organiser (owner) |
| GET | /api/users/me/results | Participant |
| GET | /api/events/{id}/results | Public |
| POST | /api/enrolments/{id}/payment | Participant (owner) |
| GET | /api/users/me/payments | Participant |
| POST | /api/events/{id}/banner | Organiser (owner) |
| POST | /api/users/me/profile-picture | Any logged in |
| GET | /api/events/mine | Organiser |

### Changes from the Part 1 plan

The API follows the Part 1 endpoint plan. These deliberate differences are explained here:

| Area | Part 1 plan | Part 2 implementation | Reason |
|---|---|---|---|
| Event fields | name, description, eventDate, location | Adds `distance` and `eventType` (Run, Walk or Cycle) | The Part 2 brief requires every event to capture distance and type. The `Events` table in `raceday-schema.sql` and the ERD were updated to match. |
| Register | { fullName, email, password, role } | Adds optional `organiserCode`; returns 403 for an Organiser without a valid code | Stops users choosing the Organiser role freely. |
| Login | 200 OK returning a JWT | 200 OK returning the user and starting a server-side session | The brief requires session management. No JWT is used. |
| Logout | Not planned | POST /api/auth/logout | Needed to end a session. |
| GET /api/events | Lists upcoming events | Lists all events, soonest first | Past events and their results stay visible. |
| Extra status codes | - | 409 on duplicate profile email and duplicate result; 400 and 403 on payment | More precise failure responses. |
| Deleting events and categories | Removes related data | Also removes dependent enrolments, results and payments in code | Foreign keys use NO ACTION in the SQL script. |

### Unit tests

71 xUnit tests run the real API in memory with an in-memory database, so authentication, role checks and validation are tested end to end. Both success and failure scenarios are covered.

| Test class | Tests | Covers |
|---|---|---|
| AuthTests | 17 | Registration, login, logout, hashed passwords, session cookie, profile |
| EventTests | 14 | Organiser event and category management, ownership, validation |
| RoleAccessTests | 8 | Wrong role rejected (403), not logged in rejected (401), forged cookie, separate sessions |
| EnrolmentTests | 13 | Enrolment recorded and linked, duplicates (409), cancelling, results, payments, free categories |
| ImageUploadTests | 12 | Banner and profile picture uploads, file validation, role checks (fake Azure storage) |
| EventQueryTests | 7 | Event filters, organiser dashboard data, finisher counts |

Run them from the repository root:
```
dotnet test Part2/RaceDay.Tests/RaceDay.Tests.csproj
```

### CI/CD

The workflow `.github/workflows/build-and-test.yml` runs on every push. It checks the Part 2 folder structure, builds the solution and runs all unit tests.

![Part 2 CI build passing](docs/part2-ci-success.png)

### Part 2 video

[(https://youtu.be/ADO-hm_i5Zs)
]
---

## Part 3 - ASP.NET Core MVC web application

`Part3/RaceDay.Web` is the web front end for RaceDay. It consumes the Part 2 API for **all** data operations. The MVC project has no database access and no business logic: it sends requests to the API and displays what comes back. Images are uploaded to the API, which stores them in Azure Blob Storage, so the web application never talks to Azure directly.

```
Browser  ->  RaceDay.Web (MVC, Docker, port 8080)  ->  RaceDay.Api (port 5003)  ->  SQL Server
                                                                               ->  Azure Blob Storage
```

### Technology

ASP.NET Core MVC (.NET 10), Bootstrap 5 with a custom RaceDay theme, server-side session, a typed `HttpClient` for the API, and Docker.

### How to run it

Prerequisites: Docker Desktop, the .NET 10 SDK, SQL Server Express with SSMS, and either the Azurite emulator or an Azure Storage account.

1. **Start Azure Blob Storage** (the Azurite emulator, once):
   ```
   docker run -d --name azurite -p 10000:10000 mcr.microsoft.com/azure-storage/azurite azurite-blob --blobHost 0.0.0.0 --skipApiVersionCheck
   ```
   To use a real Azure Storage account instead, set its connection string before starting the API: `$env:AzureStorage__ConnectionString = "<connection string>"`.
2. **Create the database** (once): `cd Part2/RaceDay.Api` then `dotnet ef database update`.
3. **Start the API**: `dotnet run --launch-profile http` (it listens on http://localhost:5003). If the container cannot reach it, start it with `dotnet run --launch-profile http --urls "http://0.0.0.0:5003"`.
4. **Start the web application in Docker with one command**:
   ```
   cd Part3
   docker compose up --build
   ```
   Then open **http://localhost:8080**. The equivalent plain Docker commands are:
   ```
   docker build -t raceday-web Part3/RaceDay.Web
   docker run --rm -p 8080:8080 -e ApiBaseUrl=http://host.docker.internal:5003 raceday-web
   ```
5. **Running without Docker** gives the identical application: `cd Part3/RaceDay.Web`, then `dotnet run --launch-profile http`, then open http://localhost:5100.

### What each role can do

| Role | Pages |
|---|---|
| Visitor | Home page with upcoming events and filters (search, type, date range), event details, register, log in |
| Participant | Browse and filter events, event details with categories, enter a category (recorded immediately), My events (status, pay, cancel), My results (finish time and position such as "47th out of 312 finishers"), Profile (update details and upload a profile picture) |
| Organiser | Dashboard (events created, total enrolments, upcoming events, next event), events (create, edit, delete with all fields and a banner image), category management, enrolments per event, results entry |

Each role has its own navigation menu. Protected pages redirect visitors who are not logged in to the login page, and a user with the wrong role sees an "access denied" page.

### Session management

On login the MVC application calls the API, then stores the user's id and role in its own server-side session. It also keeps that user's API session cookie in the session and forwards it on every API call, so the API applies its own role checks too. Logging out clears both sessions and returns the user to the login page. A `SessionAuthorize` filter protects each page, and an expired session sends the user back to the login page with a message.

### Azure Blob Storage

Organisers can upload an event banner and any user can upload a profile picture. The file goes from the MVC form to the API, which checks it (JPG, PNG, GIF or WEBP, at most 5 MB, real image signature), stores it in Azure Blob Storage (containers `event-banners` and `profile-pictures`) and saves the blob's URL in the database. The web application displays the image from that URL. If storage is unavailable the user sees a friendly message instead of an error.

### Docker

`Part3/RaceDay.Web/Dockerfile` is a multi-stage build: the SDK image restores and publishes (the project file is copied first so the restore layer is cached), then a small ASP.NET runtime image runs the published output as a non-root user on port 8080. The API address is set with the `ApiBaseUrl` environment variable (default `http://host.docker.internal:5003`). `.dockerignore` keeps build output out of the image, and `docker-compose.yml` starts the container with a single command.

### UI design

A navy and electric-orange RaceDay theme with condensed italic headings, Bootstrap 5 for layout and responsiveness, and the same fonts, colours and spacing on every page. Statuses are colour coded: green for Confirmed and Finished, amber for Pending, red for DNF and DSQ, grey for DNS. Event types have their own colours and icons, event banners are shown on cards and detail pages, and invalid input shows friendly messages instead of raw errors.

### Changes to the API and database in Part 3

- **Database:** `Events.BannerImageUrl` and `Users.ProfilePictureUrl` (migration `AddImageUrls`). The ERD in `docs/erd.png` and `docs/raceday-schema.sql` were updated to match.
- **New endpoints:** `POST /api/events/{id}/banner`, `POST /api/users/me/profile-picture` and `GET /api/events/mine` (organiser dashboard data).
- **Event filters:** `GET /api/events` accepts `eventType`, `search`, `upcomingOnly`, `from` and `to`.
- **Results** include the event date and the number of finishers, and **enrolments** include the entry fee and event date. Enrolling in a free category is confirmed immediately.

### Part 3 CI/CD

The workflow `.github/workflows/build-and-test.yml` runs on every push. It validates the repository structure, builds the API, runs all 71 unit tests, builds the MVC project, builds the Docker image, then starts the container and checks that it serves the login page.

![Part 3 CI build passing](docs/part3-ci-success.png)

### Part 3 video

PASTE_PART3_VIDEO_LINK_HERE

---
## References

Anthropic (2026) *Claude* [Large language model]. Available at: https://claude.ai (Accessed: 19 October 2026).

MDN Contributors (2026) *HTTP response status codes*. Available at: https://developer.mozilla.org/en-US/docs/Web/HTTP/Reference/Status (Accessed: 19 October 2026).

## AI acknowledgement

Generative AI (Claude, Anthropic) was used to assist with planning and drafting parts of this project, including the database design, API structure and code scaffolding. The code was reviewed, tested and is explained by the author in the video presentations.
