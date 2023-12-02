using Microsoft.AspNetCore.Mvc;
using Dapr;
using KnowledgeBase.Shared;
using Dapr.Client;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.Memory.AzureSearch;
using System.Reflection.Metadata;
using KnowledgeBase.DocumentProcessing.Services;


var builder = WebApplication.CreateBuilder(args);
builder.Services.AddDaprClient();
builder.Services.AddSingleton<IFormRecognizerManager, FormRecognizerManager>();

var app = builder.Build();

app.UseCloudEvents();
app.UseRouting();
app.UseEndpoints(endpoints => endpoints.MapSubscribeHandler());

app.MapGet("/probe", () => new OkResult());

app.MapPost("/documentprocess", [Topic("skragdemoqueue", "documentprocess")] async (IFormRecognizerManager formRecognizerManager, DaprClient daprClient, DocumentProcessing documentProcessing) =>
{
    Environment.SetEnvironmentVariable("AZURE_CLIENT_ID", "d53cfbc6-6de7-43a1-a247-4bff27284a40");
    var blobUri = new Uri(documentProcessing.BlobUri);
    Console.WriteLine($"Processing document {documentProcessing.BlobName} and URI {blobUri}");
    var forms = await formRecognizerManager.GetFormsFromPdfAsync(blobUri);

    foreach(var form in forms)
    {
        var paragraph = form.Lines.Select(y => y.Text).Aggregate((x, y) => $"{x} {y}");
        daprClient.PublishEventAsync("skragdemoqueue", "knowledgeprocess", new KnowledgeProcessing { BlobName = documentProcessing.BlobName, DocumentFrame = paragraph, PageNumber = form.PageNumber }).Wait();
    }
});

app.Run();
