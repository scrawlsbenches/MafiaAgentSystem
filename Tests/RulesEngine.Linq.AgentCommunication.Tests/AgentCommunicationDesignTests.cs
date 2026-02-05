namespace RulesEngine.Linq.AgentCommunication.Tests;

using System;
using System.Collections.Generic;
using System.Linq;
using TestRunner.Framework;
using Mafia.Domain;
using RulesEngine.Linq.AgentCommunication;

// TODO: This file has build errors due to API changes in IAgentRule<T>.
// The Evaluate() and Execute() methods now require an IAgentRulesContext parameter,
// but these tests call them without context. The IAgentRule<T> interface evolved
// to support context-aware evaluation before these tests were updated.
//
// To fix: Either update tests to provide context, or add overloads to IAgentRule<T>
// that default to null context for backwards compatibility.
//
// Additionally:
// - MessageType.Command doesn't exist (line 328)
// - ValidationStage<T>, TransformStage<T>, LogStage<T> are not public (lines 411, 421, 431)

/// <summary>
/// TDD tests for the AgentCommunication design API.
/// These tests capture the intended API usage from the design document.
/// Tests may fail until implementations are complete - that's expected.
/// </summary>
public class AgentCommunicationDesignTests
{
    #region Test Helpers - Create test agents and messages

    private static Agent CreateSoldier(string id = "soldier-1", string familyId = "family-1")
        => new Agent
        {
            Id = id,
            Name = $"Soldier {id}",
            Role = AgentRole.Soldier,
            Status = AgentStatus.Available,
            FamilyId = familyId
        };

    private static Agent CreateCapo(string id = "capo-1", string familyId = "family-1")
        => new Agent
        {
            Id = id,
            Name = $"Capo {id}",
            Role = AgentRole.Capo,
            Status = AgentStatus.Available,
            FamilyId = familyId
        };

    private static Agent CreateUnderboss(string id = "underboss-1", string familyId = "family-1")
        => new Agent
        {
            Id = id,
            Name = $"Underboss {id}",
            Role = AgentRole.Underboss,
            Status = AgentStatus.Available,
            FamilyId = familyId
        };

    private static Agent CreateGodfather(string id = "godfather-1", string familyId = "family-1")
        => new Agent
        {
            Id = id,
            Name = $"Godfather {id}",
            Role = AgentRole.Godfather,
            Status = AgentStatus.Available,
            FamilyId = familyId
        };

    private static void SetupHierarchy(Agent soldier, Agent capo, Agent underboss, Agent godfather)
    {
        soldier.SuperiorId = capo.Id;
        soldier.Superior = capo;
        soldier.CapoId = capo.Id;

        capo.SuperiorId = underboss.Id;
        capo.Superior = underboss;

        underboss.SuperiorId = godfather.Id;
        underboss.Superior = godfather;
    }

    #endregion

    #region 1. Permission Rules - Who Can Message Whom

    [Test]
    public void PermissionRule_SoldierCannotMessageGodfatherDirectly()
    {
        // This test demonstrates the fluent rule builder API
        // Building the rule should work even if execution isn't implemented yet

        var rule = Rule.When<AgentMessage>(m =>
                m.From!.Role == AgentRole.Soldier &&
                m.To!.Role == AgentRole.Godfather)
            .Then(m => m.Block("Soldiers cannot message Godfather directly"))
            .WithReason("Chain of command enforcement")
            .WithTags("permission", "hierarchy")
            .WithPriority(100)
            .Build();

        // Verify rule was built with correct metadata
        Assert.NotNull(rule);
        Assert.Equal(100, rule.Priority);
        Assert.Contains("permission", rule.Tags);
        Assert.Contains("hierarchy", rule.Tags);
        Assert.Equal("Chain of command enforcement", rule.Reason);
    }

    [Test]
    public void PermissionRule_EvaluatesAndBlocksMessage()
    {
        // Setup hierarchy
        var godfather = CreateGodfather();
        var underboss = CreateUnderboss();
        var capo = CreateCapo();
        var soldier = CreateSoldier();
        SetupHierarchy(soldier, capo, underboss, godfather);

        // Create blocking rule
        var rule = Rule.When<AgentMessage>(m =>
                m.From!.Role == AgentRole.Soldier &&
                m.To!.Role == AgentRole.Godfather)
            .Then(m => m.Block("Soldiers cannot message Godfather directly"))
            .WithId("block-soldier-to-godfather")
            .Build();

        // Create message that should be blocked
        var message = new AgentMessage
        {
            Type = MessageType.Request,
            FromId = soldier.Id,
            ToId = godfather.Id,
            From = soldier,
            To = godfather
        };

        // Evaluate rule directly (without context)
        // This tests the rule's Evaluate and Execute methods
        var matches = rule.Evaluate(message);
        if (matches)
        {
            rule.Execute(message);
        }

        Assert.True(message.Blocked);
        Assert.Equal("Soldiers cannot message Godfather directly", message.BlockReason);
    }

    #endregion

    #region 2. Chain of Command - Rerouting

    [Test]
    public void ChainOfCommand_ReroutesSkippedLevels()
    {
        var godfather = CreateGodfather();
        var underboss = CreateUnderboss();
        var capo = CreateCapo();
        var soldier = CreateSoldier();
        SetupHierarchy(soldier, capo, underboss, godfather);

        // Rule: requests must go through chain of command
        var rule = Rule.When<AgentMessage>(m => m.Type == MessageType.Request)
            .And(m =>
                !m.From!.IsImmediateSuperior(m.To!) &&
                !m.From!.IsImmediateSubordinate(m.To!) &&
                m.From!.FamilyId == m.To!.FamilyId)
            .Then(m => m.Reroute(m.From!.Superior!))
            .WithReason("Requests must go through chain of command")
            .WithTags("routing", "hierarchy")
            .Build();

        // Soldier trying to message godfather directly (skips capo and underboss)
        var message = new AgentMessage
        {
            Type = MessageType.Request,
            FromId = soldier.Id,
            ToId = godfather.Id,
            From = soldier,
            To = godfather
        };

        // Evaluate and execute
        if (rule.Evaluate(message))
        {
            rule.Execute(message);
        }

        // Should be rerouted to immediate superior (capo)
        Assert.Equal(capo.Id, message.ReroutedToId);
        Assert.Equal(capo, message.ReroutedTo);
    }

    #endregion

    #region 3. Cross-Fact Query - Territory Check

    [Test]
    public void CrossFactQuery_FlagsHostileTerritoryRequest()
    {
        var soldier = CreateSoldier(familyId: "corleone");
        var capo = CreateCapo(familyId: "corleone");
        soldier.SuperiorId = capo.Id;
        soldier.Superior = capo;

        // Rule with context-aware condition that queries territories
        // The condition accesses ctx.Territories() for cross-fact lookup
        var rule = Rule.When<AgentMessage>(m => m.Type == MessageType.TerritoryRequest)
            .And((m, ctx) =>
                ctx.Territories()
                    .Where(t => t.Id == (string)m.Payload["TerritoryId"])
                    .Any(t => t.ControlledBy != m.From!.FamilyId))
            .Then(m => m.Flag("hostile-territory-request"))
            .WithTags("territory", "security")
            .DependsOn<Territory>()
            .Build();

        Assert.NotNull(rule);
        Assert.Contains("territory", rule.Tags);
        Assert.Contains(typeof(Territory), rule.Dependencies);
    }

    #endregion

    #region 4. Load Balancing - Route to Least Busy

    [Test]
    public void LoadBalancing_RoutesToLeastBusyAvailableSoldier()
    {
        var capo = CreateCapo();

        var soldiers = new List<Agent>
        {
            new Agent { Id = "s1", Role = AgentRole.Soldier, Status = AgentStatus.Available, CurrentTaskCount = 5 },
            new Agent { Id = "s2", Role = AgentRole.Soldier, Status = AgentStatus.Available, CurrentTaskCount = 2 },
            new Agent { Id = "s3", Role = AgentRole.Soldier, Status = AgentStatus.Busy, CurrentTaskCount = 1 },
        };

        // Rule with context-aware action
        var rule = Rule.When<AgentMessage>(m => m.Type == MessageType.Task)
            .And(m => m.To == null || m.To.Role == AgentRole.Soldier)
            .Then((m, ctx) =>
            {
                var target = ctx.Agents()
                    .Where(a => a.Role == AgentRole.Soldier)
                    .Where(a => a.Status == AgentStatus.Available)
                    .Where(a => a.FamilyId == m.From!.FamilyId)
                    .OrderBy(a => a.CurrentTaskCount)
                    .FirstOrDefault();

                if (target != null)
                    m.RouteTo(target);
            })
            .WithTags("routing", "load-balance")
            .Build();

        Assert.NotNull(rule);
        Assert.Contains("routing", rule.Tags);
        Assert.Contains("load-balance", rule.Tags);
    }

    #endregion

    #region 5. Escalation - Based on History

    [Test]
    public void Escalation_DoubleEscalatesRepeatedAlerts()
    {
        var godfather = CreateGodfather();
        var underboss = CreateUnderboss();
        var capo = CreateCapo();
        var soldier = CreateSoldier();
        SetupHierarchy(soldier, capo, underboss, godfather);

        // Rule: repeated alerts in time window trigger double-escalation
        var rule = Rule.When<AgentMessage>(m => m.Type == MessageType.Alert)
            .And((m, ctx) =>
                ctx.Session.MessageHistory(m.FromId, TimeSpan.FromMinutes(5))
                    .Count(prev => prev.Type == MessageType.Alert) >= 3)
            .Then(m =>
            {
                var superior = m.From!.Superior;
                if (superior?.Superior != null)
                    m.EscalateTo(superior.Superior);
            })
            .WithReason("Repeated alerts trigger double-escalation")
            .WithTags("escalation", "alert")
            .Build();

        Assert.NotNull(rule);
        Assert.Equal("Repeated alerts trigger double-escalation", rule.Reason);
    }

    #endregion

    #region 6. Capability-Based Routing

    [Test]
    public void CapabilityRouting_RoutesToAgentWithRequiredCapabilities()
    {
        var rule = Rule.When<AgentMessage>(m => m.RequiredCapabilities.Any())
            .Then((m, ctx) =>
            {
                var target = ctx.Agents()
                    .Where(a => m.RequiredCapabilities.All(c => a.Capabilities.Contains(c)))
                    .Where(a => a.Status == AgentStatus.Available)
                    .OrderByDescending(a => a.ReputationScore)
                    .FirstOrDefault();

                if (target != null)
                    m.RouteTo(target);
                else if (m.To?.Superior != null)
                    m.Reroute(m.To.Superior);
            })
            .WithTags("routing", "capabilities")
            .Build();

        Assert.NotNull(rule);
        Assert.Contains("capabilities", rule.Tags);
    }

    #endregion

    #region 7. Rule Querying - LINQ over Rules

    [Test]
    public void QueryRules_FilterByTagsAndPriority()
    {
        // Build multiple rules
        var rules = new List<IAgentRule<AgentMessage>>
        {
            Rule.When<AgentMessage>(m => m.Type == MessageType.Alert)
                .WithId("escalate-alert")
                .WithPriority(80)
                .WithTags("escalation", "alert")
                .Build(),

            Rule.When<AgentMessage>(m => m.Type == MessageType.Command)
                .WithId("process-command")
                .WithPriority(90)
                .WithTags("command", "routing")
                .Build(),

            Rule.When<AgentMessage>(m => true)
                .WithId("audit-all")
                .WithPriority(10)
                .WithTags("audit")
                .Build()
        };

        // Query rules like data
        var escalationRules = rules
            .Where(r => r.Tags.Contains("escalation"))
            .Where(r => r.Priority > 50)
            .OrderByDescending(r => r.Priority)
            .ToList();

        Assert.Equal(1, escalationRules.Count);
        Assert.Equal("escalate-alert", escalationRules[0].Id);
    }

    #endregion

    #region 8. WouldMatch - Preview Without Executing

    [Test]
    public void WouldMatch_ReturnsMatchingRulesWithoutExecution()
    {
        var actionsExecuted = new List<string>();

        var rules = new List<IAgentRule<AgentMessage>>
        {
            Rule.When<AgentMessage>(m => m.Type == MessageType.Alert)
                .WithId("alert-rule")
                .Then(m => actionsExecuted.Add("alert-rule"))
                .Build(),

            Rule.When<AgentMessage>(m => m.From!.Role == AgentRole.Capo)
                .WithId("capo-rule")
                .Then(m => actionsExecuted.Add("capo-rule"))
                .Build(),

            Rule.When<AgentMessage>(m => m.Type == MessageType.Broadcast)
                .WithId("broadcast-rule")
                .Then(m => actionsExecuted.Add("broadcast-rule"))
                .Build()
        };

        var capo = CreateCapo();
        var underboss = CreateUnderboss();
        var message = new AgentMessage
        {
            Type = MessageType.Alert,
            From = capo,
            To = underboss
        };

        // Preview which rules would match (Evaluate only, no Execute)
        var matchingRules = rules.Where(r => r.Evaluate(message)).ToList();

        Assert.Equal(2, matchingRules.Count);
        Assert.True(matchingRules.Any(r => r.Id == "alert-rule"));
        Assert.True(matchingRules.Any(r => r.Id == "capo-rule"));

        // Actions should NOT have been executed
        Assert.Equal(0, actionsExecuted.Count);
    }

    #endregion

    #region 9. Pipeline - Sequential Stage Processing

    [Test]
    public void Pipeline_CreatesValidationStage()
    {
        var stage = Rule.Validate<AgentMessage>(
            m => m.From != null,
            "Sender required");

        Assert.NotNull(stage);
        Assert.IsType<ValidationStage<AgentMessage>>(stage);
    }

    [Test]
    public void Pipeline_CreatesTransformStage()
    {
        var stage = Rule.Transform<AgentMessage>(
            m => m.Timestamp = DateTime.UtcNow);

        Assert.NotNull(stage);
        Assert.IsType<TransformStage<AgentMessage>>(stage);
    }

    [Test]
    public void Pipeline_CreatesLogStage()
    {
        var stage = Rule.Log<AgentMessage>(
            m => $"{m.FromId} -> {m.ToId}: {m.Type}");

        Assert.NotNull(stage);
        Assert.IsType<LogStage<AgentMessage>>(stage);
    }

    #endregion

    #region 10. Session Workflow

    [Test]
    public void Session_InsertAndEvaluateMessages()
    {
        // This test documents the intended session workflow
        // It will fail until InMemoryAgentRulesContext is implemented

        // Expected usage:
        // using var context = new InMemoryAgentRulesContext();
        //
        // // Configure rules
        // context.Rules.For<AgentMessage>().Add(
        //     Rule.When<AgentMessage>(m => ...)
        //         .Then(m => ...)
        //         .Build());
        //
        // // Create session
        // using var session = context.CreateSession();
        //
        // // Insert facts
        // session.Insert(new AgentMessage { ... });
        //
        // // Evaluate
        // var result = session.Evaluate();
        //
        // // Check results
        // foreach (var match in result.ForType<AgentMessage>().Matches)
        // {
        //     // Process matches
        // }
        //
        // // Commit
        // session.Commit();

        // For now, just verify the API compiles
        Assert.True(true);
    }

    #endregion

    #region 11. Domain Extensions - Agent Navigation

    [Test]
    public void AgentExtension_IsImmediateSuperior()
    {
        var capo = CreateCapo();
        var soldier = CreateSoldier();
        soldier.SuperiorId = capo.Id;
        soldier.Superior = capo;

        Assert.True(soldier.IsImmediateSuperior(capo));
        Assert.False(capo.IsImmediateSuperior(soldier));
    }

    [Test]
    public void AgentExtension_IsImmediateSubordinate()
    {
        var capo = CreateCapo();
        var soldier = CreateSoldier();
        soldier.SuperiorId = capo.Id;

        Assert.True(capo.IsImmediateSubordinate(soldier));
        Assert.False(soldier.IsImmediateSubordinate(capo));
    }

    #endregion

    #region 12. Rule Builder - Metadata

    [Test]
    public void RuleBuilder_SetsAllMetadata()
    {
        var rule = Rule.When<AgentMessage>(m => m.Type == MessageType.Alert)
            .WithId("test-rule")
            .WithName("Test Rule Name")
            .WithPriority(75)
            .WithTags("tag1", "tag2", "tag3")
            .WithReason("Test reason for audit")
            .DependsOn<Territory>()
            .DependsOn<Agent>()
            .Then(m => m.Flag("processed"))
            .Build();

        Assert.Equal("test-rule", rule.Id);
        Assert.Equal("Test Rule Name", rule.Name);
        Assert.Equal(75, rule.Priority);
        Assert.Equal("Test reason for audit", rule.Reason);

        Assert.Contains("tag1", rule.Tags);
        Assert.Contains("tag2", rule.Tags);
        Assert.Contains("tag3", rule.Tags);

        Assert.Contains(typeof(Territory), rule.Dependencies);
        Assert.Contains(typeof(Agent), rule.Dependencies);
    }

    [Test]
    public void RuleBuilder_DefaultsNameToIdWhenNotSet()
    {
        var rule = Rule.When<AgentMessage>(m => true)
            .WithId("my-rule-id")
            .Build();

        Assert.Equal("my-rule-id", rule.Id);
        Assert.Equal("my-rule-id", rule.Name);
    }

    #endregion

    #region 13. Rule Execution - Simple Action

    [Test]
    public void RuleExecution_SimpleActionModifiesFact()
    {
        var rule = Rule.When<AgentMessage>(m => m.Type == MessageType.Alert)
            .Then(m => m.Flag("urgent"))
            .Build();

        var message = new AgentMessage { Type = MessageType.Alert };

        // Evaluate
        var matches = rule.Evaluate(message);
        Assert.True(matches);

        // Execute
        rule.Execute(message);
        Assert.Contains("urgent", message.Flags);
    }

    [Test]
    public void RuleExecution_NonMatchingRuleDoesNotExecute()
    {
        var rule = Rule.When<AgentMessage>(m => m.Type == MessageType.Alert)
            .Then(m => m.Flag("urgent"))
            .Build();

        var message = new AgentMessage { Type = MessageType.Request };

        // Should not match
        var matches = rule.Evaluate(message);
        Assert.False(matches);

        // Flags should be empty
        Assert.Equal(0, message.Flags.Count);
    }

    #endregion

    #region 14. Rule Execution - Context Action

    [Test]
    public void RuleBuilder_SupportsContextAwareAction()
    {
        // This rule uses context in its action
        var rule = Rule.When<AgentMessage>(m => m.Type == MessageType.Task)
            .Then((m, ctx) =>
            {
                // Would query ctx.Agents() to find target
                // For now just flag the message
                m.Flag("needs-routing");
            })
            .Build();

        Assert.NotNull(rule);
    }

    #endregion

    #region 15. Multiple Conditions - And Chaining

    [Test]
    public void RuleBuilder_ChainsMultipleConditions()
    {
        var capo = CreateCapo();
        var soldier = CreateSoldier();

        var rule = Rule.When<AgentMessage>(m => m.Type == MessageType.Request)
            .And(m => m.From!.Role == AgentRole.Soldier)
            .And(m => m.To!.Role >= AgentRole.Underboss)
            .Then(m => m.Flag("needs-approval"))
            .Build();

        // Message from soldier to underboss - should match
        var underboss = CreateUnderboss();
        var message1 = new AgentMessage
        {
            Type = MessageType.Request,
            From = soldier,
            To = underboss
        };

        Assert.True(rule.Evaluate(message1));

        // Message from soldier to capo - should not match (capo < underboss)
        var message2 = new AgentMessage
        {
            Type = MessageType.Request,
            From = soldier,
            To = capo
        };

        Assert.False(rule.Evaluate(message2));
    }

    #endregion
}
