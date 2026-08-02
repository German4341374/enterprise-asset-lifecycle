DOTNET ?= dotnet

.PHONY: setup format lint test test-unit test-integration build up down clean

setup:
	$(DOTNET) restore EnterpriseAssetLifecycle.slnx

format:
	$(DOTNET) format EnterpriseAssetLifecycle.slnx

lint:
	$(DOTNET) format EnterpriseAssetLifecycle.slnx --verify-no-changes
	$(DOTNET) build EnterpriseAssetLifecycle.slnx --configuration Release --no-restore

test: test-unit test-integration

test-unit:
	$(DOTNET) test tests/EnterpriseAssetLifecycle.UnitTests --configuration Release

test-integration:
	$(DOTNET) test tests/EnterpriseAssetLifecycle.IntegrationTests --configuration Release

build:
	$(DOTNET) build EnterpriseAssetLifecycle.slnx --configuration Release

up:
	docker compose up --build --detach

down:
	docker compose down

clean:
	$(DOTNET) clean EnterpriseAssetLifecycle.slnx
	docker compose down --volumes --remove-orphans

