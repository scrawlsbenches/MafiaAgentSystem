using System;
using System.Collections.Generic;
using System.Threading;
using AgentRouting.Infrastructure;
using TestRunner.Framework;

namespace TestRunner.Tests;

// Test helper class for state store tests
public class StoredPerson
{
    public string Name { get; set; } = string.Empty;
    public int Age { get; set; }
}

[TestClass]
public class StateStoreGetTests
{
    [Test]
    public void Get_ReturnsValue_WhenKeyExists()
    {
        var store = new InMemoryStateStore();
        store.Set("key1", "value1");

        var result = store.Get<string>("key1");

        Assert.Equal("value1", result);
    }

    [Test]
    public void Get_ReturnsDefault_WhenKeyDoesNotExist()
    {
        var store = new InMemoryStateStore();

        var result = store.Get<string>("nonexistent");

        Assert.Null(result);
    }

    [Test]
    public void Get_ReturnsDefaultInt_WhenKeyDoesNotExist()
    {
        var store = new InMemoryStateStore();

        var result = store.Get<int>("nonexistent");

        Assert.Equal(0, result);
    }

    [Test]
    public void Get_ReturnsDefault_WhenTypeMismatch()
    {
        var store = new InMemoryStateStore();
        store.Set("key1", "string_value");

        var result = store.Get<int>("key1");

        Assert.Equal(0, result);
    }

    [Test]
    public void Get_ReturnsNull_WhenTypeMismatch_ForReferenceType()
    {
        var store = new InMemoryStateStore();
        store.Set("key1", 42);

        var result = store.Get<string>("key1");

        Assert.Null(result);
    }

    [Test]
    public void Get_ReturnsInteger_WhenKeyExistsWithIntValue()
    {
        var store = new InMemoryStateStore();
        store.Set("count", 42);

        var result = store.Get<int>("count");

        Assert.Equal(42, result);
    }

    [Test]
    public void Get_ReturnsCustomObject_WhenKeyExists()
    {
        var store = new InMemoryStateStore();
        var person = new StoredPerson { Name = "Alice", Age = 30 };
        store.Set("person", person);

        var result = store.Get<StoredPerson>("person");

        Assert.NotNull(result);
        Assert.Equal("Alice", result!.Name);
        Assert.Equal(30, result.Age);
    }

    [Test]
    public void Get_ReturnsList_WhenKeyExists()
    {
        var store = new InMemoryStateStore();
        var list = new List<string> { "a", "b", "c" };
        store.Set("mylist", list);

        var result = store.Get<List<string>>("mylist");

        Assert.NotNull(result);
        Assert.Equal(3, result!.Count);
        Assert.Equal("a", result[0]);
    }
}

[TestClass]
public class StateStoreSetTests
{
    [Test]
    public void Set_StoresValue_Successfully()
    {
        var store = new InMemoryStateStore();

        store.Set("key1", "value1");

        Assert.Equal("value1", store.Get<string>("key1"));
    }

    [Test]
    public void Set_OverwritesExistingValue()
    {
        var store = new InMemoryStateStore();
        store.Set("key1", "original");

        store.Set("key1", "updated");

        Assert.Equal("updated", store.Get<string>("key1"));
    }

    [Test]
    public void Set_WithNullValue_DoesNotStore()
    {
        var store = new InMemoryStateStore();
        store.Set("key1", "original");

        store.Set<string>("key1", null!);

        // Original value should still be there since null is not stored
        Assert.Equal("original", store.Get<string>("key1"));
    }

    [Test]
    public void Set_WithNullValue_DoesNotCreateEntry()
    {
        var store = new InMemoryStateStore();

        store.Set<string>("key1", null!);

        Assert.Null(store.Get<string>("key1"));
        Assert.False(store.TryGet<string>("key1", out _));
    }

    [Test]
    public void Set_WithInteger_StoresCorrectly()
    {
        var store = new InMemoryStateStore();

        store.Set("count", 100);

        Assert.Equal(100, store.Get<int>("count"));
    }

    [Test]
    public void Set_WithCustomObject_StoresReference()
    {
        var store = new InMemoryStateStore();
        var person = new StoredPerson { Name = "Bob", Age = 25 };

        store.Set("person", person);
        person.Name = "Modified";

        var result = store.Get<StoredPerson>("person");
        Assert.Equal("Modified", result!.Name);
    }

    [Test]
    public void Set_WithDifferentType_OverwritesExistingValue()
    {
        var store = new InMemoryStateStore();
        store.Set("key1", "string_value");

        store.Set("key1", 42);

        // Old string value should be gone
        Assert.Null(store.Get<string>("key1"));
        Assert.Equal(42, store.Get<int>("key1"));
    }

    [Test]
    public void Set_WithList_StoresCorrectly()
    {
        var store = new InMemoryStateStore();
        var list = new List<int> { 1, 2, 3 };

        store.Set("numbers", list);

        var result = store.Get<List<int>>("numbers");
        Assert.NotNull(result);
        Assert.Equal(3, result!.Count);
    }
}

[TestClass]
public class StateStoreTryGetTests
{
    [Test]
    public void TryGet_ReturnsTrue_WhenKeyExists()
    {
        var store = new InMemoryStateStore();
        store.Set("key1", "value1");

        bool result = store.TryGet<string>("key1", out var value);

        Assert.True(result);
        Assert.Equal("value1", value);
    }

    [Test]
    public void TryGet_ReturnsFalse_WhenKeyDoesNotExist()
    {
        var store = new InMemoryStateStore();

        bool result = store.TryGet<string>("nonexistent", out var value);

        Assert.False(result);
        Assert.Null(value);
    }

    [Test]
    public void TryGet_ReturnsFalse_WhenTypeMismatch()
    {
        var store = new InMemoryStateStore();
        store.Set("key1", "string_value");

        bool result = store.TryGet<int>("key1", out var value);

        Assert.False(result);
        Assert.Equal(0, value);
    }

    [Test]
    public void TryGet_ReturnsTrue_ForInteger()
    {
        var store = new InMemoryStateStore();
        store.Set("count", 99);

        bool result = store.TryGet<int>("count", out var value);

        Assert.True(result);
        Assert.Equal(99, value);
    }

    [Test]
    public void TryGet_ReturnsTrue_ForCustomObject()
    {
        var store = new InMemoryStateStore();
        var person = new StoredPerson { Name = "Carol", Age = 35 };
        store.Set("person", person);

        bool result = store.TryGet<StoredPerson>("person", out var value);

        Assert.True(result);
        Assert.NotNull(value);
        Assert.Equal("Carol", value!.Name);
    }

    [Test]
    public void TryGet_ReturnsFalse_WhenTypeMismatch_ForReferenceType()
    {
        var store = new InMemoryStateStore();
        store.Set("key1", 123);

        bool result = store.TryGet<string>("key1", out var value);

        Assert.False(result);
        Assert.Null(value);
    }
}

[TestClass]
public class StateStoreRemoveTests
{
    [Test]
    public void Remove_ReturnsTrue_WhenKeyExists()
    {
        var store = new InMemoryStateStore();
        store.Set("key1", "value1");

        bool result = store.Remove("key1");

        Assert.True(result);
    }

    [Test]
    public void Remove_RemovesKey_Successfully()
    {
        var store = new InMemoryStateStore();
        store.Set("key1", "value1");

        store.Remove("key1");

        Assert.Null(store.Get<string>("key1"));
        Assert.False(store.TryGet<string>("key1", out _));
    }

    [Test]
    public void Remove_ReturnsFalse_WhenKeyDoesNotExist()
    {
        var store = new InMemoryStateStore();

        bool result = store.Remove("nonexistent");

        Assert.False(result);
    }

    [Test]
    public void Remove_DoesNotAffectOtherKeys()
    {
        var store = new InMemoryStateStore();
        store.Set("key1", "value1");
        store.Set("key2", "value2");

        store.Remove("key1");

        Assert.Equal("value2", store.Get<string>("key2"));
    }

    [Test]
    public void Remove_CanRemoveAfterOverwrite()
    {
        var store = new InMemoryStateStore();
        store.Set("key1", "original");
        store.Set("key1", "updated");

        bool result = store.Remove("key1");

        Assert.True(result);
        Assert.Null(store.Get<string>("key1"));
    }

    [Test]
    public void Remove_TwiceOnSameKey_ReturnsFalseSecondTime()
    {
        var store = new InMemoryStateStore();
        store.Set("key1", "value1");

        bool first = store.Remove("key1");
        bool second = store.Remove("key1");

        Assert.True(first);
        Assert.False(second);
    }
}

[TestClass]
public class StateStoreGetOrAddTests
{
    [Test]
    public void GetOrAdd_ReturnsExistingValue_WhenKeyExists()
    {
        var store = new InMemoryStateStore();
        store.Set("key1", "existing");

        var result = store.GetOrAdd("key1", k => "factory_value");

        Assert.Equal("existing", result);
    }

    [Test]
    public void GetOrAdd_DoesNotCallFactory_WhenKeyExists()
    {
        var store = new InMemoryStateStore();
        store.Set("key1", "existing");
        bool factoryCalled = false;

        store.GetOrAdd("key1", k =>
        {
            factoryCalled = true;
            return "factory_value";
        });

        Assert.False(factoryCalled);
    }

    [Test]
    public void GetOrAdd_CallsFactory_WhenKeyDoesNotExist()
    {
        var store = new InMemoryStateStore();
        bool factoryCalled = false;

        store.GetOrAdd("key1", k =>
        {
            factoryCalled = true;
            return "factory_value";
        });

        Assert.True(factoryCalled);
    }

    [Test]
    public void GetOrAdd_ReturnsFactoryResult_WhenKeyDoesNotExist()
    {
        var store = new InMemoryStateStore();

        var result = store.GetOrAdd("key1", k => "factory_value");

        Assert.Equal("factory_value", result);
    }

    [Test]
    public void GetOrAdd_StoresFactoryResult_WhenKeyDoesNotExist()
    {
        var store = new InMemoryStateStore();

        store.GetOrAdd("key1", k => "factory_value");

        Assert.Equal("factory_value", store.Get<string>("key1"));
    }

    [Test]
    public void GetOrAdd_PassesKeyToFactory()
    {
        var store = new InMemoryStateStore();
        string? receivedKey = null;

        store.GetOrAdd("mykey", k =>
        {
            receivedKey = k;
            return "value";
        });

        Assert.Equal("mykey", receivedKey);
    }

    [Test]
    public void GetOrAdd_WithInteger_ReturnsCorrectType()
    {
        var store = new InMemoryStateStore();

        var result = store.GetOrAdd("count", k => 42);

        Assert.Equal(42, result);
    }

    [Test]
    public void GetOrAdd_WithCustomObject_WorksCorrectly()
    {
        var store = new InMemoryStateStore();

        var result = store.GetOrAdd("person", k => new StoredPerson { Name = "Dan", Age = 40 });

        Assert.NotNull(result);
        Assert.Equal("Dan", result.Name);
        Assert.Equal(40, result.Age);
    }

    [Test]
    public void GetOrAdd_WithList_WorksCorrectly()
    {
        var store = new InMemoryStateStore();

        var result = store.GetOrAdd("items", k => new List<string> { "x", "y" });

        Assert.NotNull(result);
        Assert.Equal(2, result.Count);
    }
}

[TestClass]
public class StateStoreClearTests
{
    [Test]
    public void Clear_RemovesAllEntries()
    {
        var store = new InMemoryStateStore();
        store.Set("key1", "value1");
        store.Set("key2", "value2");
        store.Set("key3", "value3");

        store.Clear();

        Assert.Null(store.Get<string>("key1"));
        Assert.Null(store.Get<string>("key2"));
        Assert.Null(store.Get<string>("key3"));
    }

    [Test]
    public void Clear_OnEmptyStore_DoesNotThrow()
    {
        var store = new InMemoryStateStore();

        store.Clear(); // Should not throw

        Assert.Null(store.Get<string>("anykey"));
    }

    [Test]
    public void Clear_AllowsReaddingKeys()
    {
        var store = new InMemoryStateStore();
        store.Set("key1", "original");
        store.Clear();

        store.Set("key1", "new_value");

        Assert.Equal("new_value", store.Get<string>("key1"));
    }

    [Test]
    public void Clear_RemovesMixedTypes()
    {
        var store = new InMemoryStateStore();
        store.Set("string_key", "string_value");
        store.Set("int_key", 123);
        store.Set("object_key", new StoredPerson { Name = "Eve", Age = 28 });

        store.Clear();

        Assert.Null(store.Get<string>("string_key"));
        Assert.Equal(0, store.Get<int>("int_key"));
        Assert.Null(store.Get<StoredPerson>("object_key"));
    }
}

[TestClass]
public class StateStoreThreadSafetyTests
{
    [Test]
    public void ConcurrentSet_DoesNotThrow()
    {
        var store = new InMemoryStateStore();
        var threads = new Thread[10];

        for (int i = 0; i < 10; i++)
        {
            int index = i;
            threads[i] = new Thread(() =>
            {
                for (int j = 0; j < 100; j++)
                {
                    store.Set($"key_{index}_{j}", $"value_{index}_{j}");
                }
            });
        }

        foreach (var thread in threads) thread.Start();
        foreach (var thread in threads) thread.Join();

        // Verify some values were stored
        Assert.NotNull(store.Get<string>("key_0_0"));
        Assert.NotNull(store.Get<string>("key_9_99"));
    }

    [Test]
    public void ConcurrentGetAndSet_DoesNotThrow()
    {
        var store = new InMemoryStateStore();
        store.Set("shared", 0);
        var threads = new Thread[10];

        for (int i = 0; i < 10; i++)
        {
            threads[i] = new Thread(() =>
            {
                for (int j = 0; j < 100; j++)
                {
                    var current = store.Get<int>("shared");
                    store.Set("shared", current + 1);
                }
            });
        }

        foreach (var thread in threads) thread.Start();
        foreach (var thread in threads) thread.Join();

        // Value may not be exactly 1000 due to race conditions, but should be > 0
        Assert.True(store.Get<int>("shared") > 0);
    }

    [Test]
    public void ConcurrentGetOrAdd_FactoryCalledAtLeastOnce()
    {
        var store = new InMemoryStateStore();
        int factoryCallCount = 0;
        var threads = new Thread[10];

        for (int i = 0; i < 10; i++)
        {
            threads[i] = new Thread(() =>
            {
                store.GetOrAdd("shared_key", k =>
                {
                    Interlocked.Increment(ref factoryCallCount);
                    Thread.Sleep(10); // Simulate slow factory
                    return "value";
                });
            });
        }

        foreach (var thread in threads) thread.Start();
        foreach (var thread in threads) thread.Join();

        // Factory should be called at least once
        Assert.True(factoryCallCount >= 1);
        // All threads should see the same value
        Assert.Equal("value", store.Get<string>("shared_key"));
    }

    [Test]
    public void ConcurrentRemove_DoesNotThrow()
    {
        var store = new InMemoryStateStore();
        for (int i = 0; i < 100; i++)
        {
            store.Set($"key_{i}", $"value_{i}");
        }

        var threads = new Thread[10];
        for (int i = 0; i < 10; i++)
        {
            int index = i;
            threads[i] = new Thread(() =>
            {
                for (int j = 0; j < 10; j++)
                {
                    store.Remove($"key_{index * 10 + j}");
                }
            });
        }

        foreach (var thread in threads) thread.Start();
        foreach (var thread in threads) thread.Join();

        // All keys should be removed
        for (int i = 0; i < 100; i++)
        {
            Assert.Null(store.Get<string>($"key_{i}"));
        }
    }

    [Test]
    public void ConcurrentClear_DoesNotThrow()
    {
        var store = new InMemoryStateStore();
        var threads = new Thread[5];

        for (int i = 0; i < 5; i++)
        {
            threads[i] = new Thread(() =>
            {
                for (int j = 0; j < 100; j++)
                {
                    store.Set($"key_{j}", $"value_{j}");
                    if (j % 50 == 0) store.Clear();
                }
            });
        }

        foreach (var thread in threads) thread.Start();
        foreach (var thread in threads) thread.Join();

        // Should complete without throwing
    }
}

[TestClass]
public class StateStoreEdgeCaseTests
{
    [Test]
    public void EmptyStringKey_WorksCorrectly()
    {
        var store = new InMemoryStateStore();

        store.Set("", "empty_key_value");

        Assert.Equal("empty_key_value", store.Get<string>(""));
    }

    [Test]
    public void LongKey_WorksCorrectly()
    {
        var store = new InMemoryStateStore();
        var longKey = new string('a', 10000);

        store.Set(longKey, "long_key_value");

        Assert.Equal("long_key_value", store.Get<string>(longKey));
    }

    [Test]
    public void SpecialCharactersInKey_WorksCorrectly()
    {
        var store = new InMemoryStateStore();
        var specialKey = "key:with/special\\chars!@#$%^&*()";

        store.Set(specialKey, "special_value");

        Assert.Equal("special_value", store.Get<string>(specialKey));
    }

    [Test]
    public void UnicodeKey_WorksCorrectly()
    {
        var store = new InMemoryStateStore();
        var unicodeKey = "key_with_unicode_\u4e2d\u6587_\u65e5\u672c\u8a9e";

        store.Set(unicodeKey, "unicode_value");

        Assert.Equal("unicode_value", store.Get<string>(unicodeKey));
    }

    [Test]
    public void UnicodeValue_WorksCorrectly()
    {
        var store = new InMemoryStateStore();

        store.Set("key", "value_with_unicode_\u4e2d\u6587_\u65e5\u672c\u8a9e");

        Assert.Equal("value_with_unicode_\u4e2d\u6587_\u65e5\u672c\u8a9e", store.Get<string>("key"));
    }

    [Test]
    public void ValueTypeInheritance_WorksWithBaseType()
    {
        var store = new InMemoryStateStore();
        var list = new List<int> { 1, 2, 3 };
        store.Set("collection", list);

        // Try to get as IEnumerable<int>
        var result = store.Get<IEnumerable<int>>("collection");

        Assert.NotNull(result);
    }

    [Test]
    public void MultipleStoreInstances_AreIndependent()
    {
        var store1 = new InMemoryStateStore();
        var store2 = new InMemoryStateStore();

        store1.Set("key1", "store1_value");
        store2.Set("key1", "store2_value");

        Assert.Equal("store1_value", store1.Get<string>("key1"));
        Assert.Equal("store2_value", store2.Get<string>("key1"));
    }

    [Test]
    public void GetOrAdd_WithNullFactoryResult_StoresAndReturnsNull()
    {
        var store = new InMemoryStateStore();

        // When factory returns null, it gets stored and returned as null
        var result = store.GetOrAdd<string>("key", k => null!);

        // The result will be null
        Assert.Null(result);
    }

    [Test]
    public void CaseSensitiveKeys_AreTreatedDifferently()
    {
        var store = new InMemoryStateStore();

        store.Set("Key", "upper");
        store.Set("key", "lower");
        store.Set("KEY", "all_upper");

        Assert.Equal("upper", store.Get<string>("Key"));
        Assert.Equal("lower", store.Get<string>("key"));
        Assert.Equal("all_upper", store.Get<string>("KEY"));
    }

    [Test]
    public void BooleanValues_StoredCorrectly()
    {
        var store = new InMemoryStateStore();

        store.Set("true_key", true);
        store.Set("false_key", false);

        Assert.True(store.Get<bool>("true_key"));
        Assert.False(store.Get<bool>("false_key"));
    }

    [Test]
    public void DateTimeValues_StoredCorrectly()
    {
        var store = new InMemoryStateStore();
        var now = DateTime.UtcNow;

        store.Set("timestamp", now);

        Assert.Equal(now, store.Get<DateTime>("timestamp"));
    }

    [Test]
    public void GuidValues_StoredCorrectly()
    {
        var store = new InMemoryStateStore();
        var guid = Guid.NewGuid();

        store.Set("id", guid);

        Assert.Equal(guid, store.Get<Guid>("id"));
    }

    [Test]
    public void NullableInt_WithValue_StoredCorrectly()
    {
        var store = new InMemoryStateStore();
        int? value = 42;

        store.Set("nullable_int", value);

        var result = store.Get<int?>("nullable_int");
        Assert.True(result.HasValue);
        Assert.Equal(42, result!.Value);
    }

    [Test]
    public void ArrayValues_StoredCorrectly()
    {
        var store = new InMemoryStateStore();
        var array = new[] { 1, 2, 3, 4, 5 };

        store.Set("array", array);

        var result = store.Get<int[]>("array");
        Assert.NotNull(result);
        Assert.Equal(5, result!.Length);
        Assert.Equal(1, result[0]);
    }

    [Test]
    public void DictionaryValues_StoredCorrectly()
    {
        var store = new InMemoryStateStore();
        var dict = new Dictionary<string, int> { { "a", 1 }, { "b", 2 } };

        store.Set("dict", dict);

        var result = store.Get<Dictionary<string, int>>("dict");
        Assert.NotNull(result);
        Assert.Equal(2, result!.Count);
        Assert.Equal(1, result["a"]);
    }
}

[TestClass]
public class StateStoreInterfaceTests
{
    [Test]
    public void InMemoryStateStore_ImplementsIStateStore()
    {
        IStateStore store = new InMemoryStateStore();

        store.Set("key", "value");
        var result = store.Get<string>("key");

        Assert.Equal("value", result);
    }

    [Test]
    public void IStateStore_AllMethodsAccessible()
    {
        IStateStore store = new InMemoryStateStore();

        // Test all interface methods through interface reference
        store.Set("key1", "value1");
        Assert.Equal("value1", store.Get<string>("key1"));

        bool tryGetResult = store.TryGet<string>("key1", out var value);
        Assert.True(tryGetResult);
        Assert.Equal("value1", value);

        var getOrAddResult = store.GetOrAdd("key2", k => "factory_value");
        Assert.Equal("factory_value", getOrAddResult);

        bool removeResult = store.Remove("key1");
        Assert.True(removeResult);

        store.Clear();
        Assert.Null(store.Get<string>("key2"));
    }
}
