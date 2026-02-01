using AgentRouting.MafiaDemo.AI;
using AgentRouting.MafiaDemo.Missions;
using AgentRouting.MafiaDemo.Game;
using AgentRouting.Core;
using AgentRouting.Middleware;
using AgentRouting.MafiaDemo;

namespace AgentRouting.MafiaDemo.Autonomous;

class AutonomousPlaythroughDemo
{
    public static async Task RunAsync()
    {
        Console.Clear();
        PrintTitle();
        
        Console.WriteLine("Watch as an AI agent plays through the entire game autonomously!");
        Console.WriteLine("The agent uses the RULES ENGINE to make all decisions.\n");
        
        Console.Write("Enter character name (or press Enter for 'Vinny Rossi'): ");
        var name = Console.ReadLine();
        if (string.IsNullOrWhiteSpace(name))
            name = "Vinny Rossi";
        
        Console.Write("\nChoose personality preset (or press Enter for random):\n");
        Console.WriteLine("  1. Ambitious & Reckless");
        Console.WriteLine("  2. Loyal & Cautious");
        Console.WriteLine("  3. Ruthless & Calculating");
        Console.WriteLine("  4. Random\n");
        Console.Write("Choice: ");
        
        var choice = Console.ReadLine();
        var personality = choice switch
        {
            "1" => new PlayerPersonality { Ambition = 85, Loyalty = 60, Ruthlessness = 75, Caution = 25 },
            "2" => new PlayerPersonality { Ambition = 50, Loyalty = 90, Ruthlessness = 30, Caution = 80 },
            "3" => new PlayerPersonality { Ambition = 75, Loyalty = 50, Ruthlessness = 90, Caution = 60 },
            _ => null // Random
        };
        
        Console.Write("\nHow many weeks to simulate (1-52, or Enter for 52): ");
        var weeksInput = Console.ReadLine();
        var maxWeeks = int.TryParse(weeksInput, out var w) ? w : 52;
        
        Console.Write("\nPlayback speed (1=fast, 2=medium, 3=slow, or Enter for medium): ");
        var speedInput = Console.ReadLine();
        var delay = speedInput switch
        {
            "1" => 500,
            "3" => 2000,
            _ => 1000
        };
        
        Console.WriteLine("\n\nStarting autonomous playthrough...\n");
        await Task.Delay(1500);
        
        // === NEW: Setup AgentRouter with Middleware! ===
        var logger = new ConsoleAgentLogger();
        var router = new MiddlewareAgentRouter(logger);
        
        // Register actual mafia agents
        router.RegisterAgent(new GodfatherAgent("godfather-001", "Don Vito Corleone", logger));
        router.RegisterAgent(new UnderbossAgent("underboss-001", "Peter Clemenza", logger));
        router.RegisterAgent(new ConsigliereAgent("consigliere-001", "Tom Hagen", logger));
        router.RegisterAgent(new CapoAgent("capo-001", "Sonny Corleone", logger, new List<string> { "soldier-001" }));
        router.RegisterAgent(new SoldierAgent("soldier-001", "Luca Brasi", logger));
        
        // Setup routing rules
        router.AddRoutingRule("GODFATHER_FINAL", "Final Decisions",
            ctx => ctx.Category == "Hit" || ctx.Priority == MessagePriority.Urgent, 
            "godfather-001", 1000);
        router.AddRoutingRule("CONSIGLIERE_LEGAL", "Legal Matters",
            ctx => ctx.Category == "Negotiation", 
            "consigliere-001", 900);
        router.AddRoutingRule("UNDERBOSS_OPS", "Operations",
            ctx => ctx.Category == "Territory" || ctx.Category == "Recruitment", 
            "underboss-001", 800);
        router.AddRoutingRule("CAPO_TERRITORY", "Territory Management",
            ctx => ctx.Category == "Collection" || ctx.Category == "Intimidation", 
            "capo-001", 700);
        router.AddRoutingRule("SOLDIER_ENFORCE", "Enforcement",
            ctx => ctx.Category == "Information", 
            "soldier-001", 600);
        
        // Add middleware to the pipeline!
        router.UseMiddleware(new LoggingMiddleware(logger));
        router.UseMiddleware(new TimingMiddleware());
        router.UseMiddleware(new ValidationMiddleware());
        router.UseMiddleware(new MetricsMiddleware());
        
        Console.WriteLine("✓ AgentRouter configured with middleware pipeline");
        Console.WriteLine("✓ 5 mafia agents registered");
        Console.WriteLine("✓ Middleware: Logging, Timing, Validation, Metrics");
        Console.WriteLine();
        await Task.Delay(1000);
        
        // Create player agent WITH ROUTER
        var player = new PlayerAgent(name, personality, router);
        var gameState = new GameState(); // From existing game
        
        Console.WriteLine(player.GetSummary());
        await Task.Delay(delay * 2);
        
        // Track statistics
        var stats = new PlaythroughStats();
        
        // Main game loop
        for (int week = 1; week <= maxWeeks; week++)
        {
            Console.WriteLine($"\n╔═══ WEEK {week} ═══════════════════════════════════════════════════╗");
            await Task.Delay(delay / 2);
            
            var weekResult = await player.ProcessWeekAsync(gameState);
            
            // Display mission
            PrintMission(weekResult.GeneratedMission);
            await Task.Delay(delay);
            
            // Display decision
            PrintDecision(weekResult.Decision);
            await Task.Delay(delay);
            
            // If accepted, show execution
            if (weekResult.Decision.Accept && weekResult.ExecutionResult != null)
            {
                PrintExecution(weekResult.ExecutionResult);
                await Task.Delay(delay);
                
                // Track stats
                stats.TotalMissions++;
                if (weekResult.ExecutionResult.MissionResult.Success)
                    stats.SuccessfulMissions++;
                else
                    stats.FailedMissions++;
                
                stats.TotalMoneyEarned += weekResult.ExecutionResult.MissionResult.MoneyGained;
                stats.TotalRespectGained += weekResult.ExecutionResult.MissionResult.RespectGained;
            }
            else
            {
                stats.MissionsRejected++;
                Console.WriteLine();
            }
            
            // Show current stats
            PrintCurrentStatus(player.Character);
            await Task.Delay(delay / 2);
            
            // Check for promotion
            if (weekResult.ExecutionResult?.MissionResult != null)
            {
                var oldRank = GetPreviousRank(player.Character.Rank, player.Character.Achievements);
                if (oldRank != player.Character.Rank)
                {
                    PrintPromotion(oldRank, player.Character.Rank);
                    await Task.Delay(delay * 3);
                }
            }
            
            // Check for game over conditions
            if (player.Character.Respect <= 0)
            {
                PrintGameOver("Lost all respect - betrayed by the family!");
                break;
            }
            
            if (player.Character.Heat >= 100)
            {
                PrintGameOver("Too much heat - arrested by the Feds!");
                break;
            }
            
            if (player.Character.Rank == PlayerRank.Don)
            {
                PrintVictory(player.Character, stats);
                break;
            }
            
            Console.WriteLine("╚════════════════════════════════════════════════════════════════╝");
        }
        
        // Final summary
        if (player.Character.Rank != PlayerRank.Don && 
            player.Character.Respect > 0 && 
            player.Character.Heat < 100)
        {
            PrintFinalSummary(player.Character, stats);
        }
        
        Console.WriteLine("\n\nPress any key to exit...");
        Console.ReadKey();
    }
    
    private static void PrintTitle()
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine(@"
╔══════════════════════════════════════════════════════════════════╗
║                                                                  ║
║           AUTONOMOUS PLAYTHROUGH - AI PLAYS THE GAME             ║
║                                                                  ║
║         Rules Engine Makes All Decisions - Watch It Play!        ║
║                                                                  ║
╚══════════════════════════════════════════════════════════════════╝
");
        Console.ResetColor();
    }
    
    private static void PrintMission(Mission mission)
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine($"║");
        Console.WriteLine($"║ 📋 NEW MISSION: {mission.Title}");
        Console.ResetColor();
        Console.WriteLine($"║    {mission.Description}");
        Console.WriteLine($"║");
        Console.WriteLine($"║    Type: {mission.Type}");
        Console.WriteLine($"║    Assigned by: {mission.AssignedBy}");
        Console.WriteLine($"║    Risk Level: {new string('▓', mission.RiskLevel)}{new string('░', 10 - mission.RiskLevel)} ({mission.RiskLevel}/10)");
        Console.WriteLine($"║    Reward: +{mission.RespectReward} Respect, ${mission.MoneyReward:N0}");
        Console.WriteLine($"║    Heat: +{mission.HeatGenerated}");
        
        if (mission.SkillRequirements.Any())
        {
            Console.WriteLine($"║    Skills needed: {string.Join(", ", mission.SkillRequirements.Select(s => $"{s.Key}:{s.Value}"))}");
        }
    }
    
    private static void PrintDecision(MissionDecision decision)
    {
        Console.WriteLine($"║");
        if (decision.Accept)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"║ ✓ DECISION: ACCEPT");
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"║ ✗ DECISION: REJECT");
        }
        Console.ResetColor();
        Console.WriteLine($"║    Reason: {decision.Reason}");
        Console.WriteLine($"║    Rule Matched: {decision.RuleMatched}");
        Console.WriteLine($"║    Confidence: {decision.Confidence}%");
    }
    
    private static void PrintExecution(MissionExecutionResult result)
    {
        var missionResult = result.MissionResult;
        
        Console.WriteLine($"║");
        if (missionResult.Success)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"║ ★ MISSION SUCCESS!");
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"║ ✗ MISSION FAILED");
        }
        Console.ResetColor();
        Console.WriteLine($"║    {missionResult.Message}");
        Console.WriteLine($"║");
        Console.WriteLine($"║    Respect: {(missionResult.RespectGained >= 0 ? "+" : "")}{missionResult.RespectGained}");
        Console.WriteLine($"║    Money: {(missionResult.MoneyGained >= 0 ? "+" : "")}{missionResult.MoneyGained:C0}");
        Console.WriteLine($"║    Heat: +{missionResult.HeatGained}");
        
        if (result.NewSkills.Any())
        {
            Console.WriteLine($"║    Skills Improved: {string.Join(", ", result.NewSkills.Select(s => $"{s.Key} +{s.Value}"))}");
        }
    }
    
    private static void PrintCurrentStatus(PlayerCharacter character)
    {
        Console.WriteLine($"║");
        Console.ForegroundColor = ConsoleColor.White;
        Console.WriteLine($"║ 📊 CURRENT STATUS:");
        Console.ResetColor();
        Console.WriteLine($"║    Rank: {character.Rank}");
        Console.WriteLine($"║    Respect: {GetBar(character.Respect, 100)} {character.Respect}/100");
        Console.WriteLine($"║    Money: ${character.Money:N0}");
        Console.WriteLine($"║    Heat: {GetBar(character.Heat, 100)} {character.Heat}/100");
    }
    
    private static void PrintPromotion(PlayerRank oldRank, PlayerRank newRank)
    {
        Console.WriteLine();
        Console.WriteLine("╔════════════════════════════════════════════════════════════════╗");
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("║                                                                ║");
        Console.WriteLine($"║                    🎉 PROMOTED! 🎉                             ║");
        Console.WriteLine("║                                                                ║");
        Console.WriteLine($"║              {oldRank} → {newRank}                    ║");
        Console.WriteLine("║                                                                ║");
        Console.ResetColor();
        Console.WriteLine("╚════════════════════════════════════════════════════════════════╝");
        Console.WriteLine();
    }
    
    private static void PrintVictory(PlayerCharacter character, PlaythroughStats stats)
    {
        Console.WriteLine();
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("╔════════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║                                                                ║");
        Console.WriteLine("║                    👑 VICTORY! 👑                               ║");
        Console.WriteLine("║                                                                ║");
        Console.WriteLine($"║         {character.Name} is now the DON!                ");
        Console.WriteLine("║                                                                ║");
        Console.WriteLine("║              The family is yours to command!                   ║");
        Console.WriteLine("║                                                                ║");
        Console.WriteLine("╚════════════════════════════════════════════════════════════════╝");
        Console.ResetColor();
        
        Console.WriteLine();
        Console.WriteLine("FINAL STATISTICS:");
        Console.WriteLine($"  Time to become Don: {character.Week} weeks");
        Console.WriteLine($"  Missions Completed: {stats.SuccessfulMissions}/{stats.TotalMissions}");
        Console.WriteLine($"  Success Rate: {(stats.TotalMissions > 0 ? (stats.SuccessfulMissions * 100.0 / stats.TotalMissions) : 0):F1}%");
        Console.WriteLine($"  Total Money Earned: ${stats.TotalMoneyEarned:N0}");
        Console.WriteLine($"  Final Respect: {character.Respect}/100");
        Console.WriteLine($"  Achievements: {character.Achievements.Count}");
        
        Console.WriteLine();
        Console.WriteLine("ACHIEVEMENTS:");
        foreach (var achievement in character.Achievements)
        {
            Console.WriteLine($"  ⭐ {achievement}");
        }
    }
    
    private static void PrintGameOver(string reason)
    {
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine("╔════════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║                                                                ║");
        Console.WriteLine("║                    💀 GAME OVER 💀                             ║");
        Console.WriteLine("║                                                                ║");
        Console.WriteLine($"║         {reason.PadRight(56)}║");
        Console.WriteLine("║                                                                ║");
        Console.WriteLine("╚════════════════════════════════════════════════════════════════╝");
        Console.ResetColor();
    }
    
    private static void PrintFinalSummary(PlayerCharacter character, PlaythroughStats stats)
    {
        Console.WriteLine();
        Console.WriteLine("╔════════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║                    PLAYTHROUGH COMPLETE                        ║");
        Console.WriteLine("╚════════════════════════════════════════════════════════════════╝");
        Console.WriteLine();
        Console.WriteLine($"Final Rank: {character.Rank}");
        Console.WriteLine($"Final Respect: {character.Respect}/100");
        Console.WriteLine($"Final Money: ${character.Money:N0}");
        Console.WriteLine($"Final Heat: {character.Heat}/100");
        Console.WriteLine();
        Console.WriteLine("STATISTICS:");
        Console.WriteLine($"  Weeks Played: {character.Week}");
        Console.WriteLine($"  Missions Accepted: {stats.TotalMissions}");
        Console.WriteLine($"  Missions Rejected: {stats.MissionsRejected}");
        Console.WriteLine($"  Successful: {stats.SuccessfulMissions}");
        Console.WriteLine($"  Failed: {stats.FailedMissions}");
        Console.WriteLine($"  Success Rate: {(stats.TotalMissions > 0 ? (stats.SuccessfulMissions * 100.0 / stats.TotalMissions) : 0):F1}%");
        Console.WriteLine($"  Total Money Earned: ${stats.TotalMoneyEarned:N0}");
        Console.WriteLine($"  Total Respect Gained: +{stats.TotalRespectGained}");
    }
    
    private static string GetBar(int value, int max)
    {
        var percentage = (int)((value / (double)max) * 20);
        return $"[{new string('■', Math.Max(0, percentage))}{new string('□', Math.Max(0, 20 - percentage))}]";
    }
    
    private static PlayerRank GetPreviousRank(PlayerRank currentRank, List<string> achievements)
    {
        // Check most recent achievement for promotion
        var lastAchievement = achievements.LastOrDefault();
        if (lastAchievement != null && lastAchievement.Contains("Promoted to"))
        {
            return currentRank switch
            {
                PlayerRank.Soldier => PlayerRank.Associate,
                PlayerRank.Capo => PlayerRank.Soldier,
                PlayerRank.Underboss => PlayerRank.Capo,
                PlayerRank.Don => PlayerRank.Underboss,
                _ => currentRank
            };
        }
        return currentRank;
    }
}

class PlaythroughStats
{
    public int TotalMissions { get; set; }
    public int SuccessfulMissions { get; set; }
    public int FailedMissions { get; set; }
    public int MissionsRejected { get; set; }
    public decimal TotalMoneyEarned { get; set; }
    public int TotalRespectGained { get; set; }
}
