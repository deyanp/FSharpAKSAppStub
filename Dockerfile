FROM mcr.microsoft.com/dotnet/aspnet:5.0
#ENV AzureWebJobsScriptRoot=/home/site/wwwroot \
#    AzureFunctionsJobHost__Logging__Console__IsEnabled=true

COPY ./publish /home/site/wwwroot
# COPY ./azure-functions-secrets /azure-functions-host/Secrets
WORKDIR /home/site/wwwroot
ENTRYPOINT ["dotnet", "XxxService.YyyHandling.dll"]