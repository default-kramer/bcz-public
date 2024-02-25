using System;
using System.Collections.Generic;
using System.Linq;
using BCZ.Core;
using Godot;

abstract class Request
{
    public abstract string Path { get; }
    public abstract Godot.HTTPClient.Method Method { get; }
    public virtual string Body() => "";
    public abstract double TimeoutSeconds { get; }
    public virtual void OnRequestCompleted(long result, long responseCode, string[] headers, byte[] body) { }
    public virtual void OnError(Error error) { }
}

interface IServerConnection
{
    bool IsOnline { get; }

    void Execute(Request request);
}

/// <summary>
/// We inherit Godot.Node here because HTTPRequest is also a node that needs a parent.
/// </summary>
class ServerConnection : Godot.Node, IServerConnection
{
    private readonly string baseUrl;
    private readonly string[] customHeaderBuffer = new string[1]; // we will only send max 1 custom header

    public ServerConnection(string baseUrl)
    {
        this.baseUrl = baseUrl;
    }

    /// <summary>
    /// For "play in browser", the player name will be sent as a browser cookie
    /// and the game code will be totally unaware.
    /// For all other builds, the player name must be set here and sent as a HTTP header.
    /// </summary>
    public string? PlayerNickname { get; set; }

    public bool IsOnline => true;

    public void Execute(Request request)
    {
        RequestNode.Execute(this, request);
    }

    /// <summary>
    /// From https://docs.godotengine.org/en/3.5/tutorials/networking/http_request_class.html
    ///   Keep in mind that you have to wait for a request to finish before sending another one.
    ///   Making multiple request at once requires you to have one node per request.
    ///   A common strategy is to create and delete HTTPRequest nodes at runtime as necessary.
    /// Sounds good to me. We will create one instance of this class per HTTP request.
    /// </summary>
    class RequestNode : Godot.Node
    {
        private readonly ServerConnection parent;
        private readonly HTTPRequest http;
        private readonly Request request;
        private readonly string url;

        private RequestNode(ServerConnection parent, Request request)
        {
            this.parent = parent;
            this.http = new HTTPRequest();
            this.request = request;
            this.url = parent.baseUrl + request.Path;

            parent.AddChild(this);
            this.AddChild(http);
            http.Connect("request_completed", this, nameof(OnRequestCompleted));
        }

        public static void Execute(ServerConnection parent, Request request)
        {
            string[]? headers = null;
            if (parent.PlayerNickname != null)
            {
                parent.customHeaderBuffer[0] = "bcz-playername: " + parent.PlayerNickname;
                headers = parent.customHeaderBuffer;
            }

            var node = new RequestNode(parent, request);
            node.http.Timeout = request.TimeoutSeconds;
            var error = node.http.Request(node.url, method: request.Method, requestData: request.Body(), customHeaders: headers);
            if (error != Error.Ok)
            {
                GD.PushError($"{request.Method} {node.url} | fail | {error}");
                request.OnError(error);
                node.Cleanup();
            }
        }

        private void OnRequestCompleted(long result, long responseCode, string[] headers, byte[] body)
        {
            GD.Print($"{request.Method} {url} | {responseCode} | {result}");
            request.OnRequestCompleted(result, responseCode, headers, body);
            Cleanup();
        }

        private void Cleanup()
        {
            // I'm not sure if this is correct or even necessary...
            this.QueueFree();
        }
    }
}

class UnavailableServer : IServerConnection
{
    private UnavailableServer() { }
    public static readonly UnavailableServer Instance = new UnavailableServer();

    public bool IsOnline => false;

    public void Execute(Request request)
    {
        // Callers are supposed to check IsOnline before calling this method.
        throw new NotImplementedException("Server is offline");
    }
}
