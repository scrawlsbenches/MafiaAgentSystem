// Story System - Conversation Rules Setup
// Rules for how agents respond to questions

using RulesEngine.Core;

namespace AgentRouting.MafiaDemo.Story;

/// <summary>
/// Sets up all conversation decision rules using the RulesEngine.
/// Rules are evaluated in priority order (highest first).
///
/// RULE CATEGORIES:
/// 1. Refusal rules (100-199) - When to refuse to answer
/// 2. Lie rules (200-299) - When to lie
/// 3. Bargain rules (300-399) - When to negotiate
/// 4. Honesty rules (400-499) - When to be truthful
/// 5. Style rules (500-599) - How to phrase the response
/// </summary>
public static class ConversationRulesSetup
{
    public static RulesEngineCore<ConversationContext> CreateEngine()
    {
        var engine = new RulesEngineCore<ConversationContext>();

        // ========================================
        // REFUSAL RULES (Priority 100-199)
        // ========================================

        engine.AddRule(
            "REFUSE_ENEMY_TRUST_QUESTION",
            "Enemy Asks About Trust",
            ctx => ctx.IsEnemy && ctx.QuestionType == QuestionType.CanWeTrust,
            ctx => {
                ctx.Decision.WillAnswer = false;
                ctx.Decision.RefusalReason = "I don't discuss such matters with you.";
                ctx.Decision.MatchedRule = "REFUSE_ENEMY_TRUST_QUESTION";
            },
            priority: 150);

        engine.AddRule(
            "REFUSE_PROUD_DEMANDS",
            "Proud Persona Refuses Demands",
            ctx => ctx.IsProud && ctx.Urgency == QuestionUrgency.Critical && !ctx.IsCloseFriend,
            ctx => {
                ctx.Decision.WillAnswer = false;
                ctx.Decision.RefusalReason = "Don't presume to demand answers from me.";
                ctx.Decision.MatchedRule = "REFUSE_PROUD_DEMANDS";
            },
            priority: 140);

        engine.AddRule(
            "REFUSE_CAUTIOUS_DANGEROUS_INFO",
            "Cautious Persona Protects Dangerous Info",
            ctx => ctx.IsCautious && ctx.IsSensitive &&
                   (ctx.QuestionType == QuestionType.WhoControls ||
                    ctx.QuestionType == QuestionType.WhereDoYouStand),
            ctx => {
                ctx.Decision.WillAnswer = false;
                ctx.Decision.RefusalReason = "Some things are better left unsaid.";
                ctx.Decision.MatchedRule = "REFUSE_CAUTIOUS_DANGEROUS_INFO";
            },
            priority: 130);

        engine.AddRule(
            "REFUSE_STRANGER_HELP",
            "Won't Help Strangers/Enemies",
            ctx => ctx.RelationshipWithAsker < 0 && ctx.QuestionType == QuestionType.WillYouHelp,
            ctx => {
                ctx.Decision.WillAnswer = false;
                ctx.Decision.RefusalReason = "Why would I help you?";
                ctx.Decision.MatchedRule = "REFUSE_STRANGER_HELP";
            },
            priority: 120);

        engine.AddRule(
            "REFUSE_PROTECTING_ALLY",
            "Protect Ally from Enemy Questions",
            ctx => ctx.IsProtectingSubject && ctx.IsEnemy,
            ctx => {
                ctx.Decision.WillAnswer = false;
                ctx.Decision.RefusalReason = "I have nothing to say about that.";
                ctx.Decision.MatchedRule = "REFUSE_PROTECTING_ALLY";
            },
            priority: 110);

        // ========================================
        // LIE RULES (Priority 200-299)
        // ========================================

        engine.AddRule(
            "LIE_CUNNING_STRATEGIC",
            "Cunning Persona Lies Strategically",
            ctx => ctx.IsCunning && ctx.Honesty < 40 && ctx.HasRelevantMemories && !ctx.IsFriend,
            ctx => {
                ctx.Decision.WillLie = true;
                ctx.Decision.LieReason = "strategic_advantage";
                ctx.Decision.MatchedRule = "LIE_CUNNING_STRATEGIC";
            },
            priority: 250);

        engine.AddRule(
            "LIE_ENEMY_INFO_REQUEST",
            "Lie to Enemy Seeking Information",
            ctx => ctx.IsEnemy && ctx.QuestionType == QuestionType.WhatDoYouKnow &&
                   ctx.HasRelevantMemories && ctx.Honesty < 70,
            ctx => {
                ctx.Decision.WillLie = true;
                ctx.Decision.LieReason = "distrust";
                ctx.Decision.MatchedRule = "LIE_ENEMY_INFO_REQUEST";
            },
            priority: 240);

        engine.AddRule(
            "LIE_PROTECT_ALLY",
            "Lie to Protect Loyal Ally",
            ctx => ctx.IsLoyal && ctx.IsProtectingSubject && ctx.HasRelevantMemories,
            ctx => {
                ctx.Decision.WillLie = true;
                ctx.Decision.LieReason = "protecting_ally";
                ctx.Decision.MatchedRule = "LIE_PROTECT_ALLY";
            },
            priority: 230);

        engine.AddRule(
            "LIE_LOW_HONESTY_STRANGER",
            "Dishonest Persona Lies to Strangers",
            ctx => ctx.Honesty < 30 && ctx.IsStranger && ctx.HasRelevantMemories,
            ctx => {
                ctx.Decision.WillLie = true;
                ctx.Decision.LieReason = "habitual";
                ctx.Decision.MatchedRule = "LIE_LOW_HONESTY_STRANGER";
            },
            priority: 220);

        engine.AddRule(
            "LIE_ABOUT_SECRETS",
            "Lie About Secrets to Non-Friends",
            ctx => ctx.HasSecretMemories && !ctx.IsFriend,
            ctx => {
                ctx.Decision.WillLie = true;
                ctx.Decision.LieReason = "protecting_secrets";
                ctx.Decision.MatchedRule = "LIE_ABOUT_SECRETS";
            },
            priority: 210);

        // ========================================
        // BARGAIN RULES (Priority 300-399)
        // ========================================

        engine.AddRule(
            "BARGAIN_CUNNING_VALUABLE_INFO",
            "Cunning Persona Bargains with Valuable Info",
            ctx => ctx.IsCunning && ctx.HasSecretMemories && ctx.IsStranger,
            ctx => {
                ctx.Decision.WillBargain = true;
                ctx.Decision.ForcedResponseType = ResponseType.Bargain;
                ctx.Decision.CustomResponse = "That information has value. What's it worth to you?";
                ctx.Decision.MatchedRule = "BARGAIN_CUNNING_VALUABLE_INFO";
            },
            priority: 350);

        engine.AddRule(
            "BARGAIN_ENEMY_NEEDS_HELP",
            "Bargain When Enemy Needs Help",
            ctx => ctx.IsEnemy && ctx.QuestionType == QuestionType.WillYouHelp && ctx.Honesty > 50,
            ctx => {
                ctx.Decision.WillBargain = true;
                ctx.Decision.ForcedResponseType = ResponseType.Bargain;
                ctx.Decision.CustomResponse = "Maybe. But you'll owe me.";
                ctx.Decision.MatchedRule = "BARGAIN_ENEMY_NEEDS_HELP";
            },
            priority: 340);

        // ========================================
        // HONESTY RULES (Priority 400-499)
        // ========================================

        engine.AddRule(
            "HONEST_CLOSE_FRIEND",
            "Always Honest with Close Friends",
            ctx => ctx.IsCloseFriend && ctx.HasRelevantMemories,
            ctx => {
                ctx.Decision.WillAnswer = true;
                ctx.Decision.WillLie = false;
                ctx.Decision.RelationshipModifier = 5;
                ctx.Decision.MatchedRule = "HONEST_CLOSE_FRIEND";
            },
            priority: 490);

        engine.AddRule(
            "HONEST_HIGH_INTEGRITY",
            "High Honesty Persona Tells Truth",
            ctx => ctx.Honesty > 80 && ctx.HasRelevantMemories,
            ctx => {
                ctx.Decision.WillAnswer = true;
                ctx.Decision.WillLie = false;
                ctx.Decision.MatchedRule = "HONEST_HIGH_INTEGRITY";
            },
            priority: 480);

        engine.AddRule(
            "HONEST_TRUSTING_FRIEND",
            "Trusting Persona Honest with Friends",
            ctx => ctx.IsTrusting && ctx.IsFriend && ctx.HasRelevantMemories,
            ctx => {
                ctx.Decision.WillAnswer = true;
                ctx.Decision.WillLie = false;
                ctx.Decision.RelationshipModifier = 3;
                ctx.Decision.MatchedRule = "HONEST_TRUSTING_FRIEND";
            },
            priority: 470);

        engine.AddRule(
            "HONEST_DEFAULT",
            "Default Honest Response",
            ctx => ctx.HasRelevantMemories && !ctx.Decision.WillLie,
            ctx => {
                ctx.Decision.WillAnswer = true;
                ctx.Decision.MatchedRule = "HONEST_DEFAULT";
            },
            priority: 400);

        // ========================================
        // UNKNOWN INFO RULES (Priority 300-350)
        // ========================================

        engine.AddRule(
            "UNKNOWN_REDIRECT_HELPFUL",
            "Helpful Redirect When Unknown",
            ctx => !ctx.HasRelevantMemories && ctx.IsFriend && ctx.Persona.Empathy > 50,
            ctx => {
                ctx.Decision.WillAnswer = true;
                ctx.Decision.ForcedResponseType = ResponseType.Redirect;
                ctx.Decision.CustomResponse = "I don't know, but you might ask around the docks.";
                ctx.Decision.MatchedRule = "UNKNOWN_REDIRECT_HELPFUL";
            },
            priority: 320);

        engine.AddRule(
            "UNKNOWN_BLUNT",
            "Blunt Unknown Response",
            ctx => !ctx.HasRelevantMemories && ctx.Persona.Style == CommunicationStyle.Blunt,
            ctx => {
                ctx.Decision.WillAnswer = true;
                ctx.Decision.ForcedResponseType = ResponseType.Partial;
                ctx.Decision.CustomResponse = "Don't know.";
                ctx.Decision.MatchedRule = "UNKNOWN_BLUNT";
            },
            priority: 310);

        return engine;
    }
}
