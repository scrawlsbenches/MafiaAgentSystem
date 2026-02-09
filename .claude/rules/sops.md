# General Development Standard Operating Procedures

These procedures define the reasoning order for working on this codebase. Follow them before writing code.

## Before Any Code Change

1. Read the file you intend to modify — understand existing patterns before changing them
2. Identify which system is affected (RulesEngine Core, RulesEngine.Linq, AgentRouting, MafiaDemo)
3. Check `TASK_LIST.md` — is this work already tracked? Already done?
4. Check `EXECUTION_PLAN.md` — does this conflict with in-progress work?

## Build Verification Sequence

Always run in this order — each step depends on the previous:

1. `dotnet restore <project> --source /nonexistent` (NuGet is blocked — always use offline restore)
2. `dotnet build <project> --no-restore`
3. `dotnet run --project Tests/TestRunner/ --no-build` (runs all tests)

Never skip step 1 if project files changed. Never skip step 3 after code changes.

## When Adding a New Class or Interface

1. Determine which project it belongs to by checking the architecture table in CLAUDE.md
2. Check existing interfaces in that project — does an abstraction already exist?
3. Follow the dependency inversion pattern: constructors accept abstractions, not concretions
4. Follow single responsibility: one class, one concern
5. Add tests in the corresponding test project under `Tests/`
6. Build and run tests before considering the work complete

## When Modifying Existing Code

1. Read the file and understand the surrounding code
2. Check if the class is referenced by tests — run those tests first to establish a baseline
3. Make the change
4. Run the same tests — compare results to baseline
5. If tests fail, determine whether the test or the code is wrong before "fixing" either

## When a Test Fails

1. Read the test — understand what it asserts and why
2. Read the code under test — understand the current behavior
3. Determine: is the test wrong (outdated expectation) or is the code wrong (regression)?
4. If the code is wrong: fix the code, not the test
5. If the test is wrong: update the test expectation with a comment explaining why it changed
6. Never delete a failing test without understanding why it exists

## When Working Across Multiple Systems

This codebase has cross-system dependencies. Respect the dependency direction:

```
RulesEngine.Core ← MafiaDemo (consumes 8 engine instances)
AgentRouting     ← MafiaDemo (consumes agents, middleware, routing)
RulesEngine.Linq   (independent, future replacement for Core)
TestRunner.Framework ← All test projects
TestUtilities        ← All test projects
```

Changes to RulesEngine.Core or AgentRouting can break MafiaDemo. After modifying a core library:
1. Build MafiaDemo: `dotnet build AgentRouting/AgentRouting.sln --no-restore`
2. Run MafiaDemo tests: `dotnet run --project Tests/TestRunner/ --no-build -- Tests/MafiaDemo.Tests/bin/Debug/net8.0/MafiaDemo.Tests.dll`

## Constraint: Zero Third-Party Dependencies

This project has zero NuGet dependencies by design. Do not add NuGet packages. If you need functionality typically provided by a package, implement it locally or find an alternative approach.

## After Completing Work

1. Run full test suite: `dotnet run --project Tests/TestRunner/ --no-build`
2. If working on a tracked task, update `TASK_LIST.md` and `EXECUTION_PLAN.md`
3. Commit with a descriptive message explaining why, not what
