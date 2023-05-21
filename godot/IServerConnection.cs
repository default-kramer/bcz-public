using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Godot;

interface IServerConnection
{
    void UploadGame(string replayFile);
}

/// <summary>
/// Relies on the player token cookie... Not sure how PC version will work yet.
/// </summary>
class BrowserBasedServerConnection : Godot.Node, IServerConnection
{
    private readonly HTTPRequest http = new HTTPRequest();
    private readonly string baseUrl;

    public BrowserBasedServerConnection(string baseUrl)
    {
        this.baseUrl = baseUrl;
        AddChild(http);
        http.Connect("request_completed", this, nameof(OnRequestCompleted));
    }

    private void OnRequestCompleted(long result, long responseCode, string[] headers, byte[] body)
    {
        //Console.WriteLine($"Request completed! Got {result} / {responseCode} / {body.GetStringFromUTF8()}");
    }

    public void UploadGame(string replayFile)
    {
        http.Request($"{baseUrl}/api/upload-game/v1", method: HTTPClient.Method.Post, requestData: replayFile);
    }
}
