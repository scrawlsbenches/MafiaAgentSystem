using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using AgentRouting.Core;
using AgentRouting.Middleware;

namespace AgentRouting.Middleware.Advanced;

/// <summary>
/// Distributed tracing middleware for tracking messages across agents
/// Implements OpenTelemetry-style tracing
/// </summary>
public class DistributedTracingMiddleware : MiddlewareBase
{
    private readonly string _serviceName;
    private readonly ConcurrentBag<TraceSpan> _spans = new();
    
    public DistributedTracingMiddleware(string serviceName = "AgentRouter")
    {
        _serviceName = serviceName;
    }
    
    public override async Task<MessageResult> InvokeAsync(
        AgentMessage message,
        MessageDelegate next,
        CancellationToken ct)
    {
        // Get or create trace ID
        var traceId = message.Metadata.TryGetValue("TraceId", out var existingTraceId)
            ? existingTraceId.ToString()!
            : Guid.NewGuid().ToString("N");
        
        var spanId = Guid.NewGuid().ToString("N").Substring(0, 16);
        var parentSpanId = message.Metadata.TryGetValue("SpanId", out var existingSpanId)
            ? existingSpanId.ToString()
            : null;
        
        // Set trace context in message
        message.Metadata["TraceId"] = traceId;
        message.Metadata["SpanId"] = spanId;
        
        var sw = Stopwatch.StartNew();
        var span = new TraceSpan
        {
            TraceId = traceId,
            SpanId = spanId,
            ParentSpanId = parentSpanId,
            ServiceName = _serviceName,
            OperationName = $"ProcessMessage: {message.Subject}",
            StartTime = DateTime.UtcNow,
            Tags = new Dictionary<string, string>
            {
                ["message.id"] = message.Id,
                ["message.sender"] = message.SenderId,
                ["message.category"] = message.Category,
                ["message.priority"] = message.Priority.ToString()
            }
        };
        
        try
        {
            var result = await next(message, ct);
            
            sw.Stop();
            span.Duration = sw.Elapsed;
            span.Success = result.Success;
            span.Tags["result.success"] = result.Success.ToString();
            
            if (!result.Success && result.Error != null)
            {
                span.Tags["error.message"] = result.Error;
            }
            
            _spans.Add(span);
            
            return result;
        }
        catch (Exception ex)
        {
            sw.Stop();
            span.Duration = sw.Elapsed;
            span.Success = false;
            span.Tags["error.type"] = ex.GetType().Name;
            span.Tags["error.message"] = ex.Message;
            _spans.Add(span);
            throw;
        }
    }
    
    public List<TraceSpan> GetTraces() => _spans.ToList();
    
    public string ExportJaegerFormat()
    {
        var traces = GetTraces();
        var sb = new StringBuilder();
        
        sb.AppendLine("Jaeger Trace Export:");
        sb.AppendLine("═══════════════════");
        
        foreach (var group in traces.GroupBy(t => t.TraceId))
        {
            sb.AppendLine($"\nTrace ID: {group.Key}");
            foreach (var span in group.OrderBy(s => s.StartTime))
            {
                var indent = span.ParentSpanId != null ? "  → " : "";
                sb.AppendLine($"{indent}Span: {span.OperationName}");
                sb.AppendLine($"   Duration: {span.Duration.TotalMilliseconds:F2}ms");
                sb.AppendLine($"   Success: {span.Success}");
            }
        }
        
        return sb.ToString();
    }
}

public class TraceSpan
{
    public string TraceId { get; set; } = "";
    public string SpanId { get; set; } = "";
    public string? ParentSpanId { get; set; }
    public string ServiceName { get; set; } = "";
    public string OperationName { get; set; } = "";
    public DateTime StartTime { get; set; }
    public TimeSpan Duration { get; set; }
    public bool Success { get; set; }
    public Dictionary<string, string> Tags { get; set; } = new();
}

/// <summary>
/// Semantic analysis middleware using simple NLP
/// Routes based on message intent and sentiment
/// </summary>
public class SemanticRoutingMiddleware : MiddlewareBase
{
    private readonly Dictionary<string, List<string>> _intentKeywords = new()
    {
        ["complaint"] = new() { "angry", "frustrated", "disappointed", "terrible", "awful", "horrible", "worst", "furious", "upset" },
        ["question"] = new() { "what", "when", "where", "why", "how", "who", "can you", "could you", "would you" },
        ["urgent"] = new() { "urgent", "asap", "immediately", "emergency", "critical", "now", "quick" },
        ["praise"] = new() { "thank", "great", "excellent", "wonderful", "amazing", "fantastic", "love" },
        ["technical"] = new() { "bug", "error", "crash", "not working", "broken", "issue", "problem", "technical" }
    };
    
    public override async Task<MessageResult> InvokeAsync(
        AgentMessage message,
        MessageDelegate next,
        CancellationToken ct)
    {
        var content = (message.Subject + " " + message.Content).ToLowerInvariant();
        
        // Detect intent
        var detectedIntents = new List<string>();
        foreach (var (intent, keywords) in _intentKeywords)
        {
            if (keywords.Any(keyword => content.Contains(keyword)))
            {
                detectedIntents.Add(intent);
            }
        }
        
        // Store intent in metadata
        if (detectedIntents.Any())
        {
            message.Metadata["DetectedIntents"] = string.Join(",", detectedIntents);
            
            // Boost priority for complaints and urgent messages
            if (detectedIntents.Contains("complaint") || detectedIntents.Contains("urgent"))
            {
                if (message.Priority < MessagePriority.High)
                {
                    Console.WriteLine($"[Semantic] Boosting priority due to intent: {string.Join(", ", detectedIntents)}");
                    message.Priority = MessagePriority.High;
                }
            }
            
            // Auto-categorize technical issues
            if (detectedIntents.Contains("technical") && string.IsNullOrEmpty(message.Category))
            {
                message.Category = "TechnicalSupport";
            }
        }
        
        return await next(message, ct);
    }
}

/// <summary>
/// Message transformation middleware
/// Normalizes, sanitizes, and enriches messages
/// </summary>
public class MessageTransformationMiddleware : MiddlewareBase
{
    public override async Task<MessageResult> InvokeAsync(
        AgentMessage message,
        MessageDelegate next,
        CancellationToken ct)
    {
        // Normalize text
        message.Subject = NormalizeText(message.Subject);
        message.Content = NormalizeText(message.Content);
        
        // Sanitize (remove potential injection attacks)
        message.Content = SanitizeInput(message.Content);
        
        // Extract and tag email addresses
        var emails = ExtractEmails(message.Content);
        if (emails.Any())
        {
            message.Metadata["ContainsEmail"] = true;
            message.Metadata["EmailCount"] = emails.Count;
        }
        
        // Extract and tag phone numbers
        var phones = ExtractPhoneNumbers(message.Content);
        if (phones.Any())
        {
            message.Metadata["ContainsPhone"] = true;
            message.Metadata["PhoneCount"] = phones.Count;
        }
        
        // Detect language (simple heuristic)
        var language = DetectLanguage(message.Content);
        message.Metadata["DetectedLanguage"] = language;
        
        // Add processing timestamp
        message.Metadata["ProcessingTimestamp"] = DateTime.UtcNow.ToString("O");
        
        return await next(message, ct);
    }
    
    private string NormalizeText(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;
        
        // Trim whitespace
        text = text.Trim();
        
        // Remove excessive whitespace
        text = System.Text.RegularExpressions.Regex.Replace(text, @"\s+", " ");
        
        return text;
    }
    
    private string SanitizeInput(string input)
    {
        if (string.IsNullOrEmpty(input)) return input;
        
        // Remove common injection patterns
        input = input.Replace("<script>", "")
                    .Replace("</script>", "")
                    .Replace("javascript:", "")
                    .Replace("onerror=", "");
        
        return input;
    }
    
    private List<string> ExtractEmails(string text)
    {
        var emailPattern = @"\b[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Z|a-z]{2,}\b";
        var matches = System.Text.RegularExpressions.Regex.Matches(text, emailPattern);
        return matches.Select(m => m.Value).ToList();
    }
    
    private List<string> ExtractPhoneNumbers(string text)
    {
        var phonePattern = @"\b\d{3}[-.]?\d{3}[-.]?\d{4}\b";
        var matches = System.Text.RegularExpressions.Regex.Matches(text, phonePattern);
        return matches.Select(m => m.Value).ToList();
    }
    
    private string DetectLanguage(string text)
    {
        // Simple heuristic based on common words
        var spanishWords = new[] { "el", "la", "los", "las", "de", "que", "y", "es", "en", "un" };
        var frenchWords = new[] { "le", "la", "les", "de", "que", "et", "est", "dans", "un", "une" };
        
        var lower = text.ToLowerInvariant();
        var spanishCount = spanishWords.Count(w => lower.Contains($" {w} "));
        var frenchCount = frenchWords.Count(w => lower.Contains($" {w} "));
        
        if (spanishCount > 2) return "Spanish";
        if (frenchCount > 2) return "French";
        return "English";
    }
}

/// <summary>
/// Message queuing middleware with buffering and batch processing
/// </summary>
public class MessageQueueMiddleware : MiddlewareBase
{
    private readonly ConcurrentQueue<(AgentMessage message, TaskCompletionSource<MessageResult> tcs)> _queue = new();
    private readonly int _batchSize;
    private readonly TimeSpan _batchTimeout;
    private readonly Timer _batchTimer;
    private MessageDelegate? _next;
    
    public MessageQueueMiddleware(int batchSize = 10, TimeSpan? batchTimeout = null)
    {
        _batchSize = batchSize;
        _batchTimeout = batchTimeout ?? TimeSpan.FromSeconds(5);
        _batchTimer = new Timer(ProcessBatch, null, _batchTimeout, _batchTimeout);
    }
    
    public override async Task<MessageResult> InvokeAsync(
        AgentMessage message,
        MessageDelegate next,
        CancellationToken ct)
    {
        _next = next;
        
        var tcs = new TaskCompletionSource<MessageResult>();
        _queue.Enqueue((message, tcs));
        
        // If queue is full, process immediately
        if (_queue.Count >= _batchSize)
        {
            ProcessBatch(null);
        }
        
        return await tcs.Task;
    }
    
    private async void ProcessBatch(object? state)
    {
        if (_next == null) return;
        
        var batch = new List<(AgentMessage message, TaskCompletionSource<MessageResult> tcs)>();
        
        // Dequeue up to batchSize messages
        while (batch.Count < _batchSize && _queue.TryDequeue(out var item))
        {
            batch.Add(item);
        }
        
        if (batch.Count == 0) return;
        
        Console.WriteLine($"[Queue] Processing batch of {batch.Count} messages");
        
        // Process batch in parallel
        var tasks = batch.Select(async item =>
        {
            try
            {
                var result = await _next(item.message, default);
                item.tcs.SetResult(result);
            }
            catch (Exception ex)
            {
                item.tcs.SetResult(MessageResult.Fail($"Batch processing error: {ex.Message}"));
            }
        });
        
        await Task.WhenAll(tasks);
    }
}

/// <summary>
/// A/B testing middleware for experimenting with routing strategies
/// </summary>
public class ABTestingMiddleware : MiddlewareBase
{
    private readonly Dictionary<string, ExperimentConfig> _experiments = new();
    private readonly Random _random = new();
    
    public void RegisterExperiment(string experimentName, double probabilityA, string variantATag, string variantBTag)
    {
        _experiments[experimentName] = new ExperimentConfig
        {
            ProbabilityA = probabilityA,
            VariantATag = variantATag,
            VariantBTag = variantBTag
        };
    }
    
    public override async Task<MessageResult> InvokeAsync(
        AgentMessage message,
        MessageDelegate next,
        CancellationToken ct)
    {
        foreach (var (experimentName, config) in _experiments)
        {
            var randomValue = _random.NextDouble();
            var variant = randomValue < config.ProbabilityA ? config.VariantATag : config.VariantBTag;
            
            message.Metadata[$"Experiment_{experimentName}"] = variant;
            
            Console.WriteLine($"[A/B Test] {experimentName}: Assigned variant {variant}");
        }
        
        return await next(message, ct);
    }
    
    private class ExperimentConfig
    {
        public double ProbabilityA { get; set; }
        public string VariantATag { get; set; } = "";
        public string VariantBTag { get; set; } = "";
    }
}

/// <summary>
/// Feature flags middleware for conditional feature enablement
/// </summary>
public class FeatureFlagsMiddleware : MiddlewareBase
{
    private readonly Dictionary<string, FeatureFlag> _flags = new();
    
    public void RegisterFlag(string flagName, bool enabled, Func<AgentMessage, bool>? condition = null)
    {
        _flags[flagName] = new FeatureFlag
        {
            Enabled = enabled,
            Condition = condition
        };
    }
    
    public override async Task<MessageResult> InvokeAsync(
        AgentMessage message,
        MessageDelegate next,
        CancellationToken ct)
    {
        var context = message.GetContext();
        
        foreach (var (flagName, flag) in _flags)
        {
            var isEnabled = flag.Enabled && (flag.Condition == null || flag.Condition(message));
            context.Set($"Feature_{flagName}", isEnabled);
            
            if (isEnabled)
            {
                Console.WriteLine($"[Feature] {flagName} is ENABLED for this message");
            }
        }
        
        return await next(message, ct);
    }
    
    private class FeatureFlag
    {
        public bool Enabled { get; set; }
        public Func<AgentMessage, bool>? Condition { get; set; }
    }
}

/// <summary>
/// Agent health checking middleware
/// Monitors agent availability and routes around unhealthy agents
/// </summary>
public class AgentHealthCheckMiddleware : MiddlewareBase
{
    private readonly ConcurrentDictionary<string, HealthStatus> _health = new();
    private readonly Timer _healthCheckTimer;
    
    public AgentHealthCheckMiddleware(TimeSpan checkInterval)
    {
        _healthCheckTimer = new Timer(PerformHealthChecks, null, checkInterval, checkInterval);
    }
    
    public void RegisterAgent(string agentId, Func<Task<bool>> healthCheck)
    {
        _health[agentId] = new HealthStatus
        {
            AgentId = agentId,
            HealthCheck = healthCheck,
            IsHealthy = true,
            LastCheck = DateTime.UtcNow
        };
    }
    
    public override async Task<MessageResult> InvokeAsync(
        AgentMessage message,
        MessageDelegate next,
        CancellationToken ct)
    {
        // Check if target agent is healthy
        if (!string.IsNullOrEmpty(message.ReceiverId) &&
            _health.TryGetValue(message.ReceiverId, out var status))
        {
            if (!status.IsHealthy)
            {
                Console.WriteLine($"[Health] Agent {message.ReceiverId} is unhealthy, finding alternative");
                
                // Find healthy alternative
                var healthyAgent = _health.FirstOrDefault(h => h.Value.IsHealthy).Key;
                if (healthyAgent != null)
                {
                    message.ReceiverId = healthyAgent;
                    Console.WriteLine($"[Health] Rerouted to healthy agent: {healthyAgent}");
                }
                else
                {
                    return ShortCircuit("No healthy agents available");
                }
            }
        }
        
        return await next(message, ct);
    }
    
    private async void PerformHealthChecks(object? state)
    {
        foreach (var (agentId, status) in _health)
        {
            try
            {
                status.IsHealthy = await status.HealthCheck();
                status.LastCheck = DateTime.UtcNow;
                
                if (!status.IsHealthy)
                {
                    Console.WriteLine($"[Health] Agent {agentId} failed health check");
                }
            }
            catch (Exception ex)
            {
                status.IsHealthy = false;
                Console.WriteLine($"[Health] Health check error for {agentId}: {ex.Message}");
            }
        }
    }
    
    public Dictionary<string, bool> GetHealthStatus()
    {
        return _health.ToDictionary(kv => kv.Key, kv => kv.Value.IsHealthy);
    }
    
    private class HealthStatus
    {
        public string AgentId { get; set; } = "";
        public Func<Task<bool>> HealthCheck { get; set; } = () => Task.FromResult(true);
        public bool IsHealthy { get; set; }
        public DateTime LastCheck { get; set; }
    }
}

/// <summary>
/// Multi-stage workflow middleware
/// Orchestrates complex multi-agent workflows
/// </summary>
public class WorkflowOrchestrationMiddleware : MiddlewareBase
{
    private readonly Dictionary<string, WorkflowDefinition> _workflows = new();
    
    public void RegisterWorkflow(string workflowId, params WorkflowStage[] stages)
    {
        _workflows[workflowId] = new WorkflowDefinition
        {
            WorkflowId = workflowId,
            Stages = stages.ToList()
        };
    }
    
    public override async Task<MessageResult> InvokeAsync(
        AgentMessage message,
        MessageDelegate next,
        CancellationToken ct)
    {
        // Check if message is part of a workflow
        if (message.Metadata.TryGetValue("WorkflowId", out var workflowIdObj) &&
            workflowIdObj is string workflowId &&
            _workflows.TryGetValue(workflowId, out var workflow))
        {
            Console.WriteLine($"[Workflow] Processing workflow: {workflowId}");
            
            var stageIndex = message.Metadata.TryGetValue("StageIndex", out var stageObj)
                ? Convert.ToInt32(stageObj)
                : 0;
            
            if (stageIndex < workflow.Stages.Count)
            {
                var stage = workflow.Stages[stageIndex];
                Console.WriteLine($"[Workflow] Stage {stageIndex + 1}/{workflow.Stages.Count}: {stage.Name}");
                
                // Execute current stage
                var result = await next(message, ct);
                
                // If successful and not final stage, queue next stage
                if (result.Success && stageIndex < workflow.Stages.Count - 1)
                {
                    var nextStage = workflow.Stages[stageIndex + 1];
                    var nextMessage = new AgentMessage
                    {
                        SenderId = message.ReceiverId,
                        ReceiverId = nextStage.AgentId,
                        Subject = $"Workflow {workflowId} - Stage {stageIndex + 2}",
                        Content = result.Response ?? message.Content,
                        ConversationId = message.ConversationId,
                        Metadata = new Dictionary<string, object>(message.Metadata)
                        {
                            ["WorkflowId"] = workflowId,
                            ["StageIndex"] = stageIndex + 1
                        }
                    };
                    
                    result.ForwardedMessages.Add(nextMessage);
                    Console.WriteLine($"[Workflow] Queued next stage: {nextStage.Name}");
                }
                
                return result;
            }
        }
        
        return await next(message, ct);
    }
}

public class WorkflowDefinition
{
    public string WorkflowId { get; set; } = "";
    public List<WorkflowStage> Stages { get; set; } = new();
}

public class WorkflowStage
{
    public string Name { get; set; } = "";
    public string AgentId { get; set; } = "";
    public Func<AgentMessage, bool>? Condition { get; set; }
}
