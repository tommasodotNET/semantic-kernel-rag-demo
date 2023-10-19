using Azure;
using Azure.AI.FormRecognizer;
using Azure.AI.FormRecognizer.Models;
using Dapr.Client;

namespace KnowledgeBase.DocumentProcessing.Services;

public interface IFormRecognizerManager
{
    // Task<string> GetContentFromPdfAsync(Uri blobUri);
    Task<FormPageCollection> GetFormsFromPdfAsync(Uri blobUri);
}

public class FormRecognizerManager : IFormRecognizerManager
{

    private readonly FormRecognizerClient _formRecognizerClient;

    public FormRecognizerManager(DaprClient daprClient)
    {
        var formRecognizerEndpoint = daprClient.GetSecretAsync("skcodemotion2023akv", "FormRecognizerEndpoint").GetAwaiter().GetResult().Values.FirstOrDefault();
        var formRecognizerApiKey = daprClient.GetSecretAsync("skcodemotion2023akv", "FormRecognizerKey").GetAwaiter().GetResult().Values.FirstOrDefault();
        _formRecognizerClient = new FormRecognizerClient(new Uri(formRecognizerEndpoint), new AzureKeyCredential(formRecognizerApiKey));
    }

    // public async Task<string> GetContentFromPdfAsync(Uri blobUri)
    // {
    //     var options = new RecognizeContentOptions() { ContentType = FormContentType.Pdf };
    //     var response = await _formRecognizerClient.StartRecognizeContentFromUri(blobUri, options).WaitForCompletionAsync();
    //     var content = response?.Value.SelectMany(x => x.Lines).Select(x => x.Text).Aggregate((x, y) => $"{x} {y}");

    //     return content;
    // }

    public async Task<FormPageCollection> GetFormsFromPdfAsync(Uri blobUri)
    {
        var options = new RecognizeContentOptions() { ContentType = FormContentType.Pdf };
        var response = await _formRecognizerClient.StartRecognizeContentFromUri(blobUri, options).WaitForCompletionAsync();
        var forms = response?.Value;

        return forms;
    }
}