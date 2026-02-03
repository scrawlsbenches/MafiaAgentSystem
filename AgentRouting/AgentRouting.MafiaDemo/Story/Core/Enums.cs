// Story System Core Enums
// Extracted from StorySystemDesign.cs

namespace AgentRouting.MafiaDemo.Story;

/// <summary>
/// Location states affect what missions are available there
/// </summary>
public enum LocationState
{
    Neutral,      // Default - any mission type available
    Friendly,     // We control it - collection missions easy, lower risk
    Hostile,      // Enemy territory - high risk, special missions only
    Contested,    // Active conflict - war missions available
    Compromised,  // Under surveillance - high heat, avoid or bribe first
    Destroyed     // No longer usable - removed from mission pool
}

/// <summary>
/// NPC status determines how they can be interacted with
/// </summary>
public enum NPCStatus
{
    Active,       // Normal - available for any mission type
    Intimidated,  // Scared - won't resist, but relationship damaged
    Allied,       // On our side - provides intel, discounts, opportunities
    Hostile,      // Against us - may trigger revenge missions
    Bribed,       // Paid off - temporary alliance, may betray
    Dead,         // Eliminated - no further interaction, may have avengers
    Fled,         // Left the area - may return later with grudge
    Imprisoned,   // In jail - can potentially be freed or silenced
    Informant     // Working with Feds - high priority threat
}

/// <summary>
/// Intel reliability affects how much we trust the information
/// </summary>
public enum IntelReliability
{
    Rumor = 25,       // Street talk - might be wrong
    Observed = 50,    // Agent saw something - probably right
    Confirmed = 75,   // Multiple sources - reliable
    Absolute = 100    // Direct knowledge - certain
}

/// <summary>
/// Plot thread states for multi-mission story arcs
/// </summary>
public enum PlotState
{
    Dormant,      // Not yet triggered - waiting for activation condition
    Available,    // Can be started - player can engage
    Active,       // In progress - generates priority missions
    Completed,    // Successfully finished - rewards given
    Failed,       // Botched - negative consequences applied
    Abandoned     // Player ignored too long - opportunity lost
}

/// <summary>
/// Edge types in the story graph define causal relationships
/// </summary>
public enum StoryEdgeType
{
    Unlocks,      // Completing A makes B available
    Blocks,       // A being active prevents B
    Triggers,     // A automatically starts B
    Requires,     // B needs A completed first (prerequisite)
    Conflicts,    // A and B are mutually exclusive paths
    Chains        // A leads directly to B (same plot thread)
}

/// <summary>
/// Types of story nodes in the narrative graph
/// </summary>
public enum StoryNodeType
{
    Mission,          // A mission the player can undertake
    Event,            // Something that happens in the world
    Consequence,      // Result of a previous action
    Opportunity,      // Time-limited chance
    Threat,           // Danger that must be addressed
    Milestone         // Story progression marker
}

/// <summary>
/// Types of story events for logging
/// </summary>
public enum StoryEventType
{
    PlotActivated,
    NodeUnlocked,
    NodeCompleted,
    NodeFailed,
    NodeExpired,
    NPCStatusChanged,
    LocationStateChanged,
    IntelReceived,
    ConsequenceApplied
}

/// <summary>
/// Types of intel that can be gathered
/// </summary>
public enum IntelType
{
    // Location intel
    LocationHeat,         // Police activity at location
    LocationOwnership,    // Who controls the location
    LocationOpportunity,  // Business opportunity there

    // NPC intel
    NPCLocation,          // Where an NPC is
    NPCStatus,            // NPC's current state
    NPCIntention,         // What NPC is planning
    NPCVulnerability,     // NPC weakness to exploit

    // Faction intel
    FactionMovement,      // Faction expanding/contracting
    FactionStrength,      // Faction resource level
    FactionPlans,         // What faction is planning

    // Threat intel
    ThreatWarning,        // Danger coming
    FedActivity,          // Federal investigation
    RivalPlot             // Enemy planning attack
}

/// <summary>
/// Communication styles for personas
/// </summary>
public enum CommunicationStyle
{
    Neutral,          // Default balanced style
    Formal,           // Professional, respectful
    Casual,           // Relaxed, friendly
    Threatening,      // Intimidating, aggressive
    Diplomatic,       // Careful, measured
    Cryptic,          // Indirect, mysterious
    Blunt             // Direct, no nonsense
}

/// <summary>
/// Types of goals personas can have
/// </summary>
public enum GoalType
{
    Survive,          // Stay alive
    Prosper,          // Gain wealth
    Power,            // Gain influence/rank
    Revenge,          // Hurt specific target
    Protect,          // Keep someone safe
    Escape,           // Leave current situation
    Loyalty,          // Serve a master
    Justice,          // Right a wrong
    Knowledge         // Learn something
}

/// <summary>
/// Types of memories
/// </summary>
public enum MemoryType
{
    // Episodic - specific events witnessed or experienced
    Interaction,      // Met/talked with someone
    Event,            // Something happened
    Mission,          // Completed a task
    Betrayal,         // Someone betrayed us
    Gift,             // Received something valuable
    Threat,           // Were threatened
    Violence,         // Witnessed/experienced violence

    // Semantic - facts and knowledge
    Fact,             // Learned information
    Location,         // Know about a place
    Person,           // Know about someone
    Relationship,     // Know about a relationship
    Secret,           // Confidential information

    // Procedural - how to do things
    Skill,            // Learned ability
    Contact,          // Know how to reach someone
    Route             // Know how to get somewhere
}

/// <summary>
/// Emotional coloring of memories
/// </summary>
public enum EmotionalValence
{
    VeryNegative = -2,
    Negative = -1,
    Neutral = 0,
    Positive = 1,
    VeryPositive = 2
}

/// <summary>
/// Types of questions agents can ask
/// </summary>
public enum QuestionType
{
    // Information seeking
    WhatDoYouKnow,        // "What do you know about [entity]?"
    WhereIs,              // "Where is [entity]?"
    WhoControls,          // "Who controls [location]?"
    WhatHappened,         // "What happened at [location]?"
    HowIsRelationship,    // "How are things with [entity]?"

    // Opinion seeking
    WhatDoYouThink,       // "What do you think about [entity]?"
    ShouldWe,             // "Should we [action]?"
    CanWeTrust,           // "Can we trust [entity]?"

    // Action requests
    WillYouHelp,          // "Will you help with [action]?"
    CanYouFind,           // "Can you find out about [topic]?"
    WillYouTell,          // "Will you tell [entity] about [topic]?"

    // Loyalty tests
    WhereDoYouStand,      // "Where do your loyalties lie?"
    WouldYouBetray,       // Testing if they'd betray someone
    WhatWouldYouDo        // Hypothetical situation test
}

/// <summary>
/// Urgency levels for questions
/// </summary>
public enum QuestionUrgency
{
    Low,                  // Casual inquiry
    Normal,               // Standard question
    High,                 // Important, needs quick answer
    Critical              // Urgent, immediate response needed
}

/// <summary>
/// Types of responses to questions
/// </summary>
public enum ResponseType
{
    Answer,               // Direct answer to the question
    Partial,              // Incomplete information
    Redirect,             // "Ask someone else"
    Refuse,               // Won't answer
    Lie,                  // Deliberate misinformation
    Counter,              // Answers with a question
    Bargain               // "I'll tell you if..."
}

/// <summary>
/// Sources of mission generation
/// </summary>
public enum MissionSource
{
    StoryGraph,   // From story node
    PlotThread,   // From active plot
    WorldState,   // From world conditions
    Intel,        // From gathered intelligence
    Generated     // Fallback generation
}
