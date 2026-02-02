using AgentRouting.Core;
using AgentRouting.MafiaDemo.Game;

namespace AgentRouting.MafiaDemo;

class Program
{
    static async Task Main(string[] args)
    {
        Console.Clear();
        Console.WriteLine("═══════════════════════════════════════════════════════════");
        Console.WriteLine("      THE CORLEONE FAMILY - INTERACTIVE EXPERIENCES");
        Console.WriteLine("═══════════════════════════════════════════════════════════\n");
        Console.WriteLine("Choose your experience:\n");
        Console.WriteLine("1. 🤖 AI CAREER MODE - Watch AI agent play from Associate to Don");
        Console.WriteLine("2. 🎮 AUTONOMOUS GAME - Watch the family run itself (original)");
        Console.WriteLine("3. 🎬 SCRIPTED DEMO - Eight classic scenarios\n");
        Console.Write("Enter choice (1, 2, or 3): ");
        
        var choice = Console.ReadLine();
        
        if (choice == "1")
        {
            await AutonomousPlaythroughDemo.RunAsync();
        }
        else if (choice == "2")
        {
            await RunAutonomousGame();
        }
        else
        {
            await RunScriptedDemo();
        }
    }
    
    static async Task RunAutonomousGame()
    {
        var logger = new ConsoleAgentLogger();
        var game = new MafiaGameEngine(logger);
        
        // Create autonomous agents
        var godfather = new GodfatherAgent("godfather-001", "Don Vito Corleone", logger);
        var underboss = new UnderbossAgent("underboss-001", "Peter Clemenza", logger);
        var consigliere = new ConsigliereAgent("consigliere-001", "Tom Hagen", logger);
        var capo = new CapoAgent("capo-001", "Sonny Corleone", logger);
        var soldier = new SoldierAgent("soldier-001", "Luca Brasi", logger);
        
        // Register them
        game.RegisterAutonomousAgent(godfather);
        game.RegisterAutonomousAgent(underboss);
        game.RegisterAutonomousAgent(consigliere);
        game.RegisterAutonomousAgent(capo);
        game.RegisterAutonomousAgent(soldier);
        
        game.SetupRoutingRules();
        
        Console.WriteLine("\n⏳ Starting autonomous simulation...");
        Console.WriteLine("⏸️  Press Ctrl+C to stop\n");
        await GameTimingOptions.DelayAsync(GameTimingOptions.Current.GameStartDelayMs);
        
        // Handle Ctrl+C gracefully
        Console.CancelKeyPress += (sender, e) =>
        {
            e.Cancel = true;
            game.StopGame();
        };
        
        await game.StartGameAsync();
        
        Console.WriteLine("\n\nPress any key to exit...");
        Console.ReadKey();
    }
    
    static async Task RunScriptedDemo()
    {
        Console.Clear();
        PrintTitle();
        
        await GameTimingOptions.DelayAsync(GameTimingOptions.Current.SceneTransitionMs);
        
        Console.WriteLine("\n🎬 The Corleone Family is open for business...\n");
        await GameTimingOptions.DelayAsync(GameTimingOptions.Current.DialoguePauseMs);

        await Scenario1_RequestingFavor();
        await Scenario2_TerritoryDispute();
        await Scenario3_ProtectionRacket();
        await Scenario4_HitRequest();
        await Scenario5_LegalMatters();
        await Scenario6_CollectionDay();
        await Scenario7_ChainOfCommand();
        await Scenario8_FamilyMeeting();

        Console.WriteLine("\n" + new string('═', 70));
        Console.WriteLine("\n🎬 \"Leave the gun. Take the cannoli.\" 🍰");
        Console.WriteLine("\nThe family's business is concluded for today.");
        Console.WriteLine("\nPress any key to exit...");
        Console.ReadKey();
    }

    static void PrintTitle()
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine(@"
╔══════════════════════════════════════════════════════════════════╗
║                                                                  ║
║                    THE CORLEONE FAMILY                           ║
║                                                                  ║
║            Agent-Based Mafia Organization Demo                  ║
║                                                                  ║
║        ""I'm gonna make him an offer he can't refuse""            ║
║                                                                  ║
╚══════════════════════════════════════════════════════════════════╝
");
        Console.ResetColor();
        
        Console.WriteLine("\nOrganization Chart:");
        Console.WriteLine("═══════════════════");
        Console.WriteLine();
        Console.WriteLine("                    👑 Don Vito Corleone (The Godfather)");
        Console.WriteLine("                           /          \\");
        Console.WriteLine("                          /            \\");
        Console.WriteLine("              🤵 Underboss          👔 Consigliere");
        Console.WriteLine("                    |                  (Legal Advisor)");
        Console.WriteLine("              ───────────────");
        Console.WriteLine("             /               \\");
        Console.WriteLine("        💼 Capo           💼 Capo");
        Console.WriteLine("           / \\               / \\");
        Console.WriteLine("       👊  👊  👊        👊  👊  👊");
        Console.WriteLine("      Soldiers         Soldiers");
        Console.WriteLine("          |                 |");
        Console.WriteLine("      👥 Associates    👥 Associates");
    }

    static async Task Scenario1_RequestingFavor()
    {
        Console.WriteLine("\n\n" + new string('═', 70));
        Console.WriteLine("📋 SCENARIO 1: Requesting a Favor from the Don");
        Console.WriteLine(new string('═', 70));
        
        var logger = new ConsoleAgentLogger();
        var router = BuildMafiaOrganization(logger);

        var message = new AgentMessage
        {
            SenderId = "bonasera",
            Subject = "Seeking Justice",
            Content = "Don Corleone, I need a favor. Some boys hurt my daughter. " +
                     "The judge gave them a suspended sentence. I want justice.",
            Category = "FavorRequest"
        };

        Console.WriteLine("\n🎭 Bonasera enters the Don's office...\n");
        await GameTimingOptions.DelayAsync(GameTimingOptions.Current.DialoguePauseMs);
        
        var result = await router.RouteMessageAsync(message);
        
        Console.WriteLine($"\n💬 The Don's Response:\n");
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine(result.Response);
        Console.ResetColor();
        
        await GameTimingOptions.DelayAsync(GameTimingOptions.Current.SceneTransitionMs);
    }

    static async Task Scenario2_TerritoryDispute()
    {
        Console.WriteLine("\n\n" + new string('═', 70));
        Console.WriteLine("📋 SCENARIO 2: Territory Dispute");
        Console.WriteLine(new string('═', 70));
        
        var logger = new ConsoleAgentLogger();
        var router = BuildMafiaOrganization(logger);

        var message = new AgentMessage
        {
            SenderId = "capo-brooklyn",
            Subject = "Tattaglia Family Moving In",
            Content = "Don, the Tattaglias are moving into our territory in Brooklyn. " +
                     "They're running numbers in our neighborhood. This is a dispute that needs your decision.",
            Category = "MajorDispute"
        };

        Console.WriteLine("\n🗺️  A Capo reports a territory incursion...\n");
        await GameTimingOptions.DelayAsync(GameTimingOptions.Current.DialoguePauseMs);
        
        var result = await router.RouteMessageAsync(message);
        
        Console.WriteLine($"\n💬 The Don's Strategic Response:\n");
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine(result.Response);
        Console.ResetColor();
        
        await GameTimingOptions.DelayAsync(GameTimingOptions.Current.SceneTransitionMs);
    }

    static async Task Scenario3_ProtectionRacket()
    {
        Console.WriteLine("\n\n" + new string('═', 70));
        Console.WriteLine("📋 SCENARIO 3: Protection Racket - New Shop");
        Console.WriteLine(new string('═', 70));
        
        var logger = new ConsoleAgentLogger();
        var router = BuildMafiaOrganization(logger);

        var message = new AgentMessage
        {
            SenderId = "local-shopkeeper",
            Subject = "New Restaurant on Mulberry Street",
            Content = "There's a new Italian restaurant that just opened. " +
                     "They need to understand how protection works in our neighborhood.",
            Category = "DailyOperations"
        };

        Console.WriteLine("\n🏪 Report of new business in the territory...\n");
        await GameTimingOptions.DelayAsync(GameTimingOptions.Current.DialoguePauseMs);
        
        var result = await router.RouteMessageAsync(message);
        
        Console.WriteLine($"\n💬 Underboss Response:\n");
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine(result.Response);
        Console.ResetColor();
        
        if (result.ForwardedMessages.Any())
        {
            Console.WriteLine("\n📨 Message forwarded to Capo...");
            await GameTimingOptions.DelayAsync(GameTimingOptions.Current.TypewriterDelayMs);
            
            var capoResult = await router.GetAgent("capo-001")!
                .ProcessMessageAsync(result.ForwardedMessages[0]);
            
            Console.WriteLine($"\n💬 Capo's Plan:\n");
            Console.ForegroundColor = ConsoleColor.Magenta;
            Console.WriteLine(capoResult.Response);
            Console.ResetColor();
        }
        
        await GameTimingOptions.DelayAsync(GameTimingOptions.Current.SceneTransitionMs);
    }

    static async Task Scenario4_HitRequest()
    {
        Console.WriteLine("\n\n" + new string('═', 70));
        Console.WriteLine("📋 SCENARIO 4: Requesting Approval for a Hit");
        Console.WriteLine(new string('═', 70));
        
        var logger = new ConsoleAgentLogger();
        var router = BuildMafiaOrganization(logger);

        // Scenario A: Approved hit
        var message1 = new AgentMessage
        {
            SenderId = "underboss-001",
            Subject = "Problem with Sollozzo",
            Content = "Don, we got a problem. Sollozzo is trying to get us into the drug business. " +
                     "He won't take no for an answer. He's a threat. I'm requesting approval for a hit.",
            Category = "FinalDecision"
        };

        Console.WriteLine("\n💀 Request for sanctioned hit...\n");
        await GameTimingOptions.DelayAsync(GameTimingOptions.Current.DialoguePauseMs);
        
        var result1 = await router.RouteMessageAsync(message1);
        
        Console.WriteLine($"\n💬 The Don's Decision:\n");
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine(result1.Response);
        Console.ResetColor();
        
        await GameTimingOptions.DelayAsync(GameTimingOptions.Current.DramaticPauseMs);
        
        // Scenario B: Denied hit
        Console.WriteLine("\n---\n");
        var message2 = new AgentMessage
        {
            SenderId = "hotheaded-capo",
            Subject = "That Cop Has to Go",
            Content = "Boss, this police captain is shaking us down. He's bleeding us dry. " +
                     "I want to whack him. Send a message to the cops.",
            Category = "FinalDecision"
        };

        Console.WriteLine("💀 Another hit request (targeting law enforcement)...\n");
        await GameTimingOptions.DelayAsync(GameTimingOptions.Current.DialoguePauseMs);
        
        var result2 = await router.RouteMessageAsync(message2);
        
        Console.WriteLine($"\n💬 The Don's Wisdom:\n");
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine(result2.Response);
        Console.ResetColor();
        
        await GameTimingOptions.DelayAsync(GameTimingOptions.Current.SceneTransitionMs);
    }

    static async Task Scenario5_LegalMatters()
    {
        Console.WriteLine("\n\n" + new string('═', 70));
        Console.WriteLine("📋 SCENARIO 5: Legal Counsel Needed");
        Console.WriteLine(new string('═', 70));
        
        var logger = new ConsoleAgentLogger();
        var router = BuildMafiaOrganization(logger);

        var message = new AgentMessage
        {
            SenderId = "underboss-001",
            Subject = "Federal Investigation",
            Content = "Don, the Feds are sniffing around our business operations. " +
                     "They're asking questions about our olive oil import company. " +
                     "We need legal counsel on how to handle this.",
            Category = "FinalDecision"
        };

        Console.WriteLine("\n⚖️  Legal trouble brewing...\n");
        await GameTimingOptions.DelayAsync(GameTimingOptions.Current.DialoguePauseMs);
        
        var result = await router.RouteMessageAsync(message);
        
        Console.WriteLine($"\n💬 The Don's Response:\n");
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine(result.Response);
        Console.ResetColor();
        
        if (result.ForwardedMessages.Any())
        {
            Console.WriteLine("\n📨 Matter referred to the Consigliere...");
            await GameTimingOptions.DelayAsync(GameTimingOptions.Current.TypewriterDelayMs);
            
            var consigliereResult = await router.GetAgent("consigliere-001")!
                .ProcessMessageAsync(result.ForwardedMessages[0]);
            
            Console.WriteLine($"\n💬 Consigliere's Legal Advice:\n");
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine(consigliereResult.Response);
            Console.ResetColor();
        }
        
        await GameTimingOptions.DelayAsync(GameTimingOptions.Current.SceneTransitionMs);
    }

    static async Task Scenario6_CollectionDay()
    {
        Console.WriteLine("\n\n" + new string('═', 70));
        Console.WriteLine("📋 SCENARIO 6: Weekly Collections");
        Console.WriteLine(new string('═', 70));
        
        var logger = new ConsoleAgentLogger();
        var router = BuildMafiaOrganization(logger);

        Console.WriteLine("\n💰 It's collection day in Little Italy...\n");
        await GameTimingOptions.DelayAsync(GameTimingOptions.Current.DialoguePauseMs);

        // Soldier collects from the streets
        var soldierMessage = new AgentMessage
        {
            SenderId = "local-business",
            Subject = "Weekly Collection",
            Content = "Time to collect the weekly payment from the neighborhood.",
            Category = "Enforcement"
        };

        Console.WriteLine("👊 Soldier making rounds...\n");
        var soldierResult = await router.GetAgent("soldier-001")!
            .ProcessMessageAsync(soldierMessage);
        
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine(soldierResult.Response);
        Console.ResetColor();
        
        await GameTimingOptions.DelayAsync(GameTimingOptions.Current.DialoguePauseMs);
        
        // Report up to Capo
        Console.WriteLine("\n---\n");
        Console.WriteLine("💼 Capo totaling the week's take...\n");
        
        var capoMessage = new AgentMessage
        {
            SenderId = "soldier-001",
            Subject = "Week's Collections",
            Content = "Boss, here's the pickup from this week's collections.",
            Category = "ProtectionRacket"
        };

        var capoResult = await router.GetAgent("capo-001")!
            .ProcessMessageAsync(capoMessage);
        
        Console.ForegroundColor = ConsoleColor.Magenta;
        Console.WriteLine(capoResult.Response);
        Console.ResetColor();
        
        await GameTimingOptions.DelayAsync(GameTimingOptions.Current.DialoguePauseMs);
        
        // Report to Underboss
        Console.WriteLine("\n---\n");
        Console.WriteLine("🤵 Underboss receives the weekly report...\n");
        
        var underbossMessage = new AgentMessage
        {
            SenderId = "capo-001",
            Subject = "Weekly Revenue Report",
            Content = "Weekly collection report - payments are on schedule.",
            Category = "Revenue"
        };

        var underbossResult = await router.GetAgent("underboss-001")!
            .ProcessMessageAsync(underbossMessage);
        
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine(underbossResult.Response);
        Console.ResetColor();
        
        await GameTimingOptions.DelayAsync(GameTimingOptions.Current.SceneTransitionMs);
    }

    static async Task Scenario7_ChainOfCommand()
    {
        Console.WriteLine("\n\n" + new string('═', 70));
        Console.WriteLine("📋 SCENARIO 7: Chain of Command");
        Console.WriteLine(new string('═', 70));
        
        var logger = new ConsoleAgentLogger();
        var router = BuildMafiaOrganization(logger);

        Console.WriteLine("\n📊 Demonstrating proper chain of command...\n");
        await GameTimingOptions.DelayAsync(GameTimingOptions.Current.DialoguePauseMs);

        var scenarios = new[]
        {
            ("Street-level enforcement", "Enforcement", "soldier-001"),
            ("Territory management", "ProtectionRacket", "capo-001"),
            ("Daily operations", "DailyOperations", "underboss-001"),
            ("Strategic decisions", "FinalDecision", "godfather-001"),
            ("Legal matters", "Legal", "consigliere-001")
        };

        foreach (var (description, category, expectedAgent) in scenarios)
        {
            var message = new AgentMessage
            {
                SenderId = "tester",
                Subject = description,
                Content = $"Test message for {category}",
                Category = category
            };

            var result = await router.RouteMessageAsync(message);
            
            Console.WriteLine($"  📌 {description,-30} → {message.ReceiverId}");
            await GameTimingOptions.DelayAsync(GameTimingOptions.Current.TypewriterDelayMs);
        }

        Console.WriteLine("\n✅ Chain of command working perfectly!");
        await GameTimingOptions.DelayAsync(GameTimingOptions.Current.SceneTransitionMs);
    }

    static async Task Scenario8_FamilyMeeting()
    {
        Console.WriteLine("\n\n" + new string('═', 70));
        Console.WriteLine("📋 SCENARIO 8: Family Meeting - Major Decision");
        Console.WriteLine(new string('═', 70));
        
        var logger = new ConsoleAgentLogger();
        var router = BuildMafiaOrganization(logger);

        Console.WriteLine("\n🏛️  The Commission is meeting to discuss the future...\n");
        await GameTimingOptions.DelayAsync(GameTimingOptions.Current.DramaticPauseMs);

        var message = new AgentMessage
        {
            SenderId = "other-family",
            Subject = "Proposal: Enter the Drug Trade",
            Content = "Don Corleone, the other families want to get into narcotics. " +
                     "There's enormous profit to be made. " +
                     "We're asking the Corleone family to provide political protection.",
            Category = "FinalDecision"
        };

        Console.WriteLine("💊 Proposal presented to the Don...\n");
        await GameTimingOptions.DelayAsync(GameTimingOptions.Current.DialoguePauseMs);
        
        var result = await router.RouteMessageAsync(message);
        
        Console.WriteLine($"\n💬 The Don Addresses the Commission:\n");
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine(result.Response);
        Console.ResetColor();
        
        await GameTimingOptions.DelayAsync(GameTimingOptions.Current.DialoguePauseMs);
        
        Console.WriteLine("\n---\n");
        Console.WriteLine("🎬 *The Don stands and addresses everyone in the room*\n");
        await GameTimingOptions.DelayAsync(GameTimingOptions.Current.TypewriterDelayMs);
        
        Console.ForegroundColor = ConsoleColor.DarkYellow;
        Console.WriteLine("\"I believe in America. I raised my family in the traditions of our people.\"");
        await GameTimingOptions.DelayAsync(GameTimingOptions.Current.TypewriterDelayMs);
        Console.WriteLine("\"We don't deal in narcotics. That's not our way.\"");
        await GameTimingOptions.DelayAsync(GameTimingOptions.Current.TypewriterDelayMs);
        Console.WriteLine("\"But I'm a superstitious man... and if some unlucky accident should befall\"");
        await GameTimingOptions.DelayAsync(GameTimingOptions.Current.TypewriterDelayMs);
        Console.WriteLine("\"my son... then I will blame the people in this room.\"");
        await GameTimingOptions.DelayAsync(GameTimingOptions.Current.DialoguePauseMs);
        Console.WriteLine("\n*The room falls silent*");
        Console.ResetColor();
        
        await GameTimingOptions.DelayAsync(GameTimingOptions.Current.SceneTransitionMs);
    }

    static AgentRouter BuildMafiaOrganization(IAgentLogger logger)
    {
        var router = new AgentRouterBuilder().WithLogger(logger).Build();

        // Create the family hierarchy
        var godfather = new GodfatherAgent("godfather-001", "Don Vito Corleone", logger);
        var underboss = new UnderbossAgent("underboss-001", "Peter Clemenza", logger);
        var consigliere = new ConsigliereAgent("consigliere-001", "Tom Hagen", logger);
        
        var capo1 = new CapoAgent("capo-001", "Sonny Corleone", logger, 
            new List<string> { "soldier-001", "soldier-002", "soldier-003" });
        
        var soldier1 = new SoldierAgent("soldier-001", "Luca Brasi", logger);
        var soldier2 = new SoldierAgent("soldier-002", "Paulie Gatto", logger);
        
        var associate1 = new AssociateAgent("associate-001", "Frankie Pentangeli", logger);

        // Register all agents
        router.RegisterAgent(godfather);
        router.RegisterAgent(underboss);
        router.RegisterAgent(consigliere);
        router.RegisterAgent(capo1);
        router.RegisterAgent(soldier1);
        router.RegisterAgent(soldier2);
        router.RegisterAgent(associate1);

        // Set up routing rules - chain of command
        
        // The Don handles final decisions, major disputes, and favors
        router.AddRoutingRule(
            "GODFATHER_FINAL",
            "Final Decisions to the Don",
            ctx => ctx.Category == "FinalDecision" || 
                   ctx.Category == "MajorDispute" || 
                   ctx.Category == "FavorRequest",
            "godfather-001",
            priority: 1000
        );

        // Consigliere handles legal and strategic matters
        router.AddRoutingRule(
            "CONSIGLIERE_LEGAL",
            "Legal to Consigliere",
            ctx => ctx.Category == "Legal" || ctx.Category == "Strategy",
            "consigliere-001",
            priority: 900
        );

        // Underboss handles daily operations
        router.AddRoutingRule(
            "UNDERBOSS_OPS",
            "Operations to Underboss",
            ctx => ctx.Category == "DailyOperations" || 
                   ctx.Category == "CrewManagement" || 
                   ctx.Category == "Revenue",
            "underboss-001",
            priority: 800
        );

        // Capos handle their territories
        router.AddRoutingRule(
            "CAPO_TERRITORY",
            "Territory to Capo",
            ctx => ctx.Category == "ProtectionRacket" || 
                   ctx.Category == "LoanSharking" || 
                   ctx.Category == "Gambling",
            "capo-001",
            priority: 700
        );

        // Soldiers handle enforcement
        router.AddRoutingRule(
            "SOLDIER_ENFORCE",
            "Enforcement to Soldiers",
            ctx => ctx.Category == "Enforcement" || ctx.Category == "Hits",
            "soldier-001",
            priority: 600
        );

        // Associates handle small jobs
        router.AddRoutingRule(
            "ASSOCIATE_INFO",
            "Information to Associates",
            ctx => ctx.Category == "Information" || ctx.Category == "SmallJobs",
            "associate-001",
            priority: 500
        );

        return router;
    }
}
