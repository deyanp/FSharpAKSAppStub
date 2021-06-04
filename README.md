# FSharpAKSAppStub

1. Create an Azure resource group with:
    1. Application Insights
    1. Event Hub Namespace, Event Hub, consumer group "test-cg"
    1. Storage Account

1. Edit Properties/launchSettings.json and replace all "TODO" with proper configuration:
    1. Application Insights Instrumentation Key   
    1. Event Hub Connection String, including event hub path (name)
    1. Storage Queue Connection String

1. Compile and Run
    1. (dotnet restore)
    1. dotnet build
    1. dotnet run

1. Deploy (Docker, ACR, AKS) - TODO
