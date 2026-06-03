# Convenience wrapper around Docker Compose for the QuraEx backend.
#
# The full stack spans two compose files (infra + app services). These targets
# combine them so a single command brings everything up reproducibly — the same
# on every machine. Use `make up` for the containerized stack, or `make up-infra`
# when you'd rather run the services from your IDE / Aspire against containers.

COMPOSE    := docker compose
INFRA_FILE := docker-compose.yml
APP_FILE   := docker-compose.app.yml
STACK      := $(COMPOSE) -f $(INFRA_FILE) -f $(APP_FILE)

.PHONY: up up-infra build down logs ps clean help

help: ## Show available targets
	@grep -E '^[a-zA-Z_-]+:.*?## .*$$' $(MAKEFILE_LIST) | \
		awk 'BEGIN {FS = ":.*?## "}; {printf "  \033[36m%-12s\033[0m %s\n", $$1, $$2}'

up: ## Build images and start the full stack (services + infra)
	$(STACK) up --build -d
	@echo "Gateway:             http://localhost:$${GATEWAY_PORT:-8080}"
	@echo "Authoring (direct):  http://localhost:$${AUTHORING_PORT:-8081}"
	@echo "RabbitMQ UI:         http://localhost:$${RABBITMQ_MGMT_PORT:-15672}"

up-infra: ## Start only backing infra (run services from IDE / Aspire)
	$(COMPOSE) -f $(INFRA_FILE) up -d

build: ## (Re)build service images without starting
	$(STACK) build

down: ## Stop and remove the full stack containers
	$(STACK) down

logs: ## Follow logs from all stack containers
	$(STACK) logs -f

ps: ## List stack containers
	$(STACK) ps

clean: ## Stop the stack and remove named volumes (DESTROYS local data)
	$(STACK) down -v
