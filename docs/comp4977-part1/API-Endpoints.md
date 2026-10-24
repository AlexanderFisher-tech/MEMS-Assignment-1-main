# Municipal Elections Companion API: Endpoints

**Base URL:** https://mems-api-afisher-ane9a7cmfpb9b9at.canadacentral-01.azurewebsites.net
**Swagger UI:** https://mems-api-afisher-ane9a7cmfpb9b9at.canadacentral-01.azurewebsites.net/swagger

All responses are camelCase JSON. Every error has the shape `{ "message": "..." }`. Protected (🔒) endpoints require the header `Authorization: Bearer <token>`, using the token returned by login.

| # | Method | Endpoint | Auth | Request body | Success response | Errors |
|---|---|---|---|---|---|---|
| 1 | POST | `/api/auth/register` | Public | `{ name, email, password }` (password ≥ 6 chars) | 200 `{ message: "Registration successful" }` | 400 |
| 2 | POST | `/api/auth/login` | Public | `{ email, password }` | 200 `{ token, name, email }` | 400, 401 |
| 3 | GET | `/api/municipalities` | Public | | 200 `Municipality[]` | |
| 4 | GET | `/api/municipalities/{id}` | Public | | 200 `Municipality` | 404 |
| 5 | GET | `/api/positions` | Public | | 200 `Position[]` | |
| 6 | GET | `/api/positions/{id}` | Public | | 200 `Position` | 404 |
| 7 | GET | `/api/candidates` | Public | | 200 `Candidate[]` | |
| 8 | GET | `/api/candidates/{id}` | Public | | 200 `Candidate` | 404 |
| 9 | GET | `/api/candidates/search?name={query}` | Public | | 200 `Candidate[]` (case-insensitive partial name match; empty name returns all) | |
| 10 | GET | `/api/savedcandidates` | 🔒 JWT | | 200 `SavedCandidate[]` for the signed-in user | 401 |
| 11 | POST | `/api/savedcandidates` | 🔒 JWT | `{ candidateId }` | 201 `SavedCandidate` | 400, 401, 404, 409 (already saved) |
| 12 | DELETE | `/api/savedcandidates/{id}` | 🔒 JWT | | 204 No Content (matches SavedCandidateId, or CandidateId as a fallback) | 401, 404 |

Optional filters: `/api/positions?municipalityId=` and `/api/candidates?municipalityId=&positionId=`.

## Response objects

```json
// Municipality
{ "municipalityId": 3, "name": "Burnaby", "province": "BC", "electionDate": "2026-10-17T00:00:00",
  "description": "A large, diverse city ...", "logoUrl": "https://<host>/images/logos/burnaby.png" }

// Position
{ "positionId": 2, "name": "Councillor", "seatsAvailable": 6, "municipalityId": 1, "municipalityName": "Pitt Meadows" }

// Candidate
{ "candidateId": 1, "name": "Mara Ellison", "biography": "Mara Ellison is a fictional ...",
  "photoUrl": "https://<host>/images/demo-candidates/demo-mara-ellison.jpg",
  "website": "https://example.org/candidates/mara-ellison", "email": "mara.ellison@example.org",
  "positionId": 1, "positionName": "Mayor", "municipalityId": 1, "municipalityName": "Pitt Meadows" }

// SavedCandidate
{ "savedCandidateId": 1, "userId": "6acded42-d8a2-4e18-bb30-2f499dee5df6", "candidateId": 1,
  "dateSaved": "2026-10-24T08:08:33Z", "candidate": { /* Candidate */ } }

// Login
{ "token": "eyJhbGciOiJIUzI1NiIs...", "name": "Jane Doe", "email": "jane@example.com" }

// Error (any 4xx)
{ "message": "Invalid email or password" }
```
