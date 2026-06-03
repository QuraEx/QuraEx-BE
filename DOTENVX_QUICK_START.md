# Quick Reference: dotenvx Encrypted Environment

QuraEx v2 uses [dotenvx](https://dotenvx.com) to encrypt `.env` so it can be
committed safely. The ciphertext and a public key live in `.env`; the matching
private key lives only in `.env.keys` (gitignored) and in each developer's
shell environment.

A clone WITHOUT the private key **cannot run the Docker stack** — the committed
`.env` is encrypted ciphertext and Docker Compose will fail with
`invalid IP address: encrypted:...` if you try. Get `DOTENV_PRIVATE_KEY` from
the team lead before proceeding.

---

## How It Works

| File | Contents | Committed? |
|------|----------|-----------|
| `.env` | Encrypted ciphertext + `DOTENV_PUBLIC_KEY` | Yes |
| `.env.keys` | `DOTENV_PRIVATE_KEY` (plaintext) | **Never** |
| `.env.example` | Plaintext template with placeholder values | Yes |

dotenvx decrypts `.env` at runtime and injects values into the process — no
plaintext file ever touches disk during normal operation.

---

## For Developers

### First-Time Setup

```sh
# 1. Install dotenvx (or use npx — no global install required)
npm install -g @dotenvx/dotenvx
# -or- prefix every command below with: npx @dotenvx/dotenvx@latest

# 2. Get DOTENV_PRIVATE_KEY from your team lead via a secure channel
#    (password manager, encrypted DM — never email or public chat)

# 3. Export the key in your shell
export DOTENV_PRIVATE_KEY='your-private-key-here'
# Add to ~/.zshrc or ~/.bashrc to persist across sessions

# 4. Verify decryption works (optional)
dotenvx run -- echo "decryption OK"
```

### Running the Stack

All compose commands go through dotenvx. There is no keyless path — that is
intentional.

```sh
# Recommended: use make targets (dotenvx is always wired in)
make up           # build + start full stack
make up-infra     # start backing infra only (Postgres/RabbitMQ/Redis)
make down         # stop and remove containers
make clean        # stop + remove volumes (DESTROYS local data)
make logs         # follow logs from all containers
make ps           # list running containers
make build        # rebuild images without starting

# Direct dotenvx invocation (equivalent to make targets)
dotenvx run -- docker compose up -d --build
dotenvx run -- docker compose -f docker-compose.yml up -d
dotenvx run -- docker compose down
dotenvx run -- docker compose logs -f
```

### Daily Workflow

```sh
# Pull latest changes (may include updated encrypted .env)
git pull

# Re-run with fresh secrets (dotenvx run always reads current .env)
make up
# -or-
dotenvx run -- docker compose up -d

# Verify health
curl http://localhost:8080/api/authoring/health   # -> 200 OK
```

### Inspecting Values Locally

```sh
# Decrypt to plaintext for local inspection
dotenvx decrypt      # -or- make secrets-decrypt

cat .env             # inspect — DO NOT commit in this state

# Re-encrypt BEFORE staging or committing
dotenvx encrypt      # -or- make secrets-encrypt
git add .env
```

The pre-commit hook (see below) will block a commit if `.env` is still
plaintext — you cannot accidentally push decrypted secrets.

---

## Pre-commit Guard

The Husky pre-commit hook runs `dotenvx ext precommit` before every commit.
It scans staged `.env*` files and **fails if any are unencrypted**.

This matters because `.gitleaks.toml` allowlists `.env` (ciphertext is safe to
commit), so gitleaks alone would not catch a plaintext `.env` that was
accidentally staged after a `dotenvx decrypt`.

Workflow when the guard fires:

```sh
# You forgot to re-encrypt — the hook exits non-zero and prints:
# ❌  dotenvx precommit: unencrypted .env file detected in staged changes.

dotenvx encrypt      # re-encrypt
git add .env         # re-stage the ciphertext
git commit ...       # hook passes now
```

---

## For Admins

### Updating Environment Variables

```sh
# 1. Decrypt to plaintext
dotenvx decrypt

# 2. Edit .env with your changes

# 3. Re-encrypt
dotenvx encrypt

# 4. Stage and commit the encrypted .env
git add .env
git commit -m "feat(env): update environment variables"
git push

# 5. Notify teammates — they pull and re-run (no key change needed)
```

### Rotating the Private Key

```sh
# 1. Decrypt with the current key
dotenvx decrypt

# 2. Remove the old keys file
rm .env.keys

# 3. Re-encrypt — generates a fresh keypair
dotenvx encrypt

# 4. Share the new DOTENV_PRIVATE_KEY from .env.keys with the team
#    via a secure channel (password manager, encrypted DM)

# 5. Commit the re-encrypted .env
git add .env
git commit -m "refactor(env): rotate dotenvx encryption key"
git push

# 6. Each teammate updates their shell export and restarts their stack
```

---

## Makefile Shortcuts

```sh
make secrets-encrypt   # dotenvx encrypt  — safe to commit
make secrets-decrypt   # dotenvx decrypt  — re-encrypt before committing!
make up                # dotenvx run -- docker compose up -d --build
make up-infra          # dotenvx run -- docker compose -f docker-compose.yml up -d
make down              # dotenvx run -- docker compose down
make clean             # dotenvx run -- docker compose down -v
make logs              # dotenvx run -- docker compose logs -f
make ps                # dotenvx run -- docker compose ps
make build             # dotenvx run -- docker compose build
```

---

## Important Reminders

Do:
- Commit the encrypted `.env` (ciphertext only)
- Share `DOTENV_PRIVATE_KEY` via password manager or encrypted messaging
- Re-encrypt before committing if you decrypted for local inspection
- Add `DOTENV_PRIVATE_KEY` to CI secrets for staging/prod pipelines

Do NOT:
- Commit `.env.keys` — it is gitignored and must stay that way
- Share the private key via email, Slack, or screenshots
- Edit the encrypted `.env` directly — always decrypt first
- Leave `.env` in plaintext state on a shared branch (the pre-commit hook
  blocks this, but do not rely on it as your only safeguard)

---

## Troubleshooting

| Problem | Solution |
|---------|---------|
| `Could not decrypt` | Check `DOTENV_PRIVATE_KEY` is correct; ask lead if key was rotated |
| `invalid IP address: encrypted:...` | You ran plain `docker compose` without dotenvx; use `dotenvx run -- docker compose ...` or `make up` |
| Variables not loading | Use `dotenvx run -- docker compose up -d` instead of plain compose |
| Need to view values | `dotenvx decrypt && cat .env` (re-encrypt after: `dotenvx encrypt`) |
| `npx` slow on first run | Install globally: `npm install -g @dotenvx/dotenvx` |
| Pre-commit hook blocks commit | Run `dotenvx encrypt`, re-stage `.env`, then commit |

---

## Security Notes

- `.env.keys` is gitignored. Verify: `git check-ignore .env.keys` (should print the filename)
- `.env` is allowlisted in `.gitleaks.toml` — gitleaks knows it is ciphertext, not real secrets
- The pre-commit hook runs `dotenvx ext precommit` to block any unencrypted `.env` from being staged
- There is no keyless `docker compose` path — every run requires the private key
