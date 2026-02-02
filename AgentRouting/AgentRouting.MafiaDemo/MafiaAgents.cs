using AgentRouting.Core;
using AgentRouting.MafiaDemo.Game;

namespace AgentRouting.MafiaDemo;

/// <summary>
/// The Godfather - Don Vito Corleone
/// Makes final decisions, handles major issues, gives orders.
/// Supports both interactive message handling and autonomous decision-making.
/// </summary>
public class GodfatherAgent : AutonomousAgent
{
    private readonly Dictionary<string, string> _favorsOwed = new();
    private readonly List<string> _approvedHits = new();
    private DateTime _lastDecision = DateTime.MinValue;

    public GodfatherAgent(string id, string name, IAgentLogger logger)
        : base(id, name, logger)
    {
        DecisionDelay = TimeSpan.FromSeconds(15);
        Ambition = 8;
        Loyalty = 10;
        Aggression = 3; // Calculated, not impulsive

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

    public override AgentDecision? MakeDecision(GameState gameState, Random random)
    {
        // The Don doesn't make frivolous decisions
        if (DateTime.UtcNow - _lastDecision < TimeSpan.FromSeconds(30))
            return null;

        var roll = random.Next(0, 10);

        // 30% chance to send strategic message
        if (roll < 3)
        {
            _lastDecision = DateTime.UtcNow;

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

        // Handle favor requests
        if (content.Contains("favor") || content.Contains("help"))
        {
            _favorsOwed[message.SenderId] = DateTime.UtcNow.ToString();

            return MessageResult.Ok(
                $"*The Don nods slowly*\n\n" +
                $"Of course. We take care of our friends. " +
                $"Send someone to help. But remember - " +
                $"someday they will owe us.");
        }

        // Handle police raids
        if (content.Contains("police") || content.Contains("raid"))
        {
            return MessageResult.Ok(
                "*The Don remains calm*\n\n" +
                "This is why we have lawyers. Tom, handle this. " +
                "Pay whoever needs to be paid. " +
                "And find out who talked.");
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

        // Handle rival families
        if (content.Contains("tattaglia") || content.Contains("barzini") || content.Contains("rival"))
        {
            var isAggressive = Random.Shared.Next(0, 10) < 3;

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

        // Handle business proposals and opportunities
        if (content.Contains("business") || content.Contains("operation"))
        {
            var forward = ForwardMessage(message, "consigliere-001",
                "Review the legal implications and report back.");

            return MessageResult.Forward(forward,
                "The Consigliere will review this proposal.");
        }

        if (content.Contains("opportunity") || content.Contains("deal"))
        {
            var shouldInvest = Random.Shared.Next(0, 10) < 6;

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

        // Default wisdom
        return MessageResult.Ok(
            "*The Don listens carefully*\n\n" +
            "I understand. Do what needs to be done. " +
            "But keep the family safe.");
    }

    public Dictionary<string, string> GetFavorsOwed() => new(_favorsOwed);
    public List<string> GetApprovedHits() => new(_approvedHits);
}

/// <summary>
/// The Underboss - handles day-to-day operations.
/// Supports both interactive message handling and autonomous decision-making.
/// </summary>
public class UnderbossAgent : AutonomousAgent
{
    private readonly Dictionary<string, decimal> _territoryRevenue = new();

    public UnderbossAgent(string id, string name, IAgentLogger logger)
        : base(id, name, logger)
    {
        DecisionDelay = TimeSpan.FromSeconds(8);
        Ambition = 7;
        Loyalty = 9;
        Aggression = 7;

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

        // Handle report requests
        if (content.Contains("report"))
        {
            return MessageResult.Ok(
                "Everything's running like clockwork. " +
                "Collections are up, soldiers are happy, " +
                "and we ain't got problems.");
        }

        // Handle expansion/scouting
        if (content.Contains("scout") || content.Contains("expansion"))
        {
            return MessageResult.Ok(
                "I'll send the boys to look around. " +
                "There's some opportunities in Brooklyn and the Bronx. " +
                "Give me a week, I'll have a plan.");
        }

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
            "Consider it done, Don. The family comes first.");
    }
}

/// <summary>
/// The Consigliere - legal and strategic advisor.
/// Supports both interactive message handling and autonomous decision-making.
/// </summary>
public class ConsigliereAgent : AutonomousAgent
{
    public ConsigliereAgent(string id, string name, IAgentLogger logger)
        : base(id, name, logger)
    {
        DecisionDelay = TimeSpan.FromSeconds(12);
        Ambition = 4;
        Loyalty = 10;
        Aggression = 2;

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

        // Handle legal matters
        if (content.Contains("legal") || content.Contains("lawyer") || content.Contains("court") ||
            content.Contains("police") || content.Contains("raid"))
        {
            return MessageResult.Ok(
                "I'll talk to our lawyers and judges. " +
                "We have friends in the department. " +
                "This will go away, but it'll cost us.");
        }

        // Handle other families assessment
        if (content.Contains("other families") || content.Contains("strong"))
        {
            return MessageResult.Ok(
                "We're in a strong position, but not invincible. " +
                "The Tattaglias have the drugs, Barzini has the politicians. " +
                "We need to play smart, not hard.");
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
            "I'll study the situation and advise you, Don. " +
            "Give me a day to think it through.");
    }
}

/// <summary>
/// Capo (Captain) - manages a crew of soldiers.
/// Supports both interactive message handling and autonomous decision-making.
/// </summary>
public class CapoAgent : AutonomousAgent
{
    private readonly List<string> _crewMembers;
    private decimal _weeklyTake = 0;

    public CapoAgent(string id, string name, IAgentLogger logger, List<string>? crewMembers = null)
        : base(id, name, logger)
    {
        _crewMembers = crewMembers ?? new List<string>();

        DecisionDelay = TimeSpan.FromSeconds(6);
        Ambition = 8;
        Loyalty = 7;
        Aggression = 8;

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

        // Handle collections
        if (content.Contains("collect") || content.Contains("pickup"))
        {
            var amount = Random.Shared.Next(5000, 15000);
            _weeklyTake += amount;

            return MessageResult.Ok(
                $"My crew collected ${amount:N0} this week.\n\n" +
                $"${amount * 0.5m:N0} goes to the Underboss.\n" +
                $"${amount * 0.3m:N0} split among the crew.\n" +
                $"${amount * 0.2m:N0} for me.\n\n" +
                "Everyone's happy. Business is good.");
        }

        // Handle collection schedule
        if (content.Contains("collections") || content.Contains("schedule"))
        {
            return MessageResult.Ok(
                "Yeah, yeah, I'm on it. " +
                "My guys know what to do. " +
                "We'll have the money by Friday.");
        }

        // Handle crew management
        if (content.Contains("crew") || content.Contains("line"))
        {
            return MessageResult.Ok(
                "My crew is tight. They know the rules. " +
                "Nobody steps out of line or they answer to me.");
        }

        // Handle pressure for better numbers
        if (content.Contains("push harder") || content.Contains("numbers"))
        {
            return MessageResult.Ok(
                "I'll squeeze 'em harder. " +
                "There's a new restaurant opening up. " +
                "Perfect timing.");
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
            $"Got it, boss. Consider it handled.");
    }

    public decimal GetWeeklyTake() => _weeklyTake;
    public List<string> GetCrewMembers() => new(_crewMembers);
}

/// <summary>
/// Soldier - enforcer who carries out orders.
/// Supports both interactive message handling and autonomous decision-making.
/// </summary>
public class SoldierAgent : AutonomousAgent
{
    private readonly List<string> _completedJobs = new();

    public SoldierAgent(string id, string name, IAgentLogger logger)
        : base(id, name, logger)
    {
        DecisionDelay = TimeSpan.FromSeconds(4);
        Ambition = 5;
        Loyalty = 9;
        Aggression = 9;

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
            "Yeah, I'm on it. No problem.");
    }

    public List<string> GetCompletedJobs() => new(_completedJobs);
}

/// <summary>
/// Associate - not a made man, but works with the family.
/// Handles information gathering and small jobs.
/// </summary>
public class AssociateAgent : AutonomousAgent
{
    public AssociateAgent(string id, string name, IAgentLogger logger)
        : base(id, name, logger)
    {
        DecisionDelay = TimeSpan.FromSeconds(3);
        Ambition = 6;
        Loyalty = 6;
        Aggression = 4;

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

    public override AgentDecision? MakeDecision(GameState gameState, Random random)
    {
        var roll = random.Next(0, 10);

        // 40% chance to gather intel
        if (roll < 4)
        {
            var intel = new[]
            {
                "Heard something interesting on the street today.",
                "The Tattaglias are moving product through the docks.",
                "Cops have been asking questions around the neighborhood.",
                "There's a new guy in town, might be useful."
            };

            return new AgentDecision
            {
                Type = DecisionType.SendMessage,
                Message = new AgentMessage
                {
                    SenderId = Id,
                    ReceiverId = "soldier-001",
                    Subject = "Street Intel",
                    Content = intel[random.Next(intel.Length)],
                    Category = "Information"
                }
            };
        }

        return new AgentDecision { Type = DecisionType.Wait };
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
