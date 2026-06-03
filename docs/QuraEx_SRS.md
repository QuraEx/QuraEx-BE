**QuraEx**

AI-Powered Test Case Generation Platform

**SOFTWARE REQUIREMENTS SPECIFICATION**

*Prepared in accordance with IEEE 830 / ISO/IEC/IEEE 29148*

Version 1.0

Date: 03 June 2026

*Status: Draft for review*

# Revision History

| **Version** | **Date** | **Author** | **Description** |
| --- | --- | --- | --- |
| 0.1 | 2026-05-20 | Project team | Initial draft, scope and actors |
| 1.0 | 2026-06-03 | Project team | Complete functional and non-functional requirements |

# Table of Contents

# 1. Introduction

## 1.1 Purpose

This Software Requirements Specification (SRS) defines the functional and non-functional requirements for QuraEx, an AI-powered platform that generates software test cases from user stories. This document describes what the system must do and the constraints it must satisfy. It is solution-independent: it specifies requirements, not the technical design, which is covered separately in the Software Design Document (SDD). The intended audience is the project supervisor (for evaluation) and the development team (as the authoritative requirements baseline).

## 1.2 Scope

QuraEx enables QA engineers and product teams to author user stories with acceptance criteria, then automatically generate, refine, organize, and execute test cases using large language models. The system covers the full lifecycle from requirement authoring to test execution and defect tracking, and integrates two-way with external issue trackers such as Jira.

In scope: user and workspace management, authorization, user-story authoring, AI-assisted generation of acceptance criteria and test cases, test artifact management (suites, plans, runs, results, defects), automated test execution, external tracker integration, and notifications.

Out of scope: the design of the underlying microservice architecture, database schemas, and deployment topology (see the SDD); manual exploratory testing workflows; and billing or commercial licensing features.

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

## 1.4 References

- IEEE Std 830-1998 — Recommended Practice for Software Requirements Specifications.
- ISO/IEC/IEEE 29148:2018 — Requirements engineering.
- ISTQB Foundation Level Syllabus — testing terminology.
- QuraEx Software Design Document (companion document).

# 2. Overall Description

## 2.1 Product Perspective

QuraEx is a new, self-contained web-based product. It exposes a web frontend and mobile application backed by a service-oriented backend. It interacts with external large language model providers and external issue trackers (e.g. Jira) but does not depend on any pre-existing in-house system. The product replaces ad-hoc, manual test-case writing with an assisted, traceable workflow.

## 2.2 User Classes and Characteristics

| **Actor / User class** | **Description** | **Technical level** |
| --- | --- | --- |
| Workspace Owner | Creates and owns a workspace; manages billing-level settings and members. | Medium |
| Workspace Admin | Manages projects and members within a workspace. | Medium |
| Project Editor | Authors user stories, generates and edits test cases, runs tests. | Medium to high |
| Project Viewer | Read-only access to project artifacts. | Low |
| QA Engineer | Primary user generating and executing test cases. | High |
| System Administrator | Operates and monitors the platform. | High |
| External System (Jira) | Automated actor exchanging issues and updates via integration. | N/A |
| LLM Provider | External AI service generating content on request. | N/A |

## 2.3 Operating Environment

- Client: modern web browsers (Chrome, Edge, Firefox, Safari, latest two major versions) and a mobile application.
- Server: cloud-hosted backend reachable over HTTPS through a single public gateway at quraex.com.
- External dependencies: at least one LLM provider (self-hosted or third-party) and, optionally, a Jira instance.

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

# 3. Use Cases

This section lists the principal use cases and provides detailed specifications for the two most critical flows. Each use case maps to functional requirements in Section 4.

## 3.1 Use Case Summary

| **ID** | **Use Case** | **Primary Actor** |
| --- | --- | --- |
| UC-01 | Register and authenticate | User |
| UC-02 | Create workspace and project | Workspace Owner |
| UC-03 | Invite and manage members | Workspace Admin |
| UC-04 | Author user story and acceptance criteria | Project Editor |
| UC-05 | Refine user story with AI | Project Editor |
| UC-06 | Generate test cases with AI | QA Engineer |
| UC-07 | Review, edit and approve test cases | QA Engineer |
| UC-08 | Organize test cases into suites and plans | QA Engineer |
| UC-09 | Execute a test run (manual or automated) | QA Engineer |
| UC-10 | Record results and raise defects | QA Engineer |
| UC-11 | Synchronize with Jira | Project Editor / External System |

## 3.2 Detailed Use Case: UC-06 Generate Test Cases with AI

| **Field** | **Description** |
| --- | --- |
| Actor | QA Engineer |
| Precondition | A user story with acceptance criteria exists and the user has Editor rights. |
| Trigger | User requests test case generation for a story. |
| Main flow | 1. User selects a story and requests generation. 2. System accepts the request and returns a job identifier immediately. 3. System retrieves the story and acceptance criteria. 4. System requests generation from the LLM, preferring the primary provider. 5. System persists the generated test cases as drafts. 6. System notifies the user that generation is complete and shows progress throughout. |
| Alternative flow | 4a. Primary provider unavailable: system falls back to a secondary provider. 4b. All providers fail: job is marked failed and the user is informed. |
| Postcondition | Draft test cases are available for review, linked to the source story. |
| Related FRs | FR-15, FR-16, FR-17, FR-18, FR-19 |

## 3.3 Detailed Use Case: UC-09 Execute a Test Run

| **Field** | **Description** |
| --- | --- |
| Actor | QA Engineer |
| Precondition | A test suite with at least one approved test case exists. |
| Trigger | User starts a test run for a suite. |
| Main flow | 1. User selects a suite and starts a run, choosing manual or automated mode. 2. System creates a run record and returns its identifier. 3. For automated runs, the system executes scripts in an isolated environment. 4. System records pass/fail/blocked results per test case with artifacts. 5. On failure, the user may raise a defect linked to the result. |
| Alternative flow | 3a. Execution times out or the environment fails: the affected results are marked blocked. |
| Postcondition | A completed run with per-case results and optional defects exists. |
| Related FRs | FR-24, FR-25, FR-26, FR-27, FR-28 |

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
- Real-time progress updates for asynchronous operations (e.g. AI generation, test runs).

## 6.2 Software Interfaces

- LLM provider API: request/response for text generation; at least one primary and one fallback provider.
- Jira REST API and webhooks: bidirectional exchange of issues and test artifacts.
- Object storage: for execution artifacts such as screenshots, video and traces.

## 6.3 Communication Interfaces

- All client-server communication over HTTPS through a single public gateway.
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