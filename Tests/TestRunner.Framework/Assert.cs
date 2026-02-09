using System.Diagnostics.CodeAnalysis;

namespace TestRunner.Framework;

/// <summary>
/// Simple assertion class for testing without external dependencies.
/// </summary>
public static class Assert
{
    public static void True(bool condition, string? message = null)
    {
        if (!condition)
            throw new AssertionException(message ?? "Expected true but was false");
    }

    public static void False(bool condition, string? message = null)
    {
        if (condition)
            throw new AssertionException(message ?? "Expected false but was true");
    }

    public static void Equal<T>(T expected, T actual, string? message = null)
    {
        if (!Equals(expected, actual))
            throw new AssertionException(message ?? $"Expected '{expected}' but was '{actual}'");
    }

    public static void NotEqual<T>(T expected, T actual, string? message = null)
    {
        if (Equals(expected, actual))
            throw new AssertionException(message ?? $"Expected values to differ but both were '{expected}'");
    }

    public static void NotNull<T>([NotNull] T? value, string? message = null) where T : class
    {
        if (value is null)
            throw new AssertionException(message ?? "Expected non-null value but was null");
    }

    public static void Null<T>(T? value, string? message = null) where T : class
    {
        if (value is not null)
            throw new AssertionException(message ?? $"Expected null but was '{value}'");
    }

    public static void Contains(string expected, [NotNull] string? actual, StringComparison comparison = StringComparison.Ordinal)
    {
        if (actual is null)
            throw new AssertionException($"Expected string to contain '{expected}' but was null");
        if (!actual.Contains(expected, comparison))
            throw new AssertionException($"Expected string to contain '{expected}' but was '{actual}'");
    }

    public static void Contains<T>(T expected, IEnumerable<T> collection)
    {
        if (!collection.Contains(expected))
            throw new AssertionException($"Expected collection to contain '{expected}'");
    }

    public static void Contains<T>(IEnumerable<T> collection, Func<T, bool> predicate)
    {
        if (!collection.Any(predicate))
            throw new AssertionException("Expected collection to contain matching element");
    }

    public static void DoesNotContain<T>(T expected, IEnumerable<T> collection)
    {
        if (collection.Contains(expected))
            throw new AssertionException($"Expected collection to not contain '{expected}'");
    }

    public static void Empty<T>(IEnumerable<T> collection)
    {
        if (collection.Any())
            throw new AssertionException($"Expected empty collection but had {collection.Count()} elements");
    }

    public static void NotEmpty<T>(IEnumerable<T> collection)
    {
        if (!collection.Any())
            throw new AssertionException("Expected non-empty collection but was empty");
    }

    public static T Single<T>(IEnumerable<T> collection)
    {
        var list = collection.ToList();
        if (list.Count == 0)
            throw new AssertionException("Expected single element but collection was empty");
        if (list.Count > 1)
            throw new AssertionException($"Expected single element but collection had {list.Count} elements");
        return list[0];
    }

    public static void All<T>(IEnumerable<T> collection, Action<T> assertion)
    {
        var index = 0;
        foreach (var item in collection)
        {
            try
            {
                assertion(item);
            }
            catch (Exception ex)
            {
                throw new AssertionException($"Assertion failed for item at index {index}: {ex.Message}");
            }
            index++;
        }
    }

    public static TException Throws<TException>(Action action) where TException : Exception
    {
        try
        {
            action();
        }
        catch (TException ex)
        {
            return ex;
        }
        catch (Exception ex)
        {
            throw new AssertionException($"Expected {typeof(TException).Name} but got {ex.GetType().Name}: {ex.Message}");
        }

        throw new AssertionException($"Expected {typeof(TException).Name} but no exception was thrown");
    }

    public static async Task<TException> ThrowsAsync<TException>(Func<Task> action) where TException : Exception
    {
        try
        {
            await action();
        }
        catch (TException ex)
        {
            return ex;
        }
        catch (Exception ex)
        {
            throw new AssertionException($"Expected {typeof(TException).Name} but got {ex.GetType().Name}: {ex.Message}");
        }

        throw new AssertionException($"Expected {typeof(TException).Name} but no exception was thrown");
    }

    public static void Same(object? expected, object? actual)
    {
        if (!ReferenceEquals(expected, actual))
            throw new AssertionException("Expected same reference but objects were different");
    }

    public static void NotSame(object? expected, object? actual)
    {
        if (ReferenceEquals(expected, actual))
            throw new AssertionException("Expected different references but objects were the same");
    }

    public static void IsType<T>(object? obj)
    {
        if (obj is not T)
            throw new AssertionException($"Expected type {typeof(T).Name} but was {obj?.GetType().Name ?? "null"}");
    }

    public static void InRange<T>(T actual, T low, T high) where T : IComparable<T>
    {
        if (actual.CompareTo(low) < 0 || actual.CompareTo(high) > 0)
            throw new AssertionException($"Expected value in range [{low}, {high}] but was {actual}");
    }
}

/// <summary>
/// Exception thrown when an assertion fails.
/// </summary>
public class AssertionException : Exception
{
    public AssertionException(string message) : base(message) { }
}
