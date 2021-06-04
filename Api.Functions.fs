// thin wrapper for Azure Functions, mapping to/from and invoking the Service Layer/CommandHandling
namespace XxxService.YyyHandling.Api.AzureFunctions

open System
open System.Text
open System.Threading
open Azure.Messaging.EventHubs
open Microsoft.AspNetCore.Http
open Microsoft.Azure.WebJobs
open Microsoft.Extensions.Logging
open Microsoft.ApplicationInsights
open Microsoft.ApplicationInsights.Extensibility
open Framework.Http

module BackgroundServices =
    let worker1 (logger:ILogger) (startedEvent: ManualResetEvent) (token:CancellationToken) : Async<unit> =
        async {
            startedEvent.Set() |> ignore
            while true do
                do logger.LogInformation("Hello from worker1")
                Thread.Sleep(5000)
        }        
    let worker2 (logger:ILogger) (startedEvent: ManualResetEvent) (token:CancellationToken) : Async<unit> =
        async {
            startedEvent.Set() |> ignore
            while true do
                do logger.LogInformation("Hello from worker2")
                Thread.Sleep(5000)
        }        

module WebApi =
    let webFunction1 (logger:ILogger) (req: HttpRequest) : Async<MappedHttpResponse> =
        async {
            do logger.LogInformation("webFunction1")
            return {
                StatusCode = 200
                Content = "Some response" |> MappedHttpResponseContent.Json
                Headers = List.empty
            }
        }

type WebJobs(telemetryConfiguration: TelemetryConfiguration) =
    let telemetryClient = TelemetryClient(telemetryConfiguration)

    [<FunctionName("HandleEventHubMessage")>]
    member _.HandleEventHubMessage
        ([<EventHubTrigger("", Connection = "EventHubConnectionString", ConsumerGroup = "test-cg")>]    // path to event hub is in the connection string
        msg: EventData,
        enqueuedTimeUtc: DateTime,
        sequenceNumber: Int64,
        offset: string,
        logger: ILogger)
        =
        async {
            do logger.LogInformation($"HandleEventHubMessage: {Encoding.UTF8.GetString(msg.Body.ToArray())}")
        } |> Async.StartAsTask

    [<FunctionName("HandleQueueMessage")>]
    member _.HandleQueueMessage
        ([<QueueTrigger("test-queue", Connection = "StorageQueueConnectionString")>] msg: string,
        logger:ILogger)
        =
        async {
            do logger.LogInformation($"HandleQueueMessage: {msg}")
        } |> Async.StartAsTask
        
    [<FunctionName("HandleTimerEvent")>]
    member this.HandleTimerEvent
        ([<TimerTrigger("0 0 0 1 * *", RunOnStartup = true)>] timer: TimerInfo,
        logger: ILogger)
        =
        async {
            do logger.LogInformation("HandleTimerEvent")
        } |> Async.StartAsTask
