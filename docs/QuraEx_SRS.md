**QuraEx**

AI-Powered Test Case Generation Platform

**SOFTWARE REQUIREMENTS SPECIFICATION (Consolidated Product Specification)**

*Prepared in accordance with IEEE 830 / ISO/IEC/IEEE 29148*

Version 2.0

Date: 17 June 2026

*Status: Baseline — consolidated SRS + architecture & data overview*

> This document is the single authoritative reference to understand the whole product:
> what it does (requirements), how it is decomposed (services), and what each service's
> database stores. Pure system design lives in `docs/QuraEx_Architecture.md`; flow
> walkthroughs with diagrams live in `docs/service-flows-explainer.md`. This SRS folds in
> just enough of both to be read standalone.

# Revision History

| **Version** | **Date** | **Author** | **Description** |
| --- | --- | --- | --- |
| 0.1 | 2026-05-20 | Project team | Initial draft, scope and actors |
| 1.0 | 2026-06-03 | Project team | Complete functional and non-functional requirements |
| 2.0 | 2026-06-17 | Project team | Consolidated product spec: TOC, core use cases detailed, per-service data requirements (every table), flow↔service map, business rules, failure behaviour, architecture mapping |

# Table of Contents

1. Introduction — 1.1 Purpose · 1.2 Scope · 1.3 Definitions · 1.4 References
2. Overall Description — 2.1 Product Perspective · 2.2 User Classes · 2.3 Operating Environment · 2.4 Assumptions · 2.5 Constraints · 2.6 Architecture at a Glance
3. Use Cases — 3.1 Summary · 3.2 UC-03 Invite Members · 3.3 UC-04 Author Story · 3.4 UC-06 Generate Test Cases · 3.5 UC-09 Execute Test Run · 3.6 UC-11 Synchronize with Jira
4. Functional Requirements — 4.1 Identity · 4.2 Workspace · 4.3 Authoring · 4.4 AI Generation · 4.5 Test Artifact · 4.6 Execution & Defects · 4.7 Integration · 4.8 Notification
5. Non-Functional Requirements — 5.1 Performance · 5.2 Scalability · 5.3 Security · 5.4 Privacy · 5.5 Usability · 5.6 Maintainability
6. External Interface Requirements
7. Requirements Traceability Matrix
8. Data Requirements — 8.1 Cross-cutting patterns · 8.2–8.9 Per-service databases (every table) · 8.10 Stateful entity lifecycles
9. Business Rules
10. System Behaviour and Failure Modes
11. Flow ↔ Service Relationship Map
- Appendix A. Architecture & Decision Mapping (FR/NFR → service, locked decisions)

# 1. Introduction

## 1.1 Purpose

This Software Requirements Specification (SRS) defines the functional and non-functional requirements for QuraEx, an AI-powered platform that generates software test cases from user stories. This document describes what the system must do and the constraints it must satisfy. Version 2.0 consolidates the requirements with a service decomposition and per-service data overview so it can be read as a single product reference. Pure technical design (internal class structure, deployment topology) remains in the companion Software Design Document / Architecture document. The intended audience is the project supervisor (for evaluation) and the development team (as the authoritative requirements baseline).

## 1.2 Scope

QuraEx enables QA engineers and product teams to author user stories with acceptance criteria, then automatically generate, refine, organize, and execute test cases using large language models. The system covers the full lifecycle from requirement authoring to test execution and defect tracking, and integrates two-way with external issue trackers such as Jira.

In scope: user and workspace management, authorization, user-story authoring, AI-assisted generation of acceptance criteria and test cases, test artifact management (suites, plans, runs, results, defects), automated test execution, external tracker integration, and notifications.

Out of scope: low-level internal design of each microservice; manual exploratory testing workflows; billing or commercial licensing features.

## 1.3 Definitions, Acronyms and Abbreviations

| **Term** | **Definition** |
| --- | --- |
| User Story | A short description of a feature from an end-user perspective, in the form 'As a..., I want..., so that...'. |
| Acceptance Criteria (AC) | Conditions a user story must satisfy to be accepted; may be hierarchical. |
| Test Case | A set of preconditions, steps and expected results that verifies a specific behaviour. |
| Test Suite | A named collection of test cases grouped by theme or feature. |
| Test Plan | A strategy document describing scope, objectives and risks of testing for a project. |
| Test Run | A single execution of a suite at a point in time. |
| Defect | A recorded discrepancy between expected and actual behaviour. |
| LLM | Large Language Model, the AI used to generate content. |
| FR / NFR | Functional Requirement / Non-Functional Requirement. |
| ISTQB | International Software Testing Qualifications Board, the terminology standard followed. |
| Outbox | A table written in the same DB transaction as business data, later relayed as an event (solves dual-write). |
| Idempotency | Property that processing the same event twice has no extra effect (via a processed-message table). |
| Snapshot / Read-model | A local copy of another service's data, updated from its events; never read cross-database. |
| Saga | A long-running process coordinating steps across services, with compensation on failure. |
| ACL | Anti-Corruption Layer — isolates external models (e.g. Jira) from the internal domain. |

## 1.4 References

- IEEE Std 830-1998 — Recommended Practice for Software Requirements Specifications.
- ISO/IEC/IEEE 29148:2018 — Requirements engineering.
- ISTQB Foundation Level Syllabus — testing terminology.
- `docs/QuraEx_Architecture.md` — system design (companion).
- `docs/service-flows-explainer.md` — flow walkthroughs with sequence/state diagrams.
- `docs/database/quraex.dbml` — master database schema (source of truth).

# 2. Overall Description

## 2.1 Product Perspective

QuraEx is a new, self-contained web-based product. It exposes a web frontend and mobile application backed by a service-oriented backend of eight business microservices behind a single gateway. It interacts with external large language model providers and external issue trackers (e.g. Jira) but does not depend on any pre-existing in-house system. The product replaces ad-hoc, manual test-case writing with an assisted, traceable workflow.

## 2.2 User Classes and Characteristics

| **Actor / User class** | **Description** | **Technical level** |
| --- | --- | --- |
| Workspace Owner | Creates and owns a workspace; manages workspace-level settings and members. | Medium |
| Workspace Admin | Manages projects and members within a workspace. | Medium |
| Project Editor | Authors user stories, generates and edits test cases, runs tests. | Medium to high |
| Project Viewer | Read-only access to project artifacts. | Low |
| QA Engineer | Primary user generating and executing test cases. | High |
| System Administrator | Operates and monitors the platform. | High |
| External System (Jira) | Automated actor exchanging issues and updates via integration. | N/A |
| LLM Provider | External AI service generating content on request. | N/A |

## 2.3 Operating Environment

- Client: modern web browsers (Chrome, Edge, Firefox, Safari, latest two major versions) and a mobile application.
- Server: cloud-hosted backend (target: AWS) reachable over HTTPS through a single public gateway at quraex.com.
- External dependencies: at least one LLM provider (third-party first; self-hosted optional) and, optionally, a Jira instance.

## 2.4 Assumptions and Dependencies

- Users have authenticated accounts and stable internet access.
- At least one LLM provider is reachable; if all providers are unavailable, AI generation features are degraded but the rest of the system remains usable.
- Jira integration requires the customer to provide valid OAuth credentials and webhook configuration.
- Generated test cases are suggestions; a human reviews and approves them before they are considered authoritative.

## 2.5 Constraints

- All external access occurs through a single gateway; internal services are not publicly reachable.
- The system must follow ISTQB terminology for all testing concepts.
- Personal data handling must comply with GDPR-like principles (Section 5.4).
- AI generation latency depends on external providers and is therefore asynchronous with progress feedback.
- Each service owns its own database; no service reads another service's database directly.

## 2.6 Architecture at a Glance

QuraEx is decomposed into **eight business services** behind a single **API gateway**, communicating mostly through asynchronous events and occasionally through synchronous gRPC.

| # | Service | Responsibility (one line) | Database |
| --- | --- | --- | --- |
| 1 | Identity | Authentication — who you are (user, JWT, OAuth, 2FA). | PostgreSQL |
| 2 | Workspace | Authorization — what you may do, per project (membership, roles, invitations). | PostgreSQL |
| 3 | Authoring | User stories, acceptance criteria, business rules. | PostgreSQL |
| 4 | TestArtifact | Test cases, suites, plans, runs, results, defects. | PostgreSQL |
| 5 | AI Generation | LLM router + saga that generates ACs and test cases. | PostgreSQL + Redis |
| 6 | Execution | Runs automated tests (Playwright) and stores artifacts. | PostgreSQL + Object storage |
| 7 | Integration (Jira) | Two-way Jira sync via an Anti-Corruption Layer. | PostgreSQL |
| 8 | Notification | Fan-out in-app / email notifications. | MongoDB |

Three patterns repeat in every service and explain why their databases look similar: **Outbox** (`*_outbox_message`), **Idempotency** (`*_processed_message`), and **Read-model/Snapshot** (`*_snapshot`). These are detailed in Section 8.1. The full table-by-table data requirements are in Sections 8.2–8.9, and which services collaborate in each flow is mapped in Section 11.

# 3. Use Cases

This section lists the principal use cases and provides detailed specifications for the five most critical flows. Each use case maps to functional requirements in Section 4 and to a flow in Section 11.

## 3.1 Use Case Summary

| **ID** | **Use Case** | **Primary Actor** | **Detailed?** |
| --- | --- | --- | --- |
| UC-01 | Register and authenticate | User | summary |
| UC-02 | Create workspace and project | Workspace Owner | summary |
| UC-03 | Invite and manage members | Workspace Admin | §3.2 |
| UC-04 | Author user story and acceptance criteria | Project Editor | §3.3 |
| UC-05 | Refine user story with AI | Project Editor | summary |
| UC-06 | Generate test cases with AI | QA Engineer | §3.4 |
| UC-07 | Review, edit and approve test cases | QA Engineer | summary |
| UC-08 | Organize test cases into suites and plans | QA Engineer | summary |
| UC-09 | Execute a test run (manual or automated) | QA Engineer | §3.5 |
| UC-10 | Record results and raise defects | QA Engineer | summary |
| UC-11 | Synchronize with Jira | Project Editor / External System | §3.6 |

## 3.2 Detailed Use Case: UC-03 Invite and Manage Members

| **Field** | **Description** |
| --- | --- |
| Actor | Workspace Admin (primary), Invitee (secondary) |
| Services involved | Workspace (owner), Notification |
| Precondition | A project exists and the actor has Admin/Owner rights on the workspace. |
| Trigger | Admin invites a person by email to a project. |
| Main flow | 1. Admin submits an email and target role. 2. System creates an invitation (status Pending), stores a hashed token, transitions the saga to AwaitingResponse. 3. System emits MembershipChanged; Notification sends an invitation email with the token. 4. Invitee opens the link and accepts within 7 days. 5. System validates the token, transitions to Accepted, creates the project membership, emits MembershipChanged again. |
| Alternative flow | 4a. Invitee declines → Declined. 4b. No response within 7 days → saga times out → Expired (compensation: invitation cancelled, expiry email sent). 4c. Admin revokes before acceptance → Revoked. |
| Postcondition | Either a new project member exists (Accepted) or the invitation reached a terminal non-member state. |
| Related FRs | FR-07, FR-08, FR-09 |

## 3.3 Detailed Use Case: UC-04 Author User Story and Acceptance Criteria

| **Field** | **Description** |
| --- | --- |
| Actor | Project Editor |
| Services involved | Authoring (owner); TestArtifact and Integration react via events |
| Precondition | Actor has Editor rights on the project (verified against the membership snapshot). |
| Trigger | Editor creates or edits a user story. |
| Main flow | 1. Editor submits a story (as-a / I-want / so-that), optionally with hierarchical acceptance criteria and business rules. 2. System validates and persists the story with an authoring status. 3. System emits UserStoryCreated. 4. TestArtifact creates a placeholder test-case set; Integration optionally creates/links a Jira issue. |
| Alternative flow | 2a. Validation fails → story rejected with field-level errors. 1a. AC may be added/edited later and reordered (hierarchical). |
| Postcondition | A persisted story exists; downstream services have reacted via events (eventual consistency). |
| Related FRs | FR-10, FR-11, FR-12, FR-13 |

## 3.4 Detailed Use Case: UC-06 Generate Test Cases with AI

| **Field** | **Description** |
| --- | --- |
| Actor | QA Engineer |
| Services involved | AI Generation (owner), Authoring (gRPC source), TestArtifact (consumer), Integration (optional push) |
| Precondition | A user story with acceptance criteria exists and the user has Editor rights. |
| Trigger | User requests test case generation for a story. |
| Main flow | 1. User selects a story and requests generation. 2. System accepts the request and returns a job identifier immediately (202); progress is shown in real time. 3. System retrieves the story and acceptance criteria from Authoring (gRPC). 4. System requests generation from the LLM, preferring the primary provider. 5. System persists job completion and emits the generated test cases as an event in one transaction. 6. TestArtifact saves the test cases as drafts (idempotently) and notifies completion. |
| Alternative flow | 4a. Primary provider unavailable: system falls back to a secondary provider (circuit breaker). 4b. All providers fail: job is marked failed and the user is informed. 3a. Story fetch fails (no side-effect yet): job marked failed. |
| Postcondition | Draft test cases are available for review, linked to the source story. |
| Related FRs | FR-15, FR-16, FR-17, FR-18, FR-19 |

## 3.5 Detailed Use Case: UC-09 Execute a Test Run

| **Field** | **Description** |
| --- | --- |
| Actor | QA Engineer |
| Services involved | TestArtifact (owner), Execution (runner), Notification, Integration (defect push) |
| Precondition | A test suite with at least one approved test case exists. |
| Trigger | User starts a test run for a suite. |
| Main flow | 1. User selects a suite and starts a run, choosing manual or automated mode. 2. TestArtifact creates a run record and emits TestRunRequested. 3. For automated runs, Execution executes scripts in an isolated, resource-limited environment. 4. Execution records pass/fail/blocked results per case with artifacts (screenshots, video, trace) stored in object storage. 5. Execution emits TestRunCompleted; Notification informs the user. 6. On failure, the user may raise a defect linked to the result, optionally pushed to Jira. |
| Alternative flow | 3a. Execution times out or the environment fails: the affected results are marked blocked. |
| Postcondition | A completed run with per-case results and optional defects exists. |
| Related FRs | FR-24, FR-25, FR-26, FR-27, FR-28 |

## 3.6 Detailed Use Case: UC-11 Synchronize with Jira

| **Field** | **Description** |
| --- | --- |
| Actor | Project Editor (outbound), External System Jira (inbound) |
| Services involved | Integration (owner, ACL), Authoring, TestArtifact |
| Precondition | A valid Jira connection (OAuth) is configured for the project. |
| Trigger | Inbound: Jira sends a webhook. Outbound: an internal story/test artifact changes. |
| Main flow (inbound) | 1. Jira sends a webhook. 2. Integration verifies the signature and deduplicates by event id. 3. Integration's ACL translates the Jira payload into the clean internal model. 4. Integration upserts an external link and emits an import event. 5. Authoring creates/updates a story carrying an external reference. |
| Main flow (outbound) | 1. A story is created or test cases are generated. 2. Integration consumes the event and enqueues an outbound sync job. 3. The job pushes to Jira with retry on transient failure; emits JiraIssueLinked on success. |
| Alternative flow | 2a. Signature invalid → request rejected. 2b. Duplicate event id → ignored. Outbound: Jira transient error → retried by the outbound saga. |
| Postcondition | Internal and Jira artifacts are linked and consistent; Authoring never sees Jira's raw structure. |
| Related FRs | FR-29, FR-30, FR-31 |

# 4. Functional Requirements

Requirements are grouped by capability and uniquely identified. Priority uses MoSCoW: M = Must, S = Should, C = Could.

## 4.1 Identity and Access

| **ID** | **Requirement** | **Priority** |
| --- | --- | --- |
| FR-01 | The system shall allow a user to register with email and password. | M |
| FR-02 | The system shall authenticate users and issue a session token on success. | M |
| FR-03 | The system shall support third-party sign-in (OAuth). | S |
| FR-04 | The system shall support two-factor authentication. | C |
| FR-05 | The system shall allow users to reset a forgotten password securely. | M |

## 4.2 Workspace, Project and Authorization

| **ID** | **Requirement** | **Priority** |
| --- | --- | --- |
| FR-06 | The system shall allow an owner to create workspaces and projects. | M |
| FR-07 | The system shall allow admins to invite members by email with an expiring invitation. | M |
| FR-08 | The system shall enforce role-based access (Owner, Admin, Member; Editor, Viewer). | M |
| FR-09 | The system shall allow members to be removed and roles to be changed. | M |

## 4.3 Authoring

| **ID** | **Requirement** | **Priority** |
| --- | --- | --- |
| FR-10 | The system shall allow editors to create, edit and delete user stories. | M |
| FR-11 | The system shall support hierarchical acceptance criteria per story. | M |
| FR-12 | The system shall allow business rules to be attached to a story. | S |
| FR-13 | The system shall track an authoring status for each story. | M |
| FR-14 | The system shall allow AI-assisted refinement of a story's wording. | S |

## 4.4 AI Generation

| **ID** | **Requirement** | **Priority** |
| --- | --- | --- |
| FR-15 | The system shall generate acceptance criteria from a user story on request. | M |
| FR-16 | The system shall generate test cases from a story and its acceptance criteria. | M |
| FR-17 | The system shall process generation asynchronously and expose progress. | M |
| FR-18 | The system shall route to a primary LLM provider and fall back to a secondary on failure. | M |
| FR-19 | The system shall record which provider and model produced each generation. | S |

## 4.5 Test Artifact Management

| **ID** | **Requirement** | **Priority** |
| --- | --- | --- |
| FR-20 | The system shall allow review, editing and approval of generated test cases. | M |
| FR-21 | The system shall classify test cases by polarity and design technique (ISTQB). | M |
| FR-22 | The system shall organize test cases into suites. | M |
| FR-23 | The system shall allow creation of test plans at project level. | S |

## 4.6 Execution and Defects

| **ID** | **Requirement** | **Priority** |
| --- | --- | --- |
| FR-24 | The system shall execute a test run for a suite in manual or automated mode. | M |
| FR-25 | The system shall run automated scripts in an isolated, resource-limited environment. | M |
| FR-26 | The system shall record per-case results (passed, failed, blocked, skipped, not run). | M |
| FR-27 | The system shall capture execution artifacts (screenshots, video, trace) for automated runs. | S |
| FR-28 | The system shall allow defects to be raised and linked to a failing result. | M |

## 4.7 Integration

| **ID** | **Requirement** | **Priority** |
| --- | --- | --- |
| FR-29 | The system shall import stories from Jira via webhook with deduplication. | S |
| FR-30 | The system shall push generated test cases to a linked Jira issue. | S |
| FR-31 | The system shall maintain external links in a provider-agnostic way. | S |

## 4.8 Notification

| **ID** | **Requirement** | **Priority** |
| --- | --- | --- |
| FR-32 | The system shall notify users of relevant events via in-app and email channels. | S |

# 5. Non-Functional Requirements

Non-functional requirements are measurable and testable. Targets are scoped to an academic/portfolio deployment rather than large-scale production.

## 5.1 Performance

| **ID** | **Requirement** | **Target** |
| --- | --- | --- |
| NFR-01 | Interactive API responses (read/write, excluding AI) latency. | p95 < 500 ms |
| NFR-02 | Concurrent active users supported without degradation. | ≥ 200 |
| NFR-03 | Sustained request throughput at the gateway. | ≥ 50 req/s |
| NFR-04 | AI generation acknowledgement (job accepted) latency. | < 1 s |

## 5.2 Scalability and Availability

| **ID** | **Requirement** | **Target** |
| --- | --- | --- |
| NFR-05 | Service availability during demonstration periods. | ≥ 99.5% |
| NFR-06 | Each service shall scale horizontally without code change. | Stateless |
| NFR-07 | Failure of a non-critical service shall not bring down core flows. | Isolated |

## 5.3 Security

| **ID** | **Requirement** | **Target** |
| --- | --- | --- |
| NFR-08 | All external traffic shall be encrypted in transit. | TLS 1.2+ |
| NFR-09 | Passwords shall be stored using a strong adaptive hash. | Argon2id |
| NFR-10 | Tokens and secrets shall never be stored in plaintext or in source control. | Enforced |
| NFR-11 | Automated test execution shall be sandboxed with resource limits. | Mandatory |
| NFR-12 | Inbound webhooks shall be signature-verified and deduplicated. | Mandatory |
| NFR-13 | Authorization shall be enforced on every request at the gateway and service. | Mandatory |

## 5.4 Privacy and Compliance (GDPR-like)

| **ID** | **Requirement** | **Target** |
| --- | --- | --- |
| NFR-14 | The system shall collect only data necessary for its function (data minimization). | Enforced |
| NFR-15 | User content shall not be used to train third-party models. | Enforced |
| NFR-16 | Users shall be able to export their personal data on request. | Supported |
| NFR-17 | Users shall be able to request deletion of their account and personal data. | Supported |
| NFR-18 | Security-relevant actions shall be recorded in an audit log. | Mandatory |
| NFR-19 | Data sent to external LLM providers shall be disclosed to the user. | Transparent |

## 5.5 Usability

| **ID** | **Requirement** | **Target** |
| --- | --- | --- |
| NFR-20 | Long-running operations shall show real-time progress feedback. | Required |
| NFR-21 | The UI shall present clear error messages distinguishing user and system errors. | Required |

## 5.6 Maintainability and Observability

| **ID** | **Requirement** | **Target** |
| --- | --- | --- |
| NFR-22 | All services shall emit structured logs, metrics and distributed traces. | Required |
| NFR-23 | Each service shall expose a health endpoint for readiness and liveness. | Required |

# 6. External Interface Requirements

## 6.1 User Interfaces

- A responsive web application and a mobile application, both communicating with the backend over HTTPS.
- Real-time progress updates for asynchronous operations (e.g. AI generation, test runs) via SignalR/WebSocket.

## 6.2 Software Interfaces

- LLM provider API: request/response for text generation; at least one primary and one fallback provider.
- Jira REST API and webhooks: bidirectional exchange of issues and test artifacts.
- Object storage: for execution artifacts such as screenshots, video and traces.

## 6.3 Communication Interfaces

- All client-server communication over HTTPS through a single public gateway.
- Inter-service: asynchronous events over a message broker; synchronous gRPC only where an immediate answer is required.
- Webhook callbacks secured by signature verification.

# 7. Requirements Traceability Matrix

This matrix links use cases to the functional requirements that realize them, supporting verification that every use case is covered.

| **Use Case** | **Related Functional Requirements** |
| --- | --- |
| UC-01 Register and authenticate | FR-01, FR-02, FR-03, FR-04, FR-05 |
| UC-02 Create workspace and project | FR-06 |
| UC-03 Invite and manage members | FR-07, FR-08, FR-09 |
| UC-04 Author user story | FR-10, FR-11, FR-12, FR-13 |
| UC-05 Refine story with AI | FR-14 |
| UC-06 Generate test cases | FR-15, FR-16, FR-17, FR-18, FR-19 |
| UC-07 Review and approve | FR-20, FR-21 |
| UC-08 Organize suites and plans | FR-22, FR-23 |
| UC-09 Execute test run | FR-24, FR-25, FR-26, FR-27 |
| UC-10 Record results and defects | FR-26, FR-28 |
| UC-11 Synchronize with Jira | FR-29, FR-30, FR-31 |

*Non-functional requirements NFR-01 to NFR-23 apply across all use cases and are verified through performance testing, security review and operational monitoring.*

# 8. Data Requirements

Each service owns one database (polyglot persistence). No service reads another service's database; cross-service references use bare UUIDs, never foreign keys. The authoritative schema is `docs/database/quraex.dbml`; this section states what each table is **for**.

## 8.1 Cross-cutting patterns (why every database looks similar)

| Pattern | Table(s) | Purpose |
| --- | --- | --- |
| Transactional Outbox | `*_outbox_message` | Written in the same transaction as business data; a relay later publishes it to the broker. Solves dual-write (DB + event can't desync). |
| Idempotency | `*_processed_message` | Records handled message ids so an at-least-once redelivery is processed only once. 7-day retention. |
| Read-model / Snapshot | `*_snapshot` (e.g. `membership_snapshot`, `story_snapshot`) | A lightweight local copy of another service's data, updated from its events, so the service never reads another database. |
| Saga state | `*_saga_state` | Persists the state of a long-running coordinated process (MassTransit). |

## 8.2 Identity database (PostgreSQL) — "who you are"

| Table | Purpose |
| --- | --- |
| `app_user` | User account: email (unique), `password_hash` (Argon2id), display name, status, timestamps. |
| `refresh_token` | Issued refresh tokens stored as a **hash** (never raw), with expiry and revoked flag. |
| `user_mfa` | Two-factor secret and enabled flag per user. |
| `openiddict_application` / `_authorization` / `_scope` / `_token` | OpenIddict OIDC server tables: registered clients, granted authorizations, scopes, and issued tokens — the basis for JWKS-validated JWTs. |
| `identity_outbox_message` | Outbox for `UserRegistered`, `UserUpdated`. |

## 8.3 Workspace database (PostgreSQL) — "what you may do"

| Table | Purpose |
| --- | --- |
| `workspace` | A workspace: name, type (PERSONAL/TEAM), owner user id. |
| `project` | A project within a workspace, with a unique `project_key`. |
| `workspace_member` | Workspace-level role per user (OWNER / ADMIN / MEMBER). |
| `project_member` | Project-level role per user (EDITOR / VIEWER). |
| `invitation` | Project-scoped invitation: email, `token_hash`, status, expiry — drives the invite saga. |
| `workspace_outbox_message` | Outbox for `MembershipChanged`, `ProjectCreated`. |

## 8.4 Authoring database (PostgreSQL) — requirement source (reference service)

| Table | Purpose |
| --- | --- |
| `user_story` | The story: title, as-a / i-want-to / so-that, description, `authoring_status`, `external_ref` (Jira link). |
| `acceptance_criteria` | Criteria per story; hierarchical via `parent_id`; `order_no`, `completed`. |
| `business_rule` | Business rules attached to a story. |
| `membership_snapshot` | Read-model of project membership/roles from Workspace, used to authorize without cross-service calls. |
| `authoring_outbox_message` | Outbox for `UserStoryCreated`. |
| `authoring_processed_message` | Idempotency for consumed events (e.g. `MembershipChanged`). |

## 8.5 TestArtifact database (PostgreSQL) — test store & run lifecycle

| Table | Purpose |
| --- | --- |
| `test_case` | Steps, expected result, `polarity` (POSITIVE/NEGATIVE), `design_technique` (BVA/EP/Decision Table…), priority, `lifecycle_status`, `generated_by_ai`. |
| `test_suite` / `test_suite_item` | A themed collection of test cases (many-to-many via the item table). |
| `test_plan` | Project-level strategy document (scope, objectives, risks). |
| `test_run` | One execution of one suite at a point in time (status, started/finished). |
| `test_run_result` | Per-case result in a run: PASSED / FAILED / BLOCKED / SKIPPED / NOT_RUN / IN_PROGRESS. |
| `defect` | A logged defect (severity, status) linked to a failing result; bridge to Jira. |
| `story_snapshot` | Read-model of stories from Authoring. |
| `testartifact_outbox_message` / `_processed_message` | Outbox for `TestCasesSaved`, `TestRunRequested`; idempotency for consumed events. |

## 8.6 AI Generation database (PostgreSQL + Redis) — generation brain

| Store | Purpose |
| --- | --- |
| `generation_job` | A generation job: story id, job type (refine / AC / TC), status, `llm_source`, timestamps. |
| `llm_provider_config` | Per-project LLM configuration: preferred source, model, temperature, max tokens. |
| `ai_saga_state` | Saga state for the generation orchestration. |
| Redis `job:{id}` | Hot, real-time job progress; published via Pub/Sub to SignalR. |
| `ai_outbox_message` / `ai_processed_message` | Outbox for `TestCasesGenerated`; idempotency for `TestRunRequested`. |

## 8.7 Execution database (PostgreSQL + Object storage) — real test runs

| Table | Purpose |
| --- | --- |
| `exec_run` | One execution: environment, status, triggered-by, started/finished. |
| `exec_result` | Per-case outcome: status, duration, error message. |
| `exec_artifact` | Artifact metadata: type (screenshot/video/trace) and `storage_path`; **the file lives in object storage**, the DB keeps only the path. |
| `test_script` | The automation script: framework (Playwright), content, `generated_by_ai`. |
| `execution_outbox_message` / `_processed_message` | Outbox for `TestRunCompleted`; idempotency for `TestRunRequested` / `TestCasesGenerated`. |

## 8.8 Integration database (PostgreSQL) — Jira ACL

| Table | Purpose |
| --- | --- |
| `jira_connection` | Per-project Jira OAuth token / refresh token, `jira_site_id`, status. |
| `external_link` | Provider-agnostic mapping internal id ↔ external id/key, `last_synced_at`, `sync_status`. |
| `outbound_sync_job` | Queue of pushes to Jira: payload, status, `retry_count`. |
| `processed_webhook` | Inbound webhook deduplication by `event_id`. |
| `integration_outbox_message` / saga state | Outbox for `JiraIssueLinked`; outbound saga state. |

## 8.9 Notification database (MongoDB) — fan-out

| Collection | Purpose |
| --- | --- |
| `notifications` | A notification: user id, type, flexible `payload` object, channel, status, read flag, created at. |
| `notification_preferences` | Per-user, per-type email/push toggles. |
| `processed_messages` | Idempotency for consumed events. |

## 8.10 Stateful entity lifecycles

Two entities have non-trivial lifecycles (full state diagrams in `docs/service-flows-explainer.md` §3.7):

- **`invitation`** (Workspace): `Pending → AwaitingResponse → Accepted`, with terminal branches `Declined`, `Expired` (saga timeout, compensation), `Revoked`. Only `Accepted` creates a `project_member`.
- **`generation_job`** (AI Generation): `PENDING → FETCHING_STORY → GENERATING → PERSISTING → COMPLETED`; `FAILED` on story-fetch failure or after both LLM sources fail. `COMPLETED` is reached only once the outbox event is written in the same transaction.

# 9. Business Rules

| **ID** | **Rule** |
| --- | --- |
| BR-01 | Invitation tokens are stored hashed; the raw token exists only in the email link. |
| BR-02 | An invitation not accepted within 7 days expires automatically (saga compensation). |
| BR-03 | AI-generated test cases are created as drafts and are not authoritative until a human approves them. |
| BR-04 | A user story may not be marked "tested" unless its test cases actually exist (event ordering prevents half states). |
| BR-05 | Test case `polarity` and `design_technique` are separate axes (ISTQB), never merged into one "type" field. |
| BR-06 | Authoring status is independent of test status; "tested" is derived, not a story status value. |
| BR-07 | Automated execution must run in a sandbox with CPU/RAM/time limits; no run may exceed them. |
| BR-08 | Authorization is context-based: project role (Editor/Viewer) governs story and test actions. |
| BR-09 | The internal domain never depends on Jira's structure; all translation happens in the Integration ACL. |
| BR-10 | A defect can only be raised against an existing failing run result. |

# 10. System Behaviour and Failure Modes

| Aspect | Behaviour |
| --- | --- |
| Consistency model | Eventual consistency across services; a story may show "has test cases" a few seconds after generation. Event ordering guarantees no half states. |
| Asynchronous operations | Generation and execution return an id immediately (202) and report progress via SignalR; they never block the client. |
| LLM failure | Router tries the primary provider, then a secondary (circuit breaker); only when both fail is the job marked FAILED. |
| Event delivery | At-least-once. Consumers are idempotent via `*_processed_message`, so redelivery self-heals without manual compensation. |
| Dual-write | Prevented by the transactional outbox: business data and the event are written in one transaction. |
| Non-critical service down | Notification or Integration being down does not block core flows; events queue and are processed when they recover. |
| Webhook trust | Inbound webhooks are signature-verified and deduplicated before any processing. |

# 11. Flow ↔ Service Relationship Map

Which services collaborate in each flow, and through what mechanism (event unless noted). ⭐ = orchestrator/owner.

| Flow | Services involved (and how they relate) |
| --- | --- |
| **A · Register / Login** | ⭐ Identity issues JWT → emits `UserRegistered` → Workspace seeds profile. Gateway + all services validate the JWT via Identity's JWKS. |
| **B · Invite member** | ⭐ Workspace runs the invite saga → emits `MembershipChanged` → Notification emails the invitee; Authoring/TestArtifact update their `membership_snapshot`. |
| **C · Author story / Jira import** | ⭐ Authoring emits `UserStoryCreated` → TestArtifact (placeholder set) + Integration (link Jira). Inbound: Integration (ACL) verifies+translates a Jira webhook → emits import → Authoring creates the story with `external_ref`. |
| **D · Generate test cases** | ⭐ AI Generation; **gRPC** to Authoring for story content; LLM router (external); emits `TestCasesGenerated` → TestArtifact saves (idempotent) → `TestCasesSaved` → Authoring updates status, Integration optionally pushes to Jira. Progress via Redis/SignalR. |
| **E · Execute test run** | TestArtifact emits `TestRunRequested` → ⭐ Execution runs Playwright in a sandbox, stores artifacts in object storage → emits `TestRunCompleted` → TestArtifact updates results, Notification informs the user; FAIL → defect (optionally to Jira). |
| **F · Notify** | ⭐ Notification consumes `TestRunCompleted` and `MembershipChanged`, reads `notification_preferences`, writes MongoDB, sends in-app/email. Pure consumer. |

Service-by-flow matrix (⭐ lead · ✓ participates):

| Service | A | B | C | D | E | F |
| --- | :--: | :--: | :--: | :--: | :--: | :--: |
| Identity | ⭐ | | | | | |
| Workspace | ✓ | ⭐ | | | | |
| Authoring | | | ⭐ | ✓ (gRPC) | | |
| TestArtifact | | | ✓ | ✓ | ⭐ | |
| AI Generation | | | | ⭐ | | |
| Execution | | | | | ⭐ | |
| Integration | | | ✓ | ✓ | (defect) | |
| Notification | | ✓ | | | ✓ | ⭐ |

# Appendix A. Architecture & Decision Mapping

This appendix bridges requirements to the realized design (full detail in `docs/QuraEx_Architecture.md`).

## A.1 FR group → owning service

| FR group | Owning service |
| --- | --- |
| FR-01…05 Identity & Access | Identity |
| FR-06…09 Workspace & Authorization | Workspace |
| FR-10…14 Authoring | Authoring (+ AI Generation for FR-14 refine) |
| FR-15…19 AI Generation | AI Generation (gRPC to Authoring) |
| FR-20…23 Test artifact | TestArtifact |
| FR-24…28 Execution & defects | Execution (+ TestArtifact for runs/defects) |
| FR-29…31 Integration | Integration |
| FR-32 Notification | Notification |

## A.2 Locked technical decisions (affect NFRs)

| Decision | Choice | Requirement link |
| --- | --- | --- |
| API gateway | Kong DB-less (single public entry) | NFR-13, Constraint 2.5 |
| Identity provider | OpenIddict, federation-ready (AWS Cognito later) | FR-01…05, NFR-08/09 |
| Cloud target | AWS (EKS + RDS + S3 + Secrets Manager) | NFR-05, NFR-11 |
| AWS IAM scope | Infrastructure access only (IRSA, S3, Secrets Manager) — not user login | NFR-10 |
| LLM strategy | Third-party first (OpenAI/Gemini) + router with circuit breaker; self-host optional | FR-18, NFR-07 |
| Execution automation | Playwright Level 1 first (script-run); Level 2 (LLM-authored) if time permits | FR-24, FR-25 |
| Scope sequencing | Core backbone first; Notification & Integration last/minimal | — |

## A.3 NFR realization (how each is met)

| NFR | Realized by |
| --- | --- |
| NFR-06 Stateless scaling | Each service stateless; state in its DB/Redis; horizontal scale on AWS EKS. |
| NFR-07 Failure isolation | Async events + outbox; non-critical services can lag without blocking core flows. |
| NFR-11 Sandboxed execution | Execution workers run browsers in isolated containers with CPU/RAM/timeout limits. |
| NFR-12 Webhook trust | Integration verifies signatures and deduplicates via `processed_webhook`. |
| NFR-13 Authorization everywhere | Kong validates JWT at the edge; each service re-checks project role via snapshot. |
| NFR-22 Observability | .NET Aspire + OpenTelemetry traces/metrics/logs across all services. |
| NFR-23 Health endpoints | Every service exposes `/health` (readiness/liveness). |
