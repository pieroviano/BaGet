# OpenTelemetry: traces to Jaeger, metrics and logs over OTLP, configurable live from the Settings page

## Blocking — resolve before coding

| # | Check | Why it blocks | How |
|---|---|---|---|
| **B1** | OpenTelemetry package versions vs the `Microsoft.Extensions.*` **9.0.21** pin | Recent `OpenTelemetry.*` releases may depend on `Microsoft.Extensions.*` 10.x. That is an `NU1605` downgrade error against the pin, which EF/MySQL forces (CLAUDE.md, *Package versions*). | `dotnet add package` into a scratch copy, then read the `project.assets.json` dependency graph. Pick the newest version set with every `Microsoft.Extensions.*` ≤ 9.0.21, or confirm a 10.x dependency resolves cleanly. Record the versions in `src/Directory.Build.props`. |
| **B2** | Database spans | EF Core 9 emits no `ActivitySource`. `OpenTelemetry.Instrumentation.EntityFrameworkCore` is **prerelease**. Driver sources differ: `Npgsql` and `MySqlConnector` emit natively, SqlClient needs a prerelease package, SQLite emits nothing. | Decide: prerelease EF instrumentation, or driver sources only (no SQLite or SQL Server spans). Default proposal: the EF package behind `Telemetry:Traces:Database` (default on), documented as prerelease. |
| **B3** | Live exporter swap | The SDK builds exporters once. D2 relies on wrapping `OtlpTraceExporter`, `OtlpMetricExporter` and the log sink in a reloadable decorator. | Spike: a `BaseExporter<Activity>` decorator swapping its inner exporter under a lock while a `BatchActivityExportProcessor` is running. Check no batch is lost or double-disposed, and that `ForceFlush`/`Shutdown` reach the current inner exporter. |
| **B4** | Logs path and the double delivery | `CLAUDE.md`, *Logging*: Serilog's `writeToProviders` delivers every event **twice** to a registered `ILoggerProvider`. An OTel logger provider fed that way would export duplicates. | Use a **Serilog sink** (`Serilog.Sinks.OpenTelemetry`) wrapped like B3, not `builder.Logging.AddOpenTelemetry`. Verify the sink carries `TraceId`/`SpanId` (Serilog 3+ records them from `Activity.Current`) and supports gRPC. |
| B5 | Jaeger accepts traces only | Jaeger's OTLP receiver rejects metrics and logs (`Unimplemented`). | Confirm against `jaegertracing/all-in-one` (B6). Metrics and logs therefore get their **own endpoints**, empty by default = not exported (D4). |
| B6 | Test receiver | Proving export needs a real Jaeger. | Testcontainers `jaegertracing/all-in-one` (OTLP 4317, query API 16686), skipped without Docker like `BaGet.Database.Tests`. |

## Decisions

| # | Decision | Consequence |
|---|---|---|
| D1 | **Traces + metrics + logs** over OTLP | Traces go to Jaeger. Metrics and logs need an OTLP backend (OTel Collector, Prometheus via the collector, Loki, Seq). Serilog stays the logging pipeline; OTLP is one more sink. |
| D2 | **Live: no restart.** Changing `Telemetry:*` on the Settings page applies within seconds | Providers are always built. A dynamic sampler and the reloadable exporters read `IOptionsMonitor<TelemetryOptions>`. Off = sampler drops everything and exporters no-op, so near-zero cost remains. |
| D3 | **Never block; show status.** Export is best effort (bounded batch queue, drops when unreachable, no user-visible errors) | A `TelemetryStatus` singleton records each signal's last export result. The Settings page shows it and offers **Test connection**. |
| D4 | **gRPC on 4317 by default**, `HttpProtobuf` on 4318 selectable | `Telemetry:Otlp:Endpoint` (e.g. `http://jaeger:4317`) applies to traces. `Metrics:Endpoint` / `Logs:Endpoint` override per signal; empty means that signal is off. |
| D5 | Libraries stay OTel-free: `System.Diagnostics.ActivitySource` / `Meter` named `"BaGet"` in `BaGet.Core` | Only the host references OpenTelemetry, as with Serilog. |
| D6 | **Default off** (`Telemetry:Enabled=false`) | Upgrades change nothing until an endpoint is set. |

## Changes

### C1 — Options (`BaGet.Core`)

`src/BaGet.Core/Configuration/TelemetryOptions.cs` (new). It is registered via `AddBaGetOptions` next to
`DependencyInjectionExtensions.cs:76-77`, and exposed as `BaGetOptions.Telemetry` (after `BaGetOptions.cs:83`). The
Settings catalog then shows it as a "Telemetry" section automatically.

```csharp
public class TelemetryOptions                       // not [RestartRequired]: applies live (D2)
{
    public bool Enabled { get; set; }
    public string ServiceName { get; set; } = "baget";
    public OtlpOptions Otlp { get; set; } = new();
    public TracesOptions Traces { get; set; } = new();     // Enabled=true, SamplingRatio=1.0, Database=true
    public SignalOptions Metrics { get; set; } = new();    // Endpoint empty = off; ExportInterval 60 s
    public SignalOptions Logs { get; set; } = new();       // Endpoint empty = off; MinimumLevel Information
}
public class OtlpOptions
{
    [SettingExample(nameof(Protocol), "Grpc", "http://jaeger:4317")]
    [SettingExample(nameof(Protocol), "HttpProtobuf", "http://jaeger:4318")]
    public string? Endpoint { get; set; }
    [AllowedSettingValues("Grpc", "HttpProtobuf")] public string Protocol { get; set; } = "Grpc";
    [SensitiveSetting] public string? Headers { get; set; }   // "api-key=..." for hosted collectors
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(10);
}
```

Validation (`ConfigureBaGetOptions`): absolute `http`/`https` endpoint; `SamplingRatio` in [0,1].

### C2 — Instrumentation sources (`BaGet.Core`)

`src/BaGet.Core/Telemetry/BaGetDiagnostics.cs` (new) holds `ActivitySource Source = new("BaGet")` and
`Meter Meter = new("BaGet")`. Spans and metrics come at:

| Span / metric | Where |
|---|---|
| `package.index` (id, version, result) | `PackageIndexingService.IndexAsync` |
| `package.validate` | `PackageValidationRunner` |
| `mirror.fetch` | the mirror service (`PackageService`) |
| `database.copy` + per-table child spans | `DatabaseCopyService` |
| `baget.packages.pushed`, `baget.downloads`, `baget.validation.results` | counters at the same points |

Tags never include API keys, connection strings or user emails.

### C3 — Host wiring (`src/BaGet/Program.cs`)

`ConfigureTelemetry(builder)` is called from `BuildApplication` next to `ConfigureLogging` (`Program.cs:303`):

```csharp
services.AddOpenTelemetry()
    .ConfigureResource(r => r.AddService(serviceName, serviceVersion: assemblyVersion))
    .WithTracing(t => t
        .SetSampler(sp => new DynamicSampler(sp.GetRequiredService<IOptionsMonitor<TelemetryOptions>>()))
        .AddSource("BaGet")
        .AddAspNetCoreInstrumentation(o => { o.Filter = IsNotStaticFile; })   // query values redacted (C6)
        .AddHttpClientInstrumentation()                                         // mirror/upstream, NuGetClient
        .AddEntityFrameworkCoreInstrumentation()                                // B2
        .AddProcessor(sp => new BatchActivityExportProcessor(sp.GetRequiredService<ReloadableTraceExporter>())))
    .WithMetrics(m => m
        .AddMeter("BaGet").AddAspNetCoreInstrumentation().AddHttpClientInstrumentation().AddRuntimeInstrumentation()
        .AddReader(sp => new PeriodicExportingMetricReader(sp.GetRequiredService<ReloadableMetricExporter>())));
```

| New type (`src/BaGet/Telemetry/`) | Role |
|---|---|
| `DynamicSampler` | `ParentBased(TraceIdRatio(ratio))` when `Enabled && Traces.Enabled && endpoint set`, else `Drop`. Reads the monitor on each call |
| `ReloadableExporter<T>` (+ trace and metric subclasses) | Holds the current `OtlpExporter`. Rebuilds it on `IOptionsMonitor.OnChange` (endpoint, protocol, headers, timeout). No-op `Success` when off. Records results in `TelemetryStatus` |
| `ReloadableOpenTelemetrySink` | Serilog `ILogEventSink` wrapping `Serilog.Sinks.OpenTelemetry` the same way (B4). Added in `ConfigureLogging` (`Program.cs:315-326`) alongside `ReadFrom.Configuration` |
| `TelemetryStatus` | Singleton: per signal, last attempt, last success, last error (scrubbed) |
| `TelemetryConnectionTester` | Exports a single synthetic span to the configured endpoint, with timeout. Returns ok, or the error text |

### C4 — Settings page (`src/BaGet.Identity/Pages/Admin/Settings.cshtml(.cs)`)

| Change | Where |
|---|---|
| Status panel in the Telemetry section: per signal "last export ok at …" / "failing since …: error" | `Settings.cshtml` Telemetry form (forms render at line 51) |
| **Test connection** button: `OnPostTestTelemetryAsync`, admin-only, antiforgery, uses the *posted* (unsaved) endpoint/protocol/headers, 10 s timeout | `Settings.cshtml.cs`, next to `OnPostAsync` (line 108) |
| Save applies live: the existing `SettingsService.SaveAsync` then `root.Reload()` fires `OnChange` (no extra code beyond C3) | — |

### C5 — Configuration and docs

| File | Change |
|---|---|
| `src/BaGet/appsettings.json` (after `Serilog`, line 94) | `"Telemetry": { "Enabled": false, "Otlp": { "Endpoint": "", "Protocol": "Grpc" } }` |
| `src/Directory.Build.props` (after line 69) | Version levers from B1 |
| `docs/configuration.md` | Telemetry section: keys, Jaeger example, live reload, per-signal endpoints, what is never exported |
| `docs/installation/docker.md` | `docker compose` example with `jaegertracing/all-in-one` and `Telemetry__Otlp__Endpoint=http://jaeger:4317` |
| `CLAUDE.md` | Telemetry architecture paragraph; test counts |

### C6 — Privacy

- ASP.NET Core instrumentation: verify `url.query` values are redacted (recent versions redact them by default). Otherwise enrich to strip the query. Never record request headers (`X-NuGet-ApiKey`).
- `Otlp:Headers` is `[SensitiveSetting]`, and is never logged or shown in status errors.
- The OTLP log sink receives exactly what Serilog would write: the `Hosting.Diagnostics` Warning floor stays in force.

## Verification

| # | Check | Expected |
|---|---|---|
| 1 | `Enabled=false` (default), full suite | Unchanged behaviour; no export attempts; 0 warnings |
| 2 | In-memory exporter in the harness: push a package | Spans: ASP.NET request → `package.index` → EF commands, sharing one trace id |
| 3 | Jaeger container (B6): enable via the Settings page, no restart | Trace for a push visible through Jaeger's query API within 10 s |
| 4 | Change endpoint to an unreachable host | Requests unaffected; status shows "failing since"; restoring it resumes |
| 5 | Disable on the Settings page | No new spans exported; sampler drops |
| 6 | Test connection: good / bad / unsaved values | ok / error text / uses the posted values, saves nothing |
| 7 | Metrics or logs endpoint empty | Not exported; with an endpoint (a collector container), received |
| 8 | Logs carry trace ids | A log emitted inside a request has the request's `TraceId` |
| 9 | Secrets | No `apikey` query value, API-key header, connection string or OTLP header in spans, logs or status |
| 10 | Load | Under a push loop with Jaeger down: bounded memory (queue size), no latency regression above 5 % |

## Risks

| Risk | Handling |
|---|---|
| Version conflict with the 9.0.21 pin | B1; fallback is an older OpenTelemetry line |
| Prerelease EF instrumentation | B2; behind `Traces:Database`, documented |
| Exporter swap races | B3 spike; swap under a lock, dispose the old exporter after its in-flight batch |
| Jaeger down fills memory | Bounded `BatchExportProcessorOptions.MaxQueueSize`; drops counted in `TelemetryStatus` |
| Duplicate logs via `writeToProviders` | B4: Serilog sink, not an `ILoggerProvider` |
| Embedded hosts (`samples/BaGetWebApplication`, BaGet.Service) | Wiring lives in the host; libraries only emit `ActivitySource`/`Meter`, which cost nothing without a listener |

## Out of scope

Jaeger's legacy Thrift/UDP exporter (deprecated); Prometheus scrape endpoint (`/metrics`); tail sampling.
