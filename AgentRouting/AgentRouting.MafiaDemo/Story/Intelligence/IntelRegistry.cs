// Story System - Intel Registry
// Manages all gathered intelligence

namespace AgentRouting.MafiaDemo.Story;

/// <summary>
/// Manages all gathered intelligence.
/// Supports queries by subject, type, recency, and reliability.
/// </summary>
public class IntelRegistry
{
    private readonly List<Intel> _allIntel = new();
    private readonly Dictionary<string, List<Intel>> _bySubject = new();
    private readonly Dictionary<IntelType, List<Intel>> _byType = new();

    public void Add(Intel intel)
    {
        _allIntel.Add(intel);

        if (!_bySubject.ContainsKey(intel.SubjectId))
            _bySubject[intel.SubjectId] = new List<Intel>();
        _bySubject[intel.SubjectId].Add(intel);

        if (!_byType.ContainsKey(intel.Type))
            _byType[intel.Type] = new List<Intel>();
        _byType[intel.Type].Add(intel);
    }

    public IEnumerable<Intel> GetForSubject(string subjectId, int currentWeek) =>
        _bySubject.TryGetValue(subjectId, out var list)
            ? list.Where(i => !i.IsExpired(currentWeek))
            : Enumerable.Empty<Intel>();

    public IEnumerable<Intel> GetByType(IntelType type, int currentWeek) =>
        _byType.TryGetValue(type, out var list)
            ? list.Where(i => !i.IsExpired(currentWeek))
            : Enumerable.Empty<Intel>();

    public IEnumerable<Intel> GetRecent(int weeks, int currentWeek) =>
        _allIntel.Where(i =>
            !i.IsExpired(currentWeek) &&
            (currentWeek - i.GatheredWeek) <= weeks);

    public IEnumerable<Intel> GetReliable(int currentWeek) =>
        _allIntel.Where(i => !i.IsExpired(currentWeek) && i.IsReliable);

    public IEnumerable<Intel> GetUnprocessed() =>
        _allIntel.Where(i => !i.IsProcessed);

    public Intel? GetMostRecentForSubject(string subjectId, IntelType type, int currentWeek)
    {
        return GetForSubject(subjectId, currentWeek)
            .Where(i => i.Type == type)
            .OrderByDescending(i => i.GatheredWeek)
            .ThenByDescending(i => i.Reliability)
            .FirstOrDefault();
    }

    /// <summary>
    /// Clean up expired intel to prevent unbounded growth.
    /// </summary>
    public void PruneExpired(int currentWeek)
    {
        var expired = _allIntel.Where(i => i.IsExpired(currentWeek)).ToList();
        foreach (var intel in expired)
        {
            _allIntel.Remove(intel);
            if (_bySubject.TryGetValue(intel.SubjectId, out var bySubject))
                bySubject.Remove(intel);
            if (_byType.TryGetValue(intel.Type, out var byType))
                byType.Remove(intel);
        }
    }
}
