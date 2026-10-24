# Municipal Elections Management System (MEMS)

COMP4976 — Assignment 1
British Columbia Institute of Technology (BCIT)

## 1. Team Members and Contributions

**Abdullah Rehan — COMP4976 (ASP.NET Core MVC Web Application)**

Responsible for the COMP4976 web application component of the assignment, including the development, implementation, testing, GitHub repository management, Azure deployment, and preparation of the web application deliverables.

The web application includes:

- Municipality, position, and candidate management.
- ASP.NET Core Identity authentication and role-based authorization.
- Entity Framework Core Code First with SQLite.
- Administrator dashboards and CRUD functionality.
- Municipality logo and candidate photograph uploads.
- Public candidate browsing, searching, and filtering.
- Personal candidate selection and printable voting guides.
- Testing, debugging, and deployment to Azure App Service.

GitHub Copilot and Codex were used as AI-assisted development tools for code generation, review, debugging, testing, and development guidance.

**Alex Fisher — COMP4977 (iOS/Mobile Application)**

Responsible for the separate COMP4977 iOS/mobile application component of the overall assignment.

The COMP4977 component is developed separately from the COMP4976 ASP.NET Core MVC web application contained in this repository.

## 2. Application Description

The Municipal Elections Management System (MEMS), presented as Civic Guide, is an ASP.NET Core MVC application designed to support municipal election administration and provide election information to the public.

The application uses:

- ASP.NET Core MVC and .NET 10
- Entity Framework Core Code First migrations
- SQLite database
- ASP.NET Core Identity
- Bootstrap for responsive user interfaces
- Azure App Service for deployment
- GitHub for source control
- GitHub Copilot and Codex for AI-assisted development

The system supports multiple municipalities, with elected positions and candidates associated with each municipality.

Super Administrators can manage the entire system, while municipality-specific administrators are restricted to their assigned municipality.

Public users can browse municipalities and candidates, search and filter candidates, view profiles, select preferred candidates, and print personalized voting guides without creating an account.

The database is seeded with municipalities, positions, administrator roles, administrator accounts, and 24 fictional candidates with unique names, biographies, and synthetic portraits.

See `docs/demo-portraits.md` for information about the candidate portraits.

## 3. Completed Features

The following major requirements have been implemented:

- Entity Framework Core Code First database design and migrations.
- SQLite database integration.
- Required administrator roles and seeded users.
- Super Admin and municipality-specific authorization.
- Municipality CRUD and logo upload functionality.
- Position CRUD functionality.
- Candidate CRUD and photograph upload functionality.
- Public municipality and candidate directories.
- Candidate search and filtering.
- Candidate details pages.
- Anonymous personal voting guide.
- Candidate selection and removal.
- Printer-friendly voting guide.
- Form validation and image upload restrictions.
- GitHub repository publication.
- Azure App Service deployment.

The deployed application has been manually tested, including:

- Super Admin login and administrative operations.
- Pitt Meadows Admin login and administrative operations.
- Municipality creation, editing, and deletion.
- Position creation, editing, and deletion.
- Candidate creation, editing, and deletion.
- Candidate photograph and municipality logo uploads.
- Verification of database changes across administrator sessions.
- Public candidate browsing and voting guide printing.
- Anonymous public access using a browser Incognito window.

The completed manual tests did not reveal errors in these workflows.

## 4. Work Not Completed or Not Fully Verified

The core COMP4976 web application features have been implemented, and the application is deployed to Azure.

At the time of preparing this README, the following items remain outstanding or have not been fully verified:

- Testing the printed voting guide on a physical printer. Browser printing and PDF saving have been tested.
- Verification of Azure SQLite data and uploaded-file persistence following an App Service restart.

No major required application feature is currently known to be incomplete.

The COMP4977 iOS/mobile component is separate from this COMP4976 web application repository.

## 5. Major Challenges Encountered

**Development environment and dependencies**

Initial network and development environment restrictions affected dependency downloads and setup.

Windows Event Log permissions caused difficulties during an early integration test run. Switching to console logging resolved the issue.

An image-processing dependency initially introduced licensing requirements. The project was updated to use SkiaSharp instead.

**Database, authentication, and authorization**

The application required municipality-specific authorization to ensure that administrators could manage only data belonging to their assigned municipality.

Database relationships and deletion restrictions were implemented to help protect related municipalities, positions, and candidates.

**Image handling**

Candidate photograph and municipality logo uploads required file validation, image-processing support, and appropriate storage handling.

**Azure deployment**

Deploying the application to Azure App Service required configuring the production environment, database connection string, application storage paths, and administrator seeding.

The GitHub Actions deployment process initially published multiple .NET runtime configuration files, resulting in startup detection problems.

The deployment workflow was corrected to publish the web application project specifically, and the Azure startup command was configured to launch `Mems.Web.dll`.

After resolving these configuration issues, the application successfully launched on Azure.

**Testing and verification**

Additional testing was performed on the deployed application to verify that the administrative functionality, public features, uploads, and voting guide worked correctly outside the local development environment.

## 6. Special Testing Instructions

### Local Development

1. Install the .NET 10 SDK.
2. Open PowerShell in the solution root directory.
3. Run `./scripts/setup.ps1` to prepare the local development configuration, restore dependencies, build the solution, and apply database migrations.
4. Run `dotnet test MEMS.sln` to execute the automated tests.
5. Run `dotnet run --project src/Mems.Web --launch-profile http` to launch the application.
6. Browse to `http://localhost:5069`.

### Development Administrator Accounts

The local development environment uses the administrator credentials specified in the assignment rubric.

Username: `super`
Role: Super Admin
Password: `P@$$w0rd`

Username: `pm`
Role: Pitt Meadows Admin
Password: `P@$$w0rd`

The Super Admin can manage all municipalities, positions, and candidates.

The Pitt Meadows Admin is restricted to managing the positions and candidates associated with Pitt Meadows.

### Azure Production Administrator Accounts

The deployed Azure application contains the required `super` and `pm` administrator accounts.

For security reasons, the production deployment uses a different administrator password from the default development password listed in the assignment rubric.

**The production administrator login credentials are provided in the separate assignment documentation PDF submitted privately to the instructor.**

The production credentials should be used when testing the deployed Azure application.

### Application Testing Notes

- Public browsing, candidate searching, and voting guide selection do not require login.
- Only authenticated administrators can manage election data.
- Candidate photographs and municipality logos must be valid JPEG or PNG images, up to 2 MB.
- Parent records with dependent data cannot be deleted without resolving their dependencies.
- Select candidates through the public directory and navigate to **My voting guide** to review selections.
- Use **Print voting guide** to print the selected candidates.
- The browser's **Save as PDF** feature can be used to produce a PDF copy of the voting guide.
- For printing, select Letter or A4 paper and disable browser headers and footers where appropriate.
- A previous local regression run completed 17 integration tests successfully.
- With the application running, `node scripts/print-check.cjs` can be used for additional browser print checks.

See the root `README.md` for further application information and `deploy/README.md` for Azure deployment configuration instructions.

## 7. Azure Deployment

**Live Azure Application:**

[https://mems-civic-guide-bcit-hsbuf2fmhca4bxgh.westus3-01.azurewebsites.net/](https://mems-civic-guide-bcit-hsbuf2fmhca4bxgh.westus3-01.azurewebsites.net/)

The COMP4976 web application is deployed to Microsoft Azure App Service.

The deployed application supports public website functionality and authenticated administration.

## 8. GitHub Repository

**COMP4976 Source Repository:**

[https://github.com/kakepe/MEMS-Assignment-1](https://github.com/kakepe/MEMS-Assignment-1)

The repository contains the ASP.NET Core MVC solution, application source code, EF Core migrations, static assets, testing code, and development documentation.

GitHub Copilot prompt, instruction, and skills Markdown files are included to document the AI-assisted development workflow.

## 9. Source Code Packaging

The complete COMP4976 source code is provided as a ZIP archive for D2L submission.

The submission archive includes:

- Complete Visual Studio solution and project files.
- Application source code.
- EF Core migrations and seed-data implementation.
- Controllers, Models, Views, Services, and related components.
- Required static assets.
- Automated test source code.
- GitHub Copilot prompt, instructions, and skills Markdown files.
- `docs/readme.txt` and other relevant development documentation.

Generated build outputs, local databases, dependency caches, development secrets, and other unnecessary files are excluded.

The archive is intended to allow the instructor to inspect, restore, build, and test the complete COMP4976 application.
