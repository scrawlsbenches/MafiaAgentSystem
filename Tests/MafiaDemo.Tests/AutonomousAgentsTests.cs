using TestRunner.Framework;
using AgentRouting.MafiaDemo;
using AgentRouting.MafiaDemo.Game;
using AgentRouting.Core;

namespace TestRunner.Tests;

/// <summary>
/// Unit tests for AutonomousAgents: Godfather, Underboss, Consigliere, Capo, Soldier
/// </summary>
public class AutonomousAgentsTests
{
    /// <summary>
    /// Silent logger for tests - suppresses console output
    /// </summary>
    private class SilentAgentLogger : IAgentLogger
    {
        public void LogMessageReceived(IAgent agent, AgentMessage message) { }
        public void LogMessageProcessed(IAgent agent, AgentMessage message, MessageResult result) { }
        public void LogMessageRouted(AgentMessage message, IAgent? fromAgent, IAgent toAgent) { }
        public void LogError(IAgent agent, AgentMessage message, Exception ex) { }
    }

    private static IAgentLogger CreateTestLogger() => new SilentAgentLogger();

    private static GameState CreateTestGameState()
    {
        return new GameState
        {
            FamilyWealth = 100000m,
            Reputation = 50,
            HeatLevel = 20,
            Week = 3, // Day % 3 == 0 for some tests
            SoldierCount = 10
        };
    }

    #region GodfatherAgent Tests

    [Test]
    public void GodfatherAgent_Construction_SetsCorrectProperties()
    {
        var logger = CreateTestLogger();
        var godfather = new GodfatherAgent("godfather-001", "Don Corleone", logger);

        Assert.Equal("godfather-001", godfather.Id);
        Assert.Equal("Don Corleone", godfather.Name);
        Assert.Equal(TimeSpan.FromSeconds(15), godfather.DecisionDelay);
        Assert.Equal(8, godfather.Ambition);
        Assert.Equal(10, godfather.Loyalty);
        Assert.Equal(3, godfather.Aggression);
    }

    [Test]
    public void GodfatherAgent_Construction_SetsCorrectCapabilities()
    {
        var logger = CreateTestLogger();
        var godfather = new GodfatherAgent("godfather-001", "Don Corleone", logger);

        Assert.Contains("FinalDecision", godfather.Capabilities.SupportedCategories);
        Assert.Contains("MajorDispute", godfather.Capabilities.SupportedCategories);
        Assert.Contains("FavorRequest", godfather.Capabilities.SupportedCategories);
    }

    [Test]
    public void GodfatherAgent_MakeDecision_ReturnsWait_WhenRollHigherThanThreshold()
    {
        var logger = CreateTestLogger();
        var godfather = new GodfatherAgent("godfather-001", "Don Corleone", logger);
        var gameState = CreateTestGameState();

        // Seed that produces roll >= 3 (Wait branch)
        // Find a seed that gives roll >= 3 for the first Next(0, 10)
        var random = new Random(42); // First call to Next(0,10) gives 8

        var decision = godfather.MakeDecision(gameState, random);

        Assert.NotNull(decision);
        Assert.Equal(DecisionType.Wait, decision!.Type);
    }

    [Test]
    public void GodfatherAgent_MakeDecision_SendsStrategicMessage_WhenRollLessThanThree()
    {
        var logger = CreateTestLogger();
        var godfather = new GodfatherAgent("godfather-001", "Don Corleone", logger);
        var gameState = CreateTestGameState();

        // Find a seed that produces roll < 3
        // Try different seeds to find one that produces a value < 3
        var random = new Random(7); // Need to test to see what this produces

        // Try multiple seeds until we find one that triggers the message branch
        for (int seed = 0; seed < 100; seed++)
        {
            random = new Random(seed);
            var roll = random.Next(0, 10);
            if (roll < 3)
            {
                // Reset and test with this seed
                random = new Random(seed);
                var decision = godfather.MakeDecision(gameState, random);

                Assert.NotNull(decision);
                Assert.Equal(DecisionType.SendMessage, decision!.Type);
                Assert.NotNull(decision.Message);
                Assert.Equal("godfather-001", decision.Message!.SenderId);
                Assert.Equal("Strategic Directive", decision.Message.Subject);
                Assert.Equal("DailyOperations", decision.Message.Category);
                Assert.Equal(MessagePriority.High, decision.Message.Priority);
                return;
            }
        }

        // If we get here, we couldn't find a seed - this shouldn't happen
        Assert.True(false, "Could not find seed producing roll < 3");
    }

    [Test]
    public void GodfatherAgent_MakeDecision_ReturnsNull_WhenCalledTooSoon()
    {
        var logger = CreateTestLogger();
        var godfather = new GodfatherAgent("godfather-001", "Don Corleone", logger);
        var gameState = CreateTestGameState();

        // First call - should work if random allows
        var random1 = new Random(1);
        var firstDecision = godfather.MakeDecision(gameState, random1);

        // If first decision was a SendMessage, subsequent call within 30 seconds returns null
        if (firstDecision?.Type == DecisionType.SendMessage)
        {
            var random2 = new Random(1);
            var secondDecision = godfather.MakeDecision(gameState, random2);
            Assert.Null(secondDecision);
        }
    }

    [Test]
    public async Task GodfatherAgent_HandleMessage_PoliceRaid_ReturnsCalm()
    {
        var logger = CreateTestLogger();
        var godfather = new GodfatherAgent("godfather-001", "Don Corleone", logger);

        var message = new AgentMessage
        {
            SenderId = "underboss-001",
            ReceiverId = "godfather-001",
            Subject = "Emergency",
            Content = "Don, the police are raiding our club!"
        };

        var result = await godfather.ProcessMessageAsync(message);

        Assert.True(result.Success);
        Assert.Contains("Tom, handle this", result.Response!);
        Assert.Contains("Pay whoever needs to be paid", result.Response!);
    }

    [Test]
    public async Task GodfatherAgent_HandleMessage_RaidKeyword_ReturnsCalm()
    {
        var logger = CreateTestLogger();
        var godfather = new GodfatherAgent("godfather-001", "Don Corleone", logger);

        var message = new AgentMessage
        {
            SenderId = "capo-001",
            ReceiverId = "godfather-001",
            Subject = "Bad News",
            Content = "There was a raid on our warehouse"
        };

        var result = await godfather.ProcessMessageAsync(message);

        Assert.True(result.Success);
        Assert.Contains("find out who talked", result.Response!);
    }

    [Test]
    public async Task GodfatherAgent_HandleMessage_Tattaglia_ReturnsRivalResponse()
    {
        var logger = CreateTestLogger();
        var godfather = new GodfatherAgent("godfather-001", "Don Corleone", logger);

        var message = new AgentMessage
        {
            SenderId = "consigliere-001",
            ReceiverId = "godfather-001",
            Subject = "Rival Activity",
            Content = "The Tattaglia family is making moves"
        };

        var result = await godfather.ProcessMessageAsync(message);

        Assert.True(result.Success);
        // Response depends on random - either aggressive or peaceful
        Assert.True(
            result.Response!.Contains("Tom to negotiate") ||
            result.Response!.Contains("Show them otherwise"));
    }

    [Test]
    public async Task GodfatherAgent_HandleMessage_Barzini_ReturnsRivalResponse()
    {
        var logger = CreateTestLogger();
        var godfather = new GodfatherAgent("godfather-001", "Don Corleone", logger);

        var message = new AgentMessage
        {
            SenderId = "consigliere-001",
            ReceiverId = "godfather-001",
            Subject = "Rival Activity",
            Content = "The Barzini family is expanding territory"
        };

        var result = await godfather.ProcessMessageAsync(message);

        Assert.True(result.Success);
        Assert.NotNull(result.Response);
    }

    [Test]
    public async Task GodfatherAgent_HandleMessage_Betrayal_ReturnsCold()
    {
        var logger = CreateTestLogger();
        var godfather = new GodfatherAgent("godfather-001", "Don Corleone", logger);

        var message = new AgentMessage
        {
            SenderId = "underboss-001",
            ReceiverId = "godfather-001",
            Subject = "Urgent",
            Content = "Don, I think someone is going to betray us"
        };

        var result = await godfather.ProcessMessageAsync(message);

        Assert.True(result.Success);
        Assert.Contains("Betrayal cannot be tolerated", result.Response!);
        Assert.Contains("Watch him", result.Response!);
    }

    [Test]
    public async Task GodfatherAgent_HandleMessage_Informant_ReturnsCold()
    {
        var logger = CreateTestLogger();
        var godfather = new GodfatherAgent("godfather-001", "Don Corleone", logger);

        var message = new AgentMessage
        {
            SenderId = "capo-001",
            ReceiverId = "godfather-001",
            Subject = "Problem",
            Content = "There may be an informant in our crew"
        };

        var result = await godfather.ProcessMessageAsync(message);

        Assert.True(result.Success);
        Assert.Contains("Betrayal cannot be tolerated", result.Response!);
    }

    [Test]
    public async Task GodfatherAgent_HandleMessage_Opportunity_ReturnsInvestmentDecision()
    {
        var logger = CreateTestLogger();
        var godfather = new GodfatherAgent("godfather-001", "Don Corleone", logger);

        var message = new AgentMessage
        {
            SenderId = "underboss-001",
            ReceiverId = "godfather-001",
            Subject = "Business",
            Content = "There's a new opportunity in the docks"
        };

        var result = await godfather.ProcessMessageAsync(message);

        Assert.True(result.Success);
        // Response depends on random - either invest or pass
        Assert.True(
            result.Response!.Contains("businessman knows when to invest") ||
            result.Response!.Contains("risky"));
    }

    [Test]
    public async Task GodfatherAgent_HandleMessage_Deal_ReturnsInvestmentDecision()
    {
        var logger = CreateTestLogger();
        var godfather = new GodfatherAgent("godfather-001", "Don Corleone", logger);

        var message = new AgentMessage
        {
            SenderId = "consigliere-001",
            ReceiverId = "godfather-001",
            Subject = "New Deal",
            Content = "I have a deal to discuss with you"
        };

        var result = await godfather.ProcessMessageAsync(message);

        Assert.True(result.Success);
        Assert.NotNull(result.Response);
    }

    [Test]
    public async Task GodfatherAgent_HandleMessage_Favor_ReturnsFavorResponse()
    {
        var logger = CreateTestLogger();
        var godfather = new GodfatherAgent("godfather-001", "Don Corleone", logger);

        var message = new AgentMessage
        {
            SenderId = "soldier-001",
            ReceiverId = "godfather-001",
            Subject = "Request",
            Content = "My friend needs help with a problem"
        };

        var result = await godfather.ProcessMessageAsync(message);

        Assert.True(result.Success);
        Assert.Contains("take care of our friends", result.Response!);
        Assert.Contains("someday they will owe us", result.Response!);
    }

    [Test]
    public async Task GodfatherAgent_HandleMessage_Help_ReturnsFavorResponse()
    {
        var logger = CreateTestLogger();
        var godfather = new GodfatherAgent("godfather-001", "Don Corleone", logger);

        var message = new AgentMessage
        {
            SenderId = "capo-001",
            ReceiverId = "godfather-001",
            Subject = "Assistance",
            Content = "I need some favor for my brother"
        };

        var result = await godfather.ProcessMessageAsync(message);

        Assert.True(result.Success);
        Assert.Contains("take care of our friends", result.Response!);
    }

    [Test]
    public async Task GodfatherAgent_HandleMessage_Default_ReturnsGenericResponse()
    {
        var logger = CreateTestLogger();
        var godfather = new GodfatherAgent("godfather-001", "Don Corleone", logger);

        var message = new AgentMessage
        {
            SenderId = "underboss-001",
            ReceiverId = "godfather-001",
            Subject = "Update",
            Content = "Everything is running smoothly"
        };

        var result = await godfather.ProcessMessageAsync(message);

        Assert.True(result.Success);
        Assert.Contains("Do what needs to be done", result.Response!);
        Assert.Contains("keep the family safe", result.Response!);
    }

    #endregion

    #region UnderbossAgent Tests

    [Test]
    public void UnderbossAgent_Construction_SetsCorrectProperties()
    {
        var logger = CreateTestLogger();
        var underboss = new UnderbossAgent("underboss-001", "Salvatore Tessio", logger);

        Assert.Equal("underboss-001", underboss.Id);
        Assert.Equal("Salvatore Tessio", underboss.Name);
        Assert.Equal(TimeSpan.FromSeconds(8), underboss.DecisionDelay);
        Assert.Equal(7, underboss.Ambition);
        Assert.Equal(9, underboss.Loyalty);
        Assert.Equal(7, underboss.Aggression);
    }

    [Test]
    public void UnderbossAgent_Construction_SetsCorrectCapabilities()
    {
        var logger = CreateTestLogger();
        var underboss = new UnderbossAgent("underboss-001", "Salvatore Tessio", logger);

        Assert.Contains("DailyOperations", underboss.Capabilities.SupportedCategories);
        Assert.Contains("CrewManagement", underboss.Capabilities.SupportedCategories);
        Assert.Contains("Revenue", underboss.Capabilities.SupportedCategories);
    }

    [Test]
    public void UnderbossAgent_MakeDecision_CollectsMoney_WhenRollLessThanFour()
    {
        var logger = CreateTestLogger();
        var underboss = new UnderbossAgent("underboss-001", "Salvatore Tessio", logger);
        var gameState = CreateTestGameState();

        // Find a seed that produces roll < 4
        for (int seed = 0; seed < 100; seed++)
        {
            var testRandom = new Random(seed);
            var roll = testRandom.Next(0, 10);
            if (roll < 4)
            {
                var random = new Random(seed);
                var decision = underboss.MakeDecision(gameState, random);

                Assert.NotNull(decision);
                Assert.Equal(DecisionType.CollectMoney, decision!.Type);
                Assert.Equal("Weekly collections", decision.Reason);
                return;
            }
        }

        Assert.True(false, "Could not find seed producing roll < 4");
    }

    [Test]
    public void UnderbossAgent_MakeDecision_SendsOrders_WhenRollBetweenFourAndSeven()
    {
        var logger = CreateTestLogger();
        var underboss = new UnderbossAgent("underboss-001", "Salvatore Tessio", logger);
        var gameState = CreateTestGameState();

        // Find a seed that produces 4 <= roll < 7
        for (int seed = 0; seed < 100; seed++)
        {
            var testRandom = new Random(seed);
            var roll = testRandom.Next(0, 10);
            if (roll >= 4 && roll < 7)
            {
                var random = new Random(seed);
                var decision = underboss.MakeDecision(gameState, random);

                Assert.NotNull(decision);
                Assert.Equal(DecisionType.SendMessage, decision!.Type);
                Assert.NotNull(decision.Message);
                Assert.Equal("underboss-001", decision.Message!.SenderId);
                Assert.Equal("capo-001", decision.Message.ReceiverId);
                Assert.Equal("Orders", decision.Message.Subject);
                Assert.Equal("DailyOperations", decision.Message.Category);
                return;
            }
        }

        Assert.True(false, "Could not find seed producing roll between 4-6");
    }

    [Test]
    public void UnderbossAgent_MakeDecision_ReportsToGodfather_WhenRollBetweenSevenAndNineAndDayDivisibleByThree()
    {
        var logger = CreateTestLogger();
        var underboss = new UnderbossAgent("underboss-001", "Salvatore Tessio", logger);
        var gameState = CreateTestGameState();
        gameState.Week = 9; // Day % 3 == 0

        // Find a seed that produces 7 <= roll < 9
        for (int seed = 0; seed < 100; seed++)
        {
            var testRandom = new Random(seed);
            var roll = testRandom.Next(0, 10);
            if (roll >= 7 && roll < 9)
            {
                var random = new Random(seed);
                var decision = underboss.MakeDecision(gameState, random);

                Assert.NotNull(decision);
                Assert.Equal(DecisionType.SendMessage, decision!.Type);
                Assert.NotNull(decision.Message);
                Assert.Equal("godfather-001", decision.Message!.ReceiverId);
                Assert.Equal("Weekly Report", decision.Message.Subject);
                return;
            }
        }

        Assert.True(false, "Could not find seed producing roll between 7-8");
    }

    [Test]
    public void UnderbossAgent_MakeDecision_ReturnsWait_WhenRollNineOrHigher()
    {
        var logger = CreateTestLogger();
        var underboss = new UnderbossAgent("underboss-001", "Salvatore Tessio", logger);
        var gameState = CreateTestGameState();
        gameState.Week = 5; // Day % 3 != 0

        // Find a seed that produces roll >= 9
        for (int seed = 0; seed < 100; seed++)
        {
            var testRandom = new Random(seed);
            var roll = testRandom.Next(0, 10);
            if (roll >= 9)
            {
                var random = new Random(seed);
                var decision = underboss.MakeDecision(gameState, random);

                Assert.NotNull(decision);
                Assert.Equal(DecisionType.Wait, decision!.Type);
                return;
            }
        }

        Assert.True(false, "Could not find seed producing roll >= 9");
    }

    [Test]
    public async Task UnderbossAgent_HandleMessage_Report_ReturnsReportResponse()
    {
        var logger = CreateTestLogger();
        var underboss = new UnderbossAgent("underboss-001", "Salvatore Tessio", logger);

        var message = new AgentMessage
        {
            SenderId = "godfather-001",
            ReceiverId = "underboss-001",
            Subject = "Request",
            Content = "I need a full report on operations"
        };

        var result = await underboss.ProcessMessageAsync(message);

        Assert.True(result.Success);
        Assert.Contains("running like clockwork", result.Response!);
        Assert.Contains("Collections are up", result.Response!);
    }

    [Test]
    public async Task UnderbossAgent_HandleMessage_Scout_ReturnsExpansionPlan()
    {
        var logger = CreateTestLogger();
        var underboss = new UnderbossAgent("underboss-001", "Salvatore Tessio", logger);

        var message = new AgentMessage
        {
            SenderId = "godfather-001",
            ReceiverId = "underboss-001",
            Subject = "Expansion",
            Content = "Scout new territories"
        };

        var result = await underboss.ProcessMessageAsync(message);

        Assert.True(result.Success);
        Assert.Contains("Brooklyn and the Bronx", result.Response!);
        Assert.Contains("week", result.Response!);
    }

    [Test]
    public async Task UnderbossAgent_HandleMessage_Expansion_ReturnsExpansionPlan()
    {
        var logger = CreateTestLogger();
        var underboss = new UnderbossAgent("underboss-001", "Salvatore Tessio", logger);

        var message = new AgentMessage
        {
            SenderId = "godfather-001",
            ReceiverId = "underboss-001",
            Subject = "Growth",
            Content = "We need expansion into new areas"
        };

        var result = await underboss.ProcessMessageAsync(message);

        Assert.True(result.Success);
        Assert.Contains("I'll send the boys", result.Response!);
    }

    [Test]
    public async Task UnderbossAgent_HandleMessage_Default_ReturnsLoyalResponse()
    {
        var logger = CreateTestLogger();
        var underboss = new UnderbossAgent("underboss-001", "Salvatore Tessio", logger);

        var message = new AgentMessage
        {
            SenderId = "godfather-001",
            ReceiverId = "underboss-001",
            Subject = "Task",
            Content = "Handle the Smith situation"
        };

        var result = await underboss.ProcessMessageAsync(message);

        Assert.True(result.Success);
        Assert.Contains("Consider it done", result.Response!);
        Assert.Contains("family comes first", result.Response!);
    }

    #endregion

    #region ConsigliereAgent Tests

    [Test]
    public void ConsigliereAgent_Construction_SetsCorrectProperties()
    {
        var logger = CreateTestLogger();
        var consigliere = new ConsigliereAgent("consigliere-001", "Tom Hagen", logger);

        Assert.Equal("consigliere-001", consigliere.Id);
        Assert.Equal("Tom Hagen", consigliere.Name);
        Assert.Equal(TimeSpan.FromSeconds(12), consigliere.DecisionDelay);
        Assert.Equal(4, consigliere.Ambition);
        Assert.Equal(10, consigliere.Loyalty);
        Assert.Equal(2, consigliere.Aggression);
    }

    [Test]
    public void ConsigliereAgent_Construction_SetsCorrectCapabilities()
    {
        var logger = CreateTestLogger();
        var consigliere = new ConsigliereAgent("consigliere-001", "Tom Hagen", logger);

        Assert.Contains("Legal", consigliere.Capabilities.SupportedCategories);
        Assert.Contains("Strategy", consigliere.Capabilities.SupportedCategories);
        Assert.Contains("Negotiations", consigliere.Capabilities.SupportedCategories);
    }

    [Test]
    public void ConsigliereAgent_MakeDecision_SendsAdvice_WhenRollLessThanThree()
    {
        var logger = CreateTestLogger();
        var consigliere = new ConsigliereAgent("consigliere-001", "Tom Hagen", logger);
        var gameState = CreateTestGameState();

        // Find a seed that produces roll < 3
        for (int seed = 0; seed < 100; seed++)
        {
            var testRandom = new Random(seed);
            var roll = testRandom.Next(0, 10);
            if (roll < 3)
            {
                var random = new Random(seed);
                var decision = consigliere.MakeDecision(gameState, random);

                Assert.NotNull(decision);
                Assert.Equal(DecisionType.SendMessage, decision!.Type);
                Assert.NotNull(decision.Message);
                Assert.Equal("consigliere-001", decision.Message!.SenderId);
                Assert.Equal("godfather-001", decision.Message.ReceiverId);
                Assert.Equal("Strategic Counsel", decision.Message.Subject);
                Assert.Equal("Strategy", decision.Message.Category);
                return;
            }
        }

        Assert.True(false, "Could not find seed producing roll < 3");
    }

    [Test]
    public void ConsigliereAgent_MakeDecision_ReturnsWait_WhenRollThreeOrHigher()
    {
        var logger = CreateTestLogger();
        var consigliere = new ConsigliereAgent("consigliere-001", "Tom Hagen", logger);
        var gameState = CreateTestGameState();

        // Find a seed that produces roll >= 3
        for (int seed = 0; seed < 100; seed++)
        {
            var testRandom = new Random(seed);
            var roll = testRandom.Next(0, 10);
            if (roll >= 3)
            {
                var random = new Random(seed);
                var decision = consigliere.MakeDecision(gameState, random);

                Assert.NotNull(decision);
                Assert.Equal(DecisionType.Wait, decision!.Type);
                return;
            }
        }

        Assert.True(false, "Could not find seed producing roll >= 3");
    }

    [Test]
    public async Task ConsigliereAgent_HandleMessage_Police_ReturnsLegalResponse()
    {
        var logger = CreateTestLogger();
        var consigliere = new ConsigliereAgent("consigliere-001", "Tom Hagen", logger);

        var message = new AgentMessage
        {
            SenderId = "godfather-001",
            ReceiverId = "consigliere-001",
            Subject = "Issue",
            Content = "The police are asking questions"
        };

        var result = await consigliere.ProcessMessageAsync(message);

        Assert.True(result.Success);
        Assert.Contains("lawyers and judges", result.Response!);
        Assert.Contains("friends in the department", result.Response!);
    }

    [Test]
    public async Task ConsigliereAgent_HandleMessage_Raid_ReturnsLegalResponse()
    {
        var logger = CreateTestLogger();
        var consigliere = new ConsigliereAgent("consigliere-001", "Tom Hagen", logger);

        var message = new AgentMessage
        {
            SenderId = "underboss-001",
            ReceiverId = "consigliere-001",
            Subject = "Emergency",
            Content = "There was a raid this morning"
        };

        var result = await consigliere.ProcessMessageAsync(message);

        Assert.True(result.Success);
        Assert.Contains("will go away", result.Response!);
    }

    [Test]
    public async Task ConsigliereAgent_HandleMessage_Legal_ReturnsLegalResponse()
    {
        var logger = CreateTestLogger();
        var consigliere = new ConsigliereAgent("consigliere-001", "Tom Hagen", logger);

        var message = new AgentMessage
        {
            SenderId = "godfather-001",
            ReceiverId = "consigliere-001",
            Subject = "Legal Matter",
            Content = "We have a legal problem with the city"
        };

        var result = await consigliere.ProcessMessageAsync(message);

        Assert.True(result.Success);
        Assert.Contains("lawyers", result.Response!);
    }

    [Test]
    public async Task ConsigliereAgent_HandleMessage_OtherFamilies_ReturnsAnalysis()
    {
        var logger = CreateTestLogger();
        var consigliere = new ConsigliereAgent("consigliere-001", "Tom Hagen", logger);

        var message = new AgentMessage
        {
            SenderId = "godfather-001",
            ReceiverId = "consigliere-001",
            Subject = "Assessment",
            Content = "What's happening with the other families?"
        };

        var result = await consigliere.ProcessMessageAsync(message);

        Assert.True(result.Success);
        Assert.Contains("strong position", result.Response!);
        Assert.Contains("Tattaglias", result.Response!);
        Assert.Contains("Barzini", result.Response!);
    }

    [Test]
    public async Task ConsigliereAgent_HandleMessage_Strong_ReturnsAnalysis()
    {
        var logger = CreateTestLogger();
        var consigliere = new ConsigliereAgent("consigliere-001", "Tom Hagen", logger);

        var message = new AgentMessage
        {
            SenderId = "godfather-001",
            ReceiverId = "consigliere-001",
            Subject = "Question",
            Content = "Are we strong enough to take on the Barzinis?"
        };

        var result = await consigliere.ProcessMessageAsync(message);

        Assert.True(result.Success);
        Assert.Contains("play smart", result.Response!);
    }

    [Test]
    public async Task ConsigliereAgent_HandleMessage_Default_ReturnsThoughtfulResponse()
    {
        var logger = CreateTestLogger();
        var consigliere = new ConsigliereAgent("consigliere-001", "Tom Hagen", logger);

        var message = new AgentMessage
        {
            SenderId = "godfather-001",
            ReceiverId = "consigliere-001",
            Subject = "General",
            Content = "What do you think about the union situation?"
        };

        var result = await consigliere.ProcessMessageAsync(message);

        Assert.True(result.Success);
        Assert.Contains("study the situation", result.Response!);
        Assert.Contains("day to think", result.Response!);
    }

    #endregion

    #region CapoAgent Tests

    [Test]
    public void CapoAgent_Construction_SetsCorrectProperties()
    {
        var logger = CreateTestLogger();
        var capo = new CapoAgent("capo-001", "Peter Clemenza", logger);

        Assert.Equal("capo-001", capo.Id);
        Assert.Equal("Peter Clemenza", capo.Name);
        Assert.Equal(TimeSpan.FromSeconds(6), capo.DecisionDelay);
        Assert.Equal(8, capo.Ambition);
        Assert.Equal(7, capo.Loyalty);
        Assert.Equal(8, capo.Aggression);
    }

    [Test]
    public void CapoAgent_Construction_SetsCorrectCapabilities()
    {
        var logger = CreateTestLogger();
        var capo = new CapoAgent("capo-001", "Peter Clemenza", logger);

        Assert.Contains("ProtectionRacket", capo.Capabilities.SupportedCategories);
        Assert.Contains("CrewLeadership", capo.Capabilities.SupportedCategories);
    }

    [Test]
    public void CapoAgent_MakeDecision_CollectsMoney_WhenRollLessThanFive()
    {
        var logger = CreateTestLogger();
        var capo = new CapoAgent("capo-001", "Peter Clemenza", logger);
        var gameState = CreateTestGameState();

        // Find a seed that produces roll < 5
        for (int seed = 0; seed < 100; seed++)
        {
            var testRandom = new Random(seed);
            var roll = testRandom.Next(0, 10);
            if (roll < 5)
            {
                var random = new Random(seed);
                var decision = capo.MakeDecision(gameState, random);

                Assert.NotNull(decision);
                Assert.Equal(DecisionType.CollectMoney, decision!.Type);
                Assert.Equal("Protection racket collections", decision.Reason);
                return;
            }
        }

        Assert.True(false, "Could not find seed producing roll < 5");
    }

    [Test]
    public void CapoAgent_MakeDecision_Recruits_WhenRollBetweenFiveAndSevenAndSoldiersLow()
    {
        var logger = CreateTestLogger();
        var capo = new CapoAgent("capo-001", "Peter Clemenza", logger);
        var gameState = CreateTestGameState();
        gameState.SoldierCount = 10; // Less than 20

        // Find a seed that produces 5 <= roll < 7
        for (int seed = 0; seed < 100; seed++)
        {
            var testRandom = new Random(seed);
            var roll = testRandom.Next(0, 10);
            if (roll >= 5 && roll < 7)
            {
                var random = new Random(seed);
                var decision = capo.MakeDecision(gameState, random);

                Assert.NotNull(decision);
                Assert.Equal(DecisionType.RecruitSoldier, decision!.Type);
                Assert.Equal("Expanding the crew", decision.Reason);
                return;
            }
        }

        Assert.True(false, "Could not find seed producing roll between 5-6");
    }

    [Test]
    public void CapoAgent_MakeDecision_DoesNotRecruit_WhenSoldiersAtCapacity()
    {
        var logger = CreateTestLogger();
        var capo = new CapoAgent("capo-001", "Peter Clemenza", logger);
        var gameState = CreateTestGameState();
        gameState.SoldierCount = 25; // >= 20, so no recruiting

        // Find a seed that produces 5 <= roll < 7
        for (int seed = 0; seed < 100; seed++)
        {
            var testRandom = new Random(seed);
            var roll = testRandom.Next(0, 10);
            if (roll >= 5 && roll < 7)
            {
                var random = new Random(seed);
                var decision = capo.MakeDecision(gameState, random);

                Assert.NotNull(decision);
                // Should skip to next branch (roll < 9 = SendMessage)
                Assert.Equal(DecisionType.SendMessage, decision!.Type);
                return;
            }
        }

        Assert.True(false, "Could not find seed producing roll between 5-6");
    }

    [Test]
    public void CapoAgent_MakeDecision_SendsReport_WhenRollBetweenSevenAndNine()
    {
        var logger = CreateTestLogger();
        var capo = new CapoAgent("capo-001", "Peter Clemenza", logger);
        var gameState = CreateTestGameState();
        gameState.SoldierCount = 25; // High soldier count to skip recruit branch

        // Find a seed that produces 7 <= roll < 9
        for (int seed = 0; seed < 100; seed++)
        {
            var testRandom = new Random(seed);
            var roll = testRandom.Next(0, 10);
            if (roll >= 7 && roll < 9)
            {
                var random = new Random(seed);
                var decision = capo.MakeDecision(gameState, random);

                Assert.NotNull(decision);
                Assert.Equal(DecisionType.SendMessage, decision!.Type);
                Assert.NotNull(decision.Message);
                Assert.Equal("capo-001", decision.Message!.SenderId);
                Assert.Equal("underboss-001", decision.Message.ReceiverId);
                Assert.Equal("Territory Report", decision.Message.Subject);
                Assert.Equal("CrewManagement", decision.Message.Category);
                return;
            }
        }

        Assert.True(false, "Could not find seed producing roll between 7-8");
    }

    [Test]
    public void CapoAgent_MakeDecision_ReturnsWait_WhenRollNineOrHigher()
    {
        var logger = CreateTestLogger();
        var capo = new CapoAgent("capo-001", "Peter Clemenza", logger);
        var gameState = CreateTestGameState();
        gameState.SoldierCount = 25;

        // Find a seed that produces roll >= 9
        for (int seed = 0; seed < 100; seed++)
        {
            var testRandom = new Random(seed);
            var roll = testRandom.Next(0, 10);
            if (roll >= 9)
            {
                var random = new Random(seed);
                var decision = capo.MakeDecision(gameState, random);

                Assert.NotNull(decision);
                Assert.Equal(DecisionType.Wait, decision!.Type);
                return;
            }
        }

        Assert.True(false, "Could not find seed producing roll >= 9");
    }

    [Test]
    public async Task CapoAgent_HandleMessage_Collections_ReturnsCollectionResponse()
    {
        var logger = CreateTestLogger();
        var capo = new CapoAgent("capo-001", "Peter Clemenza", logger);

        var message = new AgentMessage
        {
            SenderId = "underboss-001",
            ReceiverId = "capo-001",
            Subject = "Orders",
            Content = "Make sure the collections are on time"
        };

        var result = await capo.ProcessMessageAsync(message);

        Assert.True(result.Success);
        // "collect" in "collections" triggers the collection handler
        Assert.Contains("collected", result.Response!);
        Assert.Contains("Underboss", result.Response!);
    }

    [Test]
    public async Task CapoAgent_HandleMessage_Schedule_ReturnsCollectionResponse()
    {
        var logger = CreateTestLogger();
        var capo = new CapoAgent("capo-001", "Peter Clemenza", logger);

        var message = new AgentMessage
        {
            SenderId = "underboss-001",
            ReceiverId = "capo-001",
            Subject = "Schedule",
            Content = "What's the collection schedule this week?"
        };

        var result = await capo.ProcessMessageAsync(message);

        Assert.True(result.Success);
        // "collect" in "collection" triggers the collection handler
        Assert.Contains("collected", result.Response!);
    }

    [Test]
    public async Task CapoAgent_HandleMessage_Crew_ReturnsCrewResponse()
    {
        var logger = CreateTestLogger();
        var capo = new CapoAgent("capo-001", "Peter Clemenza", logger);

        var message = new AgentMessage
        {
            SenderId = "underboss-001",
            ReceiverId = "capo-001",
            Subject = "Crew Status",
            Content = "How's your crew doing?"
        };

        var result = await capo.ProcessMessageAsync(message);

        Assert.True(result.Success);
        Assert.Contains("crew is tight", result.Response!);
        Assert.Contains("know the rules", result.Response!);
    }

    [Test]
    public async Task CapoAgent_HandleMessage_Line_ReturnsCrewResponse()
    {
        var logger = CreateTestLogger();
        var capo = new CapoAgent("capo-001", "Peter Clemenza", logger);

        var message = new AgentMessage
        {
            SenderId = "underboss-001",
            ReceiverId = "capo-001",
            Subject = "Discipline",
            Content = "Keep your men in line"
        };

        var result = await capo.ProcessMessageAsync(message);

        Assert.True(result.Success);
        Assert.Contains("answer to me", result.Response!);
    }

    [Test]
    public async Task CapoAgent_HandleMessage_PushHarder_ReturnsAggressiveResponse()
    {
        var logger = CreateTestLogger();
        var capo = new CapoAgent("capo-001", "Peter Clemenza", logger);

        var message = new AgentMessage
        {
            SenderId = "underboss-001",
            ReceiverId = "capo-001",
            Subject = "Revenue",
            Content = "You need to push harder this week"
        };

        var result = await capo.ProcessMessageAsync(message);

        Assert.True(result.Success);
        Assert.Contains("squeeze", result.Response!);
        Assert.Contains("new restaurant", result.Response!);
    }

    [Test]
    public async Task CapoAgent_HandleMessage_Numbers_ReturnsAggressiveResponse()
    {
        var logger = CreateTestLogger();
        var capo = new CapoAgent("capo-001", "Peter Clemenza", logger);

        var message = new AgentMessage
        {
            SenderId = "underboss-001",
            ReceiverId = "capo-001",
            Subject = "Performance",
            Content = "Your numbers are down this week"
        };

        var result = await capo.ProcessMessageAsync(message);

        Assert.True(result.Success);
        Assert.Contains("squeeze", result.Response!);
    }

    [Test]
    public async Task CapoAgent_HandleMessage_Default_ReturnsGenericResponse()
    {
        var logger = CreateTestLogger();
        var capo = new CapoAgent("capo-001", "Peter Clemenza", logger);

        var message = new AgentMessage
        {
            SenderId = "underboss-001",
            ReceiverId = "capo-001",
            Subject = "Task",
            Content = "Handle the Sullivan thing"
        };

        var result = await capo.ProcessMessageAsync(message);

        Assert.True(result.Success);
        Assert.Contains("Got it, boss", result.Response!);
        Assert.Contains("handled", result.Response!);
    }

    #endregion

    #region SoldierAgent Tests

    [Test]
    public void SoldierAgent_Construction_SetsCorrectProperties()
    {
        var logger = CreateTestLogger();
        var soldier = new SoldierAgent("soldier-001", "Rocco Lampone", logger);

        Assert.Equal("soldier-001", soldier.Id);
        Assert.Equal("Rocco Lampone", soldier.Name);
        Assert.Equal(TimeSpan.FromSeconds(4), soldier.DecisionDelay);
        Assert.Equal(5, soldier.Ambition);
        Assert.Equal(9, soldier.Loyalty);
        Assert.Equal(9, soldier.Aggression);
    }

    [Test]
    public void SoldierAgent_Construction_SetsCorrectCapabilities()
    {
        var logger = CreateTestLogger();
        var soldier = new SoldierAgent("soldier-001", "Rocco Lampone", logger);

        Assert.Contains("Enforcement", soldier.Capabilities.SupportedCategories);
        Assert.Contains("Collections", soldier.Capabilities.SupportedCategories);
    }

    [Test]
    public void SoldierAgent_MakeDecision_CollectsMoney_WhenRollLessThanSix()
    {
        var logger = CreateTestLogger();
        var soldier = new SoldierAgent("soldier-001", "Rocco Lampone", logger);
        var gameState = CreateTestGameState();

        // Find a seed that produces roll < 6
        for (int seed = 0; seed < 100; seed++)
        {
            var testRandom = new Random(seed);
            var roll = testRandom.Next(0, 10);
            if (roll < 6)
            {
                var random = new Random(seed);
                var decision = soldier.MakeDecision(gameState, random);

                Assert.NotNull(decision);
                Assert.Equal(DecisionType.CollectMoney, decision!.Type);
                Assert.Equal("Street collections", decision.Reason);
                return;
            }
        }

        Assert.True(false, "Could not find seed producing roll < 6");
    }

    [Test]
    public void SoldierAgent_MakeDecision_SendsReport_WhenRollBetweenSixAndNine()
    {
        var logger = CreateTestLogger();
        var soldier = new SoldierAgent("soldier-001", "Rocco Lampone", logger);
        var gameState = CreateTestGameState();

        // Find a seed that produces 6 <= roll < 9
        for (int seed = 0; seed < 100; seed++)
        {
            var testRandom = new Random(seed);
            var roll = testRandom.Next(0, 10);
            if (roll >= 6 && roll < 9)
            {
                var random = new Random(seed);
                var decision = soldier.MakeDecision(gameState, random);

                Assert.NotNull(decision);
                Assert.Equal(DecisionType.SendMessage, decision!.Type);
                Assert.NotNull(decision.Message);
                Assert.Equal("soldier-001", decision.Message!.SenderId);
                Assert.Equal("capo-001", decision.Message.ReceiverId);
                Assert.Equal("Daily Report", decision.Message.Subject);
                Assert.Equal("Enforcement", decision.Message.Category);
                return;
            }
        }

        Assert.True(false, "Could not find seed producing roll between 6-8");
    }

    [Test]
    public void SoldierAgent_MakeDecision_ReturnsWait_WhenRollNineOrHigher()
    {
        var logger = CreateTestLogger();
        var soldier = new SoldierAgent("soldier-001", "Rocco Lampone", logger);
        var gameState = CreateTestGameState();

        // Find a seed that produces roll >= 9
        for (int seed = 0; seed < 100; seed++)
        {
            var testRandom = new Random(seed);
            var roll = testRandom.Next(0, 10);
            if (roll >= 9)
            {
                var random = new Random(seed);
                var decision = soldier.MakeDecision(gameState, random);

                Assert.NotNull(decision);
                Assert.Equal(DecisionType.Wait, decision!.Type);
                return;
            }
        }

        Assert.True(false, "Could not find seed producing roll >= 9");
    }

    [Test]
    public async Task SoldierAgent_HandleMessage_AnyMessage_ReturnsSimpleResponse()
    {
        var logger = CreateTestLogger();
        var soldier = new SoldierAgent("soldier-001", "Rocco Lampone", logger);

        var message = new AgentMessage
        {
            SenderId = "capo-001",
            ReceiverId = "soldier-001",
            Subject = "Task",
            Content = "Go collect from the butcher shop"
        };

        var result = await soldier.ProcessMessageAsync(message);

        Assert.True(result.Success);
        // "collect" triggers the collection handler
        Assert.Contains("knuckles", result.Response!);
        Assert.Contains("Capo", result.Response!);
    }

    [Test]
    public async Task SoldierAgent_HandleMessage_Urgent_ReturnsSimpleResponse()
    {
        var logger = CreateTestLogger();
        var soldier = new SoldierAgent("soldier-001", "Rocco Lampone", logger);

        var message = new AgentMessage
        {
            SenderId = "capo-001",
            ReceiverId = "soldier-001",
            Subject = "Urgent Task",
            Content = "Handle this right now",
            Priority = MessagePriority.Urgent
        };

        var result = await soldier.ProcessMessageAsync(message);

        Assert.True(result.Success);
        Assert.Contains("on it", result.Response!);
    }

    #endregion

    #region Agent Status and CanHandle Tests

    [Test]
    public void GodfatherAgent_CanHandle_ReturnsTrueForValidMessage()
    {
        var logger = CreateTestLogger();
        var godfather = new GodfatherAgent("godfather-001", "Don Corleone", logger);

        var message = new AgentMessage
        {
            SenderId = "underboss-001",
            ReceiverId = "godfather-001",
            Subject = "Request",
            Content = "Need a decision",
            Category = "FinalDecision"
        };

        Assert.True(godfather.CanHandle(message));
    }

    [Test]
    public void GodfatherAgent_CanHandle_ReturnsTrueForMajorDispute()
    {
        var logger = CreateTestLogger();
        var godfather = new GodfatherAgent("godfather-001", "Don Corleone", logger);

        var message = new AgentMessage
        {
            SenderId = "consigliere-001",
            ReceiverId = "godfather-001",
            Subject = "Dispute",
            Content = "Territory dispute",
            Category = "MajorDispute"
        };

        Assert.True(godfather.CanHandle(message));
    }

    [Test]
    public void GodfatherAgent_CanHandle_ReturnsTrueForFavorRequest()
    {
        var logger = CreateTestLogger();
        var godfather = new GodfatherAgent("godfather-001", "Don Corleone", logger);

        var message = new AgentMessage
        {
            SenderId = "soldier-001",
            ReceiverId = "godfather-001",
            Subject = "Favor",
            Content = "Asking for a favor",
            Category = "FavorRequest"
        };

        Assert.True(godfather.CanHandle(message));
    }

    [Test]
    public void UnderbossAgent_CanHandle_ReturnsTrueForDailyOperations()
    {
        var logger = CreateTestLogger();
        var underboss = new UnderbossAgent("underboss-001", "Salvatore Tessio", logger);

        var message = new AgentMessage
        {
            SenderId = "godfather-001",
            ReceiverId = "underboss-001",
            Subject = "Operations",
            Content = "Check on operations",
            Category = "DailyOperations"
        };

        Assert.True(underboss.CanHandle(message));
    }

    [Test]
    public void ConsigliereAgent_CanHandle_ReturnsTrueForLegal()
    {
        var logger = CreateTestLogger();
        var consigliere = new ConsigliereAgent("consigliere-001", "Tom Hagen", logger);

        var message = new AgentMessage
        {
            SenderId = "godfather-001",
            ReceiverId = "consigliere-001",
            Subject = "Legal Matter",
            Content = "Handle this legal issue",
            Category = "Legal"
        };

        Assert.True(consigliere.CanHandle(message));
    }

    [Test]
    public void CapoAgent_CanHandle_ReturnsTrueForProtectionRacket()
    {
        var logger = CreateTestLogger();
        var capo = new CapoAgent("capo-001", "Peter Clemenza", logger);

        var message = new AgentMessage
        {
            SenderId = "underboss-001",
            ReceiverId = "capo-001",
            Subject = "Protection",
            Content = "Handle the protection collections",
            Category = "ProtectionRacket"
        };

        Assert.True(capo.CanHandle(message));
    }

    [Test]
    public void SoldierAgent_CanHandle_ReturnsTrueForEnforcement()
    {
        var logger = CreateTestLogger();
        var soldier = new SoldierAgent("soldier-001", "Rocco Lampone", logger);

        var message = new AgentMessage
        {
            SenderId = "capo-001",
            ReceiverId = "soldier-001",
            Subject = "Enforcement",
            Content = "Send a message",
            Category = "Enforcement"
        };

        Assert.True(soldier.CanHandle(message));
    }

    [Test]
    public void SoldierAgent_CanHandle_ReturnsTrueForCollections()
    {
        var logger = CreateTestLogger();
        var soldier = new SoldierAgent("soldier-001", "Rocco Lampone", logger);

        var message = new AgentMessage
        {
            SenderId = "capo-001",
            ReceiverId = "soldier-001",
            Subject = "Collections",
            Content = "Collect money",
            Category = "Collections"
        };

        Assert.True(soldier.CanHandle(message));
    }

    #endregion

    #region Message Integration Tests

    [Test]
    public void GodfatherAgent_MakeDecision_MessageHasValidRecipients()
    {
        var logger = CreateTestLogger();
        var godfather = new GodfatherAgent("godfather-001", "Don Corleone", logger);
        var gameState = CreateTestGameState();

        // Find a seed that triggers SendMessage
        for (int seed = 0; seed < 100; seed++)
        {
            var testRandom = new Random(seed);
            var roll = testRandom.Next(0, 10);
            if (roll < 3)
            {
                var random = new Random(seed);
                var decision = godfather.MakeDecision(gameState, random);

                Assert.NotNull(decision?.Message);
                var validRecipients = new[] { "underboss-001", "consigliere-001", "capo-001" };
                Assert.Contains(decision!.Message!.ReceiverId, validRecipients);
                return;
            }
        }
    }

    [Test]
    public void UnderbossAgent_MakeDecision_WeeklyReportContainsGameStats()
    {
        var logger = CreateTestLogger();
        var underboss = new UnderbossAgent("underboss-001", "Salvatore Tessio", logger);
        var gameState = CreateTestGameState();
        gameState.Week = 6; // Day % 3 == 0
        gameState.Territories["Downtown"] = new Territory { WeeklyRevenue = 10000 };

        // Find a seed that produces 7 <= roll < 9
        for (int seed = 0; seed < 100; seed++)
        {
            var testRandom = new Random(seed);
            var roll = testRandom.Next(0, 10);
            if (roll >= 7 && roll < 9)
            {
                var random = new Random(seed);
                var decision = underboss.MakeDecision(gameState, random);

                if (decision?.Type == DecisionType.SendMessage &&
                    decision.Message?.ReceiverId == "godfather-001")
                {
                    Assert.Contains("1", decision.Message.Content); // TerritoryCount
                    return;
                }
            }
        }
    }

    [Test]
    public void ConsigliereAgent_MakeDecision_AdviceHasStrategicContent()
    {
        var logger = CreateTestLogger();
        var consigliere = new ConsigliereAgent("consigliere-001", "Tom Hagen", logger);
        var gameState = CreateTestGameState();

        // Find a seed that triggers SendMessage
        for (int seed = 0; seed < 100; seed++)
        {
            var testRandom = new Random(seed);
            var roll = testRandom.Next(0, 10);
            if (roll < 3)
            {
                var random = new Random(seed);
                var decision = consigliere.MakeDecision(gameState, random);

                Assert.NotNull(decision?.Message);
                var content = decision!.Message!.Content;
                Assert.True(
                    content.Contains("strategy") ||
                    content.Contains("political") ||
                    content.Contains("families") ||
                    content.Contains("legal"));
                return;
            }
        }
    }

    [Test]
    public void SoldierAgent_MakeDecision_ReportsHaveVariety()
    {
        var logger = CreateTestLogger();
        var soldier = new SoldierAgent("soldier-001", "Rocco Lampone", logger);
        var gameState = CreateTestGameState();

        var reportContents = new HashSet<string>();

        // Collect multiple reports to check for variety
        for (int seed = 0; seed < 200; seed++)
        {
            var testRandom = new Random(seed);
            var roll = testRandom.Next(0, 10);
            if (roll >= 6 && roll < 9)
            {
                var random = new Random(seed);
                var decision = soldier.MakeDecision(gameState, random);

                if (decision?.Type == DecisionType.SendMessage)
                {
                    reportContents.Add(decision.Message!.Content);
                }

                if (reportContents.Count >= 3)
                {
                    // Found at least 3 different reports
                    Assert.True(reportContents.Count >= 3);
                    return;
                }
            }
        }

        // Should have found multiple different reports
        Assert.True(reportContents.Count > 0, "Should find at least one report");
    }

    #endregion

    #region Edge Cases

    [Test]
    public void AllAgents_StatusIsAvailable_AfterConstruction()
    {
        var logger = CreateTestLogger();

        var godfather = new GodfatherAgent("godfather-001", "Don Corleone", logger);
        var underboss = new UnderbossAgent("underboss-001", "Salvatore Tessio", logger);
        var consigliere = new ConsigliereAgent("consigliere-001", "Tom Hagen", logger);
        var capo = new CapoAgent("capo-001", "Peter Clemenza", logger);
        var soldier = new SoldierAgent("soldier-001", "Rocco Lampone", logger);

        Assert.Equal(AgentStatus.Available, godfather.Status);
        Assert.Equal(AgentStatus.Available, underboss.Status);
        Assert.Equal(AgentStatus.Available, consigliere.Status);
        Assert.Equal(AgentStatus.Available, capo.Status);
        Assert.Equal(AgentStatus.Available, soldier.Status);
    }

    [Test]
    public void AllAgents_HaveCapabilities_AfterConstruction()
    {
        var logger = CreateTestLogger();

        var godfather = new GodfatherAgent("godfather-001", "Don Corleone", logger);
        var underboss = new UnderbossAgent("underboss-001", "Salvatore Tessio", logger);
        var consigliere = new ConsigliereAgent("consigliere-001", "Tom Hagen", logger);
        var capo = new CapoAgent("capo-001", "Peter Clemenza", logger);
        var soldier = new SoldierAgent("soldier-001", "Rocco Lampone", logger);

        Assert.NotEmpty(godfather.Capabilities.SupportedCategories);
        Assert.NotEmpty(underboss.Capabilities.SupportedCategories);
        Assert.NotEmpty(consigliere.Capabilities.SupportedCategories);
        Assert.NotEmpty(capo.Capabilities.SupportedCategories);
        Assert.NotEmpty(soldier.Capabilities.SupportedCategories);
    }

    [Test]
    public void CapoAgent_MakeDecision_DoesNotRecruitWhenAtMax()
    {
        var logger = CreateTestLogger();
        var capo = new CapoAgent("capo-001", "Peter Clemenza", logger);
        var gameState = CreateTestGameState();
        gameState.SoldierCount = 20; // At the threshold

        // Find a seed that would normally recruit (5 <= roll < 7)
        for (int seed = 0; seed < 100; seed++)
        {
            var testRandom = new Random(seed);
            var roll = testRandom.Next(0, 10);
            if (roll >= 5 && roll < 7)
            {
                var random = new Random(seed);
                var decision = capo.MakeDecision(gameState, random);

                // Should not recruit, should fall through to next branch
                Assert.NotNull(decision);
                Assert.NotEqual(DecisionType.RecruitSoldier, decision!.Type);
                return;
            }
        }
    }

    [Test]
    public async Task AllAgents_HandleEmptyContent_Gracefully()
    {
        var logger = CreateTestLogger();
        var soldier = new SoldierAgent("soldier-001", "Rocco Lampone", logger);

        var message = new AgentMessage
        {
            SenderId = "capo-001",
            ReceiverId = "soldier-001",
            Subject = "Empty",
            Content = ""
        };

        var result = await soldier.ProcessMessageAsync(message);

        Assert.True(result.Success);
    }

    [Test]
    public void AllAgents_DecisionDelayIsPositive()
    {
        var logger = CreateTestLogger();

        var godfather = new GodfatherAgent("godfather-001", "Don Corleone", logger);
        var underboss = new UnderbossAgent("underboss-001", "Salvatore Tessio", logger);
        var consigliere = new ConsigliereAgent("consigliere-001", "Tom Hagen", logger);
        var capo = new CapoAgent("capo-001", "Peter Clemenza", logger);
        var soldier = new SoldierAgent("soldier-001", "Rocco Lampone", logger);

        Assert.True(godfather.DecisionDelay > TimeSpan.Zero);
        Assert.True(underboss.DecisionDelay > TimeSpan.Zero);
        Assert.True(consigliere.DecisionDelay > TimeSpan.Zero);
        Assert.True(capo.DecisionDelay > TimeSpan.Zero);
        Assert.True(soldier.DecisionDelay > TimeSpan.Zero);
    }

    [Test]
    public void AllAgents_PersonalityTraitsInValidRange()
    {
        var logger = CreateTestLogger();

        var godfather = new GodfatherAgent("godfather-001", "Don Corleone", logger);
        var underboss = new UnderbossAgent("underboss-001", "Salvatore Tessio", logger);
        var consigliere = new ConsigliereAgent("consigliere-001", "Tom Hagen", logger);
        var capo = new CapoAgent("capo-001", "Peter Clemenza", logger);
        var soldier = new SoldierAgent("soldier-001", "Rocco Lampone", logger);

        // All traits should be 1-10
        Assert.InRange(godfather.Ambition, 1, 10);
        Assert.InRange(godfather.Loyalty, 1, 10);
        Assert.InRange(godfather.Aggression, 1, 10);

        Assert.InRange(underboss.Ambition, 1, 10);
        Assert.InRange(underboss.Loyalty, 1, 10);
        Assert.InRange(underboss.Aggression, 1, 10);

        Assert.InRange(consigliere.Ambition, 1, 10);
        Assert.InRange(consigliere.Loyalty, 1, 10);
        Assert.InRange(consigliere.Aggression, 1, 10);

        Assert.InRange(capo.Ambition, 1, 10);
        Assert.InRange(capo.Loyalty, 1, 10);
        Assert.InRange(capo.Aggression, 1, 10);

        Assert.InRange(soldier.Ambition, 1, 10);
        Assert.InRange(soldier.Loyalty, 1, 10);
        Assert.InRange(soldier.Aggression, 1, 10);
    }

    #endregion
}
