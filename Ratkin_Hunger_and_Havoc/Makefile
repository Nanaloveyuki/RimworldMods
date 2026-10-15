PYTHON ?= python3
RHAH = $(PYTHON) scripts/rhah.py
.DEFAULT_GOAL := help
.NOTPARALLEL:

.PHONY: help doctor check build test ci package deploy report logs carbon-doctor carbon-status carbon-tools carbon-events carbon-smoke carbon-prepare carbon-start carbon-stop carbon-new-test mcp-config tools-test
help:
	@$(RHAH) --help

doctor check build test ci package deploy:
	@$(RHAH) $@

report:
	@$(RHAH) report

logs:
	@$(RHAH) logs

carbon-doctor:
	@$(RHAH) carbon doctor

carbon-status:
	@$(RHAH) carbon status

carbon-tools:
	@$(RHAH) carbon tools

carbon-events:
	@$(RHAH) carbon events

carbon-smoke:
	@$(RHAH) carbon run

carbon-prepare:
	@$(RHAH) carbon prepare --minimal

carbon-start:
	@$(RHAH) carbon start

carbon-stop:
	@$(RHAH) carbon stop

carbon-new-test:
	@$(RHAH) carbon run scripts/carbon/rhah-new-test.json

mcp-config:
	@$(RHAH) carbon config

tools-test:
	@$(RHAH) test --tools-only
