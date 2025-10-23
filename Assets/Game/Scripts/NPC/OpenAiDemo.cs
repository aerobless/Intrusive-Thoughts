using UnityEngine;
using System.Threading.Tasks;
using OpenAI;
using OpenAI.Chat;

public class OpenAiDemo : MonoBehaviour
{

    private async void Start()
    {
        await GeneratePoemAsync();
    }

    private async Task GeneratePoemAsync()
    {
        try
        {
            var key = OpenAiConfig.GetApiKey();

            // Initialize the OpenAI client provided by the com.openai.unity package
            var client = new OpenAIClient(new OpenAIAuthentication(key));

            // Create chat completion request
            var chatRequest = new ChatRequest(
                new[]
                {
                    new Message(Role.User, "Write a short, fun Hello World poem.")
                },
                model: "gpt-4.1"
            );

            var response = await client.ChatEndpoint.GetCompletionAsync(chatRequest);

            Debug.Log("OpenAI response:\n" + response.FirstChoice.Message.Content);
        }
        catch (System.Exception ex)
        {
            Debug.LogError("OpenAI error: " + ex);
        }
    }
}
