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

# ─── SPT Installation ────────────────────────────────────────────────
SPT_DIR      ?= /mnt/e/sp_tarkov/40

# DLLs from SPT/ (server assemblies)
SPT_SERVER_DLLS := \
	SPTarkov.Server.Core.dll \
	SPTarkov.DI.dll \
	SPTarkov.Common.dll

# DLLs from BepInEx/core/
BEPINEX_CORE_DLLS := \
	BepInEx.dll \
	0Harmony.dll

# DLLs from EscapeFromTarkov_Data/Managed/
EFT_MANAGED_DLLS := \
	Assembly-CSharp.dll \
	UnityEngine.dll \
	UnityEngine.CoreModule.dll \
	UnityEngine.IMGUIModule.dll \
	UnityEngine.PhysicsModule.dll \
	UnityEngine.TextRenderingModule.dll \
	UnityEngine.AIModule.dll \
	UnityEngine.UI.dll \
	Comfort.dll \
	Comfort.Unity.dll \
	CommonExtensions.dll \
	DissonanceVoip.dll \
	ItemComponent.Types.dll \
	Newtonsoft.Json.dll \
	Sirenix.Serialization.dll \
	Unity.Postprocessing.Runtime.dll \
	Unity.TextMeshPro.dll

# DLLs from BepInEx/plugins/spt/
SPT_PLUGIN_DLLS := \
	spt-common.dll \
	spt-core.dll \
	spt-custom.dll \
	spt-reflection.dll \
	spt-singleplayer.dll

# DLLs from BepInEx/patchers/
SPT_PATCHER_DLLS := \
	spt-prepatch.dll

# DLLs from BepInEx/plugins/ (third-party mods)
THIRDPARTY_DLLS := \
	DrakiaXYZ-BigBrain.dll

# All DLLs that must be present in libs/
ALL_DLLS := $(SPT_SERVER_DLLS) $(BEPINEX_CORE_DLLS) $(EFT_MANAGED_DLLS) \
	$(SPT_PLUGIN_DLLS) $(SPT_PATCHER_DLLS) $(THIRDPARTY_DLLS)

.DEFAULT_GOAL := help
.PHONY: help all ci restore build build-server build-client build-tests \
	test test-server test-client clean format format-check lint lint-fix \
	copy-libs check-libs

# ─── Meta ─────────────────────────────────────────────────────────────

help: ## Show available targets
	@echo "Usage: make <target>"
	@echo ""
	@grep -E '^[a-zA-Z_-]+:.*?## .*$$' $(MAKEFILE_LIST) | \
		awk 'BEGIN {FS = ":.*?## "}; {printf "  \033[36m%-16s\033[0m %s\n", $$1, $$2}'

all: format-check lint test ## Run format-check, lint, and test

ci: restore format-check lint build-tests test ## Full CI pipeline

# ─── Libs ─────────────────────────────────────────────────────────────

copy-libs: ## Copy required DLLs from SPT installation (SPT_DIR)
	@if [ ! -d "$(SPT_DIR)" ]; then \
		echo "Error: SPT_DIR not found: $(SPT_DIR)"; \
		echo "Set SPT_DIR to your SPT 4.x installation, e.g.:"; \
		echo "  make copy-libs SPT_DIR=/path/to/spt"; \
		exit 1; \
	fi
	@mkdir -p $(LIBS_DIR)
	@echo "Copying DLLs from $(SPT_DIR) → $(LIBS_DIR)/"
	@for dll in $(SPT_SERVER_DLLS); do \
		cp -v "$(SPT_DIR)/SPT/$$dll" $(LIBS_DIR)/; \
	done
	@for dll in $(BEPINEX_CORE_DLLS); do \
		cp -v "$(SPT_DIR)/BepInEx/core/$$dll" $(LIBS_DIR)/; \
	done
	@for dll in $(EFT_MANAGED_DLLS); do \
		cp -v "$(SPT_DIR)/EscapeFromTarkov_Data/Managed/$$dll" $(LIBS_DIR)/; \
	done
	@for dll in $(SPT_PLUGIN_DLLS); do \
		cp -v "$(SPT_DIR)/BepInEx/plugins/spt/$$dll" $(LIBS_DIR)/; \
	done
	@for dll in $(SPT_PATCHER_DLLS); do \
		cp -v "$(SPT_DIR)/BepInEx/patchers/$$dll" $(LIBS_DIR)/; \
	done
	@for dll in $(THIRDPARTY_DLLS); do \
		cp -v "$(SPT_DIR)/BepInEx/plugins/$$dll" $(LIBS_DIR)/; \
	done
	@echo "Done. Copied $$(ls $(LIBS_DIR)/*.dll 2>/dev/null | wc -l) DLLs."

check-libs: ## Check that all required DLLs are present in libs/
	@missing=0; \
	for dll in $(ALL_DLLS); do \
		if [ ! -f "$(LIBS_DIR)/$$dll" ]; then \
			echo "MISSING: $(LIBS_DIR)/$$dll"; \
			missing=$$((missing + 1)); \
		fi; \
	done; \
	if [ $$missing -gt 0 ]; then \
		echo ""; \
		echo "$$missing DLL(s) missing. Run 'make copy-libs' to copy them."; \
		exit 1; \
	else \
		echo "All $$(echo $(ALL_DLLS) | wc -w) required DLLs are present."; \
	fi

# ─── Build ────────────────────────────────────────────────────────────

restore: ## Restore NuGet packages
	$(DOTNET) restore $(SOLUTION)

build: build-server build-client ## Build all plugin DLLs (requires libs/)

build-server: check-libs ## Build the server plugin DLL
	$(DOTNET) build $(SERVER_PROJECT) -c $(CONFIGURATION) --nologo

build-client: check-libs ## Build the client plugin DLL
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
