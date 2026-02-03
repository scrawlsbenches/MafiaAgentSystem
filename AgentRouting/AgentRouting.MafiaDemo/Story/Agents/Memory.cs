// Story System - Memory and MemoryBank
// Memory system for agents and NPCs

namespace AgentRouting.MafiaDemo.Story;

/// <summary>
/// A single memory representing something an agent/NPC knows or experienced.
///
/// DESIGN DECISION: Memories are typed and have salience (importance).
/// High-salience memories persist longer and influence behavior more.
/// This mimics how humans remember significant events better.
/// </summary>
public class Memory
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public MemoryType Type { get; set; }

    // Content
    public string Summary { get; set; } = "";           // Human-readable summary
    public Dictionary<string, object> Data { get; set; } = new();

    // Context
    public int CreatedWeek { get; set; }
    public string? LocationId { get; set; }
    public string? InvolvesEntityId { get; set; }       // Who/what is this about
    public string? SourceAgentId { get; set; }          // Who told us (if learned)

    // Importance and decay
    public int Salience { get; set; } = 50;             // 0-100, how important
    public int AccessCount { get; set; }                // Times recalled
    public int? LastAccessedWeek { get; set; }

    // Emotional coloring
    public EmotionalValence Emotion { get; set; } = EmotionalValence.Neutral;
    public int EmotionalIntensity { get; set; } = 50;   // 0-100

    // Reliability
    public bool IsFirsthand { get; set; }               // Did we witness it?
    public int Confidence { get; set; } = 100;          // How sure are we?

    /// <summary>
    /// Calculate effective salience considering recency and access.
    /// </summary>
    public float GetEffectiveSalience(int currentWeek)
    {
        float base_salience = Salience / 100f;

        // Recency bonus (recent memories more accessible)
        int weeksSince = currentWeek - CreatedWeek;
        float recencyFactor = 1f / (1f + weeksSince * 0.1f);

        // Access bonus (frequently recalled memories stronger)
        float accessFactor = 1f + Math.Min(AccessCount * 0.1f, 0.5f);

        // Emotional memories persist better
        float emotionFactor = 1f + (EmotionalIntensity / 200f);

        return base_salience * recencyFactor * accessFactor * emotionFactor;
    }

    /// <summary>
    /// Should this memory be forgotten (pruned)?
    /// </summary>
    public bool ShouldForget(int currentWeek)
    {
        // High salience memories never forgotten
        if (Salience > Thresholds.HighSalience) return false;

        // Emotional memories persist longer
        if (EmotionalIntensity > Thresholds.TraitHigh) return false;

        // Calculate effective salience
        float effective = GetEffectiveSalience(currentWeek);

        // Forget if effective salience drops too low
        return effective < Thresholds.ForgetThreshold;
    }
}

/// <summary>
/// Manages memories for an agent or NPC.
/// Supports recall, learning, and natural forgetting.
///
/// ALGORITHM NOTES:
/// - Recall uses salience-weighted retrieval
/// - Learning strengthens related memories
/// - Forgetting uses threshold-based pruning
/// </summary>
public class MemoryBank
{
    private readonly List<Memory> _memories = new();
    private readonly Dictionary<string, List<Memory>> _byEntity = new();
    private readonly Dictionary<string, List<Memory>> _byLocation = new();
    private readonly Dictionary<MemoryType, List<Memory>> _byType = new();

    public int Capacity { get; set; } = 100;            // Max memories before forced pruning

    #region Learning (Adding Memories)

    public void Remember(Memory memory)
    {
        _memories.Add(memory);

        // Index by entity
        if (memory.InvolvesEntityId != null)
        {
            if (!_byEntity.ContainsKey(memory.InvolvesEntityId))
                _byEntity[memory.InvolvesEntityId] = new List<Memory>();
            _byEntity[memory.InvolvesEntityId].Add(memory);
        }

        // Index by location
        if (memory.LocationId != null)
        {
            if (!_byLocation.ContainsKey(memory.LocationId))
                _byLocation[memory.LocationId] = new List<Memory>();
            _byLocation[memory.LocationId].Add(memory);
        }

        // Index by type
        if (!_byType.ContainsKey(memory.Type))
            _byType[memory.Type] = new List<Memory>();
        _byType[memory.Type].Add(memory);

        // Prune if over capacity
        if (_memories.Count > Capacity)
            PruneLeastImportant(1);
    }

    /// <summary>
    /// Learn a fact from another agent (secondhand memory).
    /// </summary>
    public void LearnFrom(Memory sourceMemory, string sourceAgentId, int currentWeek)
    {
        var learned = new Memory
        {
            Type = sourceMemory.Type,
            Summary = sourceMemory.Summary,
            Data = new Dictionary<string, object>(sourceMemory.Data),
            CreatedWeek = currentWeek,
            LocationId = sourceMemory.LocationId,
            InvolvesEntityId = sourceMemory.InvolvesEntityId,
            SourceAgentId = sourceAgentId,
            Salience = sourceMemory.Salience / 2,       // Secondhand less salient
            IsFirsthand = false,
            Confidence = sourceMemory.Confidence - 20,  // Less confident in secondhand
            Emotion = EmotionalValence.Neutral          // Secondhand less emotional
        };

        Remember(learned);
    }

    #endregion

    #region Recall (Retrieving Memories)

    /// <summary>
    /// Recall memories about a specific entity, sorted by relevance.
    /// </summary>
    public IEnumerable<Memory> RecallAbout(string entityId, int currentWeek, int limit = 5)
    {
        if (!_byEntity.TryGetValue(entityId, out var memories))
            return Enumerable.Empty<Memory>();

        return memories
            .OrderByDescending(m => m.GetEffectiveSalience(currentWeek))
            .Take(limit)
            .Select(m => {
                m.AccessCount++;
                m.LastAccessedWeek = currentWeek;
                return m;
            });
    }

    /// <summary>
    /// Recall memories at a specific location.
    /// </summary>
    public IEnumerable<Memory> RecallAtLocation(string locationId, int currentWeek, int limit = 5)
    {
        if (!_byLocation.TryGetValue(locationId, out var memories))
            return Enumerable.Empty<Memory>();

        return memories
            .OrderByDescending(m => m.GetEffectiveSalience(currentWeek))
            .Take(limit)
            .Select(m => {
                m.AccessCount++;
                m.LastAccessedWeek = currentWeek;
                return m;
            });
    }

    /// <summary>
    /// Recall memories of a specific type.
    /// </summary>
    public IEnumerable<Memory> RecallByType(MemoryType type, int currentWeek, int limit = 5)
    {
        if (!_byType.TryGetValue(type, out var memories))
            return Enumerable.Empty<Memory>();

        return memories
            .OrderByDescending(m => m.GetEffectiveSalience(currentWeek))
            .Take(limit);
    }

    /// <summary>
    /// Search memories by keyword in summary.
    /// </summary>
    public IEnumerable<Memory> Search(string keyword, int currentWeek, int limit = 5)
    {
        return _memories
            .Where(m => m.Summary.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(m => m.GetEffectiveSalience(currentWeek))
            .Take(limit);
    }

    /// <summary>
    /// Get the most emotionally significant memories.
    /// </summary>
    public IEnumerable<Memory> RecallEmotional(int currentWeek, int limit = 5)
    {
        return _memories
            .Where(m => m.EmotionalIntensity > Thresholds.ModerateSalience)
            .OrderByDescending(m => m.EmotionalIntensity * m.GetEffectiveSalience(currentWeek))
            .Take(limit);
    }

    /// <summary>
    /// Check if we have any memory about an entity.
    /// </summary>
    public bool KnowsAbout(string entityId) => _byEntity.ContainsKey(entityId);

    /// <summary>
    /// Get overall sentiment toward an entity based on memories.
    /// </summary>
    public int GetSentiment(string entityId, int currentWeek)
    {
        if (!_byEntity.TryGetValue(entityId, out var memories))
            return 0;

        float totalSentiment = 0;
        float totalWeight = 0;

        foreach (var memory in memories)
        {
            float weight = memory.GetEffectiveSalience(currentWeek);
            float sentiment = (int)memory.Emotion * (memory.EmotionalIntensity / 50f);
            totalSentiment += sentiment * weight;
            totalWeight += weight;
        }

        return totalWeight > 0 ? (int)(totalSentiment / totalWeight * 25) : 0;
    }

    #endregion

    #region Forgetting (Pruning)

    /// <summary>
    /// Forget old, low-salience memories.
    /// </summary>
    public void Forget(int currentWeek)
    {
        var toForget = _memories.Where(m => m.ShouldForget(currentWeek)).ToList();
        foreach (var memory in toForget)
            RemoveMemory(memory);
    }

    private void PruneLeastImportant(int count)
    {
        var toPrune = _memories
            .OrderBy(m => m.Salience)
            .ThenBy(m => m.CreatedWeek)
            .Take(count)
            .ToList();

        foreach (var memory in toPrune)
            RemoveMemory(memory);
    }

    private void RemoveMemory(Memory memory)
    {
        _memories.Remove(memory);

        if (memory.InvolvesEntityId != null && _byEntity.TryGetValue(memory.InvolvesEntityId, out var byEntity))
            byEntity.Remove(memory);

        if (memory.LocationId != null && _byLocation.TryGetValue(memory.LocationId, out var byLocation))
            byLocation.Remove(memory);

        if (_byType.TryGetValue(memory.Type, out var byType))
            byType.Remove(memory);
    }

    #endregion
}
