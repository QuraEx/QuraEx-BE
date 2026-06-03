# Convenience wrapper around Docker Compose for the QuraEx backend.
#
# docker-compose.override.yml is auto-merged by `docker compose` whenever both
# docker-compose.yml and docker-compose.override.yml are present in the same
# directory.
#
# ALL compose invocations go through dotenvx so the encrypted .env is decrypted
# at runtime. A plain `docker compose up` WITHOUT dotenvx will fail with
# "invalid IP address: encrypted:..." — that is intentional (model B design).
#
# Prerequisite: export DOTENV_PRIVATE_KEY='...'  (get from team lead)
# Reference: DOTENVX_QUICK_START.md

COMPOSE    := docker compose
DOTENVX    := npx @dotenvx/dotenvx@latest

.PHONY: up up-infra build down logs ps clean secrets-encrypt secrets-decrypt help

help: ## Show available targets
	@grep -E '^[a-zA-Z_-]+:.*?## .*$$' $(MAKEFILE_LIST) | \
		awk 'BEGIN {FS = ":.*?## "}; {printf "  \033[36m%-12s\033[0m %s\n", $$1, $$2}'

up: ## Build and start full stack (requires DOTENV_PRIVATE_KEY — see DOTENVX_QUICK_START.md)
	$(DOTENVX) run -- $(COMPOSE) up -d --build
	@echo "Gateway:             http://localhost:$${GATEWAY_PORT:-8080}"
	@echo "Authoring (direct):  http://localhost:$${AUTHORING_PORT:-8081}"
	@echo "Identity (direct):   http://localhost:$${IDENTITY_PORT:-8082}"
	@echo "Workspace (direct):  http://localhost:$${WORKSPACE_PORT:-8083}"
	@echo "RabbitMQ UI:         http://localhost:$${RABBITMQ_MGMT_PORT:-15672}"

secrets-encrypt: ## Encrypt .env in place with dotenvx (writes ciphertext, safe to commit)
	$(DOTENVX) encrypt

secrets-decrypt: ## Decrypt .env to plaintext (use only locally, re-encrypt before committing)
	$(DOTENVX) decrypt

up-infra: ## Start only backing infra (run services from IDE / Aspire)
	$(DOTENVX) run -- $(COMPOSE) -f docker-compose.yml up -d

build: ## (Re)build service images without starting
	$(DOTENVX) run -- $(COMPOSE) build

down: ## Stop and remove the full stack containers
	$(DOTENVX) run -- $(COMPOSE) down

logs: ## Follow logs from all stack containers
	$(DOTENVX) run -- $(COMPOSE) logs -f

ps: ## List stack containers
	$(DOTENVX) run -- $(COMPOSE) ps

clean: ## Stop the stack and remove named volumes (DESTROYS local data)
	$(DOTENVX) run -- $(COMPOSE) down -v
