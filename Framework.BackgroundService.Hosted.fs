module Framework.BackgroundService.Hosted

open System
open System.Text.Json
open System.Threading
open System.Threading.Tasks
open Framework.Http
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.DependencyInjection

module Task = 
    let isRunning (task:Task option) =
        task
        |> Option.map (fun t ->
            match t.Status with
            | TaskStatus.WaitingForActivation
            | TaskStatus.WaitingToRun
            | TaskStatus.Running ->
                true
            | _ ->
                false)
        |> Option.defaultValue false

module Dtos =
    type Status = {
        IsRunning: bool
        IsCancelled: bool
        StartTriggeredAt: DateTime option
        StopTriggeredAt: DateTime option
        ProcessedItems: int
        IsFaulted: bool
        UnderlyingTaskStatus: string 
    }   

type IQueryableHostedService =
    abstract member Name : string with get
    abstract member HandleProcessed : unit -> unit
    abstract member GetProcessingStatus : unit -> Dtos.Status
    abstract member GetProcessingStatusHttp : unit -> MappedHttpResponse

let create name executeOperation =
    let mutable startTriggeredAt : DateTime option = None
    let mutable processedItems = 0
    let mutable task:Task option = None

    // An object expression is used because it seems that .NET Core Dependency Injection needs a new type (class) for every injected IHostedService instance ..     
    { new BackgroundService() with
        member _.ExecuteAsync(cancellationToken: CancellationToken) : Task =
            let t = executeOperation cancellationToken |> Async.StartAsTask :> Task
            task <- Some t
            startTriggeredAt <- DateTime.UtcNow |> Some
            t
      interface IQueryableHostedService with
        member _.Name
            with get () = name
        member this.HandleProcessed() =
            processedItems <- processedItems + 1 

        member this.GetProcessingStatus () : Dtos.Status =
            {
                IsRunning = Task.isRunning task
                IsCancelled = task |> Option.map (fun t -> t.IsCanceled) |> Option.defaultValue false
                StartTriggeredAt = startTriggeredAt
                StopTriggeredAt = None  // does not make sense for HostedService
                ProcessedItems = processedItems
                IsFaulted = task |> Option.map (fun t -> t.IsFaulted) |> Option.defaultValue false
                UnderlyingTaskStatus = task |> Option.map (fun t -> t.Status.ToString()) |> Option.defaultValue ""
            }
            
        member this.GetProcessingStatusHttp () =
            {
                StatusCode = 200
                Content = this.GetProcessingStatus () |> JsonSerializer.Serialize |> MappedHttpResponseContent.Json
                Headers = [ "Content-Type", "application/json" ]
            }
    }

let findAll (serviceProvider: IServiceProvider) : IQueryableHostedService seq =
    serviceProvider.GetServices<IHostedService>()
    |> Seq.choose (fun hostedService ->
        if hostedService :? IQueryableHostedService then
            hostedService :?> IQueryableHostedService |> Some
        else None)

let find (hostedServices:IQueryableHostedService seq) name : IQueryableHostedService =
    hostedServices
    |> Seq.find (fun s -> s.Name = name)
                    