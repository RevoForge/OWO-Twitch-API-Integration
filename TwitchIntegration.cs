using NativeWebSocket;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OWOGame;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
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

    private readonly ConcurrentQueue<Action> _mainThreadActions = new();

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
        listener.Prefixes.Add("http://localhost:12345/storeToken/");
        listener.Start();
        listener.BeginGetContext(OnHttpRequestReceived, listener);

        serverStarted = true;
    }
    // Website Auth Code Grab
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

        byte[] buffer = Encoding.UTF8.GetBytes(responseString);
        context.Response.ContentLength64 = buffer.Length;
        var output = context.Response.OutputStream;
        output.Write(buffer, 0, buffer.Length);
        output.Close();

        listener.BeginGetContext(OnHttpRequestReceived, listener);
    }
    private string savedToken;
    void HandleTokenRequest(HttpListenerContext context)
    {
        Stream body = context.Request.InputStream;
        System.Text.Encoding encoding = context.Request.ContentEncoding;
        StreamReader reader = new(body, encoding);

        string data = reader.ReadToEnd();

        TokenData tokenData = JsonUtility.FromJson<TokenData>(data);
        var token = tokenData.token;

        if (!string.IsNullOrEmpty(token))
        {
            _mainThreadActions.Enqueue(() =>
            {
                // ConnectToPubSub(token);
                savedToken = token;
                FetchUserData(savedToken);
                Debug.Log("Token Success");
            });
        }
        else
        {
            _mainThreadActions.Enqueue(() =>
            {
                Debug.Log("Token Failure");
            });
        }
        byte[] responseBuffer = Encoding.UTF8.GetBytes("Token received.");
        context.Response.ContentLength64 = responseBuffer.Length;
        var responseOutput = context.Response.OutputStream;
        responseOutput.Write(responseBuffer, 0, responseBuffer.Length);
        responseOutput.Close();
    }
    private void FetchUserData(string token)
    {
        StartCoroutine(GetUserDataCoroutine(token));
    }

    private int channelIDNumber = 0;
    private IEnumerator GetUserDataCoroutine(string token)
    {
        string url = "https://api.twitch.tv/helix/users";

        using UnityWebRequest www = UnityWebRequest.Get(url);
        www.SetRequestHeader("Client-ID", "vdawpon1s1za6ioint1wqyx3mqqhy3"); // Need your Twitch Apps id
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
                    Debug.LogError("Failed to parse user ID to an integer.");
                }
                channelInputField.text = userName;
            }
            else
            {
                Debug.LogError("Received empty user data from Twitch.");
            }
        }
        else if (www.result == UnityWebRequest.Result.ConnectionError)
        {
            Debug.LogError("Connection Error: " + www.error);
        }
        else if (www.result == UnityWebRequest.Result.ProtocolError)
        {
            Debug.LogError("Protocol Error: " + www.error);
        }

    }
    [SerializeField]
    private TMP_Text channelInputField;
    [SerializeField]
    private TMP_InputField testRedeemInputField;
    [SerializeField]
    private TMP_InputField testBitsInputField;
    private WebSocket ws;
    public bool enableFollow = false;
    private bool enableRaid = false;
    private bool enableHype = false;
    private bool enableSubscribe = false;
    // Connecting To Twitch for Auth Grab
    public void EnableFollow()
    {
        enableFollow = true;
    }
    public void EnableRaid()
    {
        enableRaid = true;
    }
    public void EnableHype()
    {
        enableHype = true;
    }
    public void EnableSubscribe()
    {
        enableSubscribe = true;
    }
    public void DisableFollow()
    {
        enableFollow = false;
    }
    public void DisableRaid()
    {
        enableRaid = false;
    }
    public void DisableHype()
    {
        enableHype = false;
    }
    public void DisableSubscribe()
    {
        enableSubscribe = false;
    }
    public void InitiateOAuth()
    {
        string authorizationEndpoint = "https://id.twitch.tv/oauth2/authorize";
        string clientId = "vdawpon1s1za6ioint1wqyx3mqqhy3"; // Need your Twitch Apps id
        string redirectUri = "http://localhost:12345/callback/";
        string scopes = "channel:read:subscriptions+moderator:read:followers+channel:read:redemptions+bits:read+channel:read:hype_train";  // Check the specific scopes you require.
        string fullUrl = $"{authorizationEndpoint}?client_id={clientId}&redirect_uri={redirectUri}&response_type=token&scope={scopes}";
        Application.OpenURL(fullUrl);
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

    private readonly string twitchClientId = "vdawpon1s1za6ioint1wqyx3mqqhy3";
    private string websocketSessionId = "";

    private IEnumerator SendSubscriptionRequest()
    {
        string jsonPayload = "";
        for (int i = 0; i < 6; i++)
        {
            if (channelIDNumber <= 0 || string.IsNullOrEmpty(websocketSessionId))
            {
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
                jsonPayload = $"{{\"type\":\"channel.hype_train.begin\",\"version\":\"1\",\"condition\":{{\"broadcaster_user_id\":\"{channelIDNumber}\"}},\"transport\":{{\"method\":\"websocket\",\"session_id\":\"{websocketSessionId}\"}}}}";
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
            request.SetRequestHeader("Client-Id", twitchClientId);
            // Set content type to JSON
            request.SetRequestHeader("Content-Type", "application/json");

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                Debug.Log($"EventSub {type} successful.");
            }
            else
            {
                Debug.Log($"EventSub Payload: {jsonPayload} ");
                Debug.LogError($"EventSub {type} failed: " + request.error);
            }
        }

    }
    public TMP_Text connectionText;
    private void HandleOpen()
    {
        // LogEntry("Connected to Twitch PubSub");
        connectionText.text = "Twitch Is Connected";
        // Debug.Log("Connected");
       // StartCoroutine(WebSocketPayload());
    }
    private List<string> usersHaveFollowed = new();
    public string filePath = "TwitchMessageLogs.txt"; // Path to the file, relative to the project folder
    public string textToSave = "Log Start";
    public string textToAppend = "New line of text!";
    private void HandleMessage(byte[] bytes)
    {
        var messageStr = Encoding.UTF8.GetString(bytes);
        TwitchResponseData incomingMessage = JsonConvert.DeserializeObject<TwitchResponseData>(messageStr);

        if (Debug.isDebugBuild)
        {
            string fullPath = Path.Combine(Application.dataPath, filePath);

            try
            {
                // Check if the file exists
                if (!File.Exists(fullPath))
                {
                    // Create the file if it doesn't exist
                    using StreamWriter sw = File.CreateText(fullPath);
                    // Write the initial text to the file
                    sw.WriteLine(textToSave);
                }
                else
                {
                    // Append text to an existing file
                    File.AppendAllText(fullPath, textToAppend + "\n"); // Adding a newline after each appended text
                }
            }
            catch (Exception e)
            {
                Debug.LogError("Error handling file: " + e.Message);
            }

        }
        if (incomingMessage.metadata.message_type == "notification")
        {
            if (Debug.isDebugBuild)
            {
                // Code here will only run in development builds (including Unity Editor)
            }
            if (incomingMessage.metadata.subscription_type == "channel.follow" && enableFollow)
            {
                if (!usersHaveFollowed.Contains(incomingMessage.payload.eventData.user_id))
                {
                    usersHaveFollowed.Add(incomingMessage.payload.eventData.user_id);
                    // Do Thing For Channel Following
                    SendSensationBasedOnDropdown(followDropdown.itemText.text);
                }
                else
                {
                    // User already followed this session
                }
            }
            if (incomingMessage.metadata.subscription_type == "channel.raid" && enableRaid)
            {
                SendSensationBasedOnDropdown(raidDropdown.itemText.text);
                // Do Thing For Channel Raid
            }
            if (incomingMessage.metadata.subscription_type == "channel.cheer")
            {
                // Do the Bit Thing
                SendSensationBasedOnBits(int.Parse(incomingMessage.payload.eventData.bits));
            }
            if (incomingMessage.metadata.subscription_type == "channel.subscribe" && enableSubscribe)
            {
                SendSensationBasedOnDropdown(subscribeDropdown.itemText.text);
                // Do Thing For Channel Subscribe
            }
            if (incomingMessage.metadata.subscription_type == "channel.channel_points_custom_reward_redemption.add")
            {
                // Do Thing For Point Redeem
                SendSensationBasedOnRedeem(incomingMessage.payload.eventData.reward.title);
            }
            if (incomingMessage.metadata.subscription_type == "channel.hype_train.begin" && enableHype)
            {
                SendSensationBasedOnDropdown(hypeDropdown.itemText.text);
                // Do Thing For Hype Train
            }
        }
        else if (incomingMessage.metadata.message_type == "session_welcome")
        {
            if (incomingMessage != null && incomingMessage.payload != null && incomingMessage.payload.session != null)
            {
                websocketSessionId = incomingMessage.payload.session.id;
                StartCoroutine(SendSubscriptionRequest());
            }
            else
            {
                Debug.LogError("Failed to extract WebSocket Session ID from the response.");
            }

        }
        else if (incomingMessage.metadata.message_type == "session_reconnect")
        {
            ConnectToEventSub(true, incomingMessage.payload.session.reconnect_url);
        }
        else if (incomingMessage.metadata.message_type != "session_keepalive")
        {
            Debug.Log($"Received unknown message: {messageStr}");
        }
    }
    private void HandleError(string errorMessage)
    {
        Debug.LogError("Error with Twitch EventSub: " + errorMessage);
    }

    private void HandleClose(WebSocketCloseCode reason)
    {
        string closeMessage = $"Disconnected from Twitch EventSub. Close Code:{reason}";
        _mainThreadActions.Enqueue(() =>
        {
            // LogEntry(closeMessage);
            
            Debug.LogError(closeMessage);
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
        DropdownPopulator[] dropdownPopulators = FindObjectsOfType<DropdownPopulator>();
        foreach (DropdownPopulator dropdownPopulator in dropdownPopulators)
        {
            dropdownPopulator.PopulateDropdownWithFilenames();
        }
    }
    public void SendTestButton()
    {
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
            List<JObject> serializedPairs = new List<JObject>();

            // Serialize each redeemPair object in the list
            foreach (var pair in redeemPairs)
            {
                pair.UpdateValuesFromUI();

                // Use JObject to create a JSON representation
                JObject serializedPair = new JObject(
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
            List<JObject> serializedPairs = new List<JObject>();

            // Serialize each bitpairs object in the list
            foreach (var pair in bitPairs)
            {
                pair.UpdateValuesFromUI();

                // Use JObject to create a JSON representation
                JObject serializedPair = new JObject(
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
                List<RedeemSensationPair> loadedPairs = new List<RedeemSensationPair>();

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
                List<RedeemSensationPair> loadedPairs = new List<RedeemSensationPair>();

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
            // Debug.Log($"File {filename}.json does not exist in any of the directories.");
            return;
        }
        string jsonData = File.ReadAllText(fullPath);
        AppendedMicroSensations sensationFromJson = JsonUtility.FromJson<AppendedMicroSensations>(jsonData);
        OWO.Send(Sensation.Parse(sensationFromJson.data));
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
