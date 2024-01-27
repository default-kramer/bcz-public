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
    void Execute(Request request);
}

/// <summary>
/// Relies on the player token cookie... Not sure how PC version will work yet.
/// Note that we inherit Godot.Node here because HTTPRequest is also a node that needs a parent.
/// </summary>
class BrowserBasedServerConnection : Godot.Node, IServerConnection
{
    private readonly string baseUrl;

    public BrowserBasedServerConnection(string baseUrl)
    {
        this.baseUrl = baseUrl;
    }

    public void Execute(Request request)
    {
        RequestNode.Execute(this, request);
    }

    class RequestNode : Godot.Node
    {
        private readonly BrowserBasedServerConnection parent;
        public readonly HTTPRequest http;
        private readonly Request request;
        private readonly string url;

        private RequestNode(BrowserBasedServerConnection parent, Request request)
        {
            this.parent = parent;
            this.http = new HTTPRequest();
            this.request = request;
            this.url = parent.baseUrl + request.Path;

            parent.AddChild(this);
            this.AddChild(http);
            http.Connect("request_completed", this, nameof(OnRequestCompleted));
        }

        public static void Execute(BrowserBasedServerConnection parent, Request request)
        {
            var node = new RequestNode(parent, request);
            node.http.Timeout = request.TimeoutSeconds;
            var error = node.http.Request(node.url, method: request.Method, requestData: request.Body());
            if (error != Error.Ok)
            {
                GD.PushError($"{request.Method} {node.url} | fail | {error}");
                request.OnError(error);
            }
        }

        private void OnRequestCompleted(long result, long responseCode, string[] headers, byte[] body)
        {
            GD.Print($"{request.Method} {url} | {responseCode} | {result}");
            request.OnRequestCompleted(result, responseCode, headers, body);
        }
    }
}
