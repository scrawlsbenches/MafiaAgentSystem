namespace RulesEngine.Linq
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    public static class TypeExtensions
    {
        public static Type? GetSequenceElementType(this Type type)
        {
            if (type.IsGenericType)
            {
                var genericDef = type.GetGenericTypeDefinition();
                if (genericDef == typeof(IQueryable<>) ||
                    genericDef == typeof(IEnumerable<>) ||
                    genericDef == typeof(IOrderedQueryable<>) ||
                    genericDef == typeof(IOrderedEnumerable<>))
                {
                    return type.GetGenericArguments()[0];
                }
            }

            var enumerable = type.GetInterfaces()
                .FirstOrDefault(i => i.IsGenericType &&
                    i.GetGenericTypeDefinition() == typeof(IEnumerable<>));

            return enumerable?.GetGenericArguments()[0];
        }
    }

    public static class RulesContextExtensions
    {
        public static IRuleSet<T> Rules<T>(this IRulesContext context) where T : class
            => context.GetRuleSet<T>();

        public static IEvaluationResult EvaluateAll<T>(
            this IRulesContext context,
            IEnumerable<T> facts) where T : class
        {
            using var session = context.CreateSession();
            session.InsertAll(facts);
            var result = session.Evaluate();
            session.Commit();
            return result;
        }

        public static IEvaluationResult EvaluateOne<T>(
            this IRulesContext context,
            T fact) where T : class
        {
            using var session = context.CreateSession();
            session.Insert(fact);
            var result = session.Evaluate();
            session.Commit();
            return result;
        }
    }

    public static class RuleSetExtensions
    {
        public static IRuleSet<T> WithRule<T>(this IRuleSet<T> ruleSet, IRule<T> rule) where T : class
        {
            ruleSet.Add(rule);
            return ruleSet;
        }

        public static IRuleSet<T> WithRules<T>(this IRuleSet<T> ruleSet, params IRule<T>[] rules) where T : class
        {
            ruleSet.AddRange(rules);
            return ruleSet;
        }

        /// <summary>
        /// Returns all rules that would match the given fact without executing their actions.
        /// Useful for previewing/debugging which rules apply.
        /// </summary>
        public static IEnumerable<IRule<T>> WouldMatch<T>(this IRuleSet<T> ruleSet, T fact) where T : class
        {
            if (fact == null) yield break;

            foreach (var rule in ruleSet.OrderByDescending(r => r.Priority))
            {
                if (rule.Evaluate(fact))
                {
                    yield return rule;
                }
            }
        }

        /// <summary>
        /// Returns all rules that would match the given fact, as a queryable.
        /// </summary>
        public static IQueryable<IRule<T>> WouldMatchQuery<T>(this IRuleSet<T> ruleSet, T fact) where T : class
        {
            return ruleSet.WouldMatch(fact).AsQueryable();
        }
    }

    public static class SessionExtensions
    {
        public static IRuleSession WithFact<T>(this IRuleSession session, T fact) where T : class
        {
            session.Insert(fact);
            return session;
        }

        public static IRuleSession WithFacts<T>(this IRuleSession session, IEnumerable<T> facts) where T : class
        {
            session.InsertAll(facts);
            return session;
        }
    }
}
