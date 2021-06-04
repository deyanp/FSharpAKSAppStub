module XxxService.YyyHandling.Program

open System
open System.Threading
open System.Threading.Tasks
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Routing
open Microsoft.Azure.WebJobs
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Logging
open Framework.Tasks
open Framework.Http
open XxxService.YyyHandling.Api.AzureFunctions

let startedEvent1 = new ManualResetEvent(false) 
let startedEvent2 = new ManualResetEvent(false) 

let configureWorker1 (builder:IHostBuilder) : IHostBuilder =
    builder.ConfigureServices(fun context services ->
        services.AddSingleton<IHostedService>(
            fun serviceProvider -> 
                let logger = serviceProvider.GetService<ILogger<Object>>()
                Framework.BackgroundService.Hosted.create
                    "Worker1"
                    (BackgroundServices.worker1 logger startedEvent1) :> IHostedService) |> ignore
        )

let configureWorker2 (builder:IHostBuilder) : IHostBuilder =
    builder.ConfigureServices(fun context services ->
        services.AddSingleton<IHostedService>(
            fun serviceProvider -> 
                let logger = serviceProvider.GetService<ILogger<Object>>()
                Framework.BackgroundService.Hosted.create
                    "Worker2"
                    (BackgroundServices.worker2 logger startedEvent2) :> IHostedService) |> ignore
        )

let configureEndpoints (app:IApplicationBuilder) (endpoints:IEndpointRouteBuilder) =
    let hostedServices = Framework.BackgroundService.Hosted.findAll app.ApplicationServices

    let worker1 = Framework.BackgroundService.Hosted.find hostedServices "Worker1"
    endpoints.MapGet("api/v1/background-services/worker1/status", fun context ->
        worker1.GetProcessingStatusHttp()
        |> MappedHttpResponse.toHttpResponse context
        :> Task) |> ignore

    let worker2 = Framework.BackgroundService.Hosted.find hostedServices "Worker2"
    endpoints.MapGet("api/v1/background-services/worker2/status", fun context ->
        worker2.GetProcessingStatusHttp()
        |> MappedHttpResponse.toHttpResponse context
        :> Task) |> ignore
        
    endpoints.MapGet("api/v1/webFunction1", fun context ->
        let logger = app.ApplicationServices.GetService<ILogger<Object>>()
        
        WebApi.webFunction1 logger context.Request
        |> Async.StartAsTask
        |> Task.bind (MappedHttpResponse.toHttpResponse context)
        :> Task) |> ignore
    
let configureWebJobs (builder:IHostBuilder) = 
    builder.ConfigureWebJobs(fun b ->
        b.AddAzureStorageCoreServices() |> ignore
        b.AddEventHubs() |> ignore
        b.AddAzureStorageQueues() |> ignore
        b.AddTimers() |> ignore)

[<EntryPoint>]
let main argv =
    let builder =
        Host.CreateDefaultBuilder(argv)
        |> Framework.Hosting.HostBuilder.configureLogging
        |> Framework.Hosting.HostBuilder.configureAppInsights
        |> configureWorker1
        |> configureWorker2
        |> Framework.Hosting.HostBuilder.configureWebHost configureEndpoints
        |> configureWebJobs
    
    use tokenSource = new CancellationTokenSource()    
    use host = builder.Build()
    host.RunAsync(tokenSource.Token) |> Async.AwaitTask |> Async.RunSynchronously
    
    0 // return an integer exit code
