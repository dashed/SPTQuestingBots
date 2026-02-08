# ─── Environment ──────────────────────────────────────────────────────
DOTNET_ROOT  ?= $(HOME)/.dotnet
DOTNET       := $(DOTNET_ROOT)/dotnet
export DOTNET_ROOT
export PATH  := $(DOTNET_ROOT):$(DOTNET_ROOT)/tools:$(PATH)

# ─── Projects ─────────────────────────────────────────────────────────
SOLUTION        := SPTQuestingBots.sln
SERVER_PROJECT  := src/SPTQuestingBots.Server/SPTQuestingBots.Server.csproj
CLIENT_PROJECT  := src/SPTQuestingBots.Client/SPTQuestingBots.Client.csproj
SERVER_TEST     := tests/SPTQuestingBots.Server.Tests/SPTQuestingBots.Server.Tests.csproj
CLIENT_TEST     := tests/SPTQuestingBots.Client.Tests/SPTQuestingBots.Client.Tests.csproj
CONFIGURATION   ?= Release

# ─── Directories ──────────────────────────────────────────────────────
SRC_DIR      := src
TEST_DIR     := tests
LIBS_DIR     := libs

.DEFAULT_GOAL := help
.PHONY: help all ci restore build build-server build-client build-tests test clean format format-check lint lint-fix

# ─── Meta ─────────────────────────────────────────────────────────────

help: ## Show available targets
	@echo "Usage: make <target>"
	@echo ""
	@grep -E '^[a-zA-Z_-]+:.*?## .*$$' $(MAKEFILE_LIST) | \
		awk 'BEGIN {FS = ":.*?## "}; {printf "  \033[36m%-16s\033[0m %s\n", $$1, $$2}'

all: format-check lint test ## Run format-check, lint, and test

ci: restore format-check lint build-tests test ## Full CI pipeline

# ─── Build ────────────────────────────────────────────────────────────

restore: ## Restore NuGet packages
	$(DOTNET) restore $(SOLUTION)

build: build-server build-client ## Build all plugin DLLs (requires libs/)

build-server: ## Build the server plugin DLL
	@if [ ! -d "$(LIBS_DIR)" ]; then \
		echo "Error: $(LIBS_DIR)/ directory not found."; \
		echo "Copy the required DLLs from your SPT installation."; \
		echo "See src/SPTQuestingBots.Server/SPTQuestingBots.Server.csproj for the full list."; \
		exit 1; \
	fi
	$(DOTNET) build $(SERVER_PROJECT) -c $(CONFIGURATION) --nologo

build-client: ## Build the client plugin DLL
	@if [ ! -d "$(LIBS_DIR)" ]; then \
		echo "Error: $(LIBS_DIR)/ directory not found."; \
		echo "Copy the required game DLLs from your SPT installation."; \
		echo "See src/SPTQuestingBots.Client/SPTQuestingBots.Client.csproj for the full list."; \
		exit 1; \
	fi
	$(DOTNET) build $(CLIENT_PROJECT) -c $(CONFIGURATION) --nologo

build-tests: ## Build the test projects
	$(DOTNET) build $(SERVER_TEST) -c $(CONFIGURATION) --nologo
	$(DOTNET) build $(CLIENT_TEST) -c $(CONFIGURATION) --nologo

# ─── Test ─────────────────────────────────────────────────────────────

test: ## Run all unit tests
	$(DOTNET) test $(SERVER_TEST) -c $(CONFIGURATION) --nologo
	$(DOTNET) test $(CLIENT_TEST) -c $(CONFIGURATION) --nologo

test-server: ## Run server-side tests only
	$(DOTNET) test $(SERVER_TEST) -c $(CONFIGURATION) --nologo

test-client: ## Run client-side tests only
	$(DOTNET) test $(CLIENT_TEST) -c $(CONFIGURATION) --nologo

# ─── Format ───────────────────────────────────────────────────────────

format: ## Auto-format code with CSharpier
	csharpier format $(SRC_DIR) $(TEST_DIR)

format-check: ## Check code formatting (CI-safe, no changes)
	csharpier check $(SRC_DIR) $(TEST_DIR)

# ─── Lint ─────────────────────────────────────────────────────────────

lint: ## Check code style against .editorconfig
	$(DOTNET) format $(SERVER_TEST) --verify-no-changes --no-restore -v diag
	$(DOTNET) format $(CLIENT_TEST) --verify-no-changes --no-restore -v diag

lint-fix: ## Auto-fix code style issues
	$(DOTNET) format $(SERVER_TEST) --no-restore -v diag
	$(DOTNET) format $(CLIENT_TEST) --no-restore -v diag

# ─── Clean ────────────────────────────────────────────────────────────

clean: ## Remove build artifacts
	$(DOTNET) clean $(SOLUTION) --nologo -v q 2>/dev/null || true
	find . -type d \( -name bin -o -name obj \) -exec rm -rf {} + 2>/dev/null || true
