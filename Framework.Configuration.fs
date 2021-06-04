module Framework.Configuration

open System

type Environment with
    static member GetEnvironmentVariable(name:string, throwIfNotFound:bool) : string =
        let value = Environment.GetEnvironmentVariable(name)
        if throwIfNotFound && String.IsNullOrEmpty(value) then
            failwith $"Missing environment variable %s{name}!"
        else
            value
            
