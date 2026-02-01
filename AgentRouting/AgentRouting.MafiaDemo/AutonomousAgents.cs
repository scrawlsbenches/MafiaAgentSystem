using AgentRouting.Core;
using AgentRouting.MafiaDemo.Game;

namespace AgentRouting.MafiaDemo.Autonomous;

/// <summary>
/// Autonomous Godfather - makes strategic decisions, responds to major issues
/// </summary>
public class AutonomousGodfather : AutonomousAgent
{
    private DateTime _lastDecision = DateTime.MinValue;
    
    public AutonomousGodfather(string id, string name, IAgentLogger logger)
        : base(id, name, logger)
    {
        DecisionDelay = TimeSpan.FromSeconds(15); // Don takes his time
        Ambition = 8;
        Loyalty = 10;
        Aggression = 3; // Calculated, not impulsive
        
        Capabilities.SupportedCategories.AddRange(new[]
        {
            "FinalDecision", "MajorDispute", "FavorRequest"
        });
    }
    
    public override AgentDecision? MakeDecision(GameState gameState, Random random)
    {
        // The Don doesn't make frivolous decisions
        if (DateTime.Now - _lastDecision < TimeSpan.FromSeconds(30))
            return null;
        
        var roll = random.Next(0, 10);
        
        // 30% chance to send strategic message
        if (roll < 3)
        {
            _lastDecision = DateTime.Now;
            
            var messages = new[]
            {
                ("underboss-001", "I want a full report on all family operations by end of day."),
                ("consigliere-001", "What's your read on the other families? Are we strong enough?"),
                ("capo-001", "How are the men? Are they loyal? Do they have what they need?"),
                ("underboss-001", "We need to think about expansion. Scout new territories.")
            };
            
            var (recipient, content) = messages[random.Next(messages.Length)];
            
            return new AgentDecision
            {
                Type = DecisionType.SendMessage,
                Message = new AgentMessage
                {
                    SenderId = Id,
                    ReceiverId = recipient,
                    Subject = "Strategic Directive",
                    Content = content,
                    Category = "DailyOperations",
                    Priority = MessagePriority.High
                }
            };
        }
        
        return new AgentDecision { Type = DecisionType.Wait };
    }
    
    protected override async Task<MessageResult> HandleMessageAsync(
        AgentMessage message,
        CancellationToken ct)
    {
        await GameTimingOptions.DelayAsync(GameTimingOptions.Current.GodfatherThinkingMs, ct);
        
        var content = message.Content.ToLowerInvariant();
        
        // Handle police raids
        if (content.Contains("police") || content.Contains("raid"))
        {
            return MessageResult.Ok(
                "*The Don remains calm*\n\n" +
                "This is why we have lawyers. Tom, handle this. " +
                "Pay whoever needs to be paid. " +
                "And find out who talked.");
        }
        
        // Handle rival families
        if (content.Contains("tattaglia") || content.Contains("barzini") || content.Contains("rival"))
        {
            var isAggressive = Random.Next(0, 10) < 3;
            
            if (isAggressive)
            {
                return MessageResult.Ok(
                    "*The Don's eyes narrow*\n\n" +
                    "They think we're weak. Show them otherwise. " +
                    "But do it quietly. No attention.");
            }
            else
            {
                return MessageResult.Ok(
                    "*The Don shakes his head*\n\n" +
                    "A war is expensive. Send Tom to negotiate. " +
                    "If they want peace, we give it. For now.");
            }
        }
        
        // Handle betrayal
        if (content.Contains("betray") || content.Contains("informant") || content.Contains("fed"))
        {
            return MessageResult.Ok(
                "*The Don's voice grows cold*\n\n" +
                "Betrayal cannot be tolerated. " +
                "But we must be sure. Watch him. " +
                "If it's true... handle it quietly.");
        }
        
        // Handle business opportunities
        if (content.Contains("opportunity") || content.Contains("deal"))
        {
            var shouldInvest = Random.Next(0, 10) < 6;
            
            if (shouldInvest)
            {
                return MessageResult.Ok(
                    "A good businessman knows when to invest. " +
                    "Do it. But keep it legitimate.");
            }
            else
            {
                return MessageResult.Ok(
                    "It sounds risky. We have enough heat already. " +
                    "Pass on this one.");
            }
        }
        
        // Handle favors
        if (content.Contains("help") || content.Contains("favor"))
        {
            return MessageResult.Ok(
                "*The Don nods slowly*\n\n" +
                "Of course. We take care of our friends. " +
                "Send someone to help. But remember - " +
                "someday they will owe us.");
        }
        
        return MessageResult.Ok(
            "*The Don listens carefully*\n\n" +
            "I understand. Do what needs to be done. " +
            "But keep the family safe.");
    }
}

/// <summary>
/// Autonomous Underboss - actively manages operations
/// </summary>
public class AutonomousUnderboss : AutonomousAgent
{
    public AutonomousUnderboss(string id, string name, IAgentLogger logger)
        : base(id, name, logger)
    {
        DecisionDelay = TimeSpan.FromSeconds(8);
        Ambition = 7;
        Loyalty = 9;
        Aggression = 7;
        
        Capabilities.SupportedCategories.AddRange(new[]
        {
            "DailyOperations", "CrewManagement", "Revenue"
        });
    }
    
    public override AgentDecision? MakeDecision(GameState gameState, Random random)
    {
        var roll = random.Next(0, 10);
        
        // 40% chance to collect money
        if (roll < 4)
        {
            return new AgentDecision
            {
                Type = DecisionType.CollectMoney,
                Reason = "Weekly collections"
            };
        }
        
        // 30% chance to send orders to Capos
        if (roll < 7)
        {
            var orders = new[]
            {
                "Make sure the collections are on schedule. I don't want excuses.",
                "Keep your crew in line. No freelancing.",
                "The Don wants to see better numbers. Push harder.",
                "Watch for police. They're sniffing around."
            };
            
            return new AgentDecision
            {
                Type = DecisionType.SendMessage,
                Message = new AgentMessage
                {
                    SenderId = Id,
                    ReceiverId = "capo-001",
                    Subject = "Orders",
                    Content = orders[random.Next(orders.Length)],
                    Category = "DailyOperations"
                }
            };
        }
        
        // 20% chance to report to Don
        if (roll < 9 && gameState.Day % 3 == 0)
        {
            return new AgentDecision
            {
                Type = DecisionType.SendMessage,
                Message = new AgentMessage
                {
                    SenderId = Id,
                    ReceiverId = "godfather-001",
                    Subject = "Weekly Report",
                    Content = $"Don, the family pulled in ${gameState.TotalRevenue:N0} this week. " +
                             $"We control {gameState.TerritoryCount} territories. Everything's running smooth.",
                    Category = "DailyOperations"
                }
            };
        }
        
        return new AgentDecision { Type = DecisionType.Wait };
    }
    
    protected override async Task<MessageResult> HandleMessageAsync(
        AgentMessage message,
        CancellationToken ct)
    {
        await GameTimingOptions.DelayAsync(GameTimingOptions.Current.UnderbossThinkingMs, ct);
        
        var content = message.Content.ToLowerInvariant();
        
        if (content.Contains("report"))
        {
            return MessageResult.Ok(
                "Everything's running like clockwork. " +
                "Collections are up, soldiers are happy, " +
                "and we ain't got problems.");
        }
        
        if (content.Contains("scout") || content.Contains("expansion"))
        {
            return MessageResult.Ok(
                "I'll send the boys to look around. " +
                "There's some opportunities in Brooklyn and the Bronx. " +
                "Give me a week, I'll have a plan.");
        }
        
        return MessageResult.Ok(
            "Consider it done, Don. The family comes first.");
    }
}

/// <summary>
/// Autonomous Consigliere - provides strategic advice
/// </summary>
public class AutonomousConsigliere : AutonomousAgent
{
    public AutonomousConsigliere(string id, string name, IAgentLogger logger)
        : base(id, name, logger)
    {
        DecisionDelay = TimeSpan.FromSeconds(12);
        Ambition = 4;
        Loyalty = 10;
        Aggression = 2;
        
        Capabilities.SupportedCategories.AddRange(new[]
        {
            "Legal", "Strategy", "Negotiations"
        });
    }
    
    public override AgentDecision? MakeDecision(GameState gameState, Random random)
    {
        var roll = random.Next(0, 10);
        
        // 30% chance to provide strategic advice
        if (roll < 3)
        {
            var advice = new[]
            {
                "Don, I've been thinking about our long-term strategy. We should diversify into legitimate businesses.",
                "The political climate is changing. We need better connections in City Hall.",
                "The other families are nervous. This could be an opportunity to negotiate new territories.",
                "Our legal exposure is growing. I recommend we clean up some operations."
            };
            
            return new AgentDecision
            {
                Type = DecisionType.SendMessage,
                Message = new AgentMessage
                {
                    SenderId = Id,
                    ReceiverId = "godfather-001",
                    Subject = "Strategic Counsel",
                    Content = advice[random.Next(advice.Length)],
                    Category = "Strategy"
                }
            };
        }
        
        return new AgentDecision { Type = DecisionType.Wait };
    }
    
    protected override async Task<MessageResult> HandleMessageAsync(
        AgentMessage message,
        CancellationToken ct)
    {
        await GameTimingOptions.DelayAsync(GameTimingOptions.Current.ConsigliereThinkingMs, ct);
        
        var content = message.Content.ToLowerInvariant();
        
        if (content.Contains("police") || content.Contains("raid") || content.Contains("legal"))
        {
            return MessageResult.Ok(
                "I'll talk to our lawyers and judges. " +
                "We have friends in the department. " +
                "This will go away, but it'll cost us.");
        }
        
        if (content.Contains("other families") || content.Contains("strong"))
        {
            return MessageResult.Ok(
                "We're in a strong position, but not invincible. " +
                "The Tattaglias have the drugs, Barzini has the politicians. " +
                "We need to play smart, not hard.");
        }
        
        return MessageResult.Ok(
            "I'll study the situation and advise you, Don. " +
            "Give me a day to think it through.");
    }
}

/// <summary>
/// Autonomous Capo - manages territory and soldiers
/// </summary>
public class AutonomousCapo : AutonomousAgent
{
    public AutonomousCapo(string id, string name, IAgentLogger logger)
        : base(id, name, logger)
    {
        DecisionDelay = TimeSpan.FromSeconds(6);
        Ambition = 8;
        Loyalty = 7;
        Aggression = 8;
        
        Capabilities.SupportedCategories.AddRange(new[]
        {
            "ProtectionRacket", "CrewLeadership"
        });
    }
    
    public override AgentDecision? MakeDecision(GameState gameState, Random random)
    {
        var roll = random.Next(0, 10);
        
        // 50% chance to collect money
        if (roll < 5)
        {
            return new AgentDecision
            {
                Type = DecisionType.CollectMoney,
                Reason = "Protection racket collections"
            };
        }
        
        // 20% chance to recruit
        if (roll < 7 && gameState.SoldierCount < 20)
        {
            return new AgentDecision
            {
                Type = DecisionType.RecruitSoldier,
                Reason = "Expanding the crew"
            };
        }
        
        // 20% chance to send report
        if (roll < 9)
        {
            return new AgentDecision
            {
                Type = DecisionType.SendMessage,
                Message = new AgentMessage
                {
                    SenderId = Id,
                    ReceiverId = "underboss-001",
                    Subject = "Territory Report",
                    Content = "Boss, my crew is solid. We got the neighborhood locked down. " +
                             "The shopkeepers are paying on time. No problems.",
                    Category = "CrewManagement"
                }
            };
        }
        
        return new AgentDecision { Type = DecisionType.Wait };
    }
    
    protected override async Task<MessageResult> HandleMessageAsync(
        AgentMessage message,
        CancellationToken ct)
    {
        await GameTimingOptions.DelayAsync(GameTimingOptions.Current.CapoThinkingMs, ct);
        
        var content = message.Content.ToLowerInvariant();
        
        if (content.Contains("collections") || content.Contains("schedule"))
        {
            return MessageResult.Ok(
                "Yeah, yeah, I'm on it. " +
                "My guys know what to do. " +
                "We'll have the money by Friday.");
        }
        
        if (content.Contains("crew") || content.Contains("line"))
        {
            return MessageResult.Ok(
                "My crew is tight. They know the rules. " +
                "Nobody steps out of line or they answer to me.");
        }
        
        if (content.Contains("push harder") || content.Contains("numbers"))
        {
            return MessageResult.Ok(
                "I'll squeeze 'em harder. " +
                "There's a new restaurant opening up. " +
                "Perfect timing.");
        }
        
        return MessageResult.Ok(
            "Got it, boss. Consider it handled.");
    }
}

/// <summary>
/// Autonomous Soldier - carries out orders, collects money
/// </summary>
public class AutonomousSoldier : AutonomousAgent
{
    public AutonomousSoldier(string id, string name, IAgentLogger logger)
        : base(id, name, logger)
    {
        DecisionDelay = TimeSpan.FromSeconds(4);
        Ambition = 5;
        Loyalty = 9;
        Aggression = 9;
        
        Capabilities.SupportedCategories.AddRange(new[]
        {
            "Enforcement", "Collections"
        });
    }
    
    public override AgentDecision? MakeDecision(GameState gameState, Random random)
    {
        var roll = random.Next(0, 10);
        
        // 60% chance to collect money
        if (roll < 6)
        {
            return new AgentDecision
            {
                Type = DecisionType.CollectMoney,
                Reason = "Street collections"
            };
        }
        
        // 30% chance to report to Capo
        if (roll < 9)
        {
            var reports = new[]
            {
                "Boss, made the rounds. Everyone paid up.",
                "That shopkeeper on 5th Street is late again. Want me to pay him a visit?",
                "Collections were good this week. No trouble.",
                "Saw some guys I don't recognize hanging around. Might be nothing."
            };
            
            return new AgentDecision
            {
                Type = DecisionType.SendMessage,
                Message = new AgentMessage
                {
                    SenderId = Id,
                    ReceiverId = "capo-001",
                    Subject = "Daily Report",
                    Content = reports[random.Next(reports.Length)],
                    Category = "Enforcement"
                }
            };
        }
        
        return new AgentDecision { Type = DecisionType.Wait };
    }
    
    protected override async Task<MessageResult> HandleMessageAsync(
        AgentMessage message,
        CancellationToken ct)
    {
        await GameTimingOptions.DelayAsync(GameTimingOptions.Current.SoldierThinkingMs, ct);
        
        return MessageResult.Ok(
            "Yeah, I'm on it. No problem.");
    }
}
