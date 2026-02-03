// Story System - Memory Relevance Rules
// Rules for scoring memory relevance to questions

using RulesEngine.Core;

namespace AgentRouting.MafiaDemo.Story;

/// <summary>
/// Context for scoring memory relevance to a question.
/// </summary>
public class MemoryRelevanceContext
{
    public Memory Memory { get; set; } = null!;
    public AgentQuestion Question { get; set; } = null!;
    public int CurrentWeek { get; set; }

    // Memory properties
    public MemoryType MemoryType => Memory.Type;
    public int Salience => Memory.Salience;
    public int Confidence => Memory.Confidence;
    public bool IsFirsthand => Memory.IsFirsthand;
    public int WeeksSinceCreated => CurrentWeek - Memory.CreatedWeek;

    // Match checks
    public bool MatchesEntity => Memory.InvolvesEntityId == Question.SubjectEntityId;
    public bool MatchesLocation => Memory.LocationId == Question.SubjectLocationId;
    public bool MatchesTopic => !string.IsNullOrEmpty(Question.Topic) &&
        Memory.Summary.Contains(Question.Topic, StringComparison.OrdinalIgnoreCase);

    // Output
    public float RelevanceScore { get; set; }
}

/// <summary>
/// Rules for scoring how relevant a memory is to a question.
/// </summary>
public static class MemoryRelevanceRulesSetup
{
    public static RulesEngineCore<MemoryRelevanceContext> CreateEngine()
    {
        var engine = new RulesEngineCore<MemoryRelevanceContext>();

        // Base relevance from matching
        engine.AddRule(
            "RELEVANCE_ENTITY_MATCH",
            "Memory About Same Entity",
            ctx => ctx.MatchesEntity,
            ctx => ctx.RelevanceScore += 0.5f,
            priority: 100);

        engine.AddRule(
            "RELEVANCE_LOCATION_MATCH",
            "Memory About Same Location",
            ctx => ctx.MatchesLocation,
            ctx => ctx.RelevanceScore += 0.3f,
            priority: 100);

        engine.AddRule(
            "RELEVANCE_TOPIC_MATCH",
            "Memory Mentions Topic",
            ctx => ctx.MatchesTopic,
            ctx => ctx.RelevanceScore += 0.4f,
            priority: 100);

        // Quality modifiers
        engine.AddRule(
            "RELEVANCE_FIRSTHAND_BONUS",
            "Firsthand Memory More Relevant",
            ctx => ctx.IsFirsthand && ctx.RelevanceScore > 0,
            ctx => ctx.RelevanceScore *= 1.2f,
            priority: 90);

        engine.AddRule(
            "RELEVANCE_HIGH_CONFIDENCE_BONUS",
            "High Confidence Memory",
            ctx => ctx.Confidence > 80 && ctx.RelevanceScore > 0,
            ctx => ctx.RelevanceScore *= 1.1f,
            priority: 90);

        engine.AddRule(
            "RELEVANCE_RECENCY_BONUS",
            "Recent Memory More Relevant",
            ctx => ctx.WeeksSinceCreated < 4 && ctx.RelevanceScore > 0,
            ctx => ctx.RelevanceScore *= 1.15f,
            priority: 90);

        engine.AddRule(
            "RELEVANCE_STALE_PENALTY",
            "Old Memory Less Relevant",
            ctx => ctx.WeeksSinceCreated > 20 && ctx.RelevanceScore > 0,
            ctx => ctx.RelevanceScore *= 0.7f,
            priority: 85);

        // Type-specific relevance
        engine.AddRule(
            "RELEVANCE_SECRET_FOR_INFO_QUESTION",
            "Secrets Highly Relevant to Info Questions",
            ctx => ctx.MemoryType == MemoryType.Secret &&
                   ctx.Question.Type == QuestionType.WhatDoYouKnow,
            ctx => ctx.RelevanceScore += 0.3f,
            priority: 80);

        engine.AddRule(
            "RELEVANCE_INTERACTION_FOR_RELATIONSHIP",
            "Interactions Relevant to Relationship Questions",
            ctx => ctx.MemoryType == MemoryType.Interaction &&
                   ctx.Question.Type == QuestionType.HowIsRelationship,
            ctx => ctx.RelevanceScore += 0.25f,
            priority: 80);

        return engine;
    }
}
