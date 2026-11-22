# SteamWorkshopAgent local build configuration

SLN ?= SteamWorkshopAgent.slnx

.PHONY: all
all: build

.PHONY: build
build:
	@python3 scripts/check.py build "$(SLN)"

.PHONY: test
test:
	@python3 scripts/check.py test "$(SLN)"

.PHONY: install
install:
	@python3 scripts/install.py $(REMOTE)

.PHONY: clean
clean:
	rm -rf src/SteamWorkshopAgent/bin src/SteamWorkshopAgent/obj
	rm -rf tests/SteamWorkshopAgent.Tests/bin tests/SteamWorkshopAgent.Tests/obj
	rm -rf src/SteamWorkshopAgent.BridgeTools/bin src/SteamWorkshopAgent.BridgeTools/obj
	rm -rf artifacts/BridgeTools/SteamWorkshopAgent artifacts/publish

.PHONY: help
help:
	@echo "Available targets:"
	@echo "  build   - Build the server"
	@echo "  test    - Run tests"
	@echo "  install - Install the MCP binary and the RimBridge companion DLL (REMOTE=host for another Mac)"
	@echo "  clean   - Remove build artifacts"
	@echo "  help    - Show this help"
