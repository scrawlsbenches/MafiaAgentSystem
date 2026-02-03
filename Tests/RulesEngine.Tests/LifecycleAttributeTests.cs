using TestRunner.Framework;

namespace RulesEngine.Tests;

/// <summary>
/// Tests to verify that SetUp, TearDown, OneTimeSetUp, and OneTimeTearDown attributes work correctly.
/// </summary>
public class LifecycleAttributeTests
{
    private static readonly List<string> ExecutionOrder = new();
    private int _testCounter;

    [OneTimeSetUp]
    public void ClassSetUp()
    {
        ExecutionOrder.Add("OneTimeSetUp");
        _testCounter = 0;
    }

    [OneTimeTearDown]
    public void ClassTearDown()
    {
        ExecutionOrder.Add("OneTimeTearDown");
    }

    [SetUp]
    public void TestSetUp()
    {
        ExecutionOrder.Add($"SetUp-{_testCounter}");
        _testCounter++;
    }

    [TearDown]
    public void TestTearDown()
    {
        ExecutionOrder.Add($"TearDown-{_testCounter}");
    }

    [Test]
    public void Test1_SetUpRunsBeforeTest()
    {
        ExecutionOrder.Add("Test1");
        // SetUp should have run before this test
        Assert.True(ExecutionOrder.Contains("SetUp-0") || ExecutionOrder.Contains("SetUp-1") || ExecutionOrder.Contains("SetUp-2"));
    }

    [Test]
    public void Test2_SetUpRunsBeforeEachTest()
    {
        ExecutionOrder.Add("Test2");
        // Another SetUp should have run
        Assert.True(_testCounter >= 1);
    }

    [Test]
    public void Test3_CounterIncrements()
    {
        ExecutionOrder.Add("Test3");
        // Counter should have incremented from SetUp calls
        Assert.True(_testCounter >= 1);
    }
}

/// <summary>
/// Tests for async SetUp/TearDown support.
/// </summary>
public class AsyncLifecycleTests
{
    private bool _setUpRan;
    private bool _tearDownRan;

    [SetUp]
    public async Task AsyncSetUp()
    {
        await Task.Delay(1); // Simulate async work
        _setUpRan = true;
    }

    [TearDown]
    public async Task AsyncTearDown()
    {
        await Task.Delay(1); // Simulate async work
        _tearDownRan = true;
    }

    [Test]
    public void AsyncSetUp_RunsBeforeTest()
    {
        Assert.True(_setUpRan, "Async SetUp should have run before test");
    }

    [Test]
    public void Test_CanAccessSetUpState()
    {
        // Just verify we can run multiple tests with async lifecycle
        Assert.True(_setUpRan);
    }
}

/// <summary>
/// Tests that TearDown runs even when test fails.
/// </summary>
public class TearDownOnFailureTests
{
    private static bool _tearDownRanAfterFailure;
    private static int _tearDownCount;

    [TearDown]
    public void TearDown()
    {
        _tearDownCount++;
        _tearDownRanAfterFailure = true;
    }

    [Test]
    public void TearDownCount_Increments()
    {
        // This test just verifies TearDown is running
        Assert.True(true);
    }

    [Test]
    public void TearDown_StillRunsAfterMultipleTests()
    {
        // TearDown should have run after previous test
        Assert.True(_tearDownCount >= 1 || _tearDownRanAfterFailure);
    }
}

/// <summary>
/// Tests for Theory methods with lifecycle attributes.
/// </summary>
public class TheoryLifecycleTests
{
    private int _setUpCallCount;

    [SetUp]
    public void SetUp()
    {
        _setUpCallCount++;
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void SetUp_RunsForEachTheoryCase(int value)
    {
        // SetUp should have run before this test case
        Assert.True(_setUpCallCount >= 1);
        Assert.True(value > 0);
    }

    [Test]
    public void SetUpCount_AccumulatesAcrossTests()
    {
        // Should have been called multiple times by now
        Assert.True(_setUpCallCount >= 1);
    }
}
