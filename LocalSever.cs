using System;
using System.Collections.Concurrent;
using System.Net;
using UnityEngine;

public class LocalSever : MonoBehaviour
{
    [System.Serializable]
    public class TokenData
    {
        public string token;
    }

    public DynamicLog log;

    private readonly ConcurrentQueue<Action> _mainThreadActions = new ConcurrentQueue<Action>();

    void Start()
    {
        StartLocalServer();
    }

    void Update()
    {
        while (_mainThreadActions.TryDequeue(out var action))
        {
            action.Invoke();
        }
    }

    void StartLocalServer()
    {
        HttpListener listener = new HttpListener();
        listener.Prefixes.Add("http://localhost:12345/callback/");
        listener.Prefixes.Add("http://localhost:12345/storeToken/"); // New endpoint for the token
        listener.Start();
        listener.BeginGetContext(OnHttpRequestReceived, listener);
    }

    void OnHttpRequestReceived(IAsyncResult result)
    {
        var listener = (HttpListener)result.AsyncState;
        var context = listener.EndGetContext(result);

        if (context.Request.Url.AbsolutePath == "/storeToken/")
        {
            HandleTokenRequest(context);
            return;
        }

        string responseString = @"
<html>
<body>
<script>
  window.onload = function() {
    const fragment = window.location.hash.substring(1);
    const params = new URLSearchParams(fragment);
    const accessToken = params.get('access_token');
    if (accessToken) {
      fetch('http://localhost:12345/storeToken/', {
        method: 'POST',
        body: JSON.stringify({ token: accessToken }),
        headers: {
          'Content-Type': 'application/json'
        }
      }).then(() => {
        // Close the window after sending the token
        window.close();
      });
    } else {
      // Optionally, close the window even if no token is found
      window.close();
    }
  }
</script>
</body>
</html>";


        byte[] buffer = System.Text.Encoding.UTF8.GetBytes(responseString);
        context.Response.ContentLength64 = buffer.Length;
        var output = context.Response.OutputStream;
        output.Write(buffer, 0, buffer.Length);
        output.Close();

        listener.BeginGetContext(OnHttpRequestReceived, listener);
    }

    void HandleTokenRequest(HttpListenerContext context)
    {
        System.IO.Stream body = context.Request.InputStream;
        System.Text.Encoding encoding = context.Request.ContentEncoding;
        System.IO.StreamReader reader = new System.IO.StreamReader(body, encoding);

        string data = reader.ReadToEnd();

        TokenData tokenData = JsonUtility.FromJson<TokenData>(data);
        var token = tokenData.token;

        if (!string.IsNullOrEmpty(token))
        {
            _mainThreadActions.Enqueue(() =>
            {
                log.AddEntry(token + " Token received");
                Debug.Log("Token Success");
            });
        }
        else
        {
            _mainThreadActions.Enqueue(() =>
            {
                log.AddEntry("Token retrieval failed");
                Debug.Log("Token Failure");
            });
        }

        byte[] responseBuffer = System.Text.Encoding.UTF8.GetBytes("Token received.");
        context.Response.ContentLength64 = responseBuffer.Length;
        var responseOutput = context.Response.OutputStream;
        responseOutput.Write(responseBuffer, 0, responseBuffer.Length);
        responseOutput.Close();
    }

}
