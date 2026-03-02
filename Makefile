.PHONY: build deploy update up down restart logs setup clean bump \
        _jsinjector _filetransform _meta

PLUGIN_DLL := Jellyfin.Plugin.JellyFlare/bin/Release/net9.0/Jellyfin.Plugin.JellyFlare.dll
PLUGIN_DIR := docker/config/data/plugins/JellyFlare
COMPOSE    := docker compose -f docker/compose.yml
CSPROJ     := Jellyfin.Plugin.JellyFlare/JellyFlare.csproj

# ── build ──────────────────────────────────────────────────────────────────────

build:
	dotnet build --configuration Release
	mkdir -p $(PLUGIN_DIR)
	cp $(PLUGIN_DLL) $(PLUGIN_DIR)/

# ── docker ─────────────────────────────────────────────────────────────────────

up:
	$(COMPOSE) up -d
	$(COMPOSE) logs -f

down:
	$(COMPOSE) down

restart:
	$(COMPOSE) restart jellyfin

logs:
	@$(COMPOSE) ps --services --filter status=running | grep -q jellyfin \
		|| (echo "Jellyfin is not running — use 'make up' or 'make dev' first."; exit 1)
	$(COMPOSE) logs -f jellyfin

# ── dev loop ───────────────────────────────────────────────────────────────────

## build → copy DLL → restart container → logs
update: build
	$(COMPOSE) restart jellyfin
	$(COMPOSE) logs -f

# ── setup ──────────────────────────────────────────────────────────────────────

## first-time bootstrap: setup → build → start (run once)
deploy: setup build up

## download dependencies and write plugin metadata
setup: _jsinjector _filetransform _meta
	@echo "→ Setup complete."

_jsinjector:
	@echo "→ [1/3] Downloading JS Injector..."
	@mkdir -p docker/config/data/plugins/JavaScriptInjector
	@URL=$$(curl -s https://raw.githubusercontent.com/n00bcodr/jellyfin-plugins/main/10.11/manifest.json \
		| python3 -c "import json,sys; d=json.load(sys.stdin); \
		  print(next(p['versions'][0]['sourceUrl'] for p in d if 'JavaScript' in p.get('name','')))") \
	&& curl -sL -o /tmp/jsinjector.zip "$$URL" \
	&& unzip -qo /tmp/jsinjector.zip -d docker/config/data/plugins/JavaScriptInjector \
	&& rm /tmp/jsinjector.zip

_filetransform:
	@echo "→ [2/3] Downloading File Transformation..."
	@mkdir -p "docker/config/data/plugins/File Transformation"
	@URL=$$(curl -s https://www.iamparadox.dev/jellyfin/plugins/manifest.json \
		| python3 -c "import json,sys; d=json.load(sys.stdin); \
		  ft=next(p for p in d if 'Transformation' in p.get('name','')); \
		  print(next(v['sourceUrl'] for v in ft['versions'] if v.get('targetAbi')=='10.11.6.0'))") \
	&& curl -sL -o /tmp/filetransform.zip "$$URL" \
	&& unzip -qo /tmp/filetransform.zip -d "docker/config/data/plugins/File Transformation" \
	&& rm /tmp/filetransform.zip

_meta:
	@echo "→ [3/3] Writing plugin metadata..."
	@mkdir -p $(PLUGIN_DIR)
	@cp assets/icon.png $(PLUGIN_DIR)/
	@jq '.[0] | {category:.category,changelog:.versions[0].changelog,description:.description, \
	      guid:.guid,name:.name,overview:.overview,owner:.owner, \
	      targetAbi:.versions[0].targetAbi,timestamp:.versions[0].timestamp, \
	      version:.versions[0].version,status:"Active",autoUpdate:true,assemblies:[]}' \
		manifest.json > $(PLUGIN_DIR)/meta.json

# ── version bump ───────────────────────────────────────────────────────────────

## usage: make bump V=1.2.0
bump:
ifndef V
	$(error Usage: make bump V=1.2.0)
endif
	@sed -i '' 's|<AssemblyVersion>.*</AssemblyVersion>|<AssemblyVersion>$(V).0</AssemblyVersion>|' $(CSPROJ)
	@sed -i '' 's|<FileVersion>.*</FileVersion>|<FileVersion>$(V).0</FileVersion>|' $(CSPROJ)
	@echo "→ Version bumped to $(V).0 — commit, push, then create tag v$(V) on GitHub."

# ── clean ──────────────────────────────────────────────────────────────────────

clean:
	rm -rf Jellyfin.Plugin.JellyFlare/bin Jellyfin.Plugin.JellyFlare/obj
	rm -rf docker/config
