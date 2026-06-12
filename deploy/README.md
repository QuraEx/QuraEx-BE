# Production Deployment — DigitalOcean Droplet + Cloudflare Tunnel

Single-droplet production deploy. Images are built in CI and pushed to GHCR; the
droplet pulls them. **Cloudflare Tunnel** fronts the stack — the droplet exposes
**no inbound web ports** (only SSH), and TLS terminates at Cloudflare's edge.

```
Internet → Cloudflare edge (quraex.com, TLS+WAF) ──tunnel──→ cloudflared → gateway:8080
                                                                              → authoring:8080
                                                                              → postgres / rabbitmq / redis
```

## Files

| File | Purpose |
|------|---------|
| `docker-compose.prod.yml` | Production stack (pulls GHCR images, no host ports, cloudflared edge) |
| `.env.prod.example` | Template for `/opt/quraex/.env` (DB/broker passwords, tunnel token, image tag) |
| `config/*.appsettings.Production.json.example` | Template for the JWT public-key overlay mounted into each service |
| `setup-droplet.sh` | One-time droplet provisioning (Docker + dirs + firewall) |

CI jobs `publish-images` + `deploy` (in `.github/workflows/ci.yml`) run on every push to `main`: build → push GHCR → scp compose → `docker compose pull && up -d`.

## One-time setup

### 1. Provision the droplet
SSH in (or use the DO Web Console) and run:
```sh
curl -fsSL https://raw.githubusercontent.com/quraex/QuraEx-BE/main/quraexv2/deploy/setup-droplet.sh | sudo bash
```

### 2. Generate a production JWT keypair (public key is not secret; keep the private key safe for the future Identity service)
```sh
openssl genpkey -algorithm RSA -pkeyopt rsa_keygen_bits:2048 -out quraex-prod-jwt.key
openssl rsa -in quraex-prod-jwt.key -pubout -out quraex-prod-jwt.pub
```
Convert the public key to a single JSON-escaped line (`\n` between lines) and paste it into both config files below.

### 3. Create runtime config on the droplet (`/opt/quraex`)
```sh
cd /opt/quraex
# .env — fill in passwords + tunnel token (from step 4)
nano .env                       # use .env.prod.example as the template
# JWT public-key overlays
nano config/gateway.appsettings.Production.json
nano config/authoring.appsettings.Production.json
```

### 4. Create the Cloudflare Tunnel (quraex.com)
Cloudflare dashboard → **Zero Trust → Networks → Tunnels → Create tunnel** (type: *Cloudflared*):
- Name it (e.g. `quraex-prod`), copy the **tunnel token** → put it in `/opt/quraex/.env` as `CLOUDFLARE_TUNNEL_TOKEN`.
- **Public Hostnames** → add:
  - Hostname: `quraex.com` (and/or `api.quraex.com`)
  - Service: `HTTP` → `gateway:8080`
- Cloudflare auto-creates the DNS CNAME → `<tunnel-id>.cfargotunnel.com`. No manual DNS record needed.

### 5. Make the GHCR images public (one-time, so the droplet pulls without auth)
After the first `publish-images` run: GitHub → your profile → **Packages** → `quraex-gateway` / `quraex-authoring` → **Package settings → Change visibility → Public**.
(Alternatively keep them private and `docker login ghcr.io` on the droplet with a read:packages PAT.)

### 6. Add GitHub repository secrets (Settings → Secrets and variables → Actions)
| Secret | Value |
|--------|-------|
| `DROPLET_HOST` | `206.189.159.230` |
| `DROPLET_USER` | `root` (or a deploy user) |
| `DROPLET_SSH_KEY` | private key whose public half is in the droplet's `~/.ssh/authorized_keys` |

GHCR push uses the built-in `GITHUB_TOKEN` — no secret needed.

Then enable deploys: Settings → Secrets and variables → Actions → **Variables** → add `DEPLOY_ENABLED` = `true`. Until this is set, the `deploy` job stays skipped (images still publish to GHCR), so merging to `main` never fails on a missing-droplet deploy.

### 7. DigitalOcean firewall
Cloud Firewall (or the `setup-droplet.sh` UFW step): **inbound allow 22 (SSH) only**. No 80/443 — Cloudflare Tunnel is outbound-only.

## Deploy

Push to `main`. CI builds images, pushes to GHCR, then deploys. To deploy manually:
```sh
cd /opt/quraex
IMAGE_TAG=<sha-or-latest> docker compose -f docker-compose.prod.yml pull
IMAGE_TAG=<sha-or-latest> docker compose -f docker-compose.prod.yml up -d
```

## Verify
```sh
docker compose -f docker-compose.prod.yml ps          # all healthy
docker compose -f docker-compose.prod.yml logs cloudflared   # tunnel registered
curl -fsS https://quraex.com/api/authoring/health     # -> Healthy (through Cloudflare)
```

## Notes / future
- Migrations auto-apply on authoring startup (`RunMigrationsOnStartup=true`, advisory-lock serialized) — fine for one instance. For multi-replica, run a dedicated migration job and turn the flag off.
- Postgres data lives in the `postgres_authoring_data` volume on the droplet — **set up backups** (e.g. `pg_dump` cron → DO Spaces) before treating this as durable. A managed DB (DO Managed Postgres) is the next step up.
- Auth: services validate RS256 tokens against the prod public key. No token issuer exists yet, so protected endpoints return 401 until the Identity service is built — expected.
