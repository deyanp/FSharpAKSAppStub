# FSharpAKSAppStub

1. Create an Azure resource group with:
a. Application Insights
b. Event Hub Namespace, Event Hub, consumer group "test-cg"
c. Storage Account

2. Edit Properties/launchSettings.json and replace all "TODO" with proper configuration:
a. Application Insights Instrumentation Key
b. Event Hub Connection String, including event hub path (name)
c. Storage Queue Connection String

3. Compile and Run
a. (dotnet restore)
b. dotnet build
c. dotnet run

4. Deploy (Docker, ACR, AKS) - TODO
