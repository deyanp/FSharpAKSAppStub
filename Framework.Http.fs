module Framework.Http

open System.IO
open System.Collections.Generic
open System.Threading.Tasks
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.Primitives
open FSharp.Control.Tasks.V2.ContextInsensitive

type MappedHttpResponseContent =
| Json of string
| File of FileName: string * FileContent: Stream
| NoContent

type MappedHttpResponse = {
    StatusCode : int
    Content: MappedHttpResponseContent
    Headers: (string * string) list
}

module MappedHttpResponse =
    let toHttpResponse (ctx: HttpContext) (response: MappedHttpResponse) : Task<Unit> =
        task {
            ctx.Response.StatusCode <- int response.StatusCode
            
            response.Headers
            |> Seq.iter(fun (name, value) ->
                ctx.Response.Headers.Add(KeyValuePair(name, StringValues(value))))
            
            match response.Content with
            | MappedHttpResponseContent.Json jsonContent ->
                do! ctx.Response.WriteAsync(jsonContent)
            | MappedHttpResponseContent.File (_, fileContent) ->
                do! fileContent.CopyToAsync(ctx.Response.Body)
            | MappedHttpResponseContent.NoContent ->
                ()
        }        
