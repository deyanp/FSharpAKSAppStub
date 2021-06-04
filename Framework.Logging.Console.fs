module Framework.Logging.Console

// See https://docs.microsoft.com/en-us/dotnet/core/extensions/console-log-formatter for general information
// The majority of the code below was copied (translated into F#) from https://github.com/dotnet/runtime/blob/main/src/libraries/Microsoft.Extensions.Logging.Console/src/SimpleConsoleFormatter.cs

open System
open System.Collections.Generic
open System.IO
open Microsoft.Extensions.Logging
open Microsoft.Extensions.Logging.Abstractions
open Microsoft.Extensions.Logging.Console
open Microsoft.Extensions.Options

let defaultExcludes = [ "/api/v1/readiness - -" ]

type CustomConsoleFormatterOptions() =
    inherit ConsoleFormatterOptions()
        member val Excludes : string list = List.empty with get, set
        member val ColorBehavior : LoggerColorBehavior = LoggerColorBehavior.Default with get, set
        member val SingleLine : bool = true with get, set
        member val SingleLinePerCategory : IDictionary<string, bool> = Dictionary<string,bool>() :> IDictionary<string, bool> with get, set

type ConsoleColors = {
    Foreground: ConsoleColor
    Background: ConsoleColor
}

type CustomConsoleFormatter(options: IOptions<CustomConsoleFormatterOptions>) =
    inherit ConsoleFormatter("CustomConsoleFormatter")

    let getLogLevelConsoleColors (logLevel: LogLevel) (colorBehavior:LoggerColorBehavior) : ConsoleColors option =
        let disableColors =
            (colorBehavior = LoggerColorBehavior.Disabled) ||
            (colorBehavior = LoggerColorBehavior.Default && System.Console.IsOutputRedirected)
        
        if disableColors then
            None
        else
            // We must explicitly set the background color if we are setting the foreground color,
            // since just setting one can look bad on the users console.
            match logLevel with
            | LogLevel.Trace -> Some { Foreground = ConsoleColor.Gray; Background = ConsoleColor.Black }
            | LogLevel.Debug -> Some { Foreground = ConsoleColor.Gray; Background = ConsoleColor.Black }
            | LogLevel.Information -> Some { Foreground = ConsoleColor.DarkGreen; Background = ConsoleColor.Black }
            | LogLevel.Warning -> Some { Foreground = ConsoleColor.Yellow; Background = ConsoleColor.Black }
            | LogLevel.Error -> Some { Foreground = ConsoleColor.Black; Background = ConsoleColor.DarkRed }
            | LogLevel.Critical -> Some { Foreground = ConsoleColor.White; Background = ConsoleColor.DarkRed }
            | _ -> None

    let getLogLevelString (logLevel: LogLevel) : string =
        match logLevel with
        | LogLevel.Trace -> "trce"
        | LogLevel.Debug -> "dbug"
        | LogLevel.Information -> "info"
        | LogLevel.Warning -> "warn"
        | LogLevel.Error -> "fail"
        | LogLevel.Critical -> "crit"
        | _ -> failwith $"Unsupported logLevel {logLevel}" 
    
    let loglevelPadding = ": "
    let messagePadding = String(' ', getLogLevelString(LogLevel.Information).Length + loglevelPadding.Length)
    let newLineWithMessagePadding = Environment.NewLine + messagePadding

    let defaultForegroundColor = "\x1B[39m\x1B[22m"; // reset to default foreground color
    let defaultBackgroundColor = "\x1B[49m"; // reset to the background color        
    
    let getBackgroundColorEscapeCode(color: ConsoleColor): string = 
        match color with
        | ConsoleColor.Black -> "\x1B[40m"
        | ConsoleColor.DarkRed -> "\x1B[41m"
        | ConsoleColor.DarkGreen -> "\x1B[42m"
        | ConsoleColor.DarkYellow -> "\x1B[43m"
        | ConsoleColor.DarkBlue -> "\x1B[44m"
        | ConsoleColor.DarkMagenta -> "\x1B[45m"
        | ConsoleColor.DarkCyan -> "\x1B[46m"
        | ConsoleColor.Gray -> "\x1B[47m"
        | _ -> defaultBackgroundColor // Use default background color
    
    let getForegroundColorEscapeCode(color: ConsoleColor) : string = 
        match color with
        | ConsoleColor.Black -> "\x1B[30m"
        | ConsoleColor.DarkRed -> "\x1B[31m"
        | ConsoleColor.DarkGreen -> "\x1B[32m"
        | ConsoleColor.DarkYellow -> "\x1B[33m"
        | ConsoleColor.DarkBlue -> "\x1B[34m"
        | ConsoleColor.DarkMagenta -> "\x1B[35m"
        | ConsoleColor.DarkCyan -> "\x1B[36m"
        | ConsoleColor.Gray -> "\x1B[37m"
        | ConsoleColor.Red -> "\x1B[1m\x1B[31m"
        | ConsoleColor.Green -> "\x1B[1m\x1B[32m"
        | ConsoleColor.Yellow -> "\x1B[1m\x1B[33m"
        | ConsoleColor.Blue -> "\x1B[1m\x1B[34m"
        | ConsoleColor.Magenta -> "\x1B[1m\x1B[35m"
        | ConsoleColor.Cyan -> "\x1B[1m\x1B[36m"
        | ConsoleColor.White -> "\x1B[1m\x1B[37m"
        | _ -> defaultForegroundColor // default foreground color
        
    let writeColoredMessage (textWriter: TextWriter) (message: string) (background: ConsoleColor) (foreground: ConsoleColor) =
        // Order: backgroundcolor, foregroundcolor, Message, reset foregroundcolor, reset backgroundcolor
        textWriter.Write(getBackgroundColorEscapeCode background)
        textWriter.Write(getForegroundColorEscapeCode foreground)
        textWriter.Write(message)
        textWriter.Write(defaultForegroundColor) // reset to default foreground color
        textWriter.Write(defaultBackgroundColor) // reset to the background color
        
    let writeMessage (textWriter:TextWriter) (message:string) singleLine =
        if singleLine then
            textWriter.Write(' ')
            let newMessage = message.Replace(Environment.NewLine, " ")
            textWriter.Write(newMessage)
        else
            textWriter.Write(messagePadding)
            let newMessage = message.Replace(Environment.NewLine, newLineWithMessagePadding)
            textWriter.Write(newMessage)          
    
    let write (logEntry: LogEntry<'TState>) (message: string) (scopeProvider: IExternalScopeProvider) (textWriter: TextWriter) =
        let logLevelColors = getLogLevelConsoleColors logEntry.LogLevel options.Value.ColorBehavior
        let logLevelString = getLogLevelString logEntry.LogLevel

        if not (isNull options.Value.TimestampFormat) then
            let dateTimeOffset =
                if options.Value.UseUtcTimestamp then DateTimeOffset.UtcNow else DateTimeOffset.Now
            let timestamp = dateTimeOffset.ToString(options.Value.TimestampFormat)
            textWriter.Write(timestamp)
        
        match logLevelColors with
        | Some logLevelColors ->
            writeColoredMessage textWriter logLevelString logLevelColors.Background logLevelColors.Foreground
        | None ->
            textWriter.Write(logLevelString)

        textWriter.Write(loglevelPadding)
        textWriter.Write(logEntry.Category)
        textWriter.Write('[')
        textWriter.Write(logEntry.EventId.Id.ToString())
        textWriter.Write(']')
        
        let singleLine =
            let found, singleLinePerCategory = options.Value.SingleLinePerCategory.TryGetValue(logEntry.Category)
            if found then
                singleLinePerCategory
            else options.Value.SingleLine
            
        
        if not options.Value.SingleLine then
            textWriter.Write(Environment.NewLine);

        if options.Value.IncludeScopes && not (isNull scopeProvider) then
            let mutable paddingNeeded = not singleLine
            scopeProvider.ForEachScope(fun scope (writer:TextWriter) ->
                if paddingNeeded then
                    paddingNeeded <- false
                    writer.Write(messagePadding)
                    writer.Write("=> ")
                else
                    writer.Write(" => ")
                writer.Write(scope)
            , textWriter)

            if not paddingNeeded && not singleLine then
                textWriter.Write(Environment.NewLine)


        if not (String.IsNullOrEmpty(message)) then
           writeMessage textWriter message singleLine
                
        if not (isNull logEntry.Exception) then
            // exception message
            // Example:
            // System.InvalidOperationException
            //    at Namespace.Class.Function() in File:line X
            // writeMessage textWriter (logEntry.Exception.ToString()) singleLine
            textWriter.Write(Environment.NewLine)
            writeMessage textWriter (logEntry.Exception.ToString()) false
        
//            if singleLine then
        textWriter.Write(Environment.NewLine)        
    
    override _.Write<'TState>(logEntry: LogEntry<'TState> inref, scopeProvider: IExternalScopeProvider, textWriter: TextWriter) =    
        let message: string = logEntry.Formatter.Invoke(logEntry.State, logEntry.Exception)
        
        if not (isNull message) || not (isNull logEntry.Exception) then
            let skip =
                options.Value.Excludes
                |> List.fold (fun (state:bool) (e:string) -> message.Contains(e) || state) false

            if not skip then
                write logEntry message scopeProvider textWriter
                
