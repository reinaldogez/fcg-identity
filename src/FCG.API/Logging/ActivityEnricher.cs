using System.Diagnostics;
using Serilog.Core;
using Serilog.Events;

namespace FCG.API.Logging;

public class ActivityEnricher : ILogEventEnricher
{
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        Activity? activity = Activity.Current;

        if (activity is null)
        {
            return;
        }

        logEvent.AddPropertyIfAbsent(
            propertyFactory.CreateProperty("TraceId", activity.TraceId.ToHexString())
        );
        logEvent.AddPropertyIfAbsent(
            propertyFactory.CreateProperty("SpanId", activity.SpanId.ToHexString())
        );
    }
}
