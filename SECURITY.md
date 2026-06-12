# Security Policy

We take the security of QuraEx seriously. This document explains how to report a
vulnerability and what to expect in return.

## Reporting a Vulnerability

**Do not open a public issue, pull request, or discussion for security
problems.** Public disclosure before a fix is available puts every user at risk.

Use one of these private channels instead:

1. **GitHub Private Vulnerability Reporting (preferred).**
   Go to the **Security** tab of this repository → **Report a vulnerability**.
   This opens a private advisory visible only to you and the maintainers.
2. **Email.** If you cannot use GitHub reporting, email
   **hoangbavan4478@gmail.com** with the subject line `SECURITY: <short summary>`.

Please include as much of the following as you can:

- A description of the vulnerability and its impact.
- Steps to reproduce (proof-of-concept, requests, or a minimal repo).
- Affected component, endpoint, or service (e.g. Gateway, Authoring, Identity).
- Affected version, branch, or commit SHA.
- Any suggested remediation, if you have one.

## What to Expect

| Stage | Target |
|-------|--------|
| Acknowledgement of your report | within 3 business days |
| Initial assessment and severity triage | within 7 business days |
| Status updates | at least every 7 days until resolved |
| Fix and coordinated disclosure | timeline agreed with you, based on severity |

We will credit you in the published advisory unless you ask to remain anonymous.

## Scope

In scope:

- The backend services and gateway in this repository (`gateway/`,
  `services/`, `building-blocks/`, `aspire/`).
- Authentication and authorization flows (JWT validation, route policies).
- Deployment and configuration artifacts in `deploy/`.

Out of scope:

- Findings that require a private signing key we have never published. The
  committed JWT keys are **public** keys and cannot sign tokens by design.
- The committed `.env` file. It is **dotenvx ciphertext**; it is safe to read
  without the private key, which is never committed.
- Localhost-only development defaults (for example the
  `Password=postgres` and RabbitMQ `guest/guest` values in
  `appsettings.Development.json`). These never reach production, where secrets
  are injected through environment variables.
- Denial-of-service through brute-force traffic, automated scanners, or
  volumetric load.
- Social engineering, physical attacks, and findings against third-party
  services we do not control.

## Supported Versions

This project is under active development. Security fixes are applied to the
default branch (`main`) and flow through the standard branch path
(`feature → dev → release → main`). Older tags and branches do not receive
backported fixes.

## Handling of Secrets

- Application secrets are encrypted at rest with
  [dotenvx](https://dotenvx.com/encryption); only the public key and ciphertext
  are committed.
- The private decryption key (`.env.keys`) and any RSA signing keys are **never**
  committed and are excluded by `.gitignore`.
- If you believe a real secret has been committed, treat it as a vulnerability
  and report it privately using the channels above so it can be rotated.

Thank you for helping keep QuraEx and its users safe.
