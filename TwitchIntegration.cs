using NativeWebSocket;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OWOGame;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using static OwoSensationBuilderAndTester;

public class TwitchManager : MonoBehaviour
{
    // Web info 
    [Serializable]
    public class TokenData
    {
        public string token;
    }
    [Serializable]
    public class ResponseData
    {
        public UserData[] data;
    }
    [Serializable]
    public class UserData
    {
        public string id;
        public string display_name;
    }
    // Twitch EventSub Data
    [Serializable]
    private class TwitchResponseData
    {
        public Metadata metadata;
        public Payload payload;
    }

    [Serializable]
    private class Metadata
    {
        public string message_id;
        public string message_type;
        public string message_timestamp;
        public string subscription_type;
        public string subscription_version;
    }

    [Serializable]
    private class Payload
    {
        public SessionData session;
        public SubscriptionData subscription;
        [JsonProperty("event")] // Map "event" JSON property to eventData member
        public EventData eventData;
        public SessionData session_reconnect;
    }

    [Serializable]
    private class SessionData
    {
        public string id;
        public string status;
        public string connected_at;
        public int keepalive_timeout_seconds;
        public string reconnect_url;
    }

    [Serializable]
    private class SubscriptionData
    {
        public string id;
        public string status;
        public string type;
        public string version;
        public int cost;
        public ConditionData condition;
        public TransportData transport;
        public string created_at;
    }

    [Serializable]
    private class ConditionData
    {
        public string broadcaster_user_id;
        public string user_id;
    }

    [Serializable]
    private class TransportData
    {
        public string method;
        public string session_id;
    }
    [Serializable]
    private class EventData
    {
        public string user_id;
        public string user_login;
        public string user_name;
        public string broadcaster_user_id;
        public string broadcaster_user_login;
        public string broadcaster_user_name;
        public string followed_at;
        public RewardData reward;
        public string bits;
    }
    [Serializable]
    private class RewardData
    {
        public string title;
    }
    [Serializable]
    public class TwitchTokenResponse
    {
        public string access_token;
        public string refresh_token;
        public int expires_in;
        public string[] scope;
        public string token_type;
    }
    [Serializable]
    public class TwitchValidateResponse
    {
        public string login;
        public string user_id;
        public int expires_in;
        public string[] scopes;
    }



    private readonly ConcurrentQueue<Action> _mainThreadActions = new();
    private readonly string filePath = "TwitchOWOLogs.txt";
    private string fullDebugPath;
    private readonly string clientId = TwitchSecret.clientId;
    private readonly string clientSecret = TwitchSecret.clientSecret;
    void Start()
    {
        fullDebugPath = Path.Combine(Application.dataPath, filePath);
        if (!File.Exists(fullDebugPath))
        {
            File.AppendAllText(fullDebugPath, DateTime.Now.ToString() + " Log Start" + "\n");
        }
        StartLocalServer();
    }
    void Update()
    {
        if (!isRefreshing && hasTokenTimer)
        {
            if (DateTime.UtcNow >= tokenExpiresAt.AddMinutes(-5))
            {
                _ = RefreshAccessTokenAsync();
            }
        }
        while (_mainThreadActions.TryDequeue(out var action))
        {
            action.Invoke();
        }
#if !UNITY_WEBGL || UNITY_EDITOR
        ws?.DispatchMessageQueue();
#endif
    }
    private static bool serverStarted = false;

    void StartLocalServer()
    {
        if (serverStarted) return;

        HttpListener listener = new();
        listener.Prefixes.Add("http://localhost:12345/callback/");
        listener.Start();
        listener.BeginGetContext(OnHttpRequestReceived, listener);

        serverStarted = true;
    }
    // Website Auth Code Grab
    void OnHttpRequestReceived(IAsyncResult result)
    {
        var listener = (HttpListener)result.AsyncState;
        var context = listener.EndGetContext(result);

        if (context.Request.Url.AbsolutePath == "/callback/")
        {
            string code = context.Request.QueryString["code"];

            if (!string.IsNullOrEmpty(code))
            {
                _ = ExchangeCodeForTokenAsync(code);
                WriteHtmlResponse(context.Response, GetCloseWindowHtml());

            }
            else
            {
                WriteHtmlResponse(context.Response, "<h3>Authorization failed</h3>");
            }
        }

        listener.BeginGetContext(OnHttpRequestReceived, listener);
    }
    void WriteHtmlResponse(HttpListenerResponse response, string html)
    {
        byte[] buffer = Encoding.UTF8.GetBytes(html);
        response.ContentType = "text/html";
        response.ContentLength64 = buffer.Length;
        response.OutputStream.Write(buffer, 0, buffer.Length);
        response.OutputStream.Close();
    }
    string GetCloseWindowHtml()
    {
        return @"
<!DOCTYPE html>
<html>
<head>
  <title>Authorized</title>
</head>
<body>
  <p>Authorization successful. You can close this window.</p>

  <script>
    // Give Unity a moment, then close
    setTimeout(() => {
      window.close();
    }, 500);
  </script>
</body>
</html>";
    }
    async Task ExchangeCodeForTokenAsync(string code)
    {

        string redirectUri = "http://localhost:12345/callback/";

        using var http = new HttpClient();

        var values = new Dictionary<string, string>
    {
        { "client_id", clientId },
        { "client_secret", clientSecret },
        { "code", code },
        { "grant_type", "authorization_code" },
        { "redirect_uri", redirectUri }
    };

        var content = new FormUrlEncodedContent(values);
        var response = await http.PostAsync("https://id.twitch.tv/oauth2/token", content);
        var json = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            Debug.LogError("Token exchange failed: " + json);
            return;
        }

        var token = JsonUtility.FromJson<TwitchTokenResponse>(json);

        _mainThreadActions.Enqueue(() =>
        {
            savedToken = token.access_token;
            savedRefreshToken = token.refresh_token;
            tokenExpiresAt = DateTime.UtcNow.AddSeconds(token.expires_in);
            hasTokenTimer = true;
            Debug.Log("OAuth SUCCESS — User token received");
            FetchUserData(savedToken);
        });
    }
    bool isRefreshing = false;
    bool hasTokenTimer = false;
    async Task RefreshAccessTokenAsync()
    {
        if (isRefreshing) return;
        if (savedRefreshToken == null) return;
        isRefreshing = true;
        try
        {
            using var http = new HttpClient();

            var values = new Dictionary<string, string>
    {
        { "grant_type", "refresh_token" },
        { "refresh_token", savedRefreshToken },
        { "client_id", clientId },
        { "client_secret", clientSecret }
    };

            var content = new FormUrlEncodedContent(values);
            var response = await http.PostAsync("https://id.twitch.tv/oauth2/token", content);
            var json = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                Debug.LogError("Token refresh failed: " + json);
                return;
            }

            var token = JsonUtility.FromJson<TwitchTokenResponse>(json);

            _mainThreadActions.Enqueue(() =>
            {
                savedToken = token.access_token;
                savedRefreshToken = token.refresh_token; // always replace!
                tokenExpiresAt = DateTime.UtcNow.AddSeconds(token.expires_in);

                Debug.Log("Twitch token refreshed successfully");
            });

        }
        finally
        {
            isRefreshing = false;
        }

    }
    async Task ValidateTokenAsync(string token)
    {
        using var http = new HttpClient();
        http.DefaultRequestHeaders.Add("Authorization", $"OAuth {token}");

        var response = await http.GetAsync("https://id.twitch.tv/oauth2/validate");
        var json = await response.Content.ReadAsStringAsync();

        var data = JsonUtility.FromJson<TwitchValidateResponse>(json);

        string safeLog =
            $"Token valid | User {data.login} ({data.user_id}) | " +
            $"expires_in={data.expires_in}s | " +
            $"scopes=[ {string.Join(", ", data.scopes)} ]";

        Debug.Log(safeLog);

        if (debugMode)
        {
            File.AppendAllText(fullDebugPath, DateTime.Now + " " + safeLog + "\n");
        }
    }

    private string savedToken;
    private string savedRefreshToken;
    private DateTime tokenExpiresAt;
    private void FetchUserData(string token)
    {
        StartCoroutine(GetUserDataCoroutine(token));
    }

    private int channelIDNumber = 0;
    private IEnumerator GetUserDataCoroutine(string token)
    {
        string url = "https://api.twitch.tv/helix/users";

        using UnityWebRequest www = UnityWebRequest.Get(url);
        www.SetRequestHeader("Client-ID", clientId); // Need your Twitch Apps id
        www.SetRequestHeader("Authorization", $"Bearer {token}");

        yield return www.SendWebRequest();
        if (www.result == UnityWebRequest.Result.Success)
        {
            // Parse the response to extract the channel ID
            string responseText = www.downloadHandler.text;
            ResponseData responseData = JsonUtility.FromJson<ResponseData>(responseText);

            if (responseData.data.Length > 0)
            {
                string userID = responseData.data[0].id;
                string userName = responseData.data[0].display_name;
                if (int.TryParse(userID, out int channelIDNum))
                {
                    channelIDNumber = channelIDNum;
                    ConnectToEventSub(false);
                }
                else
                {
                    if (debugMode)
                    {
                        File.AppendAllText(fullDebugPath, DateTime.Now.ToString() + " Failed to parse user ID to an integer." + "\n");
                    }
                    Debug.LogError("Failed to parse user ID to an integer.");
                }
                channelInputField.text = userName;
            }
            else
            {
                if (debugMode)
                {
                    File.AppendAllText(fullDebugPath, DateTime.Now.ToString() + " Received empty user data from Twitch." + "\n");
                }
                Debug.LogError("Received empty user data from Twitch.");
            }
        }
        else if (www.result == UnityWebRequest.Result.ConnectionError)
        {
            Debug.LogError("Connection Error: " + www.error);
            if (debugMode)
            {
                File.AppendAllText(fullDebugPath, DateTime.Now.ToString() + " Connection Error: " + www.error + "\n");
            }
        }
        else if (www.result == UnityWebRequest.Result.ProtocolError)
        {
            Debug.LogError("Protocol Error: " + www.error);
            if (debugMode)
            {
                File.AppendAllText(fullDebugPath, DateTime.Now.ToString() + " Protocol Error: " + www.error + "\n");
            }
        }

    }
    [SerializeField]
    private TMP_Text channelInputField;
    [SerializeField]
    private TMP_InputField testRedeemInputField;
    [SerializeField]
    private TextMeshProUGUI testBitsInputField;
    private WebSocket ws;
    public bool enableFollow = false;
    private bool enableRaid = false;
    private bool enableHype = false;
    private bool enableSubscribe = false;
    private bool enableScalingBit = false;
    public void EnableFollow()
    {
        enableFollow = true;
        if (debugMode)
        {
            try { File.AppendAllText(fullDebugPath, DateTime.Now.ToString() + " Follow Enabled" + "\n"); }
            catch
            {//ignore
            }
        }
    }
    public void EnableRaid()
    {
        enableRaid = true;
        if (debugMode)
        {
            try { File.AppendAllText(fullDebugPath, DateTime.Now.ToString() + " Raid Enabled" + "\n"); }
            catch
            {//ignore
            }
        }
    }
    public void EnableHype()
    {
        enableHype = true;
        if (debugMode)
        {
            try { File.AppendAllText(fullDebugPath, DateTime.Now.ToString() + " Hype Enabled" + "\n"); }
            catch
            {//ignore
            }
        }
    }
    public void EnableSubscribe()
    {
        enableSubscribe = true;
        if (debugMode)
        {
            try { File.AppendAllText(fullDebugPath, DateTime.Now.ToString() + " Subscribe Enabled" + "\n"); }
            catch
            {//ignore
            }
        }
    }
    public void EnableScalingBit()
    {
        enableScalingBit = true;

    }
    public void EnableDebug()
    {
        debugMode = true;
        try { File.AppendAllText(fullDebugPath, DateTime.Now.ToString() + " Log Enabled" + "\n"); }
        catch
        {//ignore
        }
    }
    public void DisableFollow()
    {
        enableFollow = false;
        if (debugMode)
        {
            try { File.AppendAllText(fullDebugPath, DateTime.Now.ToString() + " Follow Disabled" + "\n"); }
            catch
            {//ignore
            }
        }
    }
    public void DisableRaid()
    {
        enableRaid = false;
        if (debugMode)
        {
            try { File.AppendAllText(fullDebugPath, DateTime.Now.ToString() + " Raid Disabled" + "\n"); }
            catch
            {//ignore
            }
        }
    }
    public void DisableHype()
    {
        enableHype = false;
        if (debugMode)
        {
            try { File.AppendAllText(fullDebugPath, DateTime.Now.ToString() + " Hype Disabled" + "\n"); }
            catch
            {//ignore
            }
        }
    }
    public void DisableSubscribe()
    {
        enableSubscribe = false;
        if (debugMode)
        {
            try { File.AppendAllText(fullDebugPath, DateTime.Now.ToString() + " Subscribe Disabled" + "\n"); }
            catch
            {//ignore
            }
        }
    }
    public void DisableScalingBit()
    {
        enableScalingBit = false;
    }
    public void DisableDebug()
    {
        debugMode = false;
        try { File.AppendAllText(fullDebugPath, DateTime.Now.ToString() + " Log Disabled" + "\n"); }
        catch
        {//ignore
        }
    }
    public void InitiateOAuth()
    {
        string clientId = "vdawpon1s1za6ioint1wqyx3mqqhy3";
        string redirectUri = "http://localhost:12345/callback/";
        string scopes = string.Join(" ", new[]
        {
        "channel:read:subscriptions",
        "moderator:read:followers",
        "channel:read:redemptions",
        "bits:read",
        "channel:read:hype_train"
    });

        string authUrl =
            "https://id.twitch.tv/oauth2/authorize" +
            $"?client_id={clientId}" +
            $"&redirect_uri={Uri.EscapeDataString(redirectUri)}" +
            "&response_type=code" +
            $"&scope={Uri.EscapeDataString(scopes)}";

        Application.OpenURL(authUrl);
    }
    // EventSub Connection
    private const string EVENTSUB_ENDPOINT = "wss://eventsub.wss.twitch.tv/ws";

    public async void ConnectToEventSub(bool recconnecting, string newEndpoint = null)
    {
        var headers = new Dictionary<string, string>
    {
        { "Authorization", $"Bearer {savedToken}" }
    };
        if (!recconnecting)
        {
            ws = new WebSocket(EVENTSUB_ENDPOINT, headers);
        }
        else
        {
            ws = new WebSocket(newEndpoint, headers);
        }

        ws.OnOpen += HandleOpen;
        ws.OnMessage += HandleMessage;
        ws.OnError += HandleError;
        ws.OnClose += HandleClose;

        await ws.Connect(); // Connect to the WebSocket endpoint
    }

    private string websocketSessionId = "";

    private IEnumerator SendSubscriptionRequest()
    {
        string jsonPayload = "";
        for (int i = 0; i < 6; i++)
        {
            if (channelIDNumber <= 0 || string.IsNullOrEmpty(websocketSessionId))
            {
                if (debugMode)
                {
                    File.AppendAllText(fullDebugPath, DateTime.Now.ToString() + " Invalid channel ID number or websocket session ID." + "\n");
                }
                Debug.LogError("Invalid channel ID number or websocket session ID.");
                break;  // Exit the loop if conditions are not met
            }
            string type = "Default";
            string url = "https://api.twitch.tv/helix/eventsub/subscriptions";
            if (i == 0)
            {
                jsonPayload = $"{{\"type\":\"channel.follow\",\"version\":\"2\",\"condition\":{{\"broadcaster_user_id\":\"{channelIDNumber}\",\"moderator_user_id\":\"{channelIDNumber}\"}},\"transport\":{{\"method\":\"websocket\",\"session_id\":\"{websocketSessionId}\"}}}}";
                // Debug.Log("Channel Follow payload");
                type = "Follow";
            }
            if (i == 1)
            {
                jsonPayload = $"{{\"type\":\"channel.raid\",\"version\":\"1\",\"condition\":{{\"to_broadcaster_user_id\":\"{channelIDNumber}\"}},\"transport\":{{\"method\":\"websocket\",\"session_id\":\"{websocketSessionId}\"}}}}";
                // Debug.Log("Channel Raid payload");
                type = "Raid";
            }
            if (i == 2)
            {
                jsonPayload = $"{{\"type\":\"channel.subscribe\",\"version\":\"1\",\"condition\":{{\"broadcaster_user_id\":\"{channelIDNumber}\"}},\"transport\":{{\"method\":\"websocket\",\"session_id\":\"{websocketSessionId}\"}}}}";
                //  Debug.Log("Channel Subscribe payload");
                type = "Subscribe";
            }
            if (i == 3)
            {
                jsonPayload = $"{{\"type\":\"channel.cheer\",\"version\":\"1\",\"condition\":{{\"broadcaster_user_id\":\"{channelIDNumber}\"}},\"transport\":{{\"method\":\"websocket\",\"session_id\":\"{websocketSessionId}\"}}}}";
                // Debug.Log("Channel Bits payload");
                type = "Bits";
            }
            if (i == 4)
            {
                jsonPayload = $"{{\"type\":\"channel.channel_points_custom_reward_redemption.add\",\"version\":\"1\",\"condition\":{{\"broadcaster_user_id\":\"{channelIDNumber}\"}},\"transport\":{{\"method\":\"websocket\",\"session_id\":\"{websocketSessionId}\"}}}}";
                //  Debug.Log("Channel Redeems payload");
                type = "Redeems";
            }
            if (i == 5)
            {
                jsonPayload = $"{{\"type\":\"channel.hype_train.begin\",\"version\":\"2\",\"condition\":{{\"broadcaster_user_id\":\"{channelIDNumber}\"}},\"transport\":{{\"method\":\"websocket\",\"session_id\":\"{websocketSessionId}\"}}}}";
                //  Debug.Log("Channel Train payload");
                type = "Hype Train";
            }
            var request = new UnityWebRequest(url, "POST");
            byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonPayload);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();

            // Add authorization header
            request.SetRequestHeader("Authorization", "Bearer " + savedToken);
            // Add client ID header
            request.SetRequestHeader("Client-Id", clientId);
            // Set content type to JSON
            request.SetRequestHeader("Content-Type", "application/json");

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {

            }
            else
            {
                Debug.Log($"EventSub Payload: {jsonPayload} ");
                Debug.LogError($"EventSub {type} failed: " + request.error);
            }
        }
        _ = ValidateTokenAsync(savedToken);
    }

    public TMP_Text connectionText;
    public TextMeshProUGUI minBitValueText;
    private void HandleOpen()
    {
        connectionText.text = "Twitch Is Connected";
    }
    private List<string> usersHaveFollowed = new();

    private bool debugMode = false;
    private void HandleMessage(byte[] bytes)
    {
        var messageStr = Encoding.UTF8.GetString(bytes);


        TwitchResponseData incomingMessage = JsonConvert.DeserializeObject<TwitchResponseData>(messageStr);

        if (incomingMessage.metadata.message_type != "session_keepalive" && debugMode)
        {
            File.AppendAllText(fullDebugPath, DateTime.Now.ToString() + " " + messageStr + "\n");
        }
        if (incomingMessage.metadata.message_type == "notification")
        {
            if (incomingMessage.metadata.subscription_type == "channel.follow")
            {
                if (enableFollow)
                {
                    if (!usersHaveFollowed.Contains(incomingMessage.payload.eventData.user_id))
                    {
                        usersHaveFollowed.Add(incomingMessage.payload.eventData.user_id);
                        SendSensationBasedOnDropdown(followDropdown.captionText.text);
                        if (debugMode)
                        {
                            File.AppendAllText(fullDebugPath, DateTime.Now.ToString() + " Follow Sensation Sent" + "\n");
                        }
                    }
                    else
                    {
                        if (debugMode)
                        {
                            File.AppendAllText(fullDebugPath, DateTime.Now.ToString() + " User already followed this session" + "\n");
                        }
                    }
                }
                else if (debugMode)
                {
                    File.AppendAllText(fullDebugPath, DateTime.Now.ToString() + " Follows Sensation is Disabled" + "\n");
                }
            }
            if (incomingMessage.metadata.subscription_type == "channel.raid")
            {
                if (enableRaid)
                {
                    SendSensationBasedOnDropdown(raidDropdown.captionText.text);
                    if (debugMode)
                    {
                        File.AppendAllText(fullDebugPath, DateTime.Now.ToString() + " Raid Sensation Sent" + "\n");
                    }
                }
                else if (debugMode)
                {
                    File.AppendAllText(fullDebugPath, DateTime.Now.ToString() + " Raid Sensation is Disabled" + "\n");
                }
            }
            if (incomingMessage.metadata.subscription_type == "channel.cheer")
            {
                static string Clean(string s) => new(s.Where(char.IsDigit).ToArray());
                int bitValue = int.Parse(incomingMessage.payload.eventData.bits);
                int minBitValue = int.Parse(Clean(minBitValueText.text));
                if (!enableScalingBit)
                {
                    SendSensationBasedOnBits(bitValue);
                    if (debugMode)
                    {
                        File.AppendAllText(fullDebugPath, DateTime.Now.ToString() + " Bit Sensation Sent" + "\n");
                    }
                }
                else if (bitValue >= minBitValue)
                {
                    PlayFullScalingSensation(bitDropdown.captionText.text, minBitValue, bitValue);
                    if (debugMode)
                    {
                        File.AppendAllText(fullDebugPath, DateTime.Now.ToString() + " Bit Sensation Sent" + "\n");
                    }
                }
            }
            if (incomingMessage.metadata.subscription_type == "channel.subscribe")
            {
                if (enableSubscribe)
                {
                    SendSensationBasedOnDropdown(subscribeDropdown.captionText.text);
                    if (debugMode)
                    {
                        File.AppendAllText(fullDebugPath, DateTime.Now.ToString() + " Subscribe Sensation Sent" + "\n");
                    }
                }
                else if (debugMode)
                {
                    File.AppendAllText(fullDebugPath, DateTime.Now.ToString() + " Subscribe Sensation is Disabled" + "\n");
                }
            }
            if (incomingMessage.metadata.subscription_type == "channel.channel_points_custom_reward_redemption.add")
            {
                SendSensationBasedOnRedeem(incomingMessage.payload.eventData.reward.title);
                if (debugMode)
                {
                    File.AppendAllText(fullDebugPath, DateTime.Now.ToString() + " Point Redeem Sensation Sent" + "\n");
                }
            }
            if (incomingMessage.metadata.subscription_type == "channel.hype_train.begin")
            {
                if (enableHype)
                {
                    SendSensationBasedOnDropdown(hypeDropdown.captionText.text);
                    if (debugMode)
                    {
                        File.AppendAllText(fullDebugPath, DateTime.Now.ToString() + " Hype Sensation Sent" + "\n");
                    }
                }
                else if (debugMode)
                {
                    File.AppendAllText(fullDebugPath, DateTime.Now.ToString() + " Hype Sensation is Disabled" + "\n");
                }
            }
        }
        else if (incomingMessage.metadata.message_type == "session_welcome")
        {
            string content =
                    $" Twitch Session Started. " +
                    $"Raid Enabled: {enableRaid} " +
                    $"Follow Enabled: {enableFollow} " +
                    $"Scaling Bit Enabled: {enableScalingBit} " +
                    $"Hype Train Enabled: {enableHype} " +
                    $"Subscribe Enabled: {enableSubscribe}" +
                    "\n";
            if (incomingMessage != null && incomingMessage.payload != null && incomingMessage.payload.session != null)
            {
                websocketSessionId = incomingMessage.payload.session.id;
                StartCoroutine(SendSubscriptionRequest());
                if (debugMode)
                {
                    File.AppendAllText(fullDebugPath, DateTime.Now.ToString() + content);
                }
            }
            else
            {
                if (debugMode)
                {
                    File.AppendAllText(fullDebugPath, DateTime.Now.ToString() + " Failed to extract WebSocket Session ID from the response.");
                }
            }

        }
        else if (incomingMessage.metadata.message_type == "session_reconnect")
        {
            ConnectToEventSub(true, incomingMessage.payload.session.reconnect_url);
            if (debugMode)
            {
                File.AppendAllText(fullDebugPath, DateTime.Now.ToString() + " Session Reconnecting" + "\n");
            }
        }
    }
    private void HandleError(string errorMessage)
    {
        Debug.LogError("Error with Twitch EventSub: " + errorMessage);
        if (debugMode)
        {
            File.AppendAllText(fullDebugPath, DateTime.Now.ToString() + " Error with Twitch EventSub: " + errorMessage + "\n");
        }
    }

    private void HandleClose(WebSocketCloseCode reason)
    {
        string closeMessage = $"Disconnected from Twitch EventSub. Close Code:{reason}";
        _mainThreadActions.Enqueue(() =>
        {
            // LogEntry(closeMessage);

            Debug.LogError(closeMessage);
            if (debugMode)
            {
                File.AppendAllText(fullDebugPath, DateTime.Now.ToString() + " Disconnected from Twitch EventSub. Close Code:" + reason + "\n");
            }
            connectionText.text = "Twitch Is Disconnected";
        });
    }

    private async void OnDestroy()
    {
        if (ws != null)
        {
            await ws.Close();
            ws = null;
        }
    }

    public void RePopDropDowns()
    {
        DropdownPopulator[] dropdownPopulators = FindObjectsByType<DropdownPopulator>(FindObjectsSortMode.None);
        foreach (DropdownPopulator dropdownPopulator in dropdownPopulators)
        {
            dropdownPopulator.PopulateDropdownWithFilenames();
        }
    }
    public void SendTestButton()
    { /*
        static string Clean(string s) => new(s.Where(char.IsDigit).ToArray());
        int bitValue = int.Parse(Clean(testBitsInputField.text));
        int minBitValue = int.Parse(Clean(minBitValueText.text));
        PlayFullScalingSensation(bitDropdown.captionText.text, minBitValue, bitValue);
      */
        if (testRedeemInputField.text.Length > 0)
        {
            SendSensationBasedOnRedeem(testRedeemInputField.text.ToLower());
        }
        if (testBitsInputField.text.Length > 0)
        {
            int.TryParse(testBitsInputField.text, out int redeemValue);
            SendSensationBasedOnBits(redeemValue);
        }
    }

    //OWO Logic

    [Serializable]
    public class RedeemSensationPair
    {
        public TMP_InputField redeemNameInputField;
        public TMP_Dropdown sensationDropdown;
        public string redeemName;
        public int sensationDropdownValue;
        public void UpdateValuesFromUI()
        {
            redeemName = redeemNameInputField.text;
            sensationDropdownValue = sensationDropdown.value;
        }

        public void SetValuesToUI()
        {
            redeemNameInputField.text = redeemName;
        }
    }

    public List<RedeemSensationPair> redeemPairs = new();
    public List<RedeemSensationPair> bitPairs = new();

    public GameObject redeemPrefab;
    public Transform redeemList;
    public GameObject bitPrefab;
    public Transform bitList;
    private List<GameObject> redeemPrefabList = new();
    private List<GameObject> bitPrefabList = new();
    public TMP_Dropdown followDropdown;
    public TMP_Dropdown raidDropdown;
    public TMP_Dropdown hypeDropdown;
    public TMP_Dropdown subscribeDropdown;
    public TMP_Dropdown bitDropdown;

    private readonly string saveKey1 = "SavedReddemList";
    private readonly string saveKey2 = "SavedBitList";
    public void SaveSettings()
    {
        PlayerPrefs.SetInt("followDropdown", followDropdown.value);
        PlayerPrefs.SetInt("raidDropdown", raidDropdown.value);
        PlayerPrefs.SetInt("hypeDropdown", hypeDropdown.value);
        PlayerPrefs.SetInt("subscribeDropdown", subscribeDropdown.value);

        if (redeemPairs.Count > 0)
        {
            // Create a list to hold serialized redeemPair objects
            List<JObject> serializedPairs = new();

            // Serialize each redeemPair object in the list
            foreach (var pair in redeemPairs)
            {
                pair.UpdateValuesFromUI();

                // Use JObject to create a JSON representation
                JObject serializedPair = new(
                    new JProperty("redeemName", pair.redeemName),
                    new JProperty("sensationDropdownValue", pair.sensationDropdownValue)
                );

                serializedPairs.Add(serializedPair);
            }

            // Convert the list of JObjects to a JSON string using JsonConvert
            string jsonData = JsonConvert.SerializeObject(serializedPairs, Formatting.Indented);

            PlayerPrefs.SetString(saveKey1, jsonData);
        }
        else
        {
            PlayerPrefs.SetString(saveKey1, string.Empty);
            Debug.Log("No Redeem data to save");
        }
        if (bitPairs.Count > 0)
        {
            // Create a list to hold serialized Bitpairs objects
            List<JObject> serializedPairs = new();

            // Serialize each bitpairs object in the list
            foreach (var pair in bitPairs)
            {
                pair.UpdateValuesFromUI();

                // Use JObject to create a JSON representation
                JObject serializedPair = new(
                    new JProperty("redeemName", pair.redeemName),
                    new JProperty("sensationDropdownValue", pair.sensationDropdownValue)
                );

                serializedPairs.Add(serializedPair);
            }

            // Convert the list of JObjects to a JSON string using JsonConvert
            string jsonData = JsonConvert.SerializeObject(serializedPairs, Formatting.Indented);

            PlayerPrefs.SetString(saveKey2, jsonData);
        }
        else
        {
            PlayerPrefs.SetString(saveKey2, string.Empty);
            Debug.Log("No Bit data to save");
        }
        PlayerPrefs.Save();
    }

    public void LoadSettings()
    {
        followDropdown.transform.GetComponent<DropdownPopulator>().LoadDropdownValue(PlayerPrefs.GetInt("followDropdown", 0));
        raidDropdown.transform.GetComponent<DropdownPopulator>().LoadDropdownValue(PlayerPrefs.GetInt("raidDropdown", 0));
        hypeDropdown.transform.GetComponent<DropdownPopulator>().LoadDropdownValue(PlayerPrefs.GetInt("hypeDropdown", 0));
        subscribeDropdown.transform.GetComponent<DropdownPopulator>().LoadDropdownValue(PlayerPrefs.GetInt("subscribeDropdown", 0));


        int prefabCount = redeemPrefabList.Count;
        for (int i = 0; i < prefabCount; i++)
        {
            SubtractRedeemPrefab();
        }
        string jsonData = PlayerPrefs.GetString(saveKey1, string.Empty);
        if (string.IsNullOrEmpty(jsonData))
        {
            Debug.Log("Empty Redeem List"); // Return an empty list if no data saved
        }
        else
        {
            // Deserialize the JSON string into a list of JObjects
            List<JObject> serializedPairs = JsonConvert.DeserializeObject<List<JObject>>(jsonData);

            // Create a new list to hold loaded redeemPair objects
            List<RedeemSensationPair> loadedPairs = new();

            // Process each serialized JObject
            foreach (JObject serializedPair in serializedPairs)
            {
                GameObject redeemReference = Instantiate(redeemPrefab, redeemList);
                // Create a new redeemPair instance
                RedeemSensationPair redeemPair = new()
                {
                    redeemNameInputField = redeemReference.transform.GetChild(0).GetComponent<TMP_InputField>(),
                    sensationDropdown = redeemReference.transform.GetChild(1).GetComponent<TMP_Dropdown>(),
                    // Extract values from the JObject properties
                    redeemName = serializedPair.Value<string>("redeemName"),
                    sensationDropdownValue = serializedPair.Value<int>("sensationDropdownValue")
                };
                redeemPrefabList.Add(redeemReference);
                redeemReference.transform.GetChild(1).GetComponent<DropdownPopulator>().LoadDropdownValue(redeemPair.sensationDropdownValue);
                redeemPair.SetValuesToUI();
                loadedPairs.Add(redeemPair);
            }

            redeemPairs = loadedPairs;
        }
        int prefabCount2 = bitPrefabList.Count;
        for (int i = 0; i < prefabCount2; i++)
        {
            SubtractBitPrefab();
        }
        jsonData = PlayerPrefs.GetString(saveKey2, string.Empty);
        if (string.IsNullOrEmpty(jsonData))
        {
            Debug.Log("Empty Bit List"); // Return an empty list if no data saved
        }
        else
        {
            // Deserialize the JSON string into a list of JObjects
            List<JObject> serializedPairs = JsonConvert.DeserializeObject<List<JObject>>(jsonData);

            // Create a new list to hold loaded bitpair objects
            List<RedeemSensationPair> loadedPairs = new();

            // Process each serialized JObject
            foreach (JObject serializedPair in serializedPairs)
            {
                GameObject redeemReference = Instantiate(bitPrefab, bitList);
                // Create a new bitpair instance
                RedeemSensationPair bitPair = new()
                {
                    redeemNameInputField = redeemReference.transform.GetChild(0).GetComponent<TMP_InputField>(),
                    sensationDropdown = redeemReference.transform.GetChild(1).GetComponent<TMP_Dropdown>(),
                    // Extract values from the JObject properties
                    redeemName = serializedPair.Value<string>("redeemName"),
                    sensationDropdownValue = serializedPair.Value<int>("sensationDropdownValue")
                };
                bitPrefabList.Add(redeemReference);
                redeemReference.transform.GetChild(1).GetComponent<DropdownPopulator>().LoadDropdownValue(bitPair.sensationDropdownValue);
                bitPair.SetValuesToUI();
                loadedPairs.Add(bitPair);
            }

            bitPairs = loadedPairs;
        }
    }
    public void AddRedeemPrefab()
    {
        GameObject redeemReference = Instantiate(redeemPrefab, redeemList);
        RedeemSensationPair redeemPair = new()
        {
            redeemNameInputField = redeemReference.transform.GetChild(0).GetComponent<TMP_InputField>(),
            sensationDropdown = redeemReference.transform.GetChild(1).GetComponent<TMP_Dropdown>()
        };
        redeemPrefabList.Add(redeemReference);
        redeemPairs.Add(redeemPair);
    }

    public void AddBitPrefab()
    {
        GameObject bitReference = Instantiate(bitPrefab, bitList);
        RedeemSensationPair bitPair = new()
        {
            redeemNameInputField = bitReference.transform.GetChild(0).GetComponent<TMP_InputField>(),
            sensationDropdown = bitReference.transform.GetChild(1).GetComponent<TMP_Dropdown>()
        };
        bitPrefabList.Add(bitReference);
        bitPairs.Add(bitPair);
    }
    public void SubtractRedeemPrefab()
    {
        if (redeemPrefabList.Count > 0)
        {
            int lastIndex = redeemPrefabList.Count - 1;
            GameObject lastRedeemPrefab = redeemPrefabList[lastIndex];
            redeemPrefabList.Remove(lastRedeemPrefab);
            redeemPairs.Remove(redeemPairs[lastIndex]);
            Destroy(lastRedeemPrefab);
        }
    }
    public void SubtractBitPrefab()
    {
        if (bitPrefabList.Count > 0)
        {
            int lastIndex = bitPrefabList.Count - 1;
            GameObject lastBitPrefab = bitPrefabList[lastIndex];
            bitPrefabList.Remove(lastBitPrefab);
            bitPairs.Remove(bitPairs[lastIndex]);
            Destroy(lastBitPrefab);
        }
    }
    private void SendSensationBasedOnDropdown(string dropdown)
    {
        PlayFullSensation(dropdown);
    }
    private void SendSensationBasedOnRedeem(string title)
    {
        foreach (var pair in redeemPairs)
        {
            if (title.ToLower() == pair.redeemNameInputField.text.ToLower())
            {
                PlayFullSensation(pair.sensationDropdown.options[pair.sensationDropdown.value].text);
            }
        }
    }
    private void SendSensationBasedOnBits(int bitsused)
    {

        foreach (var pair in bitPairs)
        {
            if (int.TryParse(pair.redeemNameInputField.text, out int redeemValue) && bitsused == redeemValue)
            {
                PlayFullSensation(pair.sensationDropdown.options[pair.sensationDropdown.value].text);
            }
        }

    }
    public void PlayFullSensation(string filename)
    {
        string[] directoryPaths =
        {
        "Assets/OWO/Sensation Events",
        "Assets/OWO/MicroSensation Events"
    };

        string fullPath = FindFileInDirectories(filename, directoryPaths);
        if (fullPath == null)
        {
            if (debugMode)
            {
                File.AppendAllText(fullDebugPath, DateTime.Now.ToString() + " " + $"File {filename}.json does not exist in any of the directories." + "\n");
            }
            // Debug.Log($"File {filename}.json does not exist in any of the directories.");
            return;
        }
        string jsonData = File.ReadAllText(fullPath);
        AppendedMicroSensations sensationFromJson = JsonUtility.FromJson<AppendedMicroSensations>(jsonData);
        OWO.Send(Sensation.Parse(sensationFromJson.data));
    }
    public void PlayFullScalingSensation(string filename, int min, int bit)
    {
        int finalScore = 0;
        string[] directoryPaths =
        {
        "Assets/OWO/Sensation Events",
        "Assets/OWO/MicroSensation Events"
    };

        string fullPath = FindFileInDirectories(filename, directoryPaths);
        if (fullPath == null)
        {
            if (debugMode)
            {
                File.AppendAllText(fullDebugPath, DateTime.Now.ToString() + " " + $"File {filename}.json does not exist in any of the directories." + "\n");
            }
            // Debug.Log($"File {filename}.json does not exist in any of the directories.");
            return;
        }
        string jsonData = File.ReadAllText(fullPath);
        AppendedMicroSensations sensationFromJson = JsonUtility.FromJson<AppendedMicroSensations>(jsonData);
        jsonData = sensationFromJson.data;
        string[] parts = jsonData.Split(',');
        int currentIntensity = int.Parse(parts[2]);
        if (bit >= min)
        {
            double ratio = Math.Min((double)bit / min, 25.0);
            finalScore = (int)Math.Round(Math.Clamp(
                currentIntensity + (ratio - 1.0) / (25.0 - 1.0) * (100 - currentIntensity),
                currentIntensity, 100));
        }
        parts[2] = finalScore.ToString();
        string sensation = string.Join(",", parts) + ",";

        OWO.Send(Sensation.Parse(sensation));
    }
    public void StopOWOSensation()
    {
        OWO.Stop();
    }

    private string FindFileInDirectories(string filename, string[] directories)
    {
        foreach (string directory in directories)
        {
            if (!Directory.Exists(directory))
            {
                // Debug.Log($"Directory {directory} not found.");
                continue;
            }

            string filePath = Path.Combine(directory, filename + ".json");
            if (File.Exists(filePath))
            {
                return filePath;
            }
        }
        return null;
    }


}
