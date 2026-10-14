# Public Careers - Job Board and Application Intake QA Report

| | |
| --- | --- |
| **Project** | GameHub (ASP.NET Core MVC, `net10.0`) |
| **Module** | Public Careers - `/Careers` job board and public application intake |
| **Branch** | `feature/hr-module` (all changes left uncommitted; nothing committed or pushed) |
| **Execution window** | 2026-10-14 (UTC) / 2026-10-14 (PKT) |
| **Environment** | Windows; app built and run in `Development` at `http://localhost:5113`; SQL Server LocalDB `(localdb)\MSSQLLocalDB`, database `GameHubFYPDb` |
| **Overall verdict** | **PASS** |

## 1. Execution date and environment

* **Execution window:** 2026-10-14. The app's clock (`DateTime.UtcNow`) read `2026-10-14 06:43 UTC` when the run began, and the index rule uses `DateOnly.FromDateTime(DateTime.UtcNow)` = `2026-10-14`, so the "not expired" boundary and the harness agreed on "today" throughout the run.
* **Application:** the real GameHub app built from the repository, run in the `Development` environment over HTTP at `http://localhost:5113` (the `http` launch profile). HTTPS redirection is a no-op without an HTTPS port, and addressing the app over HTTP avoided PowerShell 5.1 certificate-handling limitations.
* **Database:** SQL Server LocalDB `(localdb)\MSSQLLocalDB`, database `GameHubFYPDb`, Windows integrated authentication (`Trusted_Connection=True`). **No credentials or secrets are recorded in this report.** All persistence claims were verified directly against the database with `sqlcmd`.
* **Toolchain:** .NET SDK targeting `net10.0`, EF Core `10.0.10`, `dotnet-ef` `10.0.11`.
* **HR access:** the seeded SuperAdmin account was used only to reach the HR screens (its credentials are not reproduced here).

### Method

* The solution has **no automated test project** (the only project is `GameHub`), and there is no browser-automation package (Selenium/Playwright) in the repo. The environment is also offline for NuGet restore (`NU1900 Unable to load the service index for https://api.nuget.org/...`). Consistent with the project's established approach (`docs/qa/HR_MODULE_E2E_QA_REPORT.md`), this run was executed as **scripted HTTP end-to-end tests** driven by PowerShell 5.1 + `HttpClient` against the running Development instance, with **database assertions via `sqlcmd`**.
* Every scenario asserted both the HTTP outcome (status code, redirect target, required markup/anti-forgery token) **and** the persisted database effect. No destructive operation was performed against pre-existing data.
* Client-side JavaScript validation could not be exercised (no headless browser available); the authoritative **server-side** validation and entity rules were exercised directly. This matches the behaviour of the shipped app, which does not load jQuery/unobtrusive validation and relies on HTML5 attributes plus server-side validation.

## 2. Scope and routes covered

| Route | Purpose |
| --- | --- |
| `/Careers` | Index of open positions |
| `/Careers/Details/{id}` | Job details |
| `/Careers/Apply/{id}` | Application form (GET/POST) |
| `/Careers/ApplySuccess` | Post-submission confirmation (PRG) |

Plus the HR-side screens used to confirm integration: `/JobApplications`, `/JobApplications/Details/{id}`, `/JobApplications/Create`, and the HR module list screens.

## 3. Test case results

All executed checks passed. The tables below give the scenario, the actual observed result, and the verdict.

### 3.1 Public index and eligibility (`/Careers`)

| ID | Case | Actual | Verdict |
| --- | --- | --- | --- |
| IDX-01 | Home `/` reachable (baseline) | 200 | PASS |
| IDX-02 | `/Careers` reachable | 200 | PASS |
| IDX-03..08 | All 6 active/open/non-expired postings listed (DB Ids 1, 2, 4, 5, 6, 7) | present | PASS |
| IDX-09..12 | Draft posting (Id 3) and inactive/closed/expired postings are not listed | absent | PASS |

### 3.2 Job details (`/Careers/Details/{id}`)

| ID | Case | Actual | Verdict |
| --- | --- | --- | --- |
| DET-01..06 | Details for each eligible posting render the correct title | 200, title present | PASS |
| DET-07..10 | Draft / inactive / closed / expired postings | 404 | PASS |
| DET-11 | Unknown id | 404 | PASS |

### 3.3 Apply form (GET `/Careers/Apply/{id}`)

| ID | Case | Actual | Verdict |
| --- | --- | --- | --- |
| APG-01..06 | Apply form for each eligible posting renders the form and anti-forgery token | 200 | PASS |
| APG-07..10 | Draft / inactive / closed / expired postings | 404 | PASS |
| APG-11 | Unknown id | 404 | PASS |

### 3.4 Navbar / footer / theme parity (Home vs Careers)

| ID | Case | Actual | Verdict |
| --- | --- | --- | --- |
| UI-01 | `public-page` body class on Home and Careers | present on both | PASS |
| UI-02 | Same primary CTA (`btn-premium btn-compact`, "Book a Court") | present on both | PASS |
| UI-03 | Footer (`public-footer`, `footer-bottom`) | present on both | PASS |
| UI-04 | Same stylesheets (`site.css`, `layout.css`, `components.css`, `responsive.css`) | present on both | PASS |
| UI-05 | Careers-specific `public-careers.css` loaded | present | PASS |
| UI-06 | Careers nav link marked active on Careers pages | present | PASS |
| UI-07 | Home nav does not mark Careers active | correct | PASS |
| UI-08 | Details/Apply share the public navbar + careers CSS | present | PASS |

### 3.5 Happy-path submission (posting 1)

| ID | Case | Actual | Verdict |
| --- | --- | --- | --- |
| APP-01 | Valid application POST | 302 → `/Careers/ApplySuccess` | PASS |
| APP-02 | Persisted row count | +1 | PASS |
| APP-03 | `JobPostingId` | forced to route posting (1) | PASS |
| APP-04 | `Status` | `Submitted` | PASS |
| APP-05 | `IsActive` | 1 | PASS |
| APP-06 | `CreatedByUserId` | NULL (public submitter) | PASS |
| APP-07 | CV stored file name | present | PASS |
| APP-08 | Application reference generated | yes (`APP-xxxxxx` sequence) | PASS |
| APP-09 | PRG - GET `/Careers/ApplySuccess` | 200, shows the reference | PASS |
| APP-10 | Success page theme markers | navbar/footer + careers CSS | PASS |
| APP-11 | `/Careers/ApplySuccess` without TempData | 302 → `/Careers` | PASS |

### 3.6 Supporting documents

| ID | Case | Actual | Verdict |
| --- | --- | --- | --- |
| DOC-01 | Application with CV + 2 supporting documents | 302, persisted | PASS |
| DOC-02 | Document rows saved | 2 rows | PASS |

### 3.7 Protected-field tampering

A valid POST to `/Careers/Apply/7` was sent with body fields deliberately trying to override server-controlled values: `Form.Id=999`, `Form.ApplicationNumber=HACK-000`, `Form.JobPostingId=999`, `Form.Status=Hired`, `Form.ApplicationDate=2000-01-01`, `Form.IsActive=false`.

| ID | Case | Actual | Verdict |
| --- | --- | --- | --- |
| TMP-01 | Request succeeds as a normal application | 302 | PASS |
| TMP-02 | Stored `JobPostingId` | route value 7 | PASS |
| TMP-03 | Stored `Status` | `Submitted` | PASS |
| TMP-04 | Stored `IsActive` | 1 | PASS |
| TMP-05 | Stored `CreatedByUserId` | NULL | PASS |
| TMP-06 | Stored `ApplicationNumber` | not `HACK-000` | PASS |
| TMP-07 | Stored `Id` | not 999 | PASS |
| TMP-08 | Stored `ApplicationDate` | today (server date) | PASS |

### 3.8 Duplicate prevention (application level, posting 1)

Each duplicate case returned the in-page banner **"You have already applied to this role"**, HTTP 200, and **no** additional row.

| ID | Case | Verdict |
| --- | --- | --- |
| DUP-01 | Same email, different phone | PASS |
| DUP-02 | Same phone, different email | PASS |
| DUP-03 | Same National ID, different email + phone | PASS |
| DUP-04 | UPPERCASE equivalent email (normalised) | PASS |
| DUP-05 | Spaced equivalent phone, e.g. `+92 300 ...` (normalised) | PASS |
| DUP-06 | Same candidate, **different** job (no duplicate exists) | PASS - allowed |
| DUP-07 | Different-job application persisted to posting 2 | PASS (+1 row) |

### 3.9 DB-level uniqueness

| ID | Case | Actual | Verdict |
| --- | --- | --- | --- |
| DB-01 | `IX_JobApplications_JobPostingId_Phone` is unique | `is_unique = 1` | PASS |
| DB-02 | Index is filtered to active rows | `([IsActive]=(1))` | PASS |
| DB-03 | A raw duplicate-phone insert for the same active posting is blocked by the index | `Msg 2601 ... duplicate key ... IX_JobApplications_JobPostingId_Phone`; transaction rolled back; 0 leftover rows | PASS |

### 3.10 Validation

| ID | Case | Actual message | Verdict |
| --- | --- | --- | --- |
| VAL-01 | Missing First Name | "First Name is required." | PASS |
| VAL-02 | Missing Last Name | "Last Name is required." | PASS |
| VAL-03 | Invalid Email | "Enter a valid email address." | PASS |
| VAL-04 | Invalid Phone | "Enter a valid phone number with country code" | PASS |
| VAL-05 | Short/invalid CNIC | "Enter a valid Pakistani CNIC (13 digits)..." | PASS |
| VAL-06 | Missing City | "City is required." | PASS |
| VAL-07 | Future Date of Birth | "Date of Birth cannot be in the future." | PASS |
| VAL-08 | Underage Date of Birth | "Applicant must be at least 18 years old." | PASS |
| VAL-09 | Invalid LinkedIn URL | "Enter a valid LinkedIn profile URL..." | PASS |
| VAL-10 | Experience > 50 years | "Experience must be between 0 and 50 years." | PASS |

Every rejected submission left the row count unchanged.

### 3.11 Upload security

| ID | Case | Actual | Verdict |
| --- | --- | --- | --- |
| UP-01 | Non-PDF extension (`.exe`) | "Only .pdf files are allowed." | PASS |
| UP-02 | `.pdf` name but non-PDF content | "The file content is not a valid PDF..." | PASS |
| UP-03 | Zero-byte / empty file | rejected | PASS |
| UP-04 | Oversized (> 5 MB) | "File size exceeds the 5 MB limit." | PASS |
| UP-05 | More than 3 supporting documents | "A maximum of 3 supporting documents are allowed." | PASS |

No rejected upload wrote a record or a stored file.

### 3.12 Ineligible / unknown posting submissions

| ID | Case | Actual | Verdict |
| --- | --- | --- | --- |
| INE-01 | POST `/Careers/Apply/3` (Draft) | 404, no row | PASS |
| INE-02 | POST `/Careers/Apply/9` (inactive) | 404, no row | PASS |
| INE-03 | POST `/Careers/Apply/10` (closed) | 404, no row | PASS |
| INE-04 | POST `/Careers/Apply/11` (expired) | 404, no row | PASS |
| INE-05 | POST `/Careers/Apply/99999` (unknown) | 404, no row | PASS |

### 3.12b Ineligible submissions via the Apply route

(The POSTs above were reached through the controller action; all returned the themed 404 page `/Error/NotFound` after the status-code re-execute. Ids 9/10/11 were QA-created temporary postings used so that the closed/expired/inactive eligibility branches could be exercised without mutating real postings; they were removed afterwards - see section 10.)

### 3.12c Salary gating on the public details page

| ID | Case | Actual | Verdict |
| --- | --- | --- | --- |
| SAL-01 | Details page for a posting with no published salary | no "Salary Range" block | PASS |
| SAL-02 | Details page for a posting with a range | "Salary Range" block shown | PASS |

### 3.13 HR integration

| ID | Case | Actual | Verdict |
| --- | --- | --- | --- |
| HR-01 | SuperAdmin login | 302 | PASS |
| HR-02 | `/JobApplications` list | 200 | PASS |
| HR-03 | Submitted application reference appears in the HR list | present | PASS |
| HR-04 | Correct posting shown in the HR list | present | PASS |
| HR-05 | HR Job Details screen | 200 | PASS |
| HR-06 | Candidate email shown on HR details | present | PASS |
| HR-07 | HR create form | 200 | PASS |
| HR-08 | An HR-created application with the same identifier is blocked by the **shared** duplicate service (no row written) | blocked | PASS |

### 3.14 HR module regression sweep

| ID | Route | Actual | Verdict |
| --- | --- | --- | --- |
| REG-01 | `/JobPostings` | 200 | PASS |
| REG-02 | `/Recruitment` | 200 | PASS |
| REG-03 | `/Employees` | 200 | PASS |
| REG-04 | `/HR` | 200 | PASS |
| REG-05 | `/HRReports` | 200 | PASS |
| REG-06 | `/Offers` | 200 | PASS |
| REG-07 | `/Performance` | 200 | PASS |
| REG-08 | `/Termination` | 200 | PASS |

## 4. Migration, database and index confirmation

* **Active Development connection** (Environment = `Development`): `Server=(localdb)\MSSQLLocalDB;Database=GameHubFYPDb;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True` - documented without secrets.
* **Migration applied:** `20261014005907_AddJobApplicationPhoneUniqueIndex`. It is recorded in `__EFMigrationsHistory` and, after the update, `dotnet ef migrations list` reports no pending migrations.
* **Index confirmed** after the migration:
  * `IX_JobApplications_JobPostingId_Phone` - `is_unique = 1`, `filter_definition = ([IsActive]=(1))`.
  * Sibling filtered unique indexes `IX_JobApplications_JobPostingId_Email` and `IX_JobApplications_JobPostingId_NationalIdNumber` also present (`([IsActive]=(1))`).
* **Existing data preserved.** The migration created the index in place; no reset, drop or recreate was performed.
* **Final database state:** `JobPostings = 7`, `JobApplications = 2` (both pre-existing: `APP-000001` Submitted, `APP-000002` Shortlisted), `JobApplicationDocuments = 1` (pre-existing).

## 5. Full workflow results

| Workflow | Path exercised | Result |
| --- | --- | --- |
| Public browse | Home → `/Careers` → eligible listing | PASS |
| Job apply | Qualifications → Job Details → Apply form | PASS |
| Submission | POST Apply → 302 → `/Careers/ApplySuccess` (PRG, TempData-bound) | PASS |
| Persistence & HR | application written with route posting, `Status = Submitted`, `CreatedByUserId = NULL`, visible on HR Job Applications + Details | PASS |
| Duplicate prevention | email / phone / National ID (plus normalised equivalents) blocked with a friendly in-page banner; no second row | PASS |
| Same candidate, different job | allowed when no duplicate exists | PASS |
| Invalid attempt | ineligible / closed / expired / inactive / unknown / draft posting | PASS - 404, no row |
| Upload enforcement | type, magic-byte, size, count | PASS |

## 6. Defects found and fixed

**None.** No application defect was found in the Careers feature during this run; therefore no fix was needed and no retest was required. (Defects encountered while building the *test harness itself* - e.g. SQL Server requiring `QUOTED_IDENTIFIER ON` for DML against filtered indexes - are harness matters, not app defects, and are recorded only in the appendix.)

### Observation (not a defect)

The pre-existing `wwwroot/js/site.js` `setActiveLink()` routine throws a `SyntaxError` when it encounters anchor hrefs such as `/#home`, early-terminating that particular client pass. This is unrelated to Careers: `updateNavbar()` runs first, and the server-rendered `active` class on the Careers nav link and the navbar blur all work correctly (verified in 3.4). It is left untouched per scope.

## 7. Remaining blockers, limitations and known issues

* **No blockers.** All executed checks passed.
* **No automated test project / no browser automation.** Coverage is manual + scripted HTTP E2E. There was no Playwright/Selenium to run true browser tests, and NuGet restore is offline, so a new test project could not be built. Client-side (jQuery unobtrusive) validation is likewise not loaded by the app; server-side validation was fully exercised.
* **Migration is not auto-applied at startup.** There is no `Database.Migrate()`/`EnsureCreated()` in `Program.cs`; the pending migration was applied manually with `dotnet ef database update`. Any deployment must apply migrations explicitly.
* **Screenshots** were intentionally not captured, per instruction; evidence is HTTP status/HTML/DB based.
* **Test-data cleanup** (section 10) removed the QA-created records and files, so the live database no longer contains the tested records; this report and the raw harness output are the retained evidence.

## 8. Totals

| Metric | Count |
| --- | --- |
| Total executed checks | 125 (50 route/layout + 73 workflow + 2 salary) |
| Passed | 125 |
| Failed | 0 |
| Blocked | 0 |
| Not run | 0 |
| Defects found | 0 |
| Defects fixed and retested | 0 |
| Open defects | 0 |

## 9. Overall verdict

**PASS.** All 125 executed checks passed (0 FAIL, 0 BLOCKED, 0 not-run, 0 defects) across the four Careers routes plus HR integration. The public Careers index shows only eligible postings; details and apply screens render the correct posting with the shared public navbar/footer/theme; the full submit → success → HR-recording workflow works and links to the correct posting with `Status = Submitted` and server-controlled fields protected; duplicate prevention (same email / phone / National ID, including normalised equivalents) and the DB phone unique index both hold; validations and upload restrictions reject invalid input with no side effects; and the seven-screen HR module shows no regressions. The migration was applied and the filtered index confirmed with existing data preserved. The feature is ready for review/commit.

## 10. Test-data cleanup

Following the convention in the HR QA report, QA-created records and files were removed where ownership was certain. Pre-existing data was preserved.

**Removed (QA-created):**
* 12 `JobApplications` created by the public-flow tests (`Email LIKE 'qa.%'`) plus their 6 `JobApplicationDocuments`.
* The stored PDFs for those CVs and supporting documents (12 in `Storage/hr/cvs`, 6 in `Storage/hr/documents`).
* Temporary `JobPostings` created only to exercise the ineligible-posting and salary branches (`QA-TEMP Closed/Expired/Inactive ...` and a no-salary salary-gate posting).

**Deliberately retained:**
* The 7 pre-existing job postings.
* The 2 pre-existing job applications `APP-000001` (`ayesha.khan.smoke@example.com`) and `APP-000002` (`daniyal.verify@example.com`) with their CV files and the one pre-existing supporting document.

**Final state (verified with `sqlcmd`):** `JobPostings = 7`, `JobApplications = 2`, `JobApplicationDocuments = 1`; `Storage/hr/cvs` = 2 PDFs + `.gitkeep`; `Storage/hr/documents` = 1 PDF + `.gitkeep`.

## Appendix - reproducing the run

* The live app must be running in Development, e.g. `dotnet run --project GameHub.csproj --launch-profile http` (serves `http://localhost:5113`), with `GameHubFYPDb` migrated.
* Two PowerShell 5.1 harness scripts (kept outside the repository in the temp working directory, not committed):
  * `qa-careers.ps1` - route/eligibility/details/apply-GET/layout-parity suite (50 checks). Prints lines of the form `PASS | name | detail` and writes `qa-results.xml`.
  * `qa-flow.ps1` - happy path, supporting documents, protected-field tampering, duplicate prevention, DB index, validation, upload security, ineligible POSTs, HR login/list/details/create and HR regression sweep (73 checks). Writes `qa-flow-results.xml`.
* Both scripts use a shared `HttpClient` with a cookie container (WebRequestSession equivalent) to follow the anonymous public session, extract the anti-forgery token from `__RequestVerificationToken`, and post `multipart/form-data` with the exact `Form.*` field names (and plain field names for the HR create form). Database state is asserted by shelling out to `sqlcmd`.
