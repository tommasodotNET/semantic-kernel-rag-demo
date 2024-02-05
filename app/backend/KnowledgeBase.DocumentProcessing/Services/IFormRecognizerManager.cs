using Azure;
using Azure.AI.FormRecognizer;
using Azure.AI.FormRecognizer.Models;
using Azure.Identity;
using Dapr.Client;
using Microsoft.Identity.Client.AppConfig;

namespace KnowledgeBase.DocumentProcessing.Services;

public interface IFormRecognizerManager
{
    Task<FormPageCollection> GetFormsFromPdfAsync(Uri blobUri);
}

public class FormRecognizerManager : IFormRecognizerManager
{

    private readonly FormRecognizerClient _formRecognizerClient;

    public FormRecognizerManager(DaprClient daprClient)
    {
        var formRecognizerEndpoint = daprClient.GetSecretAsync("skragdemoakv", "FormRecognizerEndpoint").GetAwaiter().GetResult().Values.FirstOrDefault();
        // var formRecognizerApiKey = daprClient.GetSecretAsync("skcodemotion2023akv", "FormRecognizerKey").GetAwaiter().GetResult().Values.FirstOrDefault();
        // _formRecognizerClient = new FormRecognizerClient(new Uri(formRecognizerEndpoint), new AzureKeyCredential(formRecognizerApiKey));
        _formRecognizerClient = new FormRecognizerClient(new Uri(formRecognizerEndpoint), new DefaultAzureCredential(new DefaultAzureCredentialOptions { ManagedIdentityClientId = "d53cfbc6-6de7-43a1-a247-4bff27284a40" }));
    }

    public async Task<FormPageCollection> GetFormsFromPdfAsync(Uri blobUri)
    {
        var options = new RecognizeContentOptions() { ContentType = FormContentType.Pdf };
        var response = await _formRecognizerClient.StartRecognizeContentFromUri(blobUri, options).WaitForCompletionAsync();
        var forms = response?.Value;

        return forms;
    }
}