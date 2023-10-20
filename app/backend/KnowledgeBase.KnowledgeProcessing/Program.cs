using Microsoft.AspNetCore.Mvc;
using Dapr;
using KnowledgeBase.Shared;
using Dapr.Client;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.Memory.AzureSearch;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddDaprClient();

var app = builder.Build();

app.UseCloudEvents();
app.UseRouting();
app.UseEndpoints(endpoints => endpoints.MapSubscribeHandler());


app.MapGet("/probe", () => new OkResult());

app.MapPost("/knowledgeprocess", [Topic("skcodemotion2023queue", "knowledgeprocess")] async (DaprClient daprClient, KnowledgeProcessing knowledgeProcessing) => 
{
    Console.WriteLine($"Processing document {knowledgeProcessing.DocumentFrame}");
    var azureOpenAIserviceEndpoint = (await daprClient.GetSecretAsync("skcodemotion2023akv", "AzureOpenAiServiceEndpoint")).Values.FirstOrDefault();
    var azureOpenAIServiceKey = (await daprClient.GetSecretAsync("skcodemotion2023akv", "AzureOpenAiServiceKey")).Values.FirstOrDefault();
    var azureSearchEndpoint = (await daprClient.GetSecretAsync("skcodemotion2023akv", "AzureSearchServiceEndpoint")).Values.FirstOrDefault();
    var azureSearchKey = (await daprClient.GetSecretAsync("skcodemotion2023akv", "AzureSearchServiceKey")).Values.FirstOrDefault();
    var azureSearchKeyIndex = (await daprClient.GetSecretAsync("skcodemotion2023akv", "AzureSearchIndex")).Values.FirstOrDefault();

    var semanticKernel = Kernel.Builder
        .WithAzureTextEmbeddingGenerationService(
            "text-embedding-ada-002",
            azureOpenAIserviceEndpoint,
            azureOpenAIServiceKey)
        .WithMemoryStorage(new AzureSearchMemoryStore(
            azureSearchEndpoint,
            azureSearchKey))
        .Build();

    await semanticKernel.Memory.SaveInformationAsync(azureSearchKeyIndex, knowledgeProcessing.DocumentFrame, $"{knowledgeProcessing.BlobName}-{knowledgeProcessing.PageNumber}");

    return new OkResult();
});

app.Run();
