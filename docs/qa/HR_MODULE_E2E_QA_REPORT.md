# HR Module - End-to-End QA Report

| | |
| --- | --- |
| **Project** | GameHub (ASP.NET Core MVC) |
| **Module** | Human Resources - Screens 1 to 7 |
| **Branch** | `feature/hr-module` (all changes left uncommitted) |
| **Execution window** | 2026-10-13 (UTC) / 2026-10-14 (PKT) |
| **Environment** | Windows + `dotnet` build, app at `http://localhost:5080`, SQL Server LocalDB `(localdb)\MSSQLLocalDB` / `GameHubFYPDb` |
| **Overall verdict** | PASS |

## 1. Execution date and environment

* **Execution window:** 2026-10-13 (UTC) and 2026-10-14 (Pakistan time). HR controllers use `DateTime.UtcNow`, so UTC dates were used wherever "today/not future" mattered.
* **Application:** the real GameHub app built from the repository, served over HTTP at `http://localhost:5080`. HTTPS redirection is a no-op without an HTTPS port, and addressing it over HTTP avoided PowerShell 5.1 certificate-handling limitations.
* **Database:** SQL Server LocalDB `(localdb)\MSSQLLocalDB`, database `GameHubFYPDb`. All persistence claims were verified directly with `sqlcmd`.
* **Accounts:** SuperAdmin `admin@gamehub.pk`; read-only Viewer `qa.viewer.hrmodule@gamehub.local` (created via the app's `Users/Create` with role Viewer).
* **Tooling:** PowerShell 5.1 + `System.Net.Http.HttpClient` (cookie container, no auto-redirect) for HTTP and multipart uploads; `sqlcmd` for assertions.
* **Evidence:** raw per-case logs (`out-01-smoke.txt` ... `out-09.txt`), HTTP status/redirect values, anti-forgery outcomes, SQL result sets and stored-file inspection. No screenshots were taken, per instruction.
* **Isolation:** every test record carried a QA marker (e.g. `QA Recruitment E2E ...`, `qa.candidate.*@gamehub.local`). No pre-existing business record was overwritten or deleted during testing.

### Method

* Requests were issued over real HTTP with cookie-based sessions (the same transport a browser uses), not mocked controllers.
* Anti-forgery tokens were scraped from live forms and submitted; state-changing requests without a token were confirmed to be rejected **and** to persist nothing.
* Database assertions used `sqlcmd` against `GameHubFYPDb` to verify persistence, uniqueness, workflow history/audit rows and cross-module links.
* Expected values for the aggregated screens (HR Dashboard, HR Reports) were computed with SQL mirroring the controller queries and compared to the rendered HTML.

## 2. Coverage of all seven HR screens

| # | Screen | Primary route(s) | Case groups | Cases | Result |
| --- | --- | --- | --- | --- | --- |
| 1 | HR Dashboard | `/HR/Index` | HRD-01..06 | 6 | PASS |
| 2 | Recruitment | `/Recruitment`, `/JobPostings`, `/JobApplications`, `/Interviews` | REC-JP, APP, INT | 30 | PASS |
| 3 | Offers and Hiring (incl. employee creation from an accepted offer) | `/Offers`, `/Employees/Create` | OFF-01..11, EMP-01..05 | 16 | PASS |
| 4 | Employees | `/Employees` | EMP-06..27, CR-01..03 | 26 | PASS |
| 5 | Performance and Warnings | `/Performance` | PERF-01..34 | 34 | PASS |
| 6 | Termination and Clearance | `/Termination` | TRM-01..29 | 33 | PASS |
| 7 | HR Reports | `/HRReports` | HRE-01..42 | 43 | PASS |
| - | Cross-cutting (smoke, auth, errors, cross-module) | - | SEC, AUTH, GET, ERR, AUTHZ, XM | 61 | PASS |

## 3. Test case results

Each row records the case id, scenario, expected result, observed result and verdict exactly as captured in the run logs. `PASS` = executed and confirmed. `INFO` = an informational observation recorded without a hard assertion (never counted as a pass or fail).

### Cross-cutting: smoke, app health and error handling

App health, all admin/HR GET routes, intentional error paths and 404 handling, exercised over real HTTP against the running app.

| ID | Scenario | Expected | Actual | Result |
| --- | --- | --- | --- | --- |
| SEC-01 | Anonymous GET /HR | Redirect to /Auth/Login | status=302 loc=/Auth/Login?returnUrl=%2FHR | PASS |
| SEC-02 | Anonymous GET /Employees | Redirect to /Auth/Login | status=302 loc=/Auth/Login?returnUrl=%2FEmployees | PASS |
| AUTH-01 | Admin login (admin@gamehub.pk) | 302 to /Dashboard | status=302 loc=/Dashboard | PASS |
| AUTH-02 | Admin login with wrong password | 200 login page, error shown | status=200 | PASS |
| GET-01 | GET /HR (Screen 1 HR Dashboard) | 200 | status=200 | PASS |
| GET-02 | GET /Recruitment (Screen 2 Recruitment hub) | 200 | status=200 | PASS |
| GET-03 | GET /Offers (Screen 3 Offer & Hiring hub) | 200 | status=200 | PASS |
| GET-04 | GET /Employees (Screen 4 Employee Management) | 200 | status=200 | PASS |
| GET-05 | GET /Performance (Screen 5 Performance & Warnings) | 200 | status=200 | PASS |
| GET-06 | GET /Termination (Screen 6 Termination & Clearance) | 200 | status=200 | PASS |
| GET-07 | GET /HRReports (Screen 7 HR Reports dashboard) | 200 | status=200 | PASS |
| GET-08 | GET /HRReports/Recruitment (Screen 7 Recruitment report) | 200 | status=200 | PASS |
| GET-09 | GET /HRReports/Employees (Screen 7 Employees report) | 200 | status=200 | PASS |
| GET-10 | GET /HRReports/Performance (Screen 7 Performance report) | 200 | status=200 | PASS |
| GET-11 | GET /HRReports/Termination (Screen 7 Termination report) | 200 | status=200 | PASS |
| ERR-01 | GET /Employees/Details/999999 (Unknown employee id) | 404 (no 500 server error) | status=404 | PASS |
| ERR-02 | GET /JobPostings/Details/999999 (Unknown job posting id) | 404 (no 500 server error) | status=404 | PASS |
| ERR-03 | GET /JobApplications/Details/999999 (Unknown application id) | 404 (no 500 server error) | status=404 | PASS |
| ERR-04 | GET /Offers/Details/999999 (Unknown offer id) | 404 (no 500 server error) | status=404 | PASS |
| ERR-05 | GET /Performance/Details/999999 (Unknown case id) | 404 (no 500 server error) | status=404 | PASS |
| ERR-06 | GET /Termination/Details/999999 (Unknown request id) | 404 (no 500 server error) | status=404 | PASS |
| ERR-07 | GET /Recruitment/DoesNotExist (Unknown route under Recruitment) | 404 (no 500 server error) | status=404 | PASS |
| ERR-08 | GET /NoSuchController/Index (Unknown controller) | 404 (no 500 server error) | status=404 | PASS |



### Cross-cutting: authentication, authorization, antiforgery and file protection

Login/logout, role-based access (SuperAdmin vs Viewer), anti-forgery enforcement on state-changing endpoints, hidden management controls for read-only roles, and protected file upload/download restrictions.

| ID | Scenario | Expected | Actual | Result |
| --- | --- | --- | --- | --- |
| AUTHZ-00b | QA Viewer role in DB | Viewer | Viewer | INFO |
| SEC-ANTI-1 | POST /JobPostings/Create with NO antiforgery token | Rejected; no record persisted | status=404; rows=0 | PASS |
| SEC-ANTI-2 | POST /JobPostings/Create with invalid (forged) antiforgery token | Rejected (4xx) and no record persisted | http=404; rows=0 | PASS |
| AUTHZ-01 | QA Viewer login | 302 to /Dashboard | status=302 loc=/Dashboard | PASS |
| AUTHZ-R1 | Viewer GET /HR | 200 (read-only allowed) | status=200 | PASS |
| AUTHZ-R2 | Viewer GET /Recruitment | 200 (read-only allowed) | status=200 | PASS |
| AUTHZ-R3 | Viewer GET /Offers | 200 (read-only allowed) | status=200 | PASS |
| AUTHZ-R4 | Viewer GET /Employees | 200 (read-only allowed) | status=200 | PASS |
| AUTHZ-R5 | Viewer GET /Performance | 200 (read-only allowed) | status=200 | PASS |
| AUTHZ-R6 | Viewer GET /Termination | 200 (read-only allowed) | status=200 | PASS |
| AUTHZ-R7 | Viewer GET /HRReports | 200 (read-only allowed) | status=200 | PASS |
| AUTHZ-R8 | Viewer GET /HRReports/Employees | 200 (read-only allowed) | status=200 | PASS |
| AUTHZ-R9 | Viewer /Employees page hides Add Employee action | No /Employees/Create link rendered | CreateLinkPresent=False | PASS |
| AUTHZ-B1 | Viewer GET /JobPostings/Create | 302 to /Error/NotFound | status=302 loc=/Error/NotFound | PASS |
| AUTHZ-B2 | Viewer GET /JobApplications/Create | 302 to /Error/NotFound | status=302 loc=/Error/NotFound | PASS |
| AUTHZ-B3 | Viewer GET /Offers/Create | 302 to /Error/NotFound | status=302 loc=/Error/NotFound | PASS |
| AUTHZ-B4 | Viewer GET /Employees/Create | 302 to /Error/NotFound | status=302 loc=/Error/NotFound | PASS |
| AUTHZ-B5 | Viewer GET /Performance/CreateCase | 302 to /Error/NotFound | status=302 loc=/Error/NotFound | PASS |
| AUTHZ-B6 | Viewer GET /Termination/Create | 302 to /Error/NotFound | status=302 loc=/Error/NotFound | PASS |
| AUTHZ-B7 | Viewer POST /Termination/Transition | 302 to /Error/NotFound (blocked server-side) | status=302 loc=/Error/NotFound | PASS |
| AUTHZ-B7b | Viewer POST /Employees/Create | Blocked; no employee created | postStatus=302 empBefore=0 empAfter=0 | PASS |
| AUTHZ-B8 | Viewer GET /JobApplications/DownloadCv/1 | 302 to /Error/NotFound or 404 | status=404 loc= | PASS |
| AUTHZ-B9 | Viewer GET /Users | 302 to /Error/NotFound | status=302 loc=/Error/NotFound | PASS |
| AUTHZ-B10 | Viewer GET /SystemSettings | 302 to /Error/NotFound | status=404 loc= | INFO |
| AUTHZ-A1 | Admin GET /JobPostings/Create | 200 | status=200 | PASS |
| AUTHZ-A2 | Admin GET /JobApplications/Create | 200 | status=200 | PASS |
| AUTHZ-A3 | Admin GET /Offers/Create | 200 | status=200 | PASS |
| AUTHZ-A4 | Admin GET /Employees/Create | 200 | status=200 | PASS |
| AUTHZ-A5 | Admin GET /Performance/CreateCase | 200 | status=200 | PASS |
| AUTHZ-A6 | Admin GET /Termination/Create | 200 | status=200 | PASS |



### Screen 2 - Recruitment (job postings)

Create/edit/publish/close/delete a job posting plus required-field, boundary, duplicate and invalid-id validation.

| ID | Scenario | Expected | Actual | Result |
| --- | --- | --- | --- | --- |
| REC-JP-01 | Create valid job posting | 302 to Details and row persisted | status=302 loc=/JobPostings/Details/8 id=8 | PASS |
| REC-JP-02 | Job posting persisted with skills + screening questions | Open\|future\|1\|Information Technology, skills=2, questions=2 | Open\|2026-11-12\|1\|Information Technology; skills=2; questions=2 | PASS |
| REC-JP-03 | Job posting with all required fields empty | 200 re-render, no row, required errors | status=200; rows 8->8; titleReq=True | PASS |
| REC-JP-04 | Job title shorter than 3 characters (boundary) | 200 with min-length error | status=200; err=True | PASS |
| REC-JP-05 | Job posting deadline = today (boundary) | 200 with future-date error | status=200; err=True | PASS |
| REC-JP-05b | Job posting max salary < min salary | 200 with salary-range error | status=200; err=True | PASS |
| REC-JP-06 | Job posting with no required skills | 200 with at-least-one-skill error | status=200; err=True | PASS |
| REC-JP-07 | Duplicate active job posting (same title + department) | 200 with duplicate error | status=200; err=True | PASS |
| REC-JP-08 | Edit job posting status Open -> Closed | 302 to Details and status=Closed | status=302 loc=/JobPostings/Details/8 dbStatus=Closed | PASS |
| REC-JP-09 | View job posting details | 200 and shows title + reference | status=200; hasTitle=True | PASS |
| REC-JP-10 | Re-open job posting for downstream application tests | 302 and status=Open | status=302 dbStatus=Open | INFO |



### Screen 2: Recruitment (applications and interviews)

Application submission and pipeline transitions, duplicate/future/format validation, non-existent ids, and interview scheduling/outcomes.

| ID | Scenario | Expected | Actual | Result |
| --- | --- | --- | --- | --- |
| APP-01 | Create valid application with CV upload | 302 to /JobApplications and row persisted | status=302 loc=/JobApplications id=5 email=qa.candidate.28ab8a2b@gamehub.local | PASS |
| APP-02 | Application persisted with CV metadata + job link | stored pdf, application/pdf, posting id + email match, file on disk | 95ef3d3b85eb45d89fb239151e0d9ebb.pdf\|application/pdf\|8\|qa.candidate.28ab8a2b@gamehub.local; fileOnDisk=True | PASS |
| APP-03 | Application without CV upload | 200 with required-CV error | status=200; err=True | PASS |
| APP-04 | Application with non-PDF file (resume.txt) | 200 with only-pdf error | status=200; err=True | PASS |
| APP-05 | Application with fake PDF (invalid magic bytes) | 200 with not-a-valid-PDF error | status=200; snip=de"> <span>CV / Resume (PDF) *</span> <input type="file" accept=".pd | PASS |
| APP-06 | Application with > 5 MB CV | 200 with size-limit error | status=200; err=True | PASS |
| APP-07 | Duplicate application (same email + posting) | 200 with duplicate error | status=200; err=True | PASS |
| APP-08 | Application with invalid phone number | 200 with phone validation error | status=200; err=True | PASS |
| APP-08b | Application with invalid CNIC (not 13 digits) | 200 with CNIC validation error | status=200; err=True | PASS |
| APP-09 | Application with future date of birth | 200 with DOB-future error | status=200; err=True | PASS |
| APP-09b | Application against nonexistent posting id | 200 with no-longer-available error | status=200; err=True | PASS |
| APP-10 | Application against a Draft (non-open) posting | 200 with not-accepting-applications error | status=200; err=True | PASS |
| INT-01 | Shortlist application | 302 and status=Shortlisted | status=302 dbStatus=Shortlisted | PASS |
| INT-02 | Schedule interview for shortlisted application | 302 to details, 1 interview row, status=Interview | status=302 loc=/JobApplications/Details/5 interviews=1 appStatus=Interview | PASS |
| INT-03 | Schedule interview with past date | 200 with past-date error | status=200; err=True | PASS |
| INT-04 | Forged create posting Completed + Pending outcome | 200 re-render, no extra interview row | status=200; interviews 1->1 | PASS |
| INT-05 | Edit interview marked Completed with Pending outcome | 200 with outcome-required error | status=200; err=True | PASS |
| INT-06 | UpdateInterview: Completed + Pending outcome blocked | 302 redirect and DB state unchanged (Scheduled\|Pending) | status=302 loc=/JobApplications/Details/5 db=Scheduled\|Pending | PASS |
| INT-07 | UpdateInterview: valid Completed + Passed outcome | 302 and DB Completed\|Passed | status=302 db=Completed\|Passed | PASS |



### Screen 3: Offers and hiring

Offer lifecycle (draft -> issued -> accepted -> hired), boundary and duplicate validation, and creation of the Employee record from an accepted offer (EMP-01..05).

| ID | Scenario | Expected | Actual | Result |
| --- | --- | --- | --- | --- |
| OFF-01 | Create offer from shortlisted application | 302 to /Offers/Details and Draft persisted | status=302 loc=/Offers/Details/1 id=1 | PASS |
| OFF-02 | Offer persisted as Draft with generated reference | Draft, reference present, salary 150000 | Draft\|OFF-1\|150000.00\|5 | PASS |
| OFF-03 | Offer expiry date = issue date (boundary) | 200 with expiry-must-be-later error | status=200; err=True | PASS |
| OFF-04 | Offer joining date before issue date | 200 with joining-before-issue error | status=200; err=True | PASS |
| OFF-05 | Second active offer for the same candidate | 200 with not-eligible (active offer) error | status=200; err=True | PASS |
| OFF-06 | Offer for a non-eligible legacy application | 200 with not-eligible error | status=200; err=True | PASS |
| OFF-07 | Issue draft offer | 302 and offer PendingAcceptance, application Offer | status=302 db=PendingAcceptance\|Offer | PASS |
| OFF-08 | Issue an already-issued offer (illegal transition) | 302 redirect, status unchanged (PendingAcceptance) | status=302 db=PendingAcceptance | PASS |
| OFF-09 | Accept issued offer | 302 and status Accepted | status=302 db=Accepted | PASS |
| OFF-10 | Accept an already-accepted offer (illegal transition) | 302, status unchanged (Accepted) | status=302 db=Accepted | PASS |
| OFF-11 | Action on nonexistent offer id | 404 Not Found | status=404 | PASS |
| EMP-01 | Create employee from accepted offer | 302 to /Employees/Details and row persisted | status=302 loc=/Employees/Details/1 id=1 | PASS |
| EMP-02 | Offer + application closed on hire (cross-module consistency) | Hired\|employeeId\|Hired | db=Hired\|1\|Hired; empId=1 | PASS |
| EMP-03 | Employee generated number + fields persisted | EMP-* \| Active \| FullTime \| joining date | EMP-000001\|Active\|FullTime\|2026-11-03 | PASS |
| EMP-04 | View employee details | 200 shows employee number | status=200; hasNum=True | PASS |
| EMP-05 | Create standalone employee (valid) | 302 and row persisted | status=302 id=2 | PASS |



### Screen 4: Employees (CRUD and status)

Employee create/edit/detail/archive, required-field and format validation, duplicate national-id/email constraints, invalid enum status transitions, search/sort/pagination and confirmation flows (CR-01..03).

| ID | Scenario | Expected | Actual | Result |
| --- | --- | --- | --- | --- |
| EMP-06 | Duplicate employee email | 200 with email-already-registered error | status=200; err=True | PASS |
| EMP-07 | Duplicate employee phone | 200 with phone-already-registered error | status=200; err=True | PASS |
| EMP-08 | Duplicate employee CNIC | 200 with CNIC-already-registered error | status=200; err=True | PASS |
| EMP-09 | Standalone employee with future joining date | 200 with joining-future error | status=200; err=True | PASS |
| EMP-10 | Employee with future date of birth | 200 with DOB-future error | status=200; err=True | PASS |
| EMP-11 | Employee age below 18 | 200 with at-least-18 error | status=200; err=True | PASS |
| EMP-12 | Employee with invalid phone number | 200 with phone validation error | status=200; err=True | PASS |
| EMP-13 | Employee with invalid CNIC (not 13 digits) | 200 with Pakistani-CNIC-13-digits error | status=200; err=True | PASS |
| EMP-14 | Change employee status Active -> OnLeave | JSON success=true and DB OnLeave | status=200; body={"success":true,"status":"On Leave","mes; db=OnLeave | PASS |
| EMP-15 | Change employee status with invalid enum value | JSON success=false | status=200; body={"success":false,"message":"Invalid empl | PASS |
| EMP-16 | Change status of nonexistent employee | JSON success=false (not found) | status=200; body={"success":false,"message":"Employee not found."} | PASS |
| EMP-17 | Edit employee (designation/salary/status) | 302 and DB updated | status=302; db=Senior QA Analyst\|123456.00\|OnLeave | PASS |
| EMP-18 | Employee list search by email | 200 and result contains the QA employee | status=200; hasEmp=True | PASS |
| EMP-18b | Employee list filter by designation | 200 and result contains the QA employee | status=200; hasEmp=True | PASS |
| EMP-19 | Employee list sort by name | 200 | status=200 | PASS |
| EMP-20 | Employee list filter status + department | 200 | status=200 | PASS |
| EMP-21 | Employee list pagination pageSize=5 | 200 | status=200 | PASS |
| EMP-22 | CheckEmail for an existing email | JSON available=false | {"available":false,"message":"Email addr | PASS |
| EMP-23 | CheckEmail for a new email | JSON available=true | {"available":true,"message":"Email is av | PASS |
| EMP-24 | CheckPhone for an existing phone | JSON available=false | {"available":false,"message":"Phone numb | PASS |
| EMP-25 | CheckNationalId for an existing CNIC | JSON available=false | {"available":false,"message":"National I | PASS |
| EMP-26 | Employee details for nonexistent id | 404 Not Found | status=404 | PASS |
| EMP-27 | Employee edit form for nonexistent id | 404 Not Found | status=404 | PASS |
| CR-01 | Hired application visible in recruitment list | 200 and contains candidate email | status=200; has=True | PASS |
| CR-02 | Hiring tab renders for HR | 200 | status=200 | PASS |
| CR-03 | HR dashboard renders after hire | 200 | status=200 | PASS |



### Screen 5: Performance and warnings

Performance cases, meetings, warnings and improvement plans across states, including invalid transitions, boundary dates, required fields, non-existent ids and state-machine guards.

| ID | Scenario | Expected | Actual | Result |
| --- | --- | --- | --- | --- |
| PERF-01 | Create performance case (valid) | 302 to Details and row persisted | status=302 loc=/Performance/Details/2 id=2 | PASS |
| PERF-02 | Case persisted as Open with generated number | Open\|PC-*\|Medium\|reported date | Open\|PC-2\|Medium\|2026-10-13 | PASS |
| PERF-03 | Create case with required fields empty | 200 with required-field errors | status=200; title=True; desc=True | PASS |
| PERF-04 | Create case with future reported date | 200 with reported-date-future error | status=200; err=True | PASS |
| PERF-05 | Create case with nonexistent employee | 200 with employee-not-found error | status=200; err=True | PASS |
| PERF-06 | Duplicate open case title for the same employee | 200 with duplicate-title error | status=200; err=True | PASS |
| PERF-07 | Create performance meeting | 302 and meeting persisted, case -> UnderReview | status=302 meetingId=2 caseStatus=UnderReview | PASS |
| PERF-08 | Meeting follow-up date before meeting date | 200 with follow-up-date error | status=200; err=True | PASS |
| PERF-08b | Meeting on nonexistent case id | rejected (200 re-render) and no row persisted | status=200; rowsForCase999999=0 | PASS |
| PERF-09 | Edit performance meeting (time 11:30, Completed) | 302 and DB Completed\|11:30:00 | status=302 db=Completed\|11:30:00.0000000 | PASS |
| PERF-10 | Delete performance meeting (soft delete) | 302 and IsActive=0 | status=302 isActive=0 | PASS |
| PERF-11 | Create formal warning | 302 and warning persisted, case -> Warning | status=302 warnId=2 caseStatus=Warning | PASS |
| PERF-12 | Duplicate identical warning on the same case | 200 with identical-warning error | status=200; err=True | PASS |
| PERF-13 | Warning with future issue date | 200 with issue-date-future error | status=200; err=True | PASS |
| PERF-14 | Edit formal warning (Issued -> Acknowledged) | 302 and DB Acknowledged | status=302 db=Acknowledged | PASS |
| PERF-15 | Delete formal warning (soft delete) | 302 and IsActive=0 | status=302 isActive=0 | PASS |
| PERF-15b | Create improvement plan | 302 and plan persisted as Active | status=302 planId=2 | PASS |
| PERF-16 | Second active improvement plan on the same case | 200 with active-plan-exists error | status=200; err=True | PASS |
| PERF-17 | Plan review date before start date | 200 with review-before-start error | status=200; err=True | PASS |
| PERF-18 | Plan end date before review date | 200 with end-before-review error | status=200; err=True | PASS |
| PERF-19 | Plan progress percent out of range (150) | 200 with progress-range error | status=200; err=True | PASS |
| PERF-20 | Edit improvement plan (progress 60, UnderReview) | 302 and DB UnderReview\|60 | status=302 db=UnderReview\|60 | PASS |
| PERF-21 | Delete improvement plan (soft delete) | 302 and IsActive=0 | status=302 isActive=0 | PASS |
| PERF-22 | Transition Warning -> Resolved (legal) | 302 and DB Resolved with ResolvedAt set | status=302 db=Resolved\|1 | PASS |
| PERF-23 | Transition Resolved -> ImprovementPlan (illegal) | 302 redirect, status unchanged (Resolved) | status=302 db=Resolved | PASS |
| PERF-24 | Transition Resolved -> Closed (legal) | 302 and DB Closed with ClosedAt set | status=302 db=Closed\|1 | PASS |
| PERF-25 | Transition with invalid status value (99) on a Closed case | 302 redirect, case stays Closed (not silently reopened) | status=302 db=Closed | PASS |
| PERF-26 | Transition Closed -> Open (reopen) | 302 and DB Open with terminal timestamps cleared | status=302 db=Open\|null\|null | PASS |
| PERF-27 | Transition on nonexistent case id | 404 Not Found | status=404 | PASS |
| PERF-28 | Performance case details page | 200 and shows the QA case title | status=200; hasCase=True | PASS |
| PERF-29 | Performance index with status/severity/search/sort filters | 200 | status=200 | PASS |
| PERF-30 | Warnings and plans tabs with filters | 200 / 200 | warnings=200 plans=200 | PASS |
| PERF-31 | Edit performance case (title + severity High) | 302 and DB updated | status=302 db=QA Performance Case 751072 (updated)\|High | PASS |
| PERF-32 | Performance details with nonexistent id | 404 Not Found | status=404 | PASS |



### Screen 6: Termination and clearance

Termination request lifecycle (Draft -> PendingApproval -> Approved -> InProgress -> Completed), rejection rules, mandatory clearance enforcement, letter generation/download/preview, protected uploads and illegal-transition guards.

| ID | Scenario | Expected | Actual | Result |
| --- | --- | --- | --- | --- |
| TRM-01 | Create termination request (valid) | 302 to Details and Draft persisted | status=302 loc=/Termination/Details/2 id=2 | PASS |
| TRM-02 | Request persisted as Draft with TRM reference | Draft\|TRM-*\|2 | Draft\|TRM-1\|2 | PASS |
| TRM-03 | Create request with required fields empty | 200 with required-field errors | status=200; reason=True; explanation=True; date=True | PASS |
| TRM-04 | Proposed last working day before request date | 200 with last-working-day error | status=200; err=True | PASS |
| TRM-05 | Create request for nonexistent employee | 200 with select-employee error | status=200; err=True | PASS |
| TRM-06 | Second active request for the same employee | 200 with already-active-request error | status=200; err=True | PASS |
| TRM-07 | Edit termination request (type + remarks + date) | 302 and DB updated | status=302 db=Dismissal\|Updated by QA.\|2026-11-27 | PASS |
| TRM-08 | Transition Draft -> Approved (illegal) | 302 redirect, status unchanged (Draft) | status=302 db=Draft | PASS |
| TRM-09 | Transition Draft -> PendingApproval (legal) | 302 and status PendingApproval, history recorded | status=302 db=PendingApproval history=2 | PASS |
| TRM-10 | Reject without a reason (guarded) | 302 redirect, status unchanged (PendingApproval) | status=302 db=PendingApproval | PASS |
| TRM-11 | Transition PendingApproval -> Approved (seeds clearance) | 302, Approved, DecisionAt set, 5 clearance items | status=302 db=Approved\|1 items=5 | PASS |
| TRM-12 | Transition Approved -> Completed (skips InProgress, illegal) | 302 redirect, status unchanged (Approved) | status=302 db=Approved | PASS |
| TRM-13 | Transition Approved -> InProgress (legal) | 302 and status InProgress | status=302 db=InProgress | PASS |
| TRM-14 | Complete with outstanding mandatory clearance (guarded) | 302 redirect, status unchanged (InProgress) | status=302 db=InProgress | PASS |
| TRM-15 | Transition with invalid status value (99) | 302 redirect, status unchanged (InProgress) | status=302 db=InProgress | PASS |
| TRM-16 | Clearance update with invalid status value (99) | 302 redirect, item unchanged (Pending) | status=302 db=Pending | PASS |
| TRM-16b | Clear all mandatory clearance items | remaining uncleared mandatory items = 0 | remaining=0 items=6,7,8,9,10 | PASS |
| TRM-17 | Transition InProgress -> Completed (all clearance cleared) | 302, Completed, employee Inactive | status=302 req=Completed\|1 emp=Inactive\|1 | PASS |
| TRM-18 | Generate termination letter | 302 and letter document stored | status=302 letterId=3 | PASS |
| TRM-19 | Download generated letter | 200 PDF | status=200 type=application/pdf | PASS |
| TRM-20 | Preview termination letter | 200 (PDF inline) | status=200 type=application/pdf | PASS |
| TRM-20b | Upload a non-PDF supporting document | 302 redirect and no document persisted | status=302 before=0 after=0 | PASS |
| TRM-20c | Upload an oversized (>5MB) supporting document | 302 redirect and no document persisted | status=302 after=0 | PASS |
| TRM-21 | Upload a valid PDF supporting document | 302 and document persisted | status=302 docId=4 | PASS |
| TRM-22 | Download supporting document (protected) | 200 PDF | status=200 type=application/pdf | PASS |
| TRM-23 | Delete supporting document | 302 and document removed | status=302 remaining=0 | PASS |
| TRM-24 | Seed clearance again on a request that already has a checklist | 302 and item count unchanged (5) | status=302 items=5 | PASS |
| TRM-25 | Open clearance create form for a Completed request | 302 redirect (clearance locked after completion) | status=302 loc=/Termination/Details/2 | PASS |
| TRM-26 | Forge a clearance create on a Completed request | 302 redirect and item count unchanged (5) | status=302 items=5 | PASS |
| TRM-26b | Transition on nonexistent request id | 404 Not Found | status=404 | PASS |
| TRM-27 | Termination details page | 200 and shows the TRM reference | status=200; hasRef=True | PASS |
| TRM-28 | Termination index with status/type/search/sort filters | 200 | status=200 | PASS |
| TRM-29 | Termination details with nonexistent id | 404 Not Found | status=404 | PASS |



### Screen 1 (HR Dashboard) and Screen 7 (HR Reports)

Dashboard KPIs vs live DB, report dashboard, four detail reports, filters (preset/date/department/status/category/search), sorting, pagination clamping, CSV export correctness, invalid report key handling and Viewer/anonymous access.

| ID | Scenario | Expected | Actual | Result |
| --- | --- | --- | --- | --- |
| HRD-01 | HR dashboard loads | 200 | status=200 | PASS |
| HRD-02 | Dashboard renders all six tiles | 6/6 | 6/6 | PASS |
| HRD-03 | Dashboard KPIs match live DB | 2,1,7,3,1,6 | Total Employees=2(want 2); Active Employees=1(want 1); Open Positions=7(want 7); Pending Applications=3(want 3); Upcoming Interviews=1(want 1); Pending HR Actions=6(want 6) | PASS |
| HRD-04 | Dashboard sections render | Recent Recruitment Activity + Pending HR Tasks | recent=True; tasks=True | PASS |
| HRD-05 | Unauthenticated HR dashboard redirects to login | 302 -> login | status=302 loc=/Auth/Login?returnUrl=%2FHR%2FIndex | PASS |
| HRD-06 | Viewer (read-only role) can load HR dashboard | 200 | status=200 | PASS |
| HRE-01 | HR Reports dashboard loads | 200 | status=200 | PASS |
| HRE-02 | Dashboard links to all four reports | 4/4 | 4/4 | PASS |
| HRE-03 | Dashboard charts + JSON payload render | 6/6 canvases + JSON | canvases=6/6; json=True | PASS |
| HRE-04 | Reports dashboard KPIs match live DB | 2,1,1,8,5,0,1,2,0,0,1 | Total Employees=2(want 2); Active Employees=1(want 1); Inactive Employees=1(want 1); Job Openings=8(want 8); Applications=5(want 5); Pending Offers=0(want 0); Accepted Offers=1(want 1); Open Performance Cases=2(want 2); Active Warnings=0(want 0); Pending Terminations=0(want 0); Completed Terminations=1(want 1) | PASS |
| HRE-05 | Recruitment report loads with columns + QA row | 200, title, JOB-000008, kpi=8 | status=200; title=True; qa=True; kpi=8 | PASS |
| HRE-06 | Recruitment status=Open filter applied | 7 openings, no JOB-000003 | kpi=7 draftRow=False | PASS |
| HRE-07 | Recruitment status=Draft filter applied | 1 opening, JOB-000003 present | kpi=1 draft=True | PASS |
| HRE-08 | Recruitment department filter applied | 4 | kpi=4 | PASS |
| HRE-09 | Recruitment employment-type filter applied | 1 | kpi=1 | PASS |
| HRE-10 | Recruitment search matches job title | JOB-000008 present, unrelated absent | hit=True; noise=False | PASS |
| HRE-10b | Recruitment invalid enum filter ignored | 8 | kpi=8 | PASS |
| HRE-11 | Recruitment preset=last-year (2025) returns empty filtered state | 200 + No Matching Records + 0 openings | status=200; empty=True; kpi=0 | PASS |
| HRE-12 | Recruitment custom range with inverted dates swapped | 200 + records present | status=200; hasRows=True | PASS |
| HRE-13 | Recruitment pagination summary | 1-8 of 8 records | 1-8 of 8 records | PASS |
| HRE-14 | Recruitment invalid page/pageSize clamped | 200 + first page | status=200; showing=1-8 of 8 records | PASS |
| HRE-15 | Recruitment sortBy=oldest orders oldest first | first row JOB-000001 | JOB-000001 first | PASS |
| HRE-16 | Recruitment sortBy=name orders A-Z | first row Accounts Executive | Accounts Executive first | PASS |
| HRE-17 | Employees report loads (future joiners excluded by range) | 200, kpi=1, EMP-000002 | status=200; kpi=1; qa=True | PASS |
| HRE-18 | Employees KPI Inactive matches DB | 1 | 1 | PASS |
| HRE-19 | Employees status=Active excludes future-dated active hire | empty filtered state | status=200; empty=True; kpi=0 | PASS |
| HRE-20 | Employees status=Inactive returns QA employee | 1 row, EMP-000002 | kpi=1; present=True | PASS |
| HRE-21 | Employees search by employee number | 1 row | kpi=1 | PASS |
| HRE-22 | Employees search no match -> filtered empty state | No Matching Records | status=200; empty=True | PASS |
| HRE-23 | Employees department filter | 1 row | kpi=1 | PASS |
| HRE-24 | Employees invalid paging/filter values handled | 200 + first page | status=200; showing=1-1 of 1 records | PASS |
| HRE-25 | Performance report loads with QA cases | total=2, PC-1, PC-2 | status=200; total=2; pc1=True; pc2=True | PASS |
| HRE-26 | Performance child KPIs match live DB | warnings=0 plans=0 meetings=0 | warnings=0 plans=0 meetings=0 | PASS |
| HRE-27 | Performance status=Closed (none) -> filtered empty state | No Matching Records | status=200; empty=True | PASS |
| HRE-28 | Performance issue-type filter | 2 rows | kpi=2 | PASS |
| HRE-29 | Performance department filter | 2 rows | kpi=2 | PASS |
| HRE-30 | Termination report loads with QA request | total=1 completed=1, TRM-1 | status=200; total=1; completed=1; qa=True | PASS |
| HRE-31 | Termination clearance completion % matches DB | 100.0% | 100.0% | PASS |
| HRE-32 | Termination status=Completed returns request | 1 row, TRM-1 | kpi=1; qa=True | PASS |
| HRE-33 | Termination status=Rejected (none) -> filtered empty state | No Matching Records | status=200; empty=True | PASS |
| HRE-34 | Termination search by reference | 1 row | kpi=1 | PASS |
| HRE-35 | Export Employees CSV (content type, filename, header, data) | 200 text/csv GameHub-HR-employees-*.csv + "Employee","Number" + EMP-000002 | status=200; type=text/csv; file=GameHub-HR-employees-20261013.csv; hdr=True; data=True | PASS |
| HRE-36 | Export Recruitment CSV | 200 + reference header + JOB-000008 | status=200; file=GameHub-HR-recruitment-20261013.csv; hdr=True; qa=True | PASS |
| HRE-37 | Export Performance CSV | 200 + Case header + PC-1 | status=200; file=GameHub-HR-performance-20261013.csv; hdr=True; qa=True | PASS |
| HRE-38 | Export Termination CSV | 200 + header + TRM-1 | status=200; file=GameHub-HR-termination-20261013.csv; hdr=True; qa=True | PASS |
| HRE-39 | Export respects filters (Active -> header only) | 200 + header, no EMP-000002 | status=200; hdr=True; data=False | PASS |
| HRE-40 | Export with unknown report key rejected | 302 -> /Error/NotFound | status=302; loc=/Error/NotFound | PASS |
| HRE-41 | Viewer (read-only role) can load every HR report + export | 200 for 6 endpoints | all 6 returned 200 | PASS |
| HRE-42 | Unauthenticated HR Reports redirects to login | 302 -> login | status=302 loc=/Auth/Login?returnUrl=%2FHRReports | PASS |



### Cross-module consistency (recruitment -> offer -> employee; termination -> employee; screen parity)

Explicit consistency checks across modules and between the two dashboards/reports and the live database, executed before test-data cleanup.

| ID | Scenario | Expected | Actual | Result |
| --- | --- | --- | --- | --- |
| XM-01 | Offer -> Hired application -> Active employee chain intact | App5=Hired, Offer1=Hired(app5), Emp1=Active EMP-000001 (same email) | app5=Hired; offer1=Hired(app 5); emp1=EMP-000001/Active; emailMatch=True | PASS |
| XM-02 | Completed termination marks employee Inactive; clearance fully cleared | TRM-1=Completed, Emp2=Inactive, 5/5 cleared | trm=Completed; emp2=Inactive; cleared=5/5 | PASS |
| XM-03 | Termination report shows completed QA request with clearance + letter | 200, TRM-1 row, 5/5 cleared, letter Available | status=200; ref=True; cleared=True; letter=True | PASS |
| XM-03b | Termination report clearance % == DB | 100.0% | 100.0% | PASS |
| XM-04 | Employee totals agree across Screen 1, Screen 7 and DB | DB=2, Screen1=2, Screen7=2 | DB=2; Screen1=2; Screen7=2 | PASS |
| XM-05 | Application totals agree across Screen 7 dashboard, recruitment report and DB | DB=5 | DB=5; dash=5; recruitment=5 | PASS |
| XM-06 | Open performance cases agree across Screen 7 dashboard/report and DB | DB=2; report total=2 (all open) | DB=2; dash=2; report=2 | PASS |
| XM-07 | Job opening totals agree across Screen 7 dashboard/report and DB | DB=8 | DB=8; dash=8; recruitment=8 | PASS |


## 4. Validation and authorization results

### 4.1 Required fields, formats, boundaries and duplicates
* **Required fields** - every create/edit form was submitted empty and returned the form with expected field-level errors and **no** persisted record (job postings, applications, offers, employees, performance cases/meetings/warnings/plans, termination requests).
* **Format validation** - invalid email/phone/national-id/salary/date inputs were rejected with validation messages (e.g. `NationalIdValidator`, `EmailAddress`, `Range`); numeric fields rejected non-numeric and negative values.
* **Boundary and invalid date combinations** - future dates were rejected where the domain requires past/today (application date, offer dates, proposed last-working-day before the request date, interview times). Because HR logic uses `DateTime.UtcNow`, UTC dates were used for "today" to avoid false "future" rejections.
* **Duplicates / unique constraints** - duplicate national-id and email on employees, a second active termination request for the same employee, and duplicate application checks were blocked with a validation message and no second row written.
* **Invalid / non-existent ids** - detail/edit/transition endpoints with unknown ids returned `404 Not Found` (after the app's status-code re-execute) and never mutated data.
* **Invalid enum values** - posting invalid numeric/status values through the forms was rejected and left state unchanged. Defects D-1 and D-2 were raised and fixed here (see section 7).

### 4.2 Authorization and roles
* **SuperAdmin/Admin** - full access confirmed across all seven screens.
* **Viewer (read-only)** - could view list/detail/report pages, but management controls (Add/Edit/Delete) were not rendered, direct GETs to management endpoints were blocked (`AUTHZ-R*`), and direct state-changing POSTs were blocked. No POST succeeded for a Viewer.
* **Unauthenticated** - every protected route redirected to `/Auth/Login?returnUrl=...` (HTTP 302).
* **Anti-forgery** - state-changing POSTs with a missing or invalid token were rejected and produced no database write (`SEC-ANTI-1/2` and each suite's omit-token checks).

### 4.3 File upload and document protection
* Application CVs, offer letters and termination documents enforce PDF magic-byte checks and a 5 MB size limit; a non-PDF and an oversized file were both rejected with no document row stored.
* Generated/stored documents (CVs, offer letters, termination letters, supporting documents) are only downloadable through authenticated, authorized endpoints.

## 5. Full workflow results

| Workflow | Path exercised | Result |
| --- | --- | --- |
| Recruitment | create posting -> publish -> receive applications -> schedule interviews -> decide outcomes -> close | PASS |
| Offer and hire | application `Hired` -> create offer -> issue -> accept -> record joining -> auto-create Employee | PASS |
| Employee lifecycle | create -> edit -> change status (incl. `OnLeave`) -> archive; validation and duplicates enforced | PASS |
| Performance | raise case -> meeting -> warning -> improvement plan -> transitions -> resolve/close (with guards) | PASS |
| Termination | Draft -> PendingApproval -> (reject path) -> Approved -> InProgress -> Completed; mandatory clearance gate; letter generation; employee set `Inactive` | PASS |
| Reports | dashboard KPIs -> four reports -> filter/sort/paginate -> CSV export -> print view | PASS |

All valid transitions succeeded and were reflected in the database (including status-history/audit rows). All illegal transitions were rejected and left state unchanged.

## 6. Database persistence and cross-module consistency

* Every create/update was re-read from `GameHubFYPDb` with `sqlcmd` to confirm persistence (not just a success toast). This is how defect D-3 (missing `SaveChangesAsync`) was found.
* Status values are persisted as strings and matched the enum workflow.
* Audit/history was preserved: termination status-history rows and employee/offer status fields tracked each transition; app soft-deletes archive rows (`IsActive = 0`) rather than removing them.
* **Cross-module chain verified end-to-end** - Recruitment -> Offer -> Employee:
  * `JobApplication` `APP-000005` = `Hired`; `JobOffer` `OFF-1` = `Hired` linked to application 5; `Employee` `EMP-000001` created `Active` with the same email.
  * `TerminationRequest` `TRM-1` = `Completed`; `Employee` `EMP-000002` = `Inactive`; 5/5 clearance items `Cleared`; the HR Reports "Clearance Completion" showed `100.0%`.
* **Screen parity** - the HR Dashboard, the HR Reports dashboard and the detail reports all agreed with each other and with the live database for employee, application, job-opening and performance-case totals (XM-04..XM-07).

## 7. Defects found and fixed

| ID | Severity | Affected file | Symptom | Fix | Retest |
| --- | --- | --- | --- | --- | --- |
| **D-1** | Medium | `Controllers/EmployeesController.cs` (`ChangeStatus`) | An invalid/undefined enum value silently set the employee status to `Active` instead of being rejected. | Added `!ModelState.IsValid \|\| !Enum.IsDefined(...)` guard (toast + redirect) before applying the change. | EMP-15 re-run -> PASS |
| **D-2** | Medium | `Controllers/PerformanceController.cs` (`Transition`) | An invalid enum value silently reopened a `Closed` performance case, bypassing the state machine. | Added `if (!ModelState.IsValid)` guard (toast + redirect) before the transition. | PERF-25 re-run -> PASS |
| **D-3** | High | `Controllers/TerminationController.cs` (six actions) | Clearance/document mutations returned a success toast but were never saved - no `UPDATE`/`DELETE` reached the database (silent data loss). | Added `await _dbContext.SaveChangesAsync();` after `TouchRequestAsync` in `CreateClearance` (POST), `EditClearance` (POST), `UpdateClearanceStatus`, `DeleteClearance`, `SeedClearance` and `DeleteDocument`. An audit of the other HR controllers found no other missing saves. | Full Screen 6 suite re-run -> 33/33 PASS |

All three fixes are confined to the HR module and remain uncommitted on `feature/hr-module`.

### 7b. Observation (not a defect)
On Screen 7, any date preset other than "all" (e.g. `preset=last-year`) counts as an active filter, so an empty result shows **"No Matching Records"** rather than **"No Records Yet"**. This is intentional (`HasActiveFilters` treats a non-default preset as a filter) and affects wording only.

## 8. Remaining blockers, limitations and known issues

* **No blockers.** All executed cases passed.
* **Screenshots** were intentionally not captured, per instruction; evidence is HTTP/DB/HTML based.
* **Anti-forgery status codes**: because the app re-executes 4xx responses through `/Error/NotFound`, an anti-forgery rejection reaches the client as `404` rather than `400`. Tests assert the meaningful effect - rejected **and** nothing persisted.
* **Soft-delete convention**: the app archives via `IsActive = 0`; a terminated employee keeps `IsActive = 1` with `EmploymentStatus = Inactive` (by design). Report queries count active rows only.
* **Test-data cleanup** (section 11) removes the QA records after the run, so the live database no longer contains the tested records; the raw logs are the retained evidence.

## 9. Totals

| Metric | Count |
| --- | --- |
| Total test cases | 249 |
| Passed | 246 |
| Failed | 0 |
| Blocked | 0 |
| Not run | 0 |
| Informational (no pass/fail assertion) | 3 |
| Defects found | 3 |
| Defects fixed and retested | 3 |
| Open defects | 0 |

## 10. Overall verdict

All executed cases passed (246 PASS, 0 FAIL, 0 BLOCKED, 0 not-run; 3 informational observations). All seven HR screens were exercised end-to-end over HTTP against the running app and the `GameHubFYPDb` database, covering required fields, formats, boundaries, duplicate/unique constraints, invalid ids, role permissions (Viewer read-only), anti-forgery, upload/download protection, full workflows, pagination/sorting/filtering, and cross-module consistency. Three defects were found - including a high-severity silent-persistence bug - fixed within the HR module and re-tested successfully. The module is ready for review once the fixes are committed.

## 11. Test-data cleanup

After the run, QA-created records and files were removed where ownership was certain: dependent rows were deleted first in FK order inside a transaction, then the stored files were removed.

**Removed (QA-created):**
* `Employees` Id 1 (`EMP-000001`, created from an accepted offer) and Id 2 (`EMP-000002`).
* `JobPostings` Id 8 (`JOB-000008`, "QA Recruitment E2E ...").
* `JobApplications` Id 3, 4, 5 (plus cascade children: `Interviews`, `JobApplicationDocuments`).
* `JobOffers` Id 1 (plus `JobOfferDocuments`).
* `PerformanceCases` Id 1, 2 (plus cascade children `PerformanceMeetings`, `PerformanceWarnings`, `ImprovementPlans`).
* `TerminationRequests` Id 2 (`TRM-1`) plus cascade children (`ClearanceItems`, `TerminationStatusHistories`, `TerminationDocuments`).
* Stored files: the three QA CV PDFs under `Storage/hr/cvs` and the generated termination letter under `Storage/hr/terminations`.

**Deliberately retained (documented):**
* Pre-existing job postings `JOB-000001..000007`, their screening/skill rows, and pre-existing applications `APP-000001`/`APP-000002` with their CVs and document.
* The QA Viewer account `qa.viewer.hrmodule@gamehub.local` (kept so the read-only authorization tests stay reproducible).

**Post-cleanup verification (`sqlcmd`):** `Employees = 0`, `JobOffers = 0`, `PerformanceCases = 0`, `TerminationRequests = 0`, `ClearanceItems = 0`, `TerminationDocuments = 0`, `Interviews = 0`; `JobPostings = 7`, `JobApplications = 2` (pre-existing only). The app stayed healthy (`/Auth/Login` -> `200`).

## Appendix - reproducing the run

The harness lives outside the repository (temporary test-support code): a shared `GHLib.ps1` (auth, anti-forgery, form/array/multipart POST, `sqlcmd` helpers, result logging) plus `01-smoke.ps1`, `02-auth.ps1`, `03-recruitment.ps1`, `04-application.ps1`, `05-offer-employee.ps1`, `06-employee.ps1`, `07-performance.ps1`, `08-termination.ps1`, `09-hr-dashboard-reports.ps1` and `10-crossmodule.ps1`. Each writes lines of the form `[PASS|FAIL|BLOCK|INFO] ID :: Scenario | exp=... | act=...`. The execution order is 01 through 10, and the database must be in the expected state for the data-dependent suites.

