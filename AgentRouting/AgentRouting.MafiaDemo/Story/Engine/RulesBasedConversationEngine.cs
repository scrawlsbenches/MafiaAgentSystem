// Story System - Rules-Based Conversation Engine
// Conversation engine that uses RulesEngine for all decisions

using RulesEngine.Core;

namespace AgentRouting.MafiaDemo.Story;

/// <summary>
/// Conversation engine that uses RulesEngine for all decisions.
/// This replaces the hardcoded if/else logic with configurable rules.
/// </summary>
public class RulesBasedConversationEngine
{
    private readonly WorldState _world;
    private readonly StoryGraph _graph;
    private readonly RulesEngineCore<ConversationContext> _conversationRules;
    private readonly RulesEngineCore<MemoryRelevanceContext> _relevanceRules;
    private readonly RulesEngineCore<StoryTriggerContext> _triggerRules;
    private readonly RulesEngineCore<EvolutionContext> _evolutionRules;

    public RulesBasedConversationEngine(WorldState world, StoryGraph graph)
    {
        _world = world;
        _graph = graph;
        _conversationRules = ConversationRulesSetup.CreateEngine();
        _relevanceRules = MemoryRelevanceRulesSetup.CreateEngine();
        _triggerRules = StoryTriggerRulesSetup.CreateEngine();
        _evolutionRules = EvolutionRulesSetup.CreateEngine();
    }

    /// <summary>
    /// Process a question using rules for all decisions.
    /// </summary>
    public AgentResponse ProcessQuestion(
        AgentQuestion question,
        EntityMind responderMind,
        int relationshipWithAsker)
    {
        // 1. Gather relevant memories using rules-based scoring
        var relevantMemories = FindRelevantMemories(question, responderMind.Memories);

        // 2. Build conversation context
        var context = new ConversationContext
        {
            Question = question,
            Persona = responderMind.Persona,
            Memories = responderMind.Memories,
            RelationshipWithAsker = relationshipWithAsker,
            RelevantMemories = relevantMemories,
            LoyaltyToSubject = question.SubjectEntityId != null
                ? responderMind.Persona.FactionBiases.GetValueOrDefault(question.SubjectEntityId)
                : 0
        };

        // 3. Evaluate conversation rules to make decision
        _conversationRules.EvaluateAll(context);

        // 4. Generate response based on decision
        var response = GenerateResponse(context, responderMind.Persona);

        // 5. Check for story triggers
        var triggerContext = new StoryTriggerContext
        {
            Question = question,
            Response = response,
            World = _world,
            Graph = _graph
        };
        _triggerRules.EvaluateAll(triggerContext);
        response.TriggeredEvents.AddRange(triggerContext.TriggeredEvents);
        response.UnlockedNodeIds.AddRange(triggerContext.UnlockedNodes);

        // 6. Record the interaction as a memory for both parties
        var emotionalValence = response.IsHonest ? EmotionalValence.Positive : EmotionalValence.Negative;
        responderMind.RecordInteraction(
            question.AskerId,
            $"Answered question about {question.Topic}",
            emotionalValence,
            question.Urgency == QuestionUrgency.Critical ? 70 : 40,
            _world.CurrentWeek);

        return response;
    }

    private List<Memory> FindRelevantMemories(AgentQuestion question, MemoryBank memories)
    {
        var candidates = new List<(Memory memory, float score)>();

        // Score all memories using rules
        foreach (var memory in GetCandidateMemories(question, memories))
        {
            var context = new MemoryRelevanceContext
            {
                Memory = memory,
                Question = question,
                CurrentWeek = _world.CurrentWeek
            };

            _relevanceRules.EvaluateAll(context);

            if (context.RelevanceScore > 0.2f)  // Minimum threshold
            {
                candidates.Add((memory, context.RelevanceScore));
            }
        }

        // Return top 5 by relevance score
        return candidates
            .OrderByDescending(c => c.score)
            .Take(5)
            .Select(c => c.memory)
            .ToList();
    }

    private IEnumerable<Memory> GetCandidateMemories(AgentQuestion question, MemoryBank memories)
    {
        // Gather candidates from different indexes
        var candidates = new HashSet<Memory>();

        if (question.SubjectEntityId != null)
        {
            foreach (var m in memories.RecallAbout(question.SubjectEntityId, _world.CurrentWeek, 10))
                candidates.Add(m);
        }

        if (question.SubjectLocationId != null)
        {
            foreach (var m in memories.RecallAtLocation(question.SubjectLocationId, _world.CurrentWeek, 10))
                candidates.Add(m);
        }

        if (!string.IsNullOrEmpty(question.Topic))
        {
            foreach (var m in memories.Search(question.Topic, _world.CurrentWeek, 10))
                candidates.Add(m);
        }

        return candidates;
    }

    private AgentResponse GenerateResponse(ConversationContext context, Persona persona)
    {
        var response = new AgentResponse
        {
            QuestionId = context.Question.Id,
            ResponderId = context.Question.ResponderId,
            RelationshipChange = context.Decision.RelationshipModifier
        };

        // Check for forced response type from rules
        if (context.Decision.ForcedResponseType.HasValue)
        {
            response.Type = context.Decision.ForcedResponseType.Value;
            response.Content = context.Decision.CustomResponse ?? "";
            return response;
        }

        // Handle refusal
        if (!context.Decision.WillAnswer)
        {
            response.Type = ResponseType.Refuse;
            response.RefusedToAnswer = true;
            response.RefusalReason = context.Decision.RefusalReason;
            response.Content = FormatRefusal(persona, context.Decision.RefusalReason);
            return response;
        }

        // Handle lie
        if (context.Decision.WillLie)
        {
            response.Type = ResponseType.Lie;
            response.IsHonest = false;
            response.Content = GenerateMisinformation(context.Question, persona);
            response.ConfidenceLevel = 80;
            return response;
        }

        // Handle bargain
        if (context.Decision.WillBargain)
        {
            response.Type = ResponseType.Bargain;
            response.Content = context.Decision.CustomResponse ??
                "That information has a price. What's it worth to you?";
            return response;
        }

        // Handle honest answer
        if (context.HasRelevantMemories)
        {
            response.Type = ResponseType.Answer;
            response.IsHonest = true;
            response.SharedMemories = context.RelevantMemories;
            response.ConfidenceLevel = context.BestMemoryConfidence;
            response.Content = FormatHonestAnswer(context, persona);
        }
        else
        {
            response.Type = ResponseType.Partial;
            response.IsHonest = true;
            response.ConfidenceLevel = 0;
            response.Content = FormatUnknown(persona);
        }

        return response;
    }

    private string FormatRefusal(Persona persona, string? reason)
    {
        return persona.Style switch
        {
            CommunicationStyle.Threatening => "You don't want to keep asking questions like that.",
            CommunicationStyle.Formal => "I must decline to discuss this matter.",
            CommunicationStyle.Blunt => "No.",
            CommunicationStyle.Cryptic => "Not all questions deserve answers.",
            _ => reason ?? "I'd rather not say."
        };
    }

    private string FormatHonestAnswer(ConversationContext context, Persona persona)
    {
        var memory = context.RelevantMemories.FirstOrDefault();
        if (memory == null) return "I'm not certain.";

        var prefix = persona.Style switch
        {
            CommunicationStyle.Formal => "I can confirm that ",
            CommunicationStyle.Casual => "Yeah, so ",
            CommunicationStyle.Cryptic => "What I can tell you is ",
            CommunicationStyle.Blunt => "",
            _ => ""
        };

        return prefix + memory.Summary;
    }

    private string FormatUnknown(Persona persona)
    {
        return persona.Style switch
        {
            CommunicationStyle.Blunt => "Don't know.",
            CommunicationStyle.Formal => "I regret that I have no information on this matter.",
            CommunicationStyle.Diplomatic => "That's not something I have insight into, I'm afraid.",
            CommunicationStyle.Cryptic => "Some things remain hidden, even from me.",
            _ => "I'm not sure about that."
        };
    }

    private string GenerateMisinformation(AgentQuestion question, Persona persona)
    {
        // Generate contextual lies based on the question subject
        var subjectName = GetSubjectName(question);

        return question.Type switch
        {
            QuestionType.WhereIs => GenerateLocationLie(subjectName, persona),
            QuestionType.WhoControls => GenerateControlLie(question.SubjectLocationId, persona),
            QuestionType.WhatHappened => GenerateEventLie(subjectName, persona),
            QuestionType.CanWeTrust => GenerateTrustLie(subjectName, persona),
            QuestionType.WhatDoYouKnow => GenerateKnowledgeLie(subjectName, persona),
            QuestionType.HowIsRelationship => $"Things are fine with {subjectName ?? "them"}. No problems.",
            _ => "I've heard differently, but I can't say more."
        };
    }

    private string? GetSubjectName(AgentQuestion question)
    {
        if (question.SubjectEntityId != null)
        {
            var npc = _world.GetNPC(question.SubjectEntityId);
            return npc?.Name ?? question.SubjectEntityId;
        }
        if (question.SubjectLocationId != null)
        {
            var loc = _world.GetLocation(question.SubjectLocationId);
            return loc?.Name ?? question.SubjectLocationId;
        }
        return !string.IsNullOrEmpty(question.Topic) ? question.Topic : null;
    }

    private string GenerateLocationLie(string? subjectName, Persona persona)
    {
        // Pick a random misleading location
        var locations = _world.Locations.Values.ToList();
        if (locations.Count > 0)
        {
            var fakeLoc = locations[Random.Shared.Next(locations.Count)];
            return persona.Style switch
            {
                CommunicationStyle.Casual => $"Pretty sure {subjectName ?? "they"}'re over at {fakeLoc.Name}.",
                CommunicationStyle.Formal => $"My understanding is that {subjectName ?? "the individual"} was last seen near {fakeLoc.Name}.",
                CommunicationStyle.Cryptic => $"The winds blow toward {fakeLoc.Name}...",
                _ => $"Last I heard, {subjectName ?? "they"} were at {fakeLoc.Name}."
            };
        }
        return $"Last I heard, {subjectName ?? "they"} were in Brooklyn.";
    }

    private string GenerateControlLie(string? locationId, Persona persona)
    {
        // Attribute control to a random faction
        var factions = _world.Factions.Values.ToList();
        if (factions.Count > 0)
        {
            var fakeFaction = factions[Random.Shared.Next(factions.Count)];
            var locName = locationId != null ? _world.GetLocation(locationId)?.Name ?? "that area" : "that territory";
            return persona.Style switch
            {
                CommunicationStyle.Casual => $"The {fakeFaction.Name} run {locName} now, far as I know.",
                CommunicationStyle.Formal => $"I believe {locName} is under the jurisdiction of the {fakeFaction.Name}.",
                _ => $"The {fakeFaction.Name} have {locName} now."
            };
        }
        return "The Barzinis have that territory now.";
    }

    private string GenerateEventLie(string? subjectName, Persona persona)
    {
        return persona.Style switch
        {
            CommunicationStyle.Casual => $"Nah, nothing going on with {subjectName ?? "that"}. Quiet as always.",
            CommunicationStyle.Formal => $"I have no reports of anything unusual regarding {subjectName ?? "the matter"}.",
            CommunicationStyle.Cryptic => "The calm before the storm... or perhaps just calm.",
            _ => $"Nothing unusual with {subjectName ?? "that"}, far as I know."
        };
    }

    private string GenerateTrustLie(string? subjectName, Persona persona)
    {
        return persona.Style switch
        {
            CommunicationStyle.Casual => $"Oh yeah, {subjectName ?? "they"}'re solid. Good people.",
            CommunicationStyle.Formal => $"I would vouch for {subjectName ?? "them"} without reservation.",
            CommunicationStyle.Threatening => $"You questioning {subjectName ?? "them"}? They're fine. Trust me.",
            _ => $"Absolutely, {subjectName ?? "they"}'re completely reliable."
        };
    }

    private string GenerateKnowledgeLie(string? subjectName, Persona persona)
    {
        return persona.Style switch
        {
            CommunicationStyle.Casual => $"Don't know much about {subjectName ?? "that"}. Pretty clean.",
            CommunicationStyle.Cryptic => $"What is there to know about {subjectName ?? "shadows"}?",
            _ => $"Not much to tell about {subjectName ?? "that"}. Nothing interesting."
        };
    }

    /// <summary>
    /// Apply experience to evolve a character's persona using rules.
    /// </summary>
    public void EvolvePersona(EntityMind mind, string experienceType, int intensity, string? involvesEntityId = null)
    {
        var context = new EvolutionContext
        {
            Persona = mind.Persona,
            ExperienceType = experienceType,
            Intensity = intensity,
            InvolvesEntityId = involvesEntityId
        };

        _evolutionRules.EvaluateAll(context);

        // Apply trait changes using direct property access (no reflection)
        foreach (var (trait, change) in context.TraitChanges)
        {
            ApplyTraitChange(mind.Persona, trait, change);
        }

        // Add new goals/fears
        if (context.NewGoal != null)
        {
            mind.Persona.Goals.Add(new Goal
            {
                Id = context.NewGoal,
                Type = context.NewGoal.StartsWith("revenge") ? GoalType.Revenge : GoalType.Survive,
                Priority = intensity,
                TargetId = involvesEntityId
            });
        }

        if (context.NewFear != null && !mind.Persona.Fears.Contains(context.NewFear))
        {
            mind.Persona.Fears.Add(context.NewFear);
        }
    }

    /// <summary>
    /// Apply a trait change without reflection. Uses switch for O(1) dispatch.
    /// </summary>
    private static void ApplyTraitChange(Persona persona, string trait, int change)
    {
        switch (trait)
        {
            case "Ambition":
                persona.Ambition = Math.Clamp(persona.Ambition + change, 0, 100);
                break;
            case "Caution":
                persona.Caution = Math.Clamp(persona.Caution + change, 0, 100);
                break;
            case "Aggression":
                persona.Aggression = Math.Clamp(persona.Aggression + change, 0, 100);
                break;
            case "Loyalty":
                persona.Loyalty = Math.Clamp(persona.Loyalty + change, 0, 100);
                break;
            case "Trust":
                persona.Trust = Math.Clamp(persona.Trust + change, 0, 100);
                break;
            case "Empathy":
                persona.Empathy = Math.Clamp(persona.Empathy + change, 0, 100);
                break;
            case "Cunning":
                persona.Cunning = Math.Clamp(persona.Cunning + change, 0, 100);
                break;
            case "Patience":
                persona.Patience = Math.Clamp(persona.Patience + change, 0, 100);
                break;
            case "Pride":
                persona.Pride = Math.Clamp(persona.Pride + change, 0, 100);
                break;
            case "Honesty":
                persona.Honesty = Math.Clamp(persona.Honesty + change, 0, 100);
                break;
            case "Verbosity":
                persona.Verbosity = Math.Clamp(persona.Verbosity + change, 0, 100);
                break;
            // Unknown traits are silently ignored (safe default)
        }
    }
}
