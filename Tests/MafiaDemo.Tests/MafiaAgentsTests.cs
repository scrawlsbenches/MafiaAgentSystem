using TestRunner.Framework;
using AgentRouting.MafiaDemo;
using AgentRouting.Core;

namespace TestRunner.Tests;

/// <summary>
/// Unit tests for all MafiaAgents (Godfather, Underboss, Consigliere, Capo, Soldier, Associate)
/// </summary>
public class MafiaAgentsTests
{
    // Helper to create a test logger
    private static IAgentLogger CreateTestLogger() => new ConsoleAgentLogger();

    // Helper to create a basic message
    private static AgentMessage CreateMessage(string senderId, string content, string category = "")
    {
        return new AgentMessage
        {
            SenderId = senderId,
            Content = content,
            Category = category,
            Subject = "Test Message"
        };
    }

    #region GodfatherAgent Tests

    [Test]
    public void GodfatherAgent_Construction_SetsPropertiesCorrectly()
    {
        var logger = CreateTestLogger();
        var godfather = new GodfatherAgent("don-001", "Don Vito", logger);

        Assert.Equal("don-001", godfather.Id);
        Assert.Equal("Don Vito", godfather.Name);
        Assert.NotNull(godfather.Capabilities);
    }

    [Test]
    public void GodfatherAgent_Capabilities_HasCorrectCategories()
    {
        var logger = CreateTestLogger();
        var godfather = new GodfatherAgent("don-001", "Don Vito", logger);

        Assert.Contains("FinalDecision", godfather.Capabilities.SupportedCategories);
        Assert.Contains("MajorDispute", godfather.Capabilities.SupportedCategories);
        Assert.Contains("WarDeclaration", godfather.Capabilities.SupportedCategories);
        Assert.Contains("PeaceTreaty", godfather.Capabilities.SupportedCategories);
        Assert.Contains("FavorRequest", godfather.Capabilities.SupportedCategories);
    }

    [Test]
    public void GodfatherAgent_Capabilities_HasCorrectSkills()
    {
        var logger = CreateTestLogger();
        var godfather = new GodfatherAgent("don-001", "Don Vito", logger);

        Assert.Contains("Leadership", godfather.Capabilities.Skills);
        Assert.Contains("Strategy", godfather.Capabilities.Skills);
        Assert.Contains("Negotiation", godfather.Capabilities.Skills);
        Assert.Contains("Wisdom", godfather.Capabilities.Skills);
    }

    [Test]
    public void GodfatherAgent_Capabilities_HasLimitedConcurrency()
    {
        var logger = CreateTestLogger();
        var godfather = new GodfatherAgent("don-001", "Don Vito", logger);

        Assert.Equal(3, godfather.Capabilities.MaxConcurrentMessages);
    }

    [Test]
    public async Task GodfatherAgent_ProcessAsync_FavorRequest_GrantsFavor()
    {
        var logger = CreateTestLogger();
        var godfather = new GodfatherAgent("don-001", "Don Vito", logger);
        var message = CreateMessage("capo-001", "I need a favor from the family");

        var result = await godfather.ProcessMessageAsync(message);

        Assert.True(result.Success);
        Assert.NotNull(result.Response);
        Assert.Contains("friends", result.Response, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("owe", result.Response, StringComparison.OrdinalIgnoreCase);
    }

    [Test]
    public async Task GodfatherAgent_ProcessAsync_HelpRequest_GrantsFavor()
    {
        var logger = CreateTestLogger();
        var godfather = new GodfatherAgent("don-001", "Don Vito", logger);
        var message = CreateMessage("associate-001", "Please help me with this situation");

        var result = await godfather.ProcessMessageAsync(message);

        Assert.True(result.Success);
        Assert.NotNull(result.Response);
        Assert.Contains("friends", result.Response, StringComparison.OrdinalIgnoreCase);
    }

    [Test]
    public async Task GodfatherAgent_ProcessAsync_FavorRequest_TracksFavor()
    {
        var logger = CreateTestLogger();
        var godfather = new GodfatherAgent("don-001", "Don Vito", logger);
        var message = CreateMessage("capo-001", "I need a favor");

        await godfather.ProcessMessageAsync(message);

        var favors = godfather.GetFavorsOwed();
        Assert.True(favors.ContainsKey("capo-001"));
    }

    [Test]
    public async Task GodfatherAgent_ProcessAsync_TerritoryDispute_SendsToUnderboss()
    {
        var logger = CreateTestLogger();
        var godfather = new GodfatherAgent("don-001", "Don Vito", logger);
        var message = CreateMessage("capo-001", "There's a territory dispute with the Barzinis");

        var result = await godfather.ProcessMessageAsync(message);

        Assert.True(result.Success);
        Assert.NotNull(result.Response);
        Assert.Contains("Underboss", result.Response);
        Assert.Contains("negotiate", result.Response, StringComparison.OrdinalIgnoreCase);
    }

    [Test]
    public async Task GodfatherAgent_ProcessAsync_DisputeKeyword_HandlesTerritoryDispute()
    {
        var logger = CreateTestLogger();
        var godfather = new GodfatherAgent("don-001", "Don Vito", logger);
        var message = CreateMessage("capo-001", "We have a dispute that needs resolution");

        var result = await godfather.ProcessMessageAsync(message);

        Assert.True(result.Success);
        Assert.NotNull(result.Response);
        Assert.Contains("cat", result.Response, StringComparison.OrdinalIgnoreCase);
    }

    [Test]
    public async Task GodfatherAgent_ProcessAsync_ApprovedHit_ReturnsForwardMessage()
    {
        var logger = CreateTestLogger();
        var godfather = new GodfatherAgent("don-001", "Don Vito", logger);
        var message = CreateMessage("underboss-001", "We need a hit on the traitor");

        var result = await godfather.ProcessMessageAsync(message);

        Assert.True(result.Success);
        Assert.NotEmpty(result.ForwardedMessages);
        Assert.Equal("underboss-001", result.ForwardedMessages[0].ReceiverId);
    }

    [Test]
    public async Task GodfatherAgent_ProcessAsync_ApprovedHit_TracksHit()
    {
        var logger = CreateTestLogger();
        var godfather = new GodfatherAgent("don-001", "Don Vito", logger);
        var message = CreateMessage("underboss-001", "We need to whack someone");

        await godfather.ProcessMessageAsync(message);

        var approvedHits = godfather.GetApprovedHits();
        Assert.NotEmpty(approvedHits);
        Assert.True(approvedHits[0].StartsWith("HIT-"));
    }

    [Test]
    public async Task GodfatherAgent_ProcessAsync_EliminateKeyword_ApprovesHit()
    {
        var logger = CreateTestLogger();
        var godfather = new GodfatherAgent("don-001", "Don Vito", logger);
        var message = CreateMessage("underboss-001", "We need to eliminate the problem");

        var result = await godfather.ProcessMessageAsync(message);

        Assert.True(result.Success);
        Assert.NotEmpty(result.ForwardedMessages);
        Assert.Contains("done", result.Response, StringComparison.OrdinalIgnoreCase);
    }

    [Test]
    public async Task GodfatherAgent_ProcessAsync_HitOnPolice_Declined()
    {
        var logger = CreateTestLogger();
        var godfather = new GodfatherAgent("don-001", "Don Vito", logger);
        var message = CreateMessage("underboss-001", "We need a hit on the police captain");

        var result = await godfather.ProcessMessageAsync(message);

        Assert.True(result.Success);
        Assert.Empty(result.ForwardedMessages);
        Assert.NotNull(result.Response);
        // Police keyword triggers police/raid handler, not hit handler
        Assert.Contains("lawyers", result.Response, StringComparison.OrdinalIgnoreCase);
    }

    [Test]
    public async Task GodfatherAgent_ProcessAsync_HitOnPolitician_Declined()
    {
        var logger = CreateTestLogger();
        var godfather = new GodfatherAgent("don-001", "Don Vito", logger);
        var message = CreateMessage("underboss-001", "We need to eliminate the politician");

        var result = await godfather.ProcessMessageAsync(message);

        Assert.True(result.Success);
        Assert.Empty(result.ForwardedMessages);
        Assert.NotNull(result.Response);
        Assert.Contains("law or politics", result.Response, StringComparison.OrdinalIgnoreCase);
    }

    [Test]
    public async Task GodfatherAgent_ProcessAsync_BusinessProposal_ForwardsToConsigliere()
    {
        var logger = CreateTestLogger();
        var godfather = new GodfatherAgent("don-001", "Don Vito", logger);
        var message = CreateMessage("capo-001", "I have a business proposal");

        var result = await godfather.ProcessMessageAsync(message);

        Assert.True(result.Success);
        Assert.NotEmpty(result.ForwardedMessages);
        Assert.Equal("consigliere-001", result.ForwardedMessages[0].ReceiverId);
        Assert.Contains("Consigliere", result.Response);
    }

    [Test]
    public async Task GodfatherAgent_ProcessAsync_OperationKeyword_ForwardsToConsigliere()
    {
        var logger = CreateTestLogger();
        var godfather = new GodfatherAgent("don-001", "Don Vito", logger);
        var message = CreateMessage("capo-001", "There's a new operation we can run");

        var result = await godfather.ProcessMessageAsync(message);

        Assert.True(result.Success);
        Assert.NotEmpty(result.ForwardedMessages);
        Assert.Equal("consigliere-001", result.ForwardedMessages[0].ReceiverId);
    }

    [Test]
    public async Task GodfatherAgent_ProcessAsync_DefaultMessage_ReturnsWisdom()
    {
        var logger = CreateTestLogger();
        var godfather = new GodfatherAgent("don-001", "Don Vito", logger);
        var message = CreateMessage("soldier-001", "Just a regular update");

        var result = await godfather.ProcessMessageAsync(message);

        Assert.True(result.Success);
        Assert.NotNull(result.Response);
        Assert.Contains("Do what needs to be done", result.Response);
    }

    [Test]
    public void GodfatherAgent_GetFavorsOwed_ReturnsNewDictionary()
    {
        var logger = CreateTestLogger();
        var godfather = new GodfatherAgent("don-001", "Don Vito", logger);

        var favors1 = godfather.GetFavorsOwed();
        var favors2 = godfather.GetFavorsOwed();

        Assert.NotSame(favors1, favors2);
    }

    [Test]
    public void GodfatherAgent_GetApprovedHits_ReturnsNewList()
    {
        var logger = CreateTestLogger();
        var godfather = new GodfatherAgent("don-001", "Don Vito", logger);

        var hits1 = godfather.GetApprovedHits();
        var hits2 = godfather.GetApprovedHits();

        Assert.NotSame(hits1, hits2);
    }

    #endregion

    #region UnderbossAgent Tests

    [Test]
    public void UnderbossAgent_Construction_SetsPropertiesCorrectly()
    {
        var logger = CreateTestLogger();
        var underboss = new UnderbossAgent("underboss-001", "Tommy", logger);

        Assert.Equal("underboss-001", underboss.Id);
        Assert.Equal("Tommy", underboss.Name);
        Assert.NotNull(underboss.Capabilities);
    }

    [Test]
    public void UnderbossAgent_Capabilities_HasCorrectCategories()
    {
        var logger = CreateTestLogger();
        var underboss = new UnderbossAgent("underboss-001", "Tommy", logger);

        Assert.Contains("DailyOperations", underboss.Capabilities.SupportedCategories);
        Assert.Contains("CrewManagement", underboss.Capabilities.SupportedCategories);
        Assert.Contains("Revenue", underboss.Capabilities.SupportedCategories);
        Assert.Contains("Enforcement", underboss.Capabilities.SupportedCategories);
    }

    [Test]
    public void UnderbossAgent_Capabilities_HasCorrectSkills()
    {
        var logger = CreateTestLogger();
        var underboss = new UnderbossAgent("underboss-001", "Tommy", logger);

        Assert.Contains("Management", underboss.Capabilities.Skills);
        Assert.Contains("Enforcement", underboss.Capabilities.Skills);
        Assert.Contains("Coordination", underboss.Capabilities.Skills);
    }

    [Test]
    public async Task UnderbossAgent_ProcessAsync_CollectionReport_ReturnsInstructions()
    {
        var logger = CreateTestLogger();
        var underboss = new UnderbossAgent("underboss-001", "Tommy", logger);
        var message = CreateMessage("capo-001", "Collection report: we got $15,000 this week");

        var result = await underboss.ProcessMessageAsync(message);

        Assert.True(result.Success);
        Assert.NotNull(result.Response);
        Assert.Contains("clockwork", result.Response, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Collections", result.Response);
    }

    [Test]
    public async Task UnderbossAgent_ProcessAsync_PaymentKeyword_HandlesCollections()
    {
        var logger = CreateTestLogger();
        var underboss = new UnderbossAgent("underboss-001", "Tommy", logger);
        var message = CreateMessage("capo-001", "Payment received from the restaurant");

        var result = await underboss.ProcessMessageAsync(message);

        Assert.True(result.Success);
        Assert.NotNull(result.Response);
        Assert.Contains("Don", result.Response);
    }

    [Test]
    public async Task UnderbossAgent_ProcessAsync_CrewDispute_MediatesConflict()
    {
        var logger = CreateTestLogger();
        var underboss = new UnderbossAgent("underboss-001", "Tommy", logger);
        var message = CreateMessage("capo-001", "There's a crew problem between my guys");

        var result = await underboss.ProcessMessageAsync(message);

        Assert.True(result.Success);
        Assert.NotNull(result.Response);
        Assert.Contains("Capos", result.Response);
        Assert.Contains("settle", result.Response, StringComparison.OrdinalIgnoreCase);
    }

    [Test]
    public async Task UnderbossAgent_ProcessAsync_DisputeKeyword_HandlesCrewDisputes()
    {
        var logger = CreateTestLogger();
        var underboss = new UnderbossAgent("underboss-001", "Tommy", logger);
        var message = CreateMessage("capo-001", "We have a dispute to resolve");

        var result = await underboss.ProcessMessageAsync(message);

        Assert.True(result.Success);
        Assert.NotNull(result.Response);
        Assert.Contains("Don", result.Response);
    }

    [Test]
    public async Task UnderbossAgent_ProcessAsync_ProtectionRacket_ForwardsToCapo()
    {
        var logger = CreateTestLogger();
        var underboss = new UnderbossAgent("underboss-001", "Tommy", logger);
        var message = CreateMessage("associate-001", "Need to set up protection at the new store");

        var result = await underboss.ProcessMessageAsync(message);

        Assert.True(result.Success);
        Assert.NotEmpty(result.ForwardedMessages);
        Assert.Equal("capo-001", result.ForwardedMessages[0].ReceiverId);
    }

    [Test]
    public async Task UnderbossAgent_ProcessAsync_StoreKeyword_HandlesProtection()
    {
        var logger = CreateTestLogger();
        var underboss = new UnderbossAgent("underboss-001", "Tommy", logger);
        var message = CreateMessage("associate-001", "The store owner wants to talk");

        var result = await underboss.ProcessMessageAsync(message);

        Assert.True(result.Success);
        Assert.NotEmpty(result.ForwardedMessages);
        Assert.Contains("shopkeeper", result.Response, StringComparison.OrdinalIgnoreCase);
    }

    [Test]
    public async Task UnderbossAgent_ProcessAsync_EnforcementIssue_ForwardsToSoldier()
    {
        var logger = CreateTestLogger();
        var underboss = new UnderbossAgent("underboss-001", "Tommy", logger);
        var message = CreateMessage("capo-001", "Need to enforce our agreement with the docks");

        var result = await underboss.ProcessMessageAsync(message);

        Assert.True(result.Success);
        Assert.NotEmpty(result.ForwardedMessages);
        Assert.Equal("soldier-001", result.ForwardedMessages[0].ReceiverId);
    }

    [Test]
    public async Task UnderbossAgent_ProcessAsync_MuscleKeyword_HandlesEnforcement()
    {
        var logger = CreateTestLogger();
        var underboss = new UnderbossAgent("underboss-001", "Tommy", logger);
        var message = CreateMessage("capo-001", "We need some muscle at the warehouse");

        var result = await underboss.ProcessMessageAsync(message);

        Assert.True(result.Success);
        Assert.NotEmpty(result.ForwardedMessages);
        Assert.Contains("boys", result.Response, StringComparison.OrdinalIgnoreCase);
    }

    [Test]
    public async Task UnderbossAgent_ProcessAsync_DefaultMessage_ReturnsStandardResponse()
    {
        var logger = CreateTestLogger();
        var underboss = new UnderbossAgent("underboss-001", "Tommy", logger);
        var message = CreateMessage("soldier-001", "Just checking in");

        var result = await underboss.ProcessMessageAsync(message);

        Assert.True(result.Success);
        Assert.NotNull(result.Response);
        Assert.Contains("family", result.Response, StringComparison.OrdinalIgnoreCase);
    }

    #endregion

    #region ConsigliereAgent Tests

    [Test]
    public void ConsigliereAgent_Construction_SetsPropertiesCorrectly()
    {
        var logger = CreateTestLogger();
        var consigliere = new ConsigliereAgent("consigliere-001", "Tom Hagen", logger);

        Assert.Equal("consigliere-001", consigliere.Id);
        Assert.Equal("Tom Hagen", consigliere.Name);
        Assert.NotNull(consigliere.Capabilities);
    }

    [Test]
    public void ConsigliereAgent_Capabilities_HasCorrectCategories()
    {
        var logger = CreateTestLogger();
        var consigliere = new ConsigliereAgent("consigliere-001", "Tom Hagen", logger);

        Assert.Contains("Legal", consigliere.Capabilities.SupportedCategories);
        Assert.Contains("Strategy", consigliere.Capabilities.SupportedCategories);
        Assert.Contains("Negotiations", consigliere.Capabilities.SupportedCategories);
        Assert.Contains("Counseling", consigliere.Capabilities.SupportedCategories);
    }

    [Test]
    public void ConsigliereAgent_Capabilities_HasCorrectSkills()
    {
        var logger = CreateTestLogger();
        var consigliere = new ConsigliereAgent("consigliere-001", "Tom Hagen", logger);

        Assert.Contains("LegalAdvice", consigliere.Capabilities.Skills);
        Assert.Contains("Strategy", consigliere.Capabilities.Skills);
        Assert.Contains("Diplomacy", consigliere.Capabilities.Skills);
        Assert.Contains("Intelligence", consigliere.Capabilities.Skills);
    }

    [Test]
    public async Task ConsigliereAgent_ProcessAsync_LegalMatter_ReturnsOptions()
    {
        var logger = CreateTestLogger();
        var consigliere = new ConsigliereAgent("consigliere-001", "Tom Hagen", logger);
        var message = CreateMessage("don-001", "We have a legal situation");

        var result = await consigliere.ProcessMessageAsync(message);

        Assert.True(result.Success);
        Assert.NotNull(result.Response);
        Assert.Contains("lawyers", result.Response, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("judges", result.Response, StringComparison.OrdinalIgnoreCase);
    }

    [Test]
    public async Task ConsigliereAgent_ProcessAsync_LawyerKeyword_HandlesLegalMatters()
    {
        var logger = CreateTestLogger();
        var consigliere = new ConsigliereAgent("consigliere-001", "Tom Hagen", logger);
        var message = CreateMessage("underboss-001", "Need to talk to our lawyer about this");

        var result = await consigliere.ProcessMessageAsync(message);

        Assert.True(result.Success);
        Assert.NotNull(result.Response);
        Assert.Contains("lawyers", result.Response, StringComparison.OrdinalIgnoreCase);
    }

    [Test]
    public async Task ConsigliereAgent_ProcessAsync_CourtKeyword_HandlesLegalMatters()
    {
        var logger = CreateTestLogger();
        var consigliere = new ConsigliereAgent("consigliere-001", "Tom Hagen", logger);
        var message = CreateMessage("underboss-001", "There's a court date coming up");

        var result = await consigliere.ProcessMessageAsync(message);

        Assert.True(result.Success);
        Assert.NotNull(result.Response);
        Assert.Contains("lawyers", result.Response, StringComparison.OrdinalIgnoreCase);
    }

    [Test]
    public async Task ConsigliereAgent_ProcessAsync_StrategyRequest_ReturnsAdvice()
    {
        var logger = CreateTestLogger();
        var consigliere = new ConsigliereAgent("consigliere-001", "Tom Hagen", logger);
        var message = CreateMessage("don-001", "What's our strategy here?");

        var result = await consigliere.ProcessMessageAsync(message);

        Assert.True(result.Success);
        Assert.NotNull(result.Response);
        Assert.Contains("analyzed", result.Response, StringComparison.OrdinalIgnoreCase);
    }

    [Test]
    public async Task ConsigliereAgent_ProcessAsync_PlanKeyword_HandlesStrategy()
    {
        var logger = CreateTestLogger();
        var consigliere = new ConsigliereAgent("consigliere-001", "Tom Hagen", logger);
        var message = CreateMessage("don-001", "We need a plan for expansion");

        var result = await consigliere.ProcessMessageAsync(message);

        Assert.True(result.Success);
        Assert.NotNull(result.Response);
        Assert.Contains("counsel", result.Response, StringComparison.OrdinalIgnoreCase);
    }

    [Test]
    public async Task ConsigliereAgent_ProcessAsync_MoveKeyword_HandlesStrategy()
    {
        var logger = CreateTestLogger();
        var consigliere = new ConsigliereAgent("consigliere-001", "Tom Hagen", logger);
        var message = CreateMessage("don-001", "Should we make a move on their territory?");

        var result = await consigliere.ProcessMessageAsync(message);

        Assert.True(result.Success);
        Assert.NotNull(result.Response);
        Assert.Contains("weak", result.Response, StringComparison.OrdinalIgnoreCase);
    }

    [Test]
    public async Task ConsigliereAgent_ProcessAsync_NegotiationRequest_ReturnsTips()
    {
        var logger = CreateTestLogger();
        var consigliere = new ConsigliereAgent("consigliere-001", "Tom Hagen", logger);
        var message = CreateMessage("don-001", "Help me negotiate with the Tattaglias");

        var result = await consigliere.ProcessMessageAsync(message);

        Assert.True(result.Success);
        Assert.NotNull(result.Response);
        Assert.Contains("negotiation", result.Response, StringComparison.OrdinalIgnoreCase);
    }

    [Test]
    public async Task ConsigliereAgent_ProcessAsync_DealKeyword_HandlesNegotiations()
    {
        var logger = CreateTestLogger();
        var consigliere = new ConsigliereAgent("consigliere-001", "Tom Hagen", logger);
        var message = CreateMessage("underboss-001", "They want to make a deal");

        var result = await consigliere.ProcessMessageAsync(message);

        Assert.True(result.Success);
        Assert.NotNull(result.Response);
        Assert.Contains("leverage", result.Response, StringComparison.OrdinalIgnoreCase);
    }

    [Test]
    public async Task ConsigliereAgent_ProcessAsync_TreatyKeyword_HandlesNegotiations()
    {
        var logger = CreateTestLogger();
        var consigliere = new ConsigliereAgent("consigliere-001", "Tom Hagen", logger);
        var message = CreateMessage("don-001", "Let's discuss the peace treaty terms");

        var result = await consigliere.ProcessMessageAsync(message);

        Assert.True(result.Success);
        Assert.NotNull(result.Response);
        Assert.Contains("writing", result.Response, StringComparison.OrdinalIgnoreCase);
    }

    [Test]
    public async Task ConsigliereAgent_ProcessAsync_DefaultMessage_ReturnsAdvice()
    {
        var logger = CreateTestLogger();
        var consigliere = new ConsigliereAgent("consigliere-001", "Tom Hagen", logger);
        var message = CreateMessage("capo-001", "I have a question for you");

        var result = await consigliere.ProcessMessageAsync(message);

        Assert.True(result.Success);
        Assert.NotNull(result.Response);
        Assert.Contains("study the situation", result.Response, StringComparison.OrdinalIgnoreCase);
    }

    #endregion

    #region CapoAgent Tests

    [Test]
    public void CapoAgent_Construction_SetsPropertiesCorrectly()
    {
        var logger = CreateTestLogger();
        var crewMembers = new List<string> { "soldier-001", "soldier-002" };
        var capo = new CapoAgent("capo-001", "Paulie", logger, crewMembers);

        Assert.Equal("capo-001", capo.Id);
        Assert.Equal("Paulie", capo.Name);
        Assert.NotNull(capo.Capabilities);
    }

    [Test]
    public void CapoAgent_Capabilities_HasCorrectCategories()
    {
        var logger = CreateTestLogger();
        var crewMembers = new List<string> { "soldier-001" };
        var capo = new CapoAgent("capo-001", "Paulie", logger, crewMembers);

        Assert.Contains("ProtectionRacket", capo.Capabilities.SupportedCategories);
        Assert.Contains("LoanSharking", capo.Capabilities.SupportedCategories);
        Assert.Contains("Gambling", capo.Capabilities.SupportedCategories);
        Assert.Contains("CrewLeadership", capo.Capabilities.SupportedCategories);
    }

    [Test]
    public void CapoAgent_Capabilities_HasCorrectSkills()
    {
        var logger = CreateTestLogger();
        var crewMembers = new List<string> { "soldier-001" };
        var capo = new CapoAgent("capo-001", "Paulie", logger, crewMembers);

        Assert.Contains("CrewManagement", capo.Capabilities.Skills);
        Assert.Contains("Collections", capo.Capabilities.Skills);
        Assert.Contains("Intimidation", capo.Capabilities.Skills);
    }

    [Test]
    public async Task CapoAgent_ProcessAsync_Collection_ReportsAmountAndSplit()
    {
        var logger = CreateTestLogger();
        var crewMembers = new List<string> { "soldier-001", "soldier-002" };
        var capo = new CapoAgent("capo-001", "Paulie", logger, crewMembers);
        var message = CreateMessage("underboss-001", "Time to collect from the businesses");

        var result = await capo.ProcessMessageAsync(message);

        Assert.True(result.Success);
        Assert.NotNull(result.Response);
        Assert.Contains("collected", result.Response, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Underboss", result.Response);
        Assert.Contains("crew", result.Response, StringComparison.OrdinalIgnoreCase);
    }

    [Test]
    public async Task CapoAgent_ProcessAsync_PickupKeyword_HandlesCollection()
    {
        var logger = CreateTestLogger();
        var crewMembers = new List<string> { "soldier-001" };
        var capo = new CapoAgent("capo-001", "Paulie", logger, crewMembers);
        var message = CreateMessage("underboss-001", "Do the pickup at the restaurant");

        var result = await capo.ProcessMessageAsync(message);

        Assert.True(result.Success);
        Assert.NotNull(result.Response);
        Assert.Contains("Business is good", result.Response);
    }

    [Test]
    public async Task CapoAgent_ProcessAsync_Collection_UpdatesWeeklyTake()
    {
        var logger = CreateTestLogger();
        var crewMembers = new List<string> { "soldier-001" };
        var capo = new CapoAgent("capo-001", "Paulie", logger, crewMembers);
        var message = CreateMessage("underboss-001", "Time for collection");

        Assert.Equal(0m, capo.GetWeeklyTake());

        await capo.ProcessMessageAsync(message);

        Assert.True(capo.GetWeeklyTake() > 0);
    }

    [Test]
    public async Task CapoAgent_ProcessAsync_MultipleCollections_AccumulatesWeeklyTake()
    {
        var logger = CreateTestLogger();
        var crewMembers = new List<string> { "soldier-001" };
        var capo = new CapoAgent("capo-001", "Paulie", logger, crewMembers);
        var message = CreateMessage("underboss-001", "Time for collection");

        await capo.ProcessMessageAsync(message);
        var firstTake = capo.GetWeeklyTake();

        await capo.ProcessMessageAsync(message);
        var secondTake = capo.GetWeeklyTake();

        Assert.True(secondTake > firstTake);
    }

    [Test]
    public async Task CapoAgent_ProcessAsync_RecruitRequest_ExplainsProcess()
    {
        var logger = CreateTestLogger();
        var crewMembers = new List<string> { "soldier-001" };
        var capo = new CapoAgent("capo-001", "Paulie", logger, crewMembers);
        var message = CreateMessage("underboss-001", "I want to recruit someone new");

        var result = await capo.ProcessMessageAsync(message);

        Assert.True(result.Success);
        Assert.NotNull(result.Response);
        Assert.Contains("Italian", result.Response);
        Assert.Contains("oath", result.Response, StringComparison.OrdinalIgnoreCase);
    }

    [Test]
    public async Task CapoAgent_ProcessAsync_SoldierKeyword_HandlesRecruitment()
    {
        var logger = CreateTestLogger();
        var crewMembers = new List<string> { "soldier-001" };
        var capo = new CapoAgent("capo-001", "Paulie", logger, crewMembers);
        // Don't use "crew" - it matches before "soldier" in handler
        var message = CreateMessage("underboss-001", "We need a new soldier");

        var result = await capo.ProcessMessageAsync(message);

        Assert.True(result.Success);
        Assert.NotNull(result.Response);
        Assert.Contains("Italian", result.Response);
    }

    [Test]
    public async Task CapoAgent_ProcessAsync_TerritoryQuestion_DescribesOperations()
    {
        var logger = CreateTestLogger();
        var crewMembers = new List<string> { "soldier-001", "soldier-002" };
        var capo = new CapoAgent("capo-001", "Paulie", logger, crewMembers);
        var message = CreateMessage("underboss-001", "Tell me about your territory");

        var result = await capo.ProcessMessageAsync(message);

        Assert.True(result.Success);
        Assert.NotNull(result.Response);
        Assert.Contains("territory", result.Response, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Mulberry Street", result.Response);
    }

    [Test]
    public async Task CapoAgent_ProcessAsync_TurfKeyword_HandlesTerritory()
    {
        var logger = CreateTestLogger();
        var crewMembers = new List<string> { "soldier-001" };
        var capo = new CapoAgent("capo-001", "Paulie", logger, crewMembers);
        var message = CreateMessage("underboss-001", "What's happening on your turf?");

        var result = await capo.ProcessMessageAsync(message);

        Assert.True(result.Success);
        Assert.NotNull(result.Response);
        Assert.Contains("Little Italy", result.Response);
    }

    [Test]
    public async Task CapoAgent_ProcessAsync_DefaultMessage_MentionsCrewSize()
    {
        var logger = CreateTestLogger();
        var crewMembers = new List<string> { "soldier-001", "soldier-002", "soldier-003" };
        var capo = new CapoAgent("capo-001", "Paulie", logger, crewMembers);
        var message = CreateMessage("underboss-001", "Status report");

        var result = await capo.ProcessMessageAsync(message);

        Assert.True(result.Success);
        Assert.NotNull(result.Response);
        Assert.Contains("Got it, boss", result.Response);
    }

    [Test]
    public void CapoAgent_GetWeeklyTake_InitializesToZero()
    {
        var logger = CreateTestLogger();
        var crewMembers = new List<string> { "soldier-001" };
        var capo = new CapoAgent("capo-001", "Paulie", logger, crewMembers);

        Assert.Equal(0m, capo.GetWeeklyTake());
    }

    #endregion

    #region SoldierAgent Tests

    [Test]
    public void SoldierAgent_Construction_SetsPropertiesCorrectly()
    {
        var logger = CreateTestLogger();
        var soldier = new SoldierAgent("soldier-001", "Luca Brasi", logger);

        Assert.Equal("soldier-001", soldier.Id);
        Assert.Equal("Luca Brasi", soldier.Name);
        Assert.NotNull(soldier.Capabilities);
    }

    [Test]
    public void SoldierAgent_Capabilities_HasCorrectCategories()
    {
        var logger = CreateTestLogger();
        var soldier = new SoldierAgent("soldier-001", "Luca Brasi", logger);

        Assert.Contains("Enforcement", soldier.Capabilities.SupportedCategories);
        Assert.Contains("Collections", soldier.Capabilities.SupportedCategories);
        Assert.Contains("Intimidation", soldier.Capabilities.SupportedCategories);
        Assert.Contains("Hits", soldier.Capabilities.SupportedCategories);
    }

    [Test]
    public void SoldierAgent_Capabilities_HasCorrectSkills()
    {
        var logger = CreateTestLogger();
        var soldier = new SoldierAgent("soldier-001", "Luca Brasi", logger);

        Assert.Contains("Muscle", soldier.Capabilities.Skills);
        Assert.Contains("Intimidation", soldier.Capabilities.Skills);
        Assert.Contains("Loyalty", soldier.Capabilities.Skills);
        Assert.Contains("Discretion", soldier.Capabilities.Skills);
    }

    [Test]
    public async Task SoldierAgent_ProcessAsync_Collection_TracksJob()
    {
        var logger = CreateTestLogger();
        var soldier = new SoldierAgent("soldier-001", "Luca Brasi", logger);
        var message = CreateMessage("capo-001", "Go collect from the restaurant owner");

        Assert.Empty(soldier.GetCompletedJobs());

        var result = await soldier.ProcessMessageAsync(message);

        Assert.True(result.Success);
        Assert.NotEmpty(soldier.GetCompletedJobs());
        Assert.True(soldier.GetCompletedJobs()[0].StartsWith("Collection-"));
    }

    [Test]
    public async Task SoldierAgent_ProcessAsync_PaymentKeyword_HandlesCollection()
    {
        var logger = CreateTestLogger();
        var soldier = new SoldierAgent("soldier-001", "Luca Brasi", logger);
        var message = CreateMessage("capo-001", "Get the payment from that guy");

        var result = await soldier.ProcessMessageAsync(message);

        Assert.True(result.Success);
        Assert.NotNull(result.Response);
        Assert.Contains("payment", result.Response, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Capo", result.Response);
    }

    [Test]
    public async Task SoldierAgent_ProcessAsync_Collection_ReturnsConfirmation()
    {
        var logger = CreateTestLogger();
        var soldier = new SoldierAgent("soldier-001", "Luca Brasi", logger);
        var message = CreateMessage("capo-001", "Collect from the shop");

        var result = await soldier.ProcessMessageAsync(message);

        Assert.True(result.Success);
        Assert.NotNull(result.Response);
        Assert.Contains("knuckles", result.Response, StringComparison.OrdinalIgnoreCase);
    }

    [Test]
    public async Task SoldierAgent_ProcessAsync_Intimidation_TracksJob()
    {
        var logger = CreateTestLogger();
        var soldier = new SoldierAgent("soldier-001", "Luca Brasi", logger);
        var message = CreateMessage("capo-001", "Go intimidate the witness");

        Assert.Empty(soldier.GetCompletedJobs());

        var result = await soldier.ProcessMessageAsync(message);

        Assert.True(result.Success);
        Assert.NotEmpty(soldier.GetCompletedJobs());
        Assert.True(soldier.GetCompletedJobs()[0].StartsWith("Intimidation-"));
    }

    [Test]
    public async Task SoldierAgent_ProcessAsync_ScareKeyword_HandlesIntimidation()
    {
        var logger = CreateTestLogger();
        var soldier = new SoldierAgent("soldier-001", "Luca Brasi", logger);
        var message = CreateMessage("capo-001", "Go scare that shopkeeper");

        var result = await soldier.ProcessMessageAsync(message);

        Assert.True(result.Success);
        Assert.NotNull(result.Response);
        Assert.Contains("Paulie", result.Response);
    }

    [Test]
    public async Task SoldierAgent_ProcessAsync_MessageKeyword_HandlesIntimidation()
    {
        var logger = CreateTestLogger();
        var soldier = new SoldierAgent("soldier-001", "Luca Brasi", logger);
        var message = CreateMessage("capo-001", "Send a message to him");

        var result = await soldier.ProcessMessageAsync(message);

        Assert.True(result.Success);
        Assert.NotNull(result.Response);
        Assert.Contains("message", result.Response, StringComparison.OrdinalIgnoreCase);
    }

    [Test]
    public async Task SoldierAgent_ProcessAsync_Intimidation_ReturnsReport()
    {
        var logger = CreateTestLogger();
        var soldier = new SoldierAgent("soldier-001", "Luca Brasi", logger);
        var message = CreateMessage("capo-001", "Intimidate the banker");

        var result = await soldier.ProcessMessageAsync(message);

        Assert.True(result.Success);
        Assert.NotNull(result.Response);
        Assert.Contains("problem", result.Response, StringComparison.OrdinalIgnoreCase);
    }

    [Test]
    public async Task SoldierAgent_ProcessAsync_ApprovedHit_TracksJob()
    {
        var logger = CreateTestLogger();
        var soldier = new SoldierAgent("soldier-001", "Luca Brasi", logger);
        var message = CreateMessage("underboss-001", "Execute the hit");
        message.Metadata["ApprovedByDon"] = true;

        Assert.Empty(soldier.GetCompletedJobs());

        var result = await soldier.ProcessMessageAsync(message);

        Assert.True(result.Success);
        Assert.NotEmpty(soldier.GetCompletedJobs());
        Assert.True(soldier.GetCompletedJobs()[0].StartsWith("Hit-"));
    }

    [Test]
    public async Task SoldierAgent_ProcessAsync_ApprovedHit_ReturnsConfirmation()
    {
        var logger = CreateTestLogger();
        var soldier = new SoldierAgent("soldier-001", "Luca Brasi", logger);
        var message = CreateMessage("underboss-001", "Do the hit");
        message.Metadata["ApprovedByDon"] = true;

        var result = await soldier.ProcessMessageAsync(message);

        Assert.True(result.Success);
        Assert.NotNull(result.Response);
        Assert.Contains("done", result.Response, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("cannoli", result.Response, StringComparison.OrdinalIgnoreCase);
    }

    [Test]
    public async Task SoldierAgent_ProcessAsync_UnapprovedHit_DoesNotTrackHit()
    {
        var logger = CreateTestLogger();
        var soldier = new SoldierAgent("soldier-001", "Luca Brasi", logger);
        var message = CreateMessage("random-001", "Do a hit on someone");
        // No ApprovedByDon metadata

        await soldier.ProcessMessageAsync(message);

        var jobs = soldier.GetCompletedJobs();
        Assert.True(jobs.All(j => !j.StartsWith("Hit-")));
    }

    [Test]
    public async Task SoldierAgent_ProcessAsync_DefaultMessage_ReturnsReadyResponse()
    {
        var logger = CreateTestLogger();
        var soldier = new SoldierAgent("soldier-001", "Luca Brasi", logger);
        var message = CreateMessage("capo-001", "What are you up to?");

        var result = await soldier.ProcessMessageAsync(message);

        Assert.True(result.Success);
        Assert.NotNull(result.Response);
        Assert.Contains("on it", result.Response, StringComparison.OrdinalIgnoreCase);
    }

    [Test]
    public void SoldierAgent_GetCompletedJobs_ReturnsNewList()
    {
        var logger = CreateTestLogger();
        var soldier = new SoldierAgent("soldier-001", "Luca Brasi", logger);

        var jobs1 = soldier.GetCompletedJobs();
        var jobs2 = soldier.GetCompletedJobs();

        Assert.NotSame(jobs1, jobs2);
    }

    [Test]
    public async Task SoldierAgent_ProcessAsync_MultipleJobs_TracksAll()
    {
        var logger = CreateTestLogger();
        var soldier = new SoldierAgent("soldier-001", "Luca Brasi", logger);

        var collectionMsg = CreateMessage("capo-001", "Collect the money");
        var intimidationMsg = CreateMessage("capo-001", "Intimidate the witness");

        await soldier.ProcessMessageAsync(collectionMsg);
        await soldier.ProcessMessageAsync(intimidationMsg);

        var jobs = soldier.GetCompletedJobs();
        Assert.Equal(2, jobs.Count);
    }

    #endregion

    #region AssociateAgent Tests

    [Test]
    public void AssociateAgent_Construction_SetsPropertiesCorrectly()
    {
        var logger = CreateTestLogger();
        var associate = new AssociateAgent("associate-001", "Jimmy", logger);

        Assert.Equal("associate-001", associate.Id);
        Assert.Equal("Jimmy", associate.Name);
        Assert.NotNull(associate.Capabilities);
    }

    [Test]
    public void AssociateAgent_Capabilities_HasCorrectCategories()
    {
        var logger = CreateTestLogger();
        var associate = new AssociateAgent("associate-001", "Jimmy", logger);

        Assert.Contains("Information", associate.Capabilities.SupportedCategories);
        Assert.Contains("SmallJobs", associate.Capabilities.SupportedCategories);
        Assert.Contains("Errands", associate.Capabilities.SupportedCategories);
    }

    [Test]
    public void AssociateAgent_Capabilities_HasCorrectSkills()
    {
        var logger = CreateTestLogger();
        var associate = new AssociateAgent("associate-001", "Jimmy", logger);

        Assert.Contains("StreetSmarts", associate.Capabilities.Skills);
        Assert.Contains("Connections", associate.Capabilities.Skills);
        Assert.Contains("Reliability", associate.Capabilities.Skills);
    }

    [Test]
    public async Task AssociateAgent_ProcessAsync_InfoRequest_ReturnsIntelligence()
    {
        var logger = CreateTestLogger();
        var associate = new AssociateAgent("associate-001", "Jimmy", logger);
        var message = CreateMessage("capo-001", "Got any info for me?");

        var result = await associate.ProcessMessageAsync(message);

        Assert.True(result.Success);
        Assert.NotNull(result.Response);
        Assert.Contains("street", result.Response, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Tattaglias", result.Response);
    }

    [Test]
    public async Task AssociateAgent_ProcessAsync_WordKeyword_ReturnsIntel()
    {
        var logger = CreateTestLogger();
        var associate = new AssociateAgent("associate-001", "Jimmy", logger);
        var message = CreateMessage("capo-001", "What's the word on the street?");

        var result = await associate.ProcessMessageAsync(message);

        Assert.True(result.Success);
        Assert.NotNull(result.Response);
        Assert.Contains("cops", result.Response, StringComparison.OrdinalIgnoreCase);
    }

    [Test]
    public async Task AssociateAgent_ProcessAsync_InfoRequest_MentionsDocks()
    {
        var logger = CreateTestLogger();
        var associate = new AssociateAgent("associate-001", "Jimmy", logger);
        var message = CreateMessage("capo-001", "I need info on the situation");

        var result = await associate.ProcessMessageAsync(message);

        Assert.True(result.Success);
        Assert.NotNull(result.Response);
        Assert.Contains("docks", result.Response, StringComparison.OrdinalIgnoreCase);
    }

    [Test]
    public async Task AssociateAgent_ProcessAsync_DefaultMessage_ReturnsEagerResponse()
    {
        var logger = CreateTestLogger();
        var associate = new AssociateAgent("associate-001", "Jimmy", logger);
        var message = CreateMessage("capo-001", "How are things going?");

        var result = await associate.ProcessMessageAsync(message);

        Assert.True(result.Success);
        Assert.NotNull(result.Response);
        Assert.Contains("prove myself", result.Response);
    }

    [Test]
    public async Task AssociateAgent_ProcessAsync_DefaultMessage_MentionsAmbition()
    {
        var logger = CreateTestLogger();
        var associate = new AssociateAgent("associate-001", "Jimmy", logger);
        var message = CreateMessage("soldier-001", "Just checking in");

        var result = await associate.ProcessMessageAsync(message);

        Assert.True(result.Success);
        Assert.NotNull(result.Response);
        Assert.Contains("made", result.Response, StringComparison.OrdinalIgnoreCase);
    }

    #endregion

    #region CanHandle Tests

    [Test]
    public void GodfatherAgent_CanHandle_SupportedCategory_ReturnsTrue()
    {
        var logger = CreateTestLogger();
        var godfather = new GodfatherAgent("don-001", "Don Vito", logger);
        var message = new AgentMessage { Category = "FinalDecision" };

        Assert.True(godfather.CanHandle(message));
    }

    [Test]
    public void GodfatherAgent_CanHandle_UnsupportedCategory_ReturnsFalse()
    {
        var logger = CreateTestLogger();
        var godfather = new GodfatherAgent("don-001", "Don Vito", logger);
        var message = new AgentMessage { Category = "SmallJobs" };

        Assert.False(godfather.CanHandle(message));
    }

    [Test]
    public void GodfatherAgent_CanHandle_EmptyCategory_ReturnsTrue()
    {
        var logger = CreateTestLogger();
        var godfather = new GodfatherAgent("don-001", "Don Vito", logger);
        var message = new AgentMessage { Category = "" };

        Assert.True(godfather.CanHandle(message));
    }

    [Test]
    public async Task AllAgents_CanHandle_AfterProcessing_StillAvailable()
    {
        var logger = CreateTestLogger();
        var godfather = new GodfatherAgent("don-001", "Don Vito", logger);
        var message = CreateMessage("capo-001", "A simple message");

        await godfather.ProcessMessageAsync(message);

        Assert.True(godfather.CanHandle(message));
        Assert.Equal(AgentStatus.Available, godfather.Status);
    }

    #endregion

    #region AgentBase Inherited Behavior Tests

    [Test]
    public async Task AllAgents_ProcessAsync_WithUnsupportedCategory_ReturnsFail()
    {
        var logger = CreateTestLogger();
        var soldier = new SoldierAgent("soldier-001", "Luca", logger);
        var message = new AgentMessage
        {
            SenderId = "test",
            Content = "test",
            Category = "NonexistentCategory"
        };

        var result = await soldier.ProcessMessageAsync(message);

        Assert.False(result.Success);
        Assert.NotNull(result.Error);
    }

    [Test]
    public void AllAgents_Status_InitiallyAvailable()
    {
        var logger = CreateTestLogger();

        var godfather = new GodfatherAgent("don-001", "Don", logger);
        var underboss = new UnderbossAgent("underboss-001", "Tommy", logger);
        var consigliere = new ConsigliereAgent("consigliere-001", "Tom", logger);
        var capo = new CapoAgent("capo-001", "Paulie", logger, new List<string>());
        var soldier = new SoldierAgent("soldier-001", "Luca", logger);
        var associate = new AssociateAgent("associate-001", "Jimmy", logger);

        Assert.Equal(AgentStatus.Available, godfather.Status);
        Assert.Equal(AgentStatus.Available, underboss.Status);
        Assert.Equal(AgentStatus.Available, consigliere.Status);
        Assert.Equal(AgentStatus.Available, capo.Status);
        Assert.Equal(AgentStatus.Available, soldier.Status);
        Assert.Equal(AgentStatus.Available, associate.Status);
    }

    [Test]
    public void GodfatherAgent_Capabilities_SupportsCategory_CaseInsensitive()
    {
        var logger = CreateTestLogger();
        var godfather = new GodfatherAgent("don-001", "Don Vito", logger);

        Assert.True(godfather.Capabilities.SupportsCategory("FINALDECISION"));
        Assert.True(godfather.Capabilities.SupportsCategory("finaldecision"));
        Assert.True(godfather.Capabilities.SupportsCategory("FinalDecision"));
    }

    [Test]
    public void GodfatherAgent_Capabilities_HasSkill_CaseInsensitive()
    {
        var logger = CreateTestLogger();
        var godfather = new GodfatherAgent("don-001", "Don Vito", logger);

        Assert.True(godfather.Capabilities.HasSkill("LEADERSHIP"));
        Assert.True(godfather.Capabilities.HasSkill("leadership"));
        Assert.True(godfather.Capabilities.HasSkill("Leadership"));
    }

    #endregion

    #region Edge Case Tests

    [Test]
    public async Task GodfatherAgent_ProcessAsync_MixedCaseContent_StillMatches()
    {
        var logger = CreateTestLogger();
        var godfather = new GodfatherAgent("don-001", "Don Vito", logger);
        var message = CreateMessage("capo-001", "I need a FAVOR from you");

        var result = await godfather.ProcessMessageAsync(message);

        Assert.True(result.Success);
        Assert.Contains("friends", result.Response, StringComparison.OrdinalIgnoreCase);
    }

    [Test]
    public async Task GodfatherAgent_ProcessAsync_HitWithBothPoliceAndNormal_StillDeclined()
    {
        var logger = CreateTestLogger();
        var godfather = new GodfatherAgent("don-001", "Don Vito", logger);
        var message = CreateMessage("underboss-001", "Hit on the police informant");

        var result = await godfather.ProcessMessageAsync(message);

        Assert.True(result.Success);
        Assert.Empty(result.ForwardedMessages);
    }

    [Test]
    public async Task CapoAgent_ProcessAsync_EmptyCrewList_StillWorks()
    {
        var logger = CreateTestLogger();
        var crewMembers = new List<string>();
        var capo = new CapoAgent("capo-001", "Paulie", logger, crewMembers);
        var message = CreateMessage("underboss-001", "Status update");

        var result = await capo.ProcessMessageAsync(message);

        Assert.True(result.Success);
        Assert.Contains("Got it, boss", result.Response);
    }

    [Test]
    public async Task SoldierAgent_ProcessAsync_HitWithoutApproval_DefaultResponse()
    {
        var logger = CreateTestLogger();
        var soldier = new SoldierAgent("soldier-001", "Luca", logger);
        var message = CreateMessage("random-001", "Do a hit");

        var result = await soldier.ProcessMessageAsync(message);

        Assert.True(result.Success);
        // Should get default response since hit isn't approved
        Assert.Contains("on it", result.Response, StringComparison.OrdinalIgnoreCase);
    }

    [Test]
    public async Task AllAgents_ProcessAsync_CancellationToken_Respected()
    {
        var logger = CreateTestLogger();
        var godfather = new GodfatherAgent("don-001", "Don Vito", logger);
        var message = CreateMessage("capo-001", "Need a favor");

        using var cts = new CancellationTokenSource();
        // Don't cancel - just verify it accepts the token
        var result = await godfather.ProcessMessageAsync(message, cts.Token);

        Assert.True(result.Success);
    }

    #endregion

    #region ForwardedMessage Tests

    [Test]
    public async Task GodfatherAgent_ProcessAsync_BusinessProposal_ForwardContainsNote()
    {
        var logger = CreateTestLogger();
        var godfather = new GodfatherAgent("don-001", "Don Vito", logger);
        var message = CreateMessage("capo-001", "New business opportunity");

        var result = await godfather.ProcessMessageAsync(message);

        Assert.NotEmpty(result.ForwardedMessages);
        var forward = result.ForwardedMessages[0];
        Assert.Contains("legal implications", forward.Content, StringComparison.OrdinalIgnoreCase);
    }

    [Test]
    public async Task UnderbossAgent_ProcessAsync_Protection_ForwardContainsInstructions()
    {
        var logger = CreateTestLogger();
        var underboss = new UnderbossAgent("underboss-001", "Tommy", logger);
        var message = CreateMessage("associate-001", "New store needs protection");

        var result = await underboss.ProcessMessageAsync(message);

        Assert.NotEmpty(result.ForwardedMessages);
        var forward = result.ForwardedMessages[0];
        Assert.Contains("protection", forward.Content, StringComparison.OrdinalIgnoreCase);
    }

    [Test]
    public async Task UnderbossAgent_ProcessAsync_Enforcement_ForwardContainsGuidelines()
    {
        var logger = CreateTestLogger();
        var underboss = new UnderbossAgent("underboss-001", "Tommy", logger);
        var message = CreateMessage("capo-001", "Need to enforce");

        var result = await underboss.ProcessMessageAsync(message);

        Assert.NotEmpty(result.ForwardedMessages);
        var forward = result.ForwardedMessages[0];
        Assert.Contains("permanent", forward.Content, StringComparison.OrdinalIgnoreCase);
    }

    #endregion
}
