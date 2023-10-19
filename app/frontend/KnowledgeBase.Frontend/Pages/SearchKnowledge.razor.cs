using Dapr.Client;
using KnowledgeBase.Shared;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace KnowledgeBase.Frontend;

public class SearchKnowledgeBase : ComponentBase
{
    [Inject] private DaprClient daprClient { get; set; }

    protected string SearchString { get; set; }
    protected string response { get; set; }
    protected bool isVisible { get; set; }

    protected async Task Search()
    {
        ShowSpinner();

        var data = new SearchKnowledge { Prompt = SearchString };
        var result = await daprClient.InvokeMethodAsync<SearchKnowledge, string>(HttpMethod.Post, "search-knowledge", "api/searchknowldge", data);

        response = result;

        await InvokeAsync(() =>
        {
            HideSpinner();
        });
    }

    public async Task Enter(KeyboardEventArgs e)
    {
        if (e.Code == "Enter" || e.Code == "NumpadEnter")
        {
            await InvokeAsync(Search);
        }
    }

    public void ShowSpinner()
    {
        isVisible = true;
        StateHasChanged();
    }

    public void HideSpinner()
    {
        isVisible = false;
        StateHasChanged();
    }
}
