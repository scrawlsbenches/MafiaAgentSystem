using TestRunner.Framework;
using TestRunner.Tests;

// Create test runner
var runner = new SimpleTestRunner();

// Register all test classes
runner.AddTestClass<RulesEngineTests>();
runner.AddTestClass<RuleTests>();
runner.AddTestClass<RuleBuilderTests>();
runner.AddTestClass<CompositeRuleTests>();
runner.AddTestClass<AgentRoutingTests>();

// Run all tests and return exit code
return await runner.RunAllAsync();
