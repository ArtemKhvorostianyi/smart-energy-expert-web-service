using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace SmartEnergyExpert.Client.Apps;

[App(icon: Icons.FlaskConical, title: "Experiments", searchHints: ["experiments", "parameters", "input", "measurements"])]
public sealed class ExperimentsApp : ViewBase
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private static readonly Uri ApiBaseUri = new("http://localhost:5109");

    public override object? Build()
    {
        var email = UseState("operator@smartenergy.local");
        var password = UseState("Operator123!");
        var accessToken = UseState(string.Empty);
        var statusMessage = UseState("Please login to call protected API endpoints.");
        var experimentsPayload = UseState("[]");
        var title = UseState("Transformer Thermal Test");
        var experimentType = UseState("Thermal");
        var description = UseState("Daily thermal profile validation");

        return Layout.Vertical().Padding(4).Gap(2)
               | Text.H2("Experiment Input Workspace")
               | Text.Markdown("Login with API credentials, fetch experiments, and create new experiment records.")
               | new Card(
                   Layout.Vertical().Gap(2)
                   | Text.H3("API Login")
                   | email.ToInput("Email")
                   | password.ToInput("Password")
                   | Layout.Horizontal().Gap(1)
                       | new Button("Login", async _ =>
                       {
                           var result = await LoginAsync(email.Value, password.Value);
                           if (!result.Success)
                           {
                               statusMessage.Set($"Login failed: {result.Error}");
                               return;
                           }

                           accessToken.Set(result.AccessToken!);
                           statusMessage.Set($"Logged in as {result.Role}. Token expires in {result.ExpiresInSeconds}s.");
                       })
                       | new Button("Load Experiments", async _ =>
                       {
                           if (string.IsNullOrWhiteSpace(accessToken.Value))
                           {
                               statusMessage.Set("Login first to call protected endpoints.");
                               return;
                           }

                           var response = await GetExperimentsAsync(accessToken.Value);
                           if (!response.Success)
                           {
                               statusMessage.Set($"Load failed: {response.Error}");
                               return;
                           }

                           experimentsPayload.Set(response.Payload ?? "[]");
                           statusMessage.Set("Experiments loaded.");
                       })
                   | new Separator()
                   | Text.H3("Create Experiment")
                   | title.ToInput("Title")
                   | experimentType.ToInput("Experiment Type")
                   | description.ToInput("Description")
                   | new Button("Create Experiment", async _ =>
                   {
                       if (string.IsNullOrWhiteSpace(accessToken.Value))
                       {
                           statusMessage.Set("Login first to create experiments.");
                           return;
                       }

                       var createResult = await CreateExperimentAsync(
                           accessToken.Value,
                           title.Value,
                           experimentType.Value,
                           description.Value);

                       if (!createResult.Success)
                       {
                           statusMessage.Set($"Create failed: {createResult.Error}");
                           return;
                       }

                       statusMessage.Set("Experiment created successfully. Click Load Experiments to refresh.");
                   })
                   | new Separator()
                   | Text.H3("Status")
                   | Text.Block(statusMessage.Value)
                   | Text.H3("Experiments Payload")
                   | new CodeBlock(experimentsPayload.Value, Languages.Json)
               );
    }

    private static async Task<(bool Success, string? AccessToken, int ExpiresInSeconds, string? Role, string? Error)> LoginAsync(string email, string password)
    {
        using var http = new HttpClient { BaseAddress = ApiBaseUri };
        var response = await http.PostAsJsonAsync("/api/auth/login", new
        {
            Email = email,
            Password = password
        });

        if (!response.IsSuccessStatusCode)
        {
            return (false, null, 0, null, await response.Content.ReadAsStringAsync());
        }

        var payload = await response.Content.ReadFromJsonAsync<LoginApiResponse>();
        if (payload is null || string.IsNullOrWhiteSpace(payload.AccessToken))
        {
            return (false, null, 0, null, "Invalid login response payload.");
        }

        return (true, payload.AccessToken, payload.ExpiresInSeconds, payload.Role, null);
    }

    private static async Task<(bool Success, string? Payload, string? Error)> GetExperimentsAsync(string token)
    {
        using var http = new HttpClient { BaseAddress = ApiBaseUri };
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await http.GetAsync("/api/experiments");
        var content = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
        {
            return (false, null, content);
        }

        var pretty = JsonSerializer.Serialize(
            JsonSerializer.Deserialize<object>(content),
            JsonOptions);

        return (true, pretty, null);
    }

    private static async Task<(bool Success, string? Error)> CreateExperimentAsync(
        string token,
        string title,
        string experimentType,
        string description)
    {
        using var http = new HttpClient { BaseAddress = ApiBaseUri };
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await http.PostAsJsonAsync("/api/experiments", new
        {
            Title = title,
            ExperimentType = experimentType,
            Description = description,
            CreatedBy = Guid.Empty
        });

        if (response.IsSuccessStatusCode)
        {
            return (true, null);
        }

        var body = await response.Content.ReadAsStringAsync();
        return (false, body);
    }

    private sealed class LoginApiResponse
    {
        public string AccessToken { get; init; } = string.Empty;
        public int ExpiresInSeconds { get; init; }
        public string Role { get; init; } = string.Empty;
    }
}
