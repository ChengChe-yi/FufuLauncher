using System.Text.RegularExpressions;
using FufuLauncher.Helpers;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace FufuLauncher.Models;

public sealed class ServiceStatusDay
{
    public DateOnly Date
    {
        get;
        init;
    }

    public double Ratio
    {
        get;
        init;
    }

    public bool HasData
    {
        get;
        init;
    }
}

public sealed class ServiceStatusMonitor
{
    private static readonly SolidColorBrush OperationalBrush = new(Color.FromArgb(255, 46, 160, 67));
    private static readonly SolidColorBrush DegradedBrush = new(Color.FromArgb(255, 210, 153, 34));
    private static readonly SolidColorBrush OutageBrush = new(Color.FromArgb(255, 218, 54, 51));
    private static readonly SolidColorBrush PausedBrush = new(Color.FromArgb(255, 139, 148, 158));
    private static readonly SolidColorBrush UnknownBrush = new(Color.FromArgb(255, 139, 148, 158));

    public int MonitorId
    {
        get;
        init;
    }

    public string Name
    {
        get;
        init;
    } = string.Empty;

    public string StatusClass
    {
        get;
        init;
    } = string.Empty;

    public IReadOnlyList<ServiceStatusDay> Days
    {
        get;
        init;
    } = Array.Empty<ServiceStatusDay>();

    public double Uptime90
    {
        get;
        init;
    }

    public DateTime? LastDowntime
    {
        get;
        init;
    }

    public string StatusText => StatusClass switch
    {
        "success" => "ServiceStatus_Operational".GetLocalized(),
        "warning" => "ServiceStatus_Degraded".GetLocalized(),
        "danger" => "ServiceStatus_Outage".GetLocalized(),
        "paused" => "ServiceStatus_Paused".GetLocalized(),
        _ => "ServiceStatus_Unknown".GetLocalized()
    };

    public Brush StatusBrush => StatusClass switch
    {
        "success" => OperationalBrush,
        "warning" => DegradedBrush,
        "danger" => OutageBrush,
        "paused" => PausedBrush,
        _ => UnknownBrush
    };

    public string Uptime90Text => $"{Uptime90:0.00}%";

    public string CaptionText
    {
        get
        {
            var status = StatusText;
            if (LastDowntime is { } last)
            {
                var incident = string.Format("ServiceStatus_LastIncident".GetLocalized(),
                    last.ToString("MM-dd HH:mm", System.Globalization.CultureInfo.InvariantCulture));
                return $"{status} · {incident}";
            }

            if (StatusClass is "success" or "warning")
            {
                return $"{status} · {"ServiceStatus_NoIncidents".GetLocalized()}";
            }

            return status;
        }
    }
}

public sealed class ServiceStatusPage
{
    public IReadOnlyList<ServiceStatusMonitor> Monitors
    {
        get;
        init;
    } = Array.Empty<ServiceStatusMonitor>();

    public int TotalMonitors
    {
        get;
        init;
    }

    public int PerPage
    {
        get;
        init;
    } = 30;

    public int UpCount
    {
        get;
        init;
    }

    public int DownCount
    {
        get;
        init;
    }

    public int PausedCount
    {
        get;
        init;
    }
}

public sealed class ServiceResponseTimePoint
{
    public double Value
    {
        get;
        init;
    }

    public DateTime? Time
    {
        get;
        init;
    }
}

public sealed class ServiceResponseTimeStats
{
    public double Average
    {
        get;
        init;
    }

    public double Min
    {
        get;
        init;
    }

    public double Max
    {
        get;
        init;
    }

    public int TotalRequests
    {
        get;
        init;
    }
}

public sealed class ServiceMonitorEvent
{
    private static readonly Regex DurationPattern =
        new(@"(\d+)\s+(day|hour|minute|second)s?", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly SolidColorBrush OperationalBrush = new(Color.FromArgb(255, 46, 160, 67));
    private static readonly SolidColorBrush OutageBrush = new(Color.FromArgb(255, 218, 54, 51));
    private static readonly SolidColorBrush UnknownBrush = new(Color.FromArgb(255, 139, 148, 158));

    public string Class
    {
        get;
        init;
    } = string.Empty;

    public string Label
    {
        get;
        init;
    } = string.Empty;

    public DateTimeOffset? OccurredAt
    {
        get;
        init;
    }

    public string DateText
    {
        get;
        init;
    } = string.Empty;

    public string Duration
    {
        get;
        init;
    } = string.Empty;

    public string? ReasonCode
    {
        get;
        init;
    }

    public string? ReasonShort
    {
        get;
        init;
    }

    public string? ReasonFull
    {
        get;
        init;
    }

    public bool IsDown => string.Equals(Label, "down", StringComparison.OrdinalIgnoreCase) ||
                          string.Equals(Class, "danger", StringComparison.OrdinalIgnoreCase);

    public bool IsUp => string.Equals(Label, "up", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(Class, "success", StringComparison.OrdinalIgnoreCase);

    public string StatusText => IsDown
        ? "ServiceStatus_Outage".GetLocalized()
        : IsUp
            ? "ServiceStatus_Operational".GetLocalized()
            : "ServiceStatus_Unknown".GetLocalized();

    public Brush StatusBrush => IsDown ? OutageBrush : IsUp ? OperationalBrush : UnknownBrush;

    public string TimeText => OccurredAt is { } time
        ? time.ToLocalTime().ToString("MM-dd HH:mm")
        : DateText;

    public string DurationText => IsDown ? FormatDuration(Duration) : string.Empty;

    public string DetailText
    {
        get
        {
            if (!IsDown)
            {
                return string.Empty;
            }

            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(ReasonShort))
            {
                parts.Add(string.IsNullOrWhiteSpace(ReasonCode)
                    ? ReasonShort
                    : $"{ReasonShort} ({ReasonCode})");
            }

            if (!string.IsNullOrWhiteSpace(DurationText))
            {
                parts.Add(DurationText);
            }

            return string.Join(" · ", parts);
        }
    }

    public Visibility DetailVisibility =>
        string.IsNullOrWhiteSpace(DetailText) ? Visibility.Collapsed : Visibility.Visible;

    private static string FormatDuration(string duration)
    {
        if (string.IsNullOrWhiteSpace(duration))
        {
            return string.Empty;
        }

        var span = TimeSpan.Zero;
        foreach (System.Text.RegularExpressions.Match match in DurationPattern.Matches(duration))
        {
            if (!int.TryParse(match.Groups[1].Value, out var amount))
            {
                continue;
            }

            var unit = match.Groups[2].Value.ToLowerInvariant();
            span += unit switch
            {
                "day" => TimeSpan.FromDays(amount),
                "hour" => TimeSpan.FromHours(amount),
                "minute" => TimeSpan.FromMinutes(amount),
                _ => TimeSpan.FromSeconds(amount)
            };
        }

        if (span <= TimeSpan.Zero)
        {
            return string.Empty;
        }

        if (span.Days > 0)
        {
            return string.Format("ServiceDetail_DurationDays".GetLocalized(), span.Days, span.Hours);
        }

        if (span.Hours > 0)
        {
            return span.Minutes > 0
                ? string.Format("ServiceDetail_DurationHoursMinutes".GetLocalized(), span.Hours, span.Minutes)
                : string.Format("ServiceDetail_DurationHours".GetLocalized(), span.Hours);
        }

        return span.Minutes > 0
            ? string.Format("ServiceDetail_DurationMinutes".GetLocalized(), span.Minutes)
            : string.Format("ServiceDetail_DurationSeconds".GetLocalized(), span.Seconds);
    }
}

public sealed class ServiceMonitorDetail
{
    private static readonly SolidColorBrush OperationalBrush = new(Color.FromArgb(255, 46, 160, 67));
    private static readonly SolidColorBrush DegradedBrush = new(Color.FromArgb(255, 210, 153, 34));
    private static readonly SolidColorBrush OutageBrush = new(Color.FromArgb(255, 218, 54, 51));
    private static readonly SolidColorBrush UnknownBrush = new(Color.FromArgb(255, 139, 148, 158));

    public int MonitorId
    {
        get;
        init;
    }

    public string Name
    {
        get;
        init;
    } = string.Empty;

    public string StatusClass
    {
        get;
        init;
    } = string.Empty;

    public string Type
    {
        get;
        init;
    } = string.Empty;

    public string CheckInterval
    {
        get;
        init;
    } = string.Empty;

    public DateTime? CreatedAt
    {
        get;
        init;
    }

    public string? Url
    {
        get;
        init;
    }

    public string? Timezone
    {
        get;
        init;
    }

    public IReadOnlyList<ServiceStatusDay> Days
    {
        get;
        init;
    } = Array.Empty<ServiceStatusDay>();

    public IReadOnlyList<ServiceResponseTimePoint> ResponseTimes
    {
        get;
        init;
    } = Array.Empty<ServiceResponseTimePoint>();

    public ServiceResponseTimeStats ResponseTimeStats
    {
        get;
        init;
    } = new();

    public IReadOnlyList<ServiceMonitorEvent> Events
    {
        get;
        init;
    } = Array.Empty<ServiceMonitorEvent>();

    public IReadOnlyList<string> Regions
    {
        get;
        init;
    } = Array.Empty<string>();

    public double Uptime1
    {
        get;
        init;
    }

    public double Uptime7
    {
        get;
        init;
    }

    public double Uptime30
    {
        get;
        init;
    }

    public double Uptime90
    {
        get;
        init;
    }

    public string StatusText => StatusClass switch
    {
        "success" => "ServiceStatus_Operational".GetLocalized(),
        "warning" => "ServiceStatus_Degraded".GetLocalized(),
        "danger" => "ServiceStatus_Outage".GetLocalized(),
        "paused" => "ServiceStatus_Paused".GetLocalized(),
        _ => "ServiceStatus_Unknown".GetLocalized()
    };

    public Brush StatusBrush => StatusClass switch
    {
        "success" => OperationalBrush,
        "warning" => DegradedBrush,
        "danger" => OutageBrush,
        _ => UnknownBrush
    };

    public string HeaderCaption => string.IsNullOrWhiteSpace(Type)
        ? StatusText
        : $"{StatusText} · {Type}";

    public string Uptime1Text => $"{Uptime1:0.000}%";

    public string Uptime7Text => $"{Uptime7:0.000}%";

    public string Uptime30Text => $"{Uptime30:0.000}%";

    public string Uptime90Text => $"{Uptime90:0.000}%";

    public string AverageResponseText => ResponseTimeStats.Average > 0
        ? $"{ResponseTimeStats.Average:0}ms"
        : "--";

    public string MaxResponseText => ResponseTimeStats.Max > 0
        ? $"{ResponseTimeStats.Max:0}ms"
        : "--";

    public string MinResponseText => ResponseTimeStats.Min > 0
        ? $"{ResponseTimeStats.Min:0}ms"
        : "--";

    public string TotalChecksText => ResponseTimeStats.TotalRequests > 0
        ? ResponseTimeStats.TotalRequests.ToString()
        : "--";

    public bool HasResponseTimes => ResponseTimes.Count > 0;

    public bool HasEvents => Events.Count > 0;
}