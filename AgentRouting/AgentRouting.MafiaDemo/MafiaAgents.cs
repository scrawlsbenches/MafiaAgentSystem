using AgentRouting.Core;
using System.Text;

namespace AgentRouting.MafiaDemo;

/// <summary>
/// The Godfather - Don Vito Corleone
/// Makes final decisions, handles major issues, gives orders
/// </summary>
public class GodfatherAgent : AgentBase
{
    private readonly Dictionary<string, string> _favorsOwed = new();
    private readonly List<string> _approvedHits = new();
    
    public GodfatherAgent(string id, string name, IAgentLogger logger)
        : base(id, name, logger)
    {
        Capabilities.SupportedCategories.AddRange(new[]
        {
            "FinalDecision",
            "MajorDispute",
            "WarDeclaration",
            "PeaceTreaty",
            "FavorRequest"
        });
        
        Capabilities.Skills.AddRange(new[]
        {
            "Leadership",
            "Strategy",
            "Negotiation",
            "Wisdom"
        });
        
        Capabilities.MaxConcurrentMessages = 3; // The Don doesn't multitask
    }
    
    protected override async Task<MessageResult> HandleMessageAsync(
        AgentMessage message,
        CancellationToken ct)
    {
        await GameTimingOptions.DelayAsync(GameTimingOptions.Current.GodfatherThinkingMs, ct);
        
        var content = message.Content.ToLowerInvariant();
        
        // Handle favor requests
        if (content.Contains("favor") || content.Contains("help"))
        {
            _favorsOwed[message.SenderId] = DateTime.UtcNow.ToString();
            
            return MessageResult.Ok(
                $"*The Don leans back in his chair*\n\n" +
                $"You come to me on the day of my daughter's wedding... " +
                $"asking for a favor.\n\n" +
                $"Very well. I will grant your request. " +
                $"But someday - and that day may never come - " +
                $"I'll call upon you to do a service for me.\n\n" +
                $"*kisses ring*");
        }
        
        // Handle territory disputes
        if (content.Contains("territory") || content.Contains("dispute"))
        {
            return MessageResult.Ok(
                $"*The Don strokes his cat*\n\n" +
                $"A wise man does not make war. He makes peace.\n\n" +
                $"Send the Underboss to negotiate. " +
                $"If they refuse... send Luca Brasi with a message.");
        }
        
        // Handle requests for hits
        if (content.Contains("hit") || content.Contains("whack") || content.Contains("eliminate"))
        {
            var approved = !content.Contains("police") && !content.Contains("politician");
            
            if (approved)
            {
                var hitId = $"HIT-{DateTime.UtcNow.Ticks}";
                _approvedHits.Add(hitId);
                
                var forward = ForwardMessage(message, "underboss-001",
                    $"The Don has approved this. Handle it quietly. Hit ID: {hitId}");
                
                return MessageResult.Forward(forward,
                    "*The Don nods slowly*\n\n" +
                    "Consider it done. But make it look like an accident.");
            }
            else
            {
                return MessageResult.Ok(
                    "*The Don shakes his head*\n\n" +
                    "Never get involved with the law or politics. " +
                    "This cannot be approved.\n\n" +
                    "We're businessmen, not murderers.");
            }
        }
        
        // Handle business proposals
        if (content.Contains("business") || content.Contains("operation"))
        {
            var forward = ForwardMessage(message, "consigliere-001",
                "Review the legal implications and report back.");
            
            return MessageResult.Forward(forward,
                "The Consigliere will review this proposal.");
        }
        
        // Default wisdom
        return MessageResult.Ok(
            "*The Don speaks softly*\n\n" +
            "I'm gonna make him an offer he can't refuse.\n\n" +
            "Bring this matter to the Underboss. " +
            "He handles the day-to-day operations.");
    }
    
    public Dictionary<string, string> GetFavorsOwed() => new(_favorsOwed);
    public List<string> GetApprovedHits() => new(_approvedHits);
}

/// <summary>
/// The Underboss - handles day-to-day operations
/// </summary>
public class UnderbossAgent : AgentBase
{
    private readonly Dictionary<string, decimal> _territoryRevenue = new();
    
    public UnderbossAgent(string id, string name, IAgentLogger logger)
        : base(id, name, logger)
    {
        Capabilities.SupportedCategories.AddRange(new[]
        {
            "DailyOperations",
            "CrewManagement",
            "Revenue",
            "Enforcement"
        });
        
        Capabilities.Skills.AddRange(new[]
        {
            "Management",
            "Enforcement",
            "Coordination"
        });
    }
    
    protected override async Task<MessageResult> HandleMessageAsync(
        AgentMessage message,
        CancellationToken ct)
    {
        await GameTimingOptions.DelayAsync(GameTimingOptions.Current.UnderbossThinkingMs, ct);
        
        var content = message.Content.ToLowerInvariant();
        
        // Handle collection reports
        if (content.Contains("collection") || content.Contains("payment"))
        {
            return MessageResult.Ok(
                "Good. Make sure the Capos get their cut. " +
                "And remember - 50% goes up to the Don.\n\n" +
                "Keep the streets running smooth.");
        }
        
        // Handle crew disputes
        if (content.Contains("crew") || content.Contains("dispute"))
        {
            return MessageResult.Ok(
                "I'll talk to the Capos. " +
                "Tell them to settle it themselves or I'll settle it for them.\n\n" +
                "We don't need the Don hearing about petty squabbles.");
        }
        
        // Handle protection rackets
        if (content.Contains("protection") || content.Contains("store"))
        {
            var forward = ForwardMessage(message, "capo-001",
                "Handle the protection racket. Make sure they understand the benefits.");
            
            return MessageResult.Forward(forward,
                "Capo will handle the collection. " +
                "Make sure the shopkeeper understands we're protecting him... from us.");
        }
        
        // Handle enforcement issues
        if (content.Contains("enforce") || content.Contains("muscle"))
        {
            var forward = ForwardMessage(message, "soldier-001",
                "Send a message. Nothing permanent unless I say so.");
            
            return MessageResult.Forward(forward,
                "Sending the boys. They'll understand the message.");
        }
        
        return MessageResult.Ok(
            "Consider it handled. The family takes care of its own.");
    }
}

/// <summary>
/// The Consigliere - legal and strategic advisor
/// </summary>
public class ConsigliereAgent : AgentBase
{
    public ConsigliereAgent(string id, string name, IAgentLogger logger)
        : base(id, name, logger)
    {
        Capabilities.SupportedCategories.AddRange(new[]
        {
            "Legal",
            "Strategy",
            "Negotiations",
            "Counseling"
        });
        
        Capabilities.Skills.AddRange(new[]
        {
            "LegalAdvice",
            "Strategy",
            "Diplomacy",
            "Intelligence"
        });
    }
    
    protected override async Task<MessageResult> HandleMessageAsync(
        AgentMessage message,
        CancellationToken ct)
    {
        await GameTimingOptions.DelayAsync(GameTimingOptions.Current.ConsigliereThinkingMs, ct);
        
        var content = message.Content.ToLowerInvariant();
        
        // Handle legal matters
        if (content.Contains("legal") || content.Contains("lawyer") || content.Contains("court"))
        {
            return MessageResult.Ok(
                "From a legal standpoint, we have three options:\n\n" +
                "1. Settle out of court - costs money but quick\n" +
                "2. Fight it - risky but sends a message\n" +
                "3. Make it disappear - requires special handling\n\n" +
                "I recommend option 1. Keep the lawyers fat and happy.");
        }
        
        // Handle strategic planning
        if (content.Contains("strategy") || content.Contains("plan") || content.Contains("move"))
        {
            return MessageResult.Ok(
                "I've analyzed the situation. Here's my counsel:\n\n" +
                "The other families are watching. Any move we make will be seen.\n\n" +
                "We should appear weak while building strength. " +
                "Let them think we're finished, then strike when they're comfortable.\n\n" +
                "Sun Tzu would approve.");
        }
        
        // Handle negotiations
        if (content.Contains("negotiate") || content.Contains("deal") || content.Contains("treaty"))
        {
            return MessageResult.Ok(
                "In negotiations, remember:\n\n" +
                "1. Never let them see you want it\n" +
                "2. Always have leverage\n" +
                "3. Make them think they're winning\n" +
                "4. Get everything in writing\n\n" +
                "I'll handle the details. You just look intimidating.");
        }
        
        return MessageResult.Ok(
            "My advice? Keep your friends close, but your enemies closer.\n\n" +
            "I'll look into this matter and report to the Don.");
    }
}

/// <summary>
/// Capo (Captain) - manages a crew of soldiers
/// </summary>
public class CapoAgent : AgentBase
{
    private readonly List<string> _crewMembers;
    private decimal _weeklyTake = 0;
    
    public CapoAgent(string id, string name, IAgentLogger logger, List<string> crewMembers)
        : base(id, name, logger)
    {
        _crewMembers = crewMembers;
        
        Capabilities.SupportedCategories.AddRange(new[]
        {
            "ProtectionRacket",
            "LoanSharking",
            "Gambling",
            "CrewLeadership"
        });
        
        Capabilities.Skills.AddRange(new[]
        {
            "CrewManagement",
            "Collections",
            "Intimidation"
        });
    }
    
    protected override async Task<MessageResult> HandleMessageAsync(
        AgentMessage message,
        CancellationToken ct)
    {
        await GameTimingOptions.DelayAsync(GameTimingOptions.Current.CapoThinkingMs, ct);
        
        var content = message.Content.ToLowerInvariant();
        
        // Handle collections
        if (content.Contains("collect") || content.Contains("pickup"))
        {
            var amount = new Random().Next(5000, 15000);
            _weeklyTake += amount;
            
            return MessageResult.Ok(
                $"My crew collected ${amount:N0} this week.\n\n" +
                $"${amount * 0.5m:N0} goes to the Underboss.\n" +
                $"${amount * 0.3m:N0} split among the crew.\n" +
                $"${amount * 0.2m:N0} for me.\n\n" +
                "Everyone's happy. Business is good.");
        }
        
        // Handle new recruits
        if (content.Contains("recruit") || content.Contains("soldier"))
        {
            return MessageResult.Ok(
                "We don't just let anyone in. He's gotta:\n\n" +
                "1. Be full Italian\n" +
                "2. Prove himself\n" +
                "3. Take the oath\n" +
                "4. Get vouched for by a made man\n\n" +
                "If he's good, I'll put him with Tony and Paulie.");
        }
        
        // Handle territory
        if (content.Contains("territory") || content.Contains("turf"))
        {
            return MessageResult.Ok(
                "This is my territory. My crew handles:\n" +
                "- Protection rackets on Mulberry Street\n" +
                "- Numbers game in Little Italy\n" +
                "- Loan sharking in the neighborhood\n\n" +
                "Anyone moves in without permission, they answer to me.");
        }
        
        return MessageResult.Ok(
            $"I got {_crewMembers.Count} guys in my crew. " +
            $"They're good earners and they keep their mouths shut.\n\n" +
            "What do you need?");
    }
    
    public decimal GetWeeklyTake() => _weeklyTake;
}

/// <summary>
/// Soldier - enforcer who carries out orders
/// </summary>
public class SoldierAgent : AgentBase
{
    private readonly List<string> _completedJobs = new();
    
    public SoldierAgent(string id, string name, IAgentLogger logger)
        : base(id, name, logger)
    {
        Capabilities.SupportedCategories.AddRange(new[]
        {
            "Enforcement",
            "Collections",
            "Intimidation",
            "Hits"
        });
        
        Capabilities.Skills.AddRange(new[]
        {
            "Muscle",
            "Intimidation",
            "Loyalty",
            "Discretion"
        });
    }
    
    protected override async Task<MessageResult> HandleMessageAsync(
        AgentMessage message,
        CancellationToken ct)
    {
        await GameTimingOptions.DelayAsync(GameTimingOptions.Current.SoldierThinkingMs, ct);
        
        var content = message.Content.ToLowerInvariant();
        
        // Handle collection
        if (content.Contains("collect") || content.Contains("payment"))
        {
            _completedJobs.Add($"Collection-{DateTime.UtcNow.Ticks}");
            
            return MessageResult.Ok(
                "*cracks knuckles*\n\n" +
                "Yeah, I went and saw him. Had a little chat.\n\n" +
                "He remembered the payment real quick after that. " +
                "Won't be late again.\n\n" +
                "Money's with the Capo.");
        }
        
        // Handle intimidation
        if (content.Contains("intimidate") || content.Contains("scare") || content.Contains("message"))
        {
            _completedJobs.Add($"Intimidation-{DateTime.UtcNow.Ticks}");
            
            return MessageResult.Ok(
                "Done. Me and Paulie paid him a visit.\n\n" +
                "Didn't have to lay a finger on him. " +
                "Just explained how things work around here.\n\n" +
                "He gets the message. Won't be a problem.");
        }
        
        // Handle hits (approved only)
        if (content.Contains("hit") && message.Metadata.ContainsKey("ApprovedByDon"))
        {
            _completedJobs.Add($"Hit-{DateTime.UtcNow.Ticks}");
            
            return MessageResult.Ok(
                "*lights cigarette*\n\n" +
                "It's done. Clean. No witnesses.\n\n" +
                "The cannoli is in the river. " +
                "Left the gun, took the cannoli.\n\n" +
                "Nobody saw nothing.");
        }
        
        // Handle general muscle work
        return MessageResult.Ok(
            "Just tell me what needs doing. I'm a soldier.\n\n" +
            "I do what I'm told, I keep my mouth shut, " +
            "and I take care of business.\n\n" +
            "That's the job.");
    }
    
    public List<string> GetCompletedJobs() => new(_completedJobs);
}

/// <summary>
/// Associate - not a made man, but works with the family
/// </summary>
public class AssociateAgent : AgentBase
{
    public AssociateAgent(string id, string name, IAgentLogger logger)
        : base(id, name, logger)
    {
        Capabilities.SupportedCategories.AddRange(new[]
        {
            "Information",
            "SmallJobs",
            "Errands"
        });
        
        Capabilities.Skills.AddRange(new[]
        {
            "StreetSmarts",
            "Connections",
            "Reliability"
        });
    }
    
    protected override async Task<MessageResult> HandleMessageAsync(
        AgentMessage message,
        CancellationToken ct)
    {
        await GameTimingOptions.DelayAsync(GameTimingOptions.Current.AssociateThinkingMs, ct);
        
        var content = message.Content.ToLowerInvariant();
        
        if (content.Contains("info") || content.Contains("word"))
        {
            return MessageResult.Ok(
                "I hear things on the street, you know?\n\n" +
                "The Tattaglias are moving product through the docks. " +
                "The cops are sniffing around the social club. " +
                "Some guy's asking questions about the family.\n\n" +
                "I keep my ears open.");
        }
        
        return MessageResult.Ok(
            "I do what I can. Run errands, pass messages, " +
            "keep my eyes and ears open.\n\n" +
            "Someday maybe I get made. Until then, I prove myself.");
    }
}
