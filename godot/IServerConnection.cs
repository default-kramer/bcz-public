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
/// Note that we inherit Godot.Node here because HTTPRequest is also a node that needs a parent.
/// </summary>
class BrowserBasedServerConnection : Godot.Node, IServerConnection
{
    private readonly HTTPRequest http = new HTTPRequest();
    private readonly string baseUrl;

    public BrowserBasedServerConnection(string baseUrl)
    {
        this.baseUrl = baseUrl;
        AddChild(http);
        http.Timeout = 20;
        http.Connect("request_completed", this, nameof(OnRequestCompleted));
    }

    private void OnRequestCompleted(long result, long responseCode, string[] headers, byte[] body)
    {
        //Console.WriteLine($"Request completed! Got {result} / {responseCode} / {body.GetStringFromUTF8()}");
    }

    public void UploadGame(string replayFile)
    {
        string url = $"{baseUrl}/api/upload-game/v1";
        //Console.WriteLine($"Uploading! To {url}");
        http.Timeout = 20;
        var error = http.Request(url, method: HTTPClient.Method.Post, requestData: replayFile);
        //Console.WriteLine($"Result: {error}");
    }
}
