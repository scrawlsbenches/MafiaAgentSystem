using System.Reflection;

namespace TestRunner.Framework;

/// <summary>
/// Tracks API coverage by analyzing which public types and methods from target assemblies
/// are referenced by the test assembly. This is a static analysis tool that shows API coverage,
/// not runtime code coverage.
///
/// Note: True code coverage requires IL instrumentation (like Coverlet) or profiling APIs.
/// This tool provides a lightweight, zero-dependency alternative that shows which APIs
/// have tests that reference them.
/// </summary>
public class CoverageTracker
{
    private readonly HashSet<string> _targetAssemblyNames = new();
    private readonly Dictionary<string, TypeInfo> _targetTypes = new();
    private readonly HashSet<string> _referencedTypes = new();
    private readonly HashSet<string> _referencedMethods = new();

    /// <summary>
    /// Registers an assembly to track coverage for.
    /// </summary>
    public void AddAssembly(Assembly assembly)
    {
        var assemblyName = assembly.GetName().Name ?? assembly.FullName ?? "Unknown";
        _targetAssemblyNames.Add(assemblyName);
        ScanTargetAssembly(assembly, assemblyName);
    }

    /// <summary>
    /// Registers an assembly by name.
    /// </summary>
    public void AddAssembly(string assemblyName)
    {
        try
        {
            var assembly = Assembly.Load(assemblyName);
            AddAssembly(assembly);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Warning: Could not load assembly '{assemblyName}': {ex.Message}");
        }
    }

    private void ScanTargetAssembly(Assembly assembly, string assemblyName)
    {
        foreach (var type in assembly.GetExportedTypes())
        {
            // Skip compiler-generated types
            if (type.Name.StartsWith("<") ||
                type.GetCustomAttribute<System.Runtime.CompilerServices.CompilerGeneratedAttribute>() != null)
            {
                continue;
            }

            var typeInfo = new TypeInfo
            {
                FullName = type.FullName ?? type.Name,
                AssemblyName = assemblyName,
                IsInterface = type.IsInterface,
                IsAbstract = type.IsAbstract && !type.IsInterface,
                IsStatic = type.IsAbstract && type.IsSealed
            };

            // Get all public methods
            var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
                .Where(m => !m.IsSpecialName)
                .Where(m => m.DeclaringType == type);

            foreach (var method in methods)
            {
                var sig = GetMethodSignature(type, method);
                typeInfo.Methods.Add(sig);
            }

            // Get public constructors
            foreach (var ctor in type.GetConstructors(BindingFlags.Public | BindingFlags.Instance))
            {
                var sig = GetConstructorSignature(type, ctor);
                typeInfo.Methods.Add(sig);
            }

            _targetTypes[typeInfo.FullName] = typeInfo;
        }
    }

    private static string GetMethodSignature(Type type, MethodInfo method)
    {
        var parameters = string.Join(", ", method.GetParameters().Select(p => p.ParameterType.Name));
        return $"{type.FullName}.{method.Name}({parameters})";
    }

    private static string GetConstructorSignature(Type type, ConstructorInfo ctor)
    {
        var parameters = string.Join(", ", ctor.GetParameters().Select(p => p.ParameterType.Name));
        return $"{type.FullName}..ctor({parameters})";
    }

    /// <summary>
    /// No-op for API compatibility. Static analysis doesn't need tracking.
    /// </summary>
    public void StartTracking() { }

    /// <summary>
    /// No-op for API compatibility.
    /// </summary>
    public void StopTracking() { }

    /// <summary>
    /// Analyzes the test assembly to find references to target types.
    /// </summary>
    public void AnalyzeTestAssembly(Assembly testAssembly)
    {
        foreach (var type in testAssembly.GetTypes())
        {
            // Check fields
            foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static))
            {
                MarkTypeReferenced(field.FieldType);
            }

            // Check methods
            foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static))
            {
                MarkTypeReferenced(method.ReturnType);
                foreach (var param in method.GetParameters())
                {
                    MarkTypeReferenced(param.ParameterType);
                }

                // Check local variables
                try
                {
                    var body = method.GetMethodBody();
                    if (body != null)
                    {
                        foreach (var local in body.LocalVariables)
                        {
                            MarkTypeReferenced(local.LocalType);
                        }
                    }
                }
                catch { }
            }

            // Check constructors
            foreach (var ctor in type.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            {
                foreach (var param in ctor.GetParameters())
                {
                    MarkTypeReferenced(param.ParameterType);
                }
            }

            // Check properties
            foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static))
            {
                MarkTypeReferenced(prop.PropertyType);
            }

            // Check base type and interfaces
            if (type.BaseType != null)
                MarkTypeReferenced(type.BaseType);

            foreach (var iface in type.GetInterfaces())
                MarkTypeReferenced(iface);
        }

        // Propagate interface coverage to implementations
        PropagateInterfaceCoverage();
    }

    private void MarkTypeReferenced(Type? type)
    {
        if (type == null) return;

        // Handle generic types - mark both the definition and the arguments
        if (type.IsGenericType)
        {
            foreach (var arg in type.GetGenericArguments())
            {
                MarkTypeReferenced(arg);
            }

            // Also check the generic definition
            if (!type.IsGenericTypeDefinition)
            {
                MarkTypeReferenced(type.GetGenericTypeDefinition());
            }
        }

        // Handle arrays
        if (type.IsArray)
        {
            MarkTypeReferenced(type.GetElementType());
            return;
        }

        // Handle by-ref types
        if (type.IsByRef)
        {
            MarkTypeReferenced(type.GetElementType());
            return;
        }

        // Check if from target assembly
        var assemblyName = type.Assembly.GetName().Name;
        if (assemblyName == null || !_targetAssemblyNames.Contains(assemblyName))
            return;

        var fullName = type.FullName ?? type.Name;

        // Handle generic type names (remove backtick notation for matching)
        if (type.IsGenericType)
        {
            var genericDef = type.IsGenericTypeDefinition ? type : type.GetGenericTypeDefinition();
            fullName = genericDef.FullName ?? genericDef.Name;
        }

        _referencedTypes.Add(fullName);
    }

    private void PropagateInterfaceCoverage()
    {
        // For each referenced interface, mark all implementations as referenced
        var referencedInterfaces = _referencedTypes
            .Where(t => _targetTypes.ContainsKey(t) && _targetTypes[t].IsInterface)
            .ToList();

        foreach (var ifaceName in referencedInterfaces)
        {
            // Find all types that implement this interface
            foreach (var kvp in _targetTypes)
            {
                if (kvp.Value.IsInterface) continue;

                // Check if this type implements the interface
                var typeToCheck = Type.GetType(kvp.Key) ??
                    AppDomain.CurrentDomain.GetAssemblies()
                        .SelectMany(a => { try { return a.GetTypes(); } catch { return Array.Empty<Type>(); } })
                        .FirstOrDefault(t => t.FullName == kvp.Key);

                if (typeToCheck != null)
                {
                    foreach (var iface in typeToCheck.GetInterfaces())
                    {
                        var ifaceFullName = iface.IsGenericType
                            ? (iface.GetGenericTypeDefinition().FullName ?? iface.Name)
                            : (iface.FullName ?? iface.Name);

                        if (ifaceFullName == ifaceName)
                        {
                            _referencedTypes.Add(kvp.Key);
                            break;
                        }
                    }
                }
            }
        }
    }

    /// <summary>
    /// Generates a coverage report.
    /// </summary>
    public CoverageReport GenerateReport()
    {
        var report = new CoverageReport();

        foreach (var kvp in _targetTypes.OrderBy(t => t.Value.AssemblyName).ThenBy(t => t.Key))
        {
            var typeInfo = kvp.Value;
            var isCovered = _referencedTypes.Contains(kvp.Key);

            report.TypeReports.Add(new TypeCoverageReport
            {
                TypeName = typeInfo.FullName,
                AssemblyName = typeInfo.AssemblyName,
                IsCovered = isCovered,
                IsInterface = typeInfo.IsInterface,
                IsAbstract = typeInfo.IsAbstract,
                TotalMethods = typeInfo.Methods.Count,
                CoveredMethods = isCovered ? typeInfo.Methods.Count : 0, // Simplified: if type is covered, all methods count
                UncoveredMethods = isCovered ? new List<string>() : typeInfo.Methods.ToList()
            });
        }

        report.CalculateSummary();
        return report;
    }

    private class TypeInfo
    {
        public string FullName { get; set; } = "";
        public string AssemblyName { get; set; } = "";
        public bool IsInterface { get; set; }
        public bool IsAbstract { get; set; }
        public bool IsStatic { get; set; }
        public HashSet<string> Methods { get; } = new();
    }
}

/// <summary>
/// Coverage report for a single type.
/// </summary>
public class TypeCoverageReport
{
    public string TypeName { get; set; } = "";
    public string AssemblyName { get; set; } = "";
    public bool IsCovered { get; set; }
    public bool IsInterface { get; set; }
    public bool IsAbstract { get; set; }
    public int TotalMethods { get; set; }
    public int CoveredMethods { get; set; }
    public List<string> UncoveredMethods { get; set; } = new();

    public double MethodCoveragePercent => TotalMethods > 0 ? (double)CoveredMethods / TotalMethods * 100 : 100;
}

/// <summary>
/// Complete coverage report.
/// </summary>
public class CoverageReport
{
    public List<TypeCoverageReport> TypeReports { get; } = new();

    public int TotalTypes { get; private set; }
    public int CoveredTypes { get; private set; }
    public int TotalMethods { get; private set; }
    public int CoveredMethods { get; private set; }
    public double TypeCoveragePercent { get; private set; }
    public double MethodCoveragePercent { get; private set; }

    public void CalculateSummary()
    {
        // Exclude interfaces and abstract classes from type coverage (they can't be instantiated directly)
        var concreteTypes = TypeReports.Where(t => !t.IsInterface && !t.IsAbstract).ToList();

        TotalTypes = concreteTypes.Count;
        CoveredTypes = concreteTypes.Count(t => t.IsCovered);
        TotalMethods = TypeReports.Sum(t => t.TotalMethods);
        CoveredMethods = TypeReports.Sum(t => t.CoveredMethods);

        TypeCoveragePercent = TotalTypes > 0 ? (double)CoveredTypes / TotalTypes * 100 : 100;
        MethodCoveragePercent = TotalMethods > 0 ? (double)CoveredMethods / TotalMethods * 100 : 100;
    }

    /// <summary>
    /// Prints a summary to the console.
    /// </summary>
    public void PrintSummary()
    {
        Console.WriteLine();
        Console.WriteLine("══════════════════════════════════════════════════════════════════");
        Console.WriteLine("                     API COVERAGE REPORT");
        Console.WriteLine("  (Static analysis of types referenced by test code)");
        Console.WriteLine("══════════════════════════════════════════════════════════════════");
        Console.WriteLine();

        // Group by assembly
        var byAssembly = TypeReports.GroupBy(t => t.AssemblyName).OrderBy(g => g.Key);

        foreach (var assembly in byAssembly)
        {
            var asmTypes = assembly.Where(t => !t.IsInterface && !t.IsAbstract).ToList();
            var asmCovered = asmTypes.Count(t => t.IsCovered);
            var asmMethods = assembly.Sum(t => t.TotalMethods);
            var asmMethodsCovered = assembly.Sum(t => t.CoveredMethods);
            var asmTypePct = asmTypes.Count > 0 ? (double)asmCovered / asmTypes.Count * 100 : 100;
            var asmMethodPct = asmMethods > 0 ? (double)asmMethodsCovered / asmMethods * 100 : 100;

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"Assembly: {assembly.Key}");
            Console.ResetColor();
            Console.WriteLine($"  Types:   {asmCovered}/{asmTypes.Count} ({asmTypePct:F1}%)");
            Console.WriteLine($"  Methods: {asmMethodsCovered}/{asmMethods} ({asmMethodPct:F1}%)");
            Console.WriteLine();
        }

        // Print uncovered types
        var uncoveredTypes = TypeReports
            .Where(t => !t.IsCovered && !t.IsInterface && !t.IsAbstract)
            .ToList();

        if (uncoveredTypes.Any())
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("Types Not Referenced by Tests:");
            Console.ResetColor();
            foreach (var type in uncoveredTypes.Take(20))
            {
                Console.WriteLine($"  - {type.TypeName}");
            }
            if (uncoveredTypes.Count > 20)
            {
                Console.WriteLine($"  ... and {uncoveredTypes.Count - 20} more");
            }
            Console.WriteLine();
        }

        // Print summary
        Console.WriteLine("──────────────────────────────────────────────────────────────────");
        var typeColor = TypeCoveragePercent >= 80 ? ConsoleColor.Green :
                        TypeCoveragePercent >= 60 ? ConsoleColor.Yellow : ConsoleColor.Red;
        var methodColor = MethodCoveragePercent >= 80 ? ConsoleColor.Green :
                          MethodCoveragePercent >= 60 ? ConsoleColor.Yellow : ConsoleColor.Red;

        Console.Write("Type Coverage:   ");
        Console.ForegroundColor = typeColor;
        Console.WriteLine($"{CoveredTypes}/{TotalTypes} ({TypeCoveragePercent:F1}%)");
        Console.ResetColor();

        Console.Write("Method Coverage: ");
        Console.ForegroundColor = methodColor;
        Console.WriteLine($"{CoveredMethods}/{TotalMethods} ({MethodCoveragePercent:F1}%)");
        Console.ResetColor();

        Console.WriteLine("══════════════════════════════════════════════════════════════════");
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine("Note: This is API coverage (types referenced by tests), not");
        Console.WriteLine("runtime code coverage. Use Coverlet for line-level coverage.");
        Console.ResetColor();
    }

    /// <summary>
    /// Prints detailed coverage for each type.
    /// </summary>
    public void PrintDetailed()
    {
        Console.WriteLine();
        Console.WriteLine("Detailed Coverage by Type:");
        Console.WriteLine("──────────────────────────────────────────────────────────────────");

        foreach (var type in TypeReports.OrderBy(t => t.TypeName))
        {
            var statusColor = type.IsCovered ? ConsoleColor.Green : ConsoleColor.Red;
            var status = type.IsCovered ? "+" : "-";

            Console.ForegroundColor = statusColor;
            Console.Write($"  {status} ");
            Console.ResetColor();
            Console.Write(type.TypeName);

            if (type.IsInterface)
                Console.Write(" (interface)");
            else if (type.IsAbstract)
                Console.Write(" (abstract)");

            if (type.TotalMethods > 0)
            {
                Console.Write($" [{type.TotalMethods} methods]");
            }

            Console.WriteLine();
        }
    }
}
