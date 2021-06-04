module Framework.Hosting.HostBuilder

open System
open Microsoft.AspNetCore.Routing
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Logging
open Microsoft.Extensions.Logging.Console
open Microsoft.AspNetCore.Hosting
open Microsoft.ApplicationInsights.Extensibility
open Microsoft.ApplicationInsights.AspNetCore.Extensions
open Microsoft.AspNetCore.Builder
open Framework.Configuration

let configureLogging (builder:IHostBuilder) : IHostBuilder =
    builder.ConfigureLogging(fun context b ->
//        b.ClearProviders() |> ignore  // commented out because otherwise Application Insights does not collect logger.Information ...
        b.AddConsole(fun options -> options.FormatterName <- "CustomConsoleFormatter")
            .AddConsoleFormatter<Framework.Logging.Console.CustomConsoleFormatter, Framework.Logging.Console.CustomConsoleFormatterOptions>(fun options ->
                options.Excludes <- Framework.Logging.Console.defaultExcludes
                options.SingleLinePerCategory <- ["Microsoft.Azure.WebJobs.Hosting.OptionsLoggingService", false; "Host.Triggers.Timer", false] |> dict
                options.SingleLine <- true
                options.ColorBehavior <- LoggerColorBehavior.Enabled)
        |> ignore
    )
    
let configureAppInsights (builder:IHostBuilder) : IHostBuilder =    
    builder.ConfigureServices(fun context services ->
        let aiOptions = ApplicationInsightsServiceOptions(EnableAdaptiveSampling = false)   // disable sampling (turned on by default, see https://docs.microsoft.com/en-us/azure/azure-monitor/app/sampling#brief-summary)
        services.AddApplicationInsightsTelemetry(aiOptions) |> ignore
        services.AddSingleton<ITelemetryInitializer>(
            Framework.Logging.Telemetry.Configuration.CustomTelemetryInitializer(
                Environment.GetEnvironmentVariable("WEBSITE_SITE_NAME", true),
                Framework.Logging.Telemetry.Configuration.defaultSuppressedStatusCodes))
        |> ignore

        services.AddSingleton<Framework.Logging.Telemetry.Configuration.CustomTelemetryProcessorConfig>(Framework.Logging.Telemetry.Configuration.defaultCustomTelemetryProcessorConfig) |> ignore
        services.AddApplicationInsightsTelemetryProcessor<Framework.Logging.Telemetry.Configuration.CustomTelemetryProcessor>() |> ignore
    )
    
let configureWebHost configureEndpoints (builder:IHostBuilder) : IHostBuilder =
    builder.ConfigureServices(fun context services ->
        if context.HostingEnvironment.IsDevelopment() then
            services.AddCors(fun options ->
                options.AddDefaultPolicy(fun b ->
                    b
                        .AllowCredentials()
                        .WithOrigins("http://localhost:8080")   // Or any other port on which some client is running in local dev
                        .WithHeaders("authorization","content-type","if-match","etag","content-disposition","x-requested-with")
                        .WithExposedHeaders("authorization","content-type","if-match","etag","content-disposition")
                        .WithMethods("GET","POST","PUT","PATCH")
                    |> ignore
                )
            ) |> ignore
        ) |> ignore
    
    builder.ConfigureWebHostDefaults(fun b ->
        b.Configure(fun context app ->
            app.UseRouting() |> ignore
            
            if context.HostingEnvironment.IsDevelopment() then        
                app.UseCors() |> ignore

            app.UseEndpoints (fun endpoints -> configureEndpoints app endpoints) |> ignore
            
        ) |> ignore
    )
    
[<AutoOpen>]
module IEndpointRouteBuilderExtensions =
    type IEndpointRouteBuilder with
        member this.MapPatch(pattern, requestDelegate) =
            this.MapMethods(pattern, ["patch"], requestDelegate)