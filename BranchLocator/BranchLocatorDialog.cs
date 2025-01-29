using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Schema;
using Newtonsoft.Json.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.IO;
using Microsoft.Extensions.Configuration;
using Microsoft.Bot.Builder;
using System;
using System.Linq;
using Microsoft.Bot.Builder.Dialogs.Choices;
using Microsoft.AspNetCore.Http;


namespace BranchLocator
{
    public class BranchLocatorDialog : ComponentDialog
    {
        private readonly HttpClient _httpClient;
        private readonly string _azureMapsKey;
        private readonly string _cluEndpoint;
        private readonly string _cluKey;
        private readonly string _cluProjectName;
        private readonly string _cluDeploymentName;
        private readonly string _cluRegion;

        public BranchLocatorDialog(string id, IHttpClientFactory httpClientFactory) : base(id)
        {
            _httpClient = httpClientFactory.CreateClient();

            // Load configurations
            var configuration = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json")
                .Build();

            _azureMapsKey = configuration["AzureMapsKey"];
            _cluEndpoint = configuration["CLU:Endpoint"];
            _cluKey = configuration["CLU:PredictionKey"];
            _cluProjectName = configuration["CLU:ProjectName"];
            _cluDeploymentName = configuration["CLU:DeploymentName"];
            _cluRegion = configuration["CLU:Region"];

            //AddDialog(new TextPrompt("LocationPrompt"));
            AddDialog(new ChoicePrompt("RestartPrompt"));
            AddDialog(new TextPrompt("LocationPrompt", ValidateLocationInput));
            AddDialog(new WaterfallDialog(nameof(WaterfallDialog), new WaterfallStep[]
            {
            //RecognizeIntentAsync,
            HandleLocateBranchIntentAsync,
            ShowResultsAsync,
            RestartDialogAsync
            }));

            InitialDialogId = nameof(WaterfallDialog);
        }
        /*private async Task<DialogTurnResult> RecognizeIntentAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            var userInput = stepContext.Context.Activity.Text;

            // Call CLU for intent recognition
            var intent = await GetIntentFromCLUAsync(userInput);
            stepContext.Values["Intent"] = intent;

            if (intent == "LocateBranch")
            {
                return await stepContext.NextAsync(null, cancellationToken);
            }

            await stepContext.Context.SendActivityAsync("I can only help with locating branches for now.", cancellationToken: cancellationToken);
            return await stepContext.EndDialogAsync(null, cancellationToken);
        }*/
        private async Task<DialogTurnResult> HandleLocateBranchIntentAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            // Ask the user for their location
            return await stepContext.PromptAsync("LocationPrompt", new PromptOptions
            {
                Prompt = MessageFactory.Text("Please provide your location (e.g., Victoria Island).")
            }, cancellationToken);
        }

        private async Task<DialogTurnResult> ShowResultsAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            try
            { 
            var userLocation = stepContext.Result.ToString();
            var geocodeUrl = $"https://atlas.microsoft.com/search/address/json?api-version=1.0&query={Uri.EscapeDataString(userLocation)}&subscription-key={_azureMapsKey}";

            var response = await _httpClient.GetStringAsync(geocodeUrl);
            var jsonResponse = JObject.Parse(response);

            if (jsonResponse["results"]?.HasValues == true)
            {
                var coordinates = jsonResponse["results"][0]["position"];
                var userLat = double.Parse(coordinates["lat"].ToString());
                var userLon = double.Parse(coordinates["lon"].ToString());

                // Load branch data
                var branchData = JArray.Parse(File.ReadAllText("Data/branches.json"));
                var results = new List<(string BranchName, string Address, string MapsLink, double Distance)>();

                foreach (var branch in branchData)
                {
                    var branchName = branch["BranchName"].ToString();
                    var address = branch["Address"].ToString();

                    var branchLat = double.Parse(branch["latitude"].ToString());
                    var branchLon = double.Parse(branch["longitude"].ToString());

                    // Check if the branch address contains the specified area (e.g., "Victoria Island")
                    if (address.Contains(userLocation, StringComparison.OrdinalIgnoreCase))
                    {

                        // Calculate the distance between the user's location and the branch
                        var distance = GetDistance(userLat, userLon, branchLat, branchLon);

                        //if (distance <= 10) // For example, show branches within 10 km

                        var googleMapsLink = $"https://www.google.com/maps/search/?api=1&query={Uri.EscapeDataString(address)}";

                        results.Add((BranchName: branchName, Address: address, MapsLink: googleMapsLink, Distance: distance));
                    }
                   
                }

                // Sort results by distance (ascending) and take the top 5 closest branches
                var topBranches = results.OrderBy(branch => branch.Distance).Take(5).ToList();
                
                if (topBranches.Any())
                {
                    var responseMessage = "Here are the branches near your location:\n" +
                          string.Join("\n", topBranches.Select((branch, index) =>
                              $"{index + 1}. {branch.BranchName}:\n   Address: {branch.Address}\n   [View on Google Maps]({branch.MapsLink})\n   Distance: {branch.Distance:F2} km"));
                    await stepContext.Context.SendActivityAsync(MessageFactory.Text(responseMessage), cancellationToken);
                }
                else
                {
                    await stepContext.Context.SendActivityAsync("No branches were found near your location.", null, null, cancellationToken);
                }

                // Prompt for restarting the dialog
                return await stepContext.PromptAsync("RestartPrompt",
            new PromptOptions
            {
                Prompt = MessageFactory.Text("Would you like to search for another location?"),
                Choices = ChoiceFactory.ToChoices(new List<string> { "Yes", "No" }),
            },
            cancellationToken);
                //return await stepContext.EndDialogAsync(null, cancellationToken);
            }
            await stepContext.Context.SendActivityAsync("I couldn't find that location. Please try again.", cancellationToken: cancellationToken);
            return await stepContext.ReplaceDialogAsync(InitialDialogId, null, cancellationToken);

            }
            catch (Exception ex)
            {
                // Log error here
                await stepContext.Context.SendActivityAsync(
                    "Sorry, I'm having trouble processing locations right now. Please try again later.",
                    cancellationToken: cancellationToken
                );
                return await stepContext.EndDialogAsync(cancellationToken: cancellationToken);
            }

        }
        private async Task<DialogTurnResult> RestartDialogAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            {
                var choice = (FoundChoice)stepContext.Result;
                if (choice.Value == "Yes")
                {
                    // Restart the dialog
                    return await stepContext.ReplaceDialogAsync(InitialDialogId, cancellationToken: cancellationToken);
                }
                else
                {
                    // End the dialog
                    await stepContext.Context.SendActivityAsync("Goodbye!", cancellationToken: cancellationToken);
                    return await stepContext.EndDialogAsync(cancellationToken: cancellationToken);
                }
            }
        }

        // New validation method
        private async Task<bool> ValidateLocationInput(PromptValidatorContext<string> promptContext, CancellationToken cancellationToken)
        {
            var location = promptContext.Recognized.Value?.Trim();

            if (string.IsNullOrEmpty(location) || location.Length < 3)
            {
                await promptContext.Context.SendActivityAsync(
                    "Please provide a valid location name (e.g., 'Victoria Island' or 'Lagos Mainland').",
                    cancellationToken: cancellationToken
                );
                return false;
            }

            // Basic check for coordinates format
            if (double.TryParse(location, out _))
            {
                await promptContext.Context.SendActivityAsync(
                    "Please provide a location name, not coordinates. For example: 'Victoria Island'",
                    cancellationToken: cancellationToken
                );
                return false;
            }

            return true;
        }

      


        // Haversine formula to calculate distance between two lat/long points
        private double GetDistance(double lat1, double lon1, double lat2, double lon2)
        {
            var R = 6371; // Radius of the Earth in km
            var dLat = ToRadians(lat2 - lat1);
            var dLon = ToRadians(lon2 - lon1);
            var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                    Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2)) *
                    Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
            var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            var distance = R * c; // Distance in km
            return distance;
        }
        private double ToRadians(double degree)
        {
            return degree * (Math.PI / 180);
        }


        private async Task<string> GetIntentFromCLUAsync(string userInput)
        {
            var url = $"{_cluEndpoint}/language/:analyze-conversations?api-version=2022-10-01-preview";
            var payload = new
            {
                kind = "Conversation",
                analysisInput = new
                {
                    conversationItem = new
                    {
                        text = userInput,
                        id = "1",
                        participantId = "user"
                    }
                },
                parameters = new
                {
                    projectName = _cluProjectName,
                    deploymentName = _cluDeploymentName,
                    stringIndexType = "TextElement_V8"
                }
            };
            var content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(payload), System.Text.Encoding.UTF8, "application/json");
            _httpClient.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", _cluKey);

            var response = await _httpClient.PostAsync(url, content);
            var responseBody = await response.Content.ReadAsStringAsync();
            var json = JObject.Parse(responseBody);

            var topIntent = json["result"]["prediction"]["topIntent"].ToString();
            return topIntent;
        }
    }
}