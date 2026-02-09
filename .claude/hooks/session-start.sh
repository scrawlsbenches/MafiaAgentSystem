#!/bin/bash
set -euo pipefail

# Only run in remote (Claude Code on the web) environments
if [ "${CLAUDE_CODE_REMOTE:-}" != "true" ]; then
  exit 0
fi

# Install .NET SDK 8.0 if not already installed (idempotent)
if ! dotnet --version 2>/dev/null | grep -q '^8\.'; then
  # Update Ubuntu package lists only (avoids 403 errors from blocked PPAs)
  apt-get update -o Dir::Etc::sourcelist="sources.list.d/ubuntu.sources" \
    -o Dir::Etc::sourceparts="-" -o APT::Get::List-Cleanup="0" 2>/dev/null

  apt-get install -y dotnet-sdk-8.0 2>/dev/null
fi

cd "$CLAUDE_PROJECT_DIR"

# Restore all projects (offline - zero NuGet dependencies)
dotnet restore AgentRouting/AgentRouting.sln --source /nonexistent -v quiet
dotnet restore Tests/RulesEngine.Tests/ --source /nonexistent -v quiet
dotnet restore Tests/AgentRouting.Tests/ --source /nonexistent -v quiet
dotnet restore Tests/MafiaDemo.Tests/ --source /nonexistent -v quiet
dotnet restore Tests/TestRunner/ --source /nonexistent -v quiet
dotnet restore RulesEngine.Linq/RulesEngine.Linq/ --source /nonexistent -v quiet
dotnet restore Tests/RulesEngine.Linq.Tests/ --source /nonexistent -v quiet

# Build everything
dotnet build AgentRouting/AgentRouting.sln --no-restore -v quiet
dotnet build Tests/RulesEngine.Tests/ --no-restore -v quiet
dotnet build Tests/AgentRouting.Tests/ --no-restore -v quiet
dotnet build Tests/MafiaDemo.Tests/ --no-restore -v quiet
dotnet build Tests/TestRunner/ --no-restore -v quiet
dotnet build RulesEngine.Linq/RulesEngine.Linq/ --no-restore -v quiet
dotnet build Tests/RulesEngine.Linq.Tests/ --no-restore -v quiet
