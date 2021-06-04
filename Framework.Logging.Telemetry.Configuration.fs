module Framework.Logging.Telemetry.Configuration

open System
open Microsoft.ApplicationInsights.Channel
open Microsoft.ApplicationInsights.DataContracts
open Microsoft.ApplicationInsights.Extensibility

type CustomTelemetryProcessorConfig = {
    ExcludedStatusCodes: ((int list)*(string list)) list
}

let defaultCustomTelemetryProcessorConfig = {
    ExcludedStatusCodes = [
        [200], ["somepath"]
    ]}

type CustomTelemetryProcessor(next: ITelemetryProcessor, config: CustomTelemetryProcessorConfig) =
    let excludedStatusCodesDict =
        config.ExcludedStatusCodes
        |> List.collect (fun (statusCodes, paths) ->
            List.allPairs statusCodes [(if paths = List.empty then [""] else paths)])
        |> dict
        
    interface ITelemetryProcessor with

        member this.Process(item: ITelemetry) =
            if (item :? RequestTelemetry) then    // do it only for Requests
                let requestTelemetry = item :?> RequestTelemetry
                let (parsed,code) = Int32.TryParse(requestTelemetry.ResponseCode)
                if parsed
                   && excludedStatusCodesDict.ContainsKey(code)
                   && excludedStatusCodesDict.[code]
                      |> List.fold
                             (fun c i -> c || requestTelemetry.Url.AbsolutePath.Contains(i))
                             false
                    then
                        ()  // skip
                    else
                        next.Process(item)
            else
               next.Process(item)

let defaultSuppressedStatusCodes = [
    [500], ["somespecialpathforwhich500shouldnotbereported"]
    [400; 404; 409; 422], []
]

/// Custom TelemetryInitializer with primary purpose to set the cloud role name 
/// (commented out) Overrides the default SDK behavior of treating response codes >= 400 as failed requests
/// Failed Requests is a built-in App Insights metric, which can be used in alerts
/// Note: 303 is treated as failed only by APIM, App Insights treats 303 from the underlying services as successful request!
type CustomTelemetryInitializer (cloudRoleName: string, ?suppessedStatusCodes: ((int list)*(string list)) list) =
    let suppressedStatusCodesDict =
        suppessedStatusCodes
        |> Option.map (List.collect (fun (statusCodes, paths) ->
            List.allPairs statusCodes [(if paths = List.empty then [""] else paths)]))
        |> Option.map dict
    
    interface ITelemetryInitializer with
    
        member this.Initialize(item: ITelemetry) =
            item.Context.Cloud.RoleName <- cloudRoleName
            
            if item :? RequestTelemetry then    // do it only for Requests
                let requestTelemetry = item :?> RequestTelemetry 
                if not (isNull(requestTelemetry)) then
                    let (parsed,code) = Int32.TryParse(requestTelemetry.ResponseCode)
                    if parsed
                       && suppressedStatusCodesDict.IsSome
                       && suppressedStatusCodesDict.Value.ContainsKey(code)
                       && suppressedStatusCodesDict.Value.[code]
                          |> List.fold
                                 (fun c i -> c || requestTelemetry.Url.AbsolutePath.Contains(i))
                                 false
                        then
                            // If we set the Success property, the SDK won't change it:
                            requestTelemetry.Success <- Nullable(true)
                            // Allow us to filter these requests in the portal:
                            requestTelemetry.Properties.Add("SuppressedFailure", "true")
                        
            // else leave the SDK to set the Success property

