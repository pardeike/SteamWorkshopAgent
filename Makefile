# SteamWorkshopAgent local build configuration

SLN ?= SteamWorkshopAgent.slnx
PROJECT ?= src/SteamWorkshopAgent/SteamWorkshopAgent.csproj
INSTALL_DIR ?= $(HOME)/.codex/mcp-servers/steam-workshop-agent
BINARY_NAME ?= SteamWorkshopAgent
RID ?= osx-arm64

.PHONY: all
all: build

.PHONY: build
build:
	dotnet build $(SLN) -c Release

.PHONY: test
test:
	dotnet test $(SLN)

.PHONY: install
install:
	mkdir -p "$(INSTALL_DIR)"
	dotnet publish $(PROJECT) -c Release -r $(RID) --self-contained true \
		-p:PublishSingleFile=true \
		-p:PublishTrimmed=false \
		-o "$(INSTALL_DIR)"
	chmod +x "$(INSTALL_DIR)/$(BINARY_NAME)"

.PHONY: clean
clean:
	rm -rf src/SteamWorkshopAgent/bin src/SteamWorkshopAgent/obj
	rm -rf tests/SteamWorkshopAgent.Tests/bin tests/SteamWorkshopAgent.Tests/obj

.PHONY: help
help:
	@echo "Available targets:"
	@echo "  build   - Build the server"
	@echo "  test    - Run tests"
	@echo "  install - Publish stable local MCP binary to $(INSTALL_DIR)"
	@echo "  clean   - Remove build artifacts"
	@echo "  help    - Show this help"
