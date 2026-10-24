# Rubric implementation and verification

Source of truth: requirements/MEMS-Assignment-1.pdf, page 10. This is a development checklist, not the separate report or a prediction of marks.

| Rubric category | Implementation | Verification / outstanding work |
| --- | --- | --- |
| Database design (15) | Municipality → Position → Candidate relationships; municipality-linked Identity roles; restrictive deletes; unique municipality / position names; EF Core Code First migration; SQLite; ignored Development connection settings; four municipalities, twelve positions, twenty-four fictional candidates | Migration generated and applied locally; relational data and role seed verified by integration tests |
| Authentication / authorization (10) | Real ASP.NET Identity; super / pm; Super Admin and municipal roles; scoped database queries; authorization on all admin controllers; submitted parent/child ID validation | Integration tests use real sign-in cookies and forged cross-municipality GET/POST requests; production demo-user safeguards and Identity lockout tested; browser sign-in checked for both accounts |
| Municipality / position management (10) | Separate Admin area; municipality CRUD with logos for Super Admin; scoped position CRUD and seats; linked group creation | Create/read/update/delete tested for both entities; blocked parent deletes tested; logo upload/removal and generated group creation/deletion tested |
| Candidate management (20) | Scoped CRUD and details, biography, email/HTTP(S) URL, UTC creation date, generated photo storage; size/content/format restrictions; client/server validation | Candidate lifecycle and email/URL validation tested; forged parent IDs, spoofed SVG/PNG, >2 MB upload rejection, and valid uploads tested; browser required-field validation and candidate CRUD checked |
| Public features (15) | Municipality and position browsing; candidate directory and details/photos; name search and municipality/position filters; pagination | Integration filters/details/empty-search results; browser municipality navigation, live filter refresh, and profiles/pages render |
| Voting guide (10) | Anonymous session add/remove/clear; seat limit; municipality, election date, candidates/positions, Vancouver print timestamp; print CSS | Integration session independence/seat limits/add/remove/clear; browser print media hides navigation/buttons and generates a voting-guide PDF; physical printer not tested |
| UI / usability (10) | Responsive Bootstrap navigation, cards, forms, status messages, scoped dashboard, responsive tables, accessible labels, local CSS/JS/assets | Desktop screenshot inspected; 390px mobile browser check found no horizontal overflow; mobile menu worked; no browser JavaScript errors |
| Azure (5) | .NET 10 container recipe, persistent /home paths, production secret settings and bootstrap procedure, single-instance SQLite caveats | Release publish verified locally. Azure resource creation and deployment await owner approval. Docker daemon unavailable; Linux container and Azure runtime have not been tested |
| Code / configuration / Copilot / submission (5) | MVC/form/service separation; minimal docs/readme.txt; Copilot instructions/prompt/skill; setup and safe source-archive scripts; no separate report | Debug and Release builds/tests passed; local release publish excludes Development settings/databases/keys; student names/contributions, GitHub/Azure URLs, and D2L upload remain owner actions |

## Observed checks

- `dotnet restore MEMS.sln`: passed after approved NuGet access.
- `dotnet build MEMS.sln --no-restore`: passed with zero warnings/errors after replacing the initial image dependency.
- `dotnet ef migrations add InitialCreate --project src/Mems.Web --output-dir Data/Migrations`: passed.
- `dotnet ef database update --project src/Mems.Web`: passed.
- `dotnet test MEMS.sln --no-restore`: seventeen meaningful integration tests passed after candidate/printing improvements.
- `dotnet test MEMS.sln -c Release --no-restore`: seventeen integration tests passed after candidate/printing improvements.
- `dotnet publish src/Mems.Web -c Release --no-restore -o artifacts/publish`: passed.
- `node scripts/browser-check.cjs`: five browser scenarios passed; generated screenshots, voting-guide PDF, and results JSON under ignored artifacts/browser.
- `node scripts/print-check.cjs`: confirmed all 24 distinct live names/portraits, a full candidate biography, and 20 valid selections grouped into four municipalities / twelve positions. Native browser printing and Letter/A4 PDF generation passed.
- Letter/A4 PDFs were inspected with PdfPig: each contains 20 complete candidate records and 20 portraits across three pages, the election date and refreshed print date, and no navigation, forms, buttons, or biography controls.
- `AddCandidateDemoSeedKey` was applied in place; `dotnet ef migrations has-pending-model-changes --project src/Mems.Web --no-build` reports none. Backup comparison confirmed unchanged candidate IDs, position IDs, creation dates and contact fields, and unchanged Identity, municipality and position tables. All 24 live biographies contain 108-120 words.
- Regression tests cover legacy upgrade idempotence, preservation of custom records/uploads/removals/deletions, and replacing a synthetic portrait twice through authenticated multipart requests. Candidate group headings now use database IDs rather than object identity.
- `dotnet list src/Mems.Web package --vulnerable --include-transitive --no-restore`: no vulnerable application packages reported by NuGet at audit time.

## Owner actions

1. Fill student names/contributions in docs/readme.txt.
2. Approve the Azure subscription/resources/cost tier, deployment, and production credential setup before any cloud operation.
3. Approve GitHub push and provide repository destination/access, then record the actual repository and deployed URLs.
4. Test the deployed app and persistence after restart; produce deployment evidence outside this coding task if required by the instructor.
5. Create the final source archive with scripts/package.ps1 and submit it to D2L. The archive prepared during development retains clearly marked team/URL placeholders.

The original ignored source ZIP and publish folder predate the candidate/printing refinements. Regenerate final submission/deployment artifacts from the current source; no deployment or GitHub push was performed. Portrait generation provenance and the exact prompt set are in docs/demo-portraits.md and docs/demo-portrait-prompts.json. Physical printer hardware remains untested.

