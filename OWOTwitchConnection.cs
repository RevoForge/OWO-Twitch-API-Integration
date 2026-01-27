using Michsky.UI.Shift;
using OWOGame;
using System;
using System.Collections;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

public class TwitchOwoConnection : MonoBehaviour
{
    private MicroSensation startup;
    public TimedEvent startupEvent;
    public SplashScreenManager splashScreenManager;
    private void Start()
    {
        startup = SensationsFactory.Create(100, 1, 25, 1, 1, 0);
    }
    public async void InitializeOWO()
    {
        var cts = new CancellationTokenSource();
        _ = OWO.AutoConnect();
        var timeoutTask = Task.Delay(TimeSpan.FromSeconds(60), cts.Token);
        while (OWO.ConnectionState != ConnectionState.Connected && !timeoutTask.IsCompleted)
        {
            await Task.Delay(100);
        }
        if (OWO.ConnectionState == ConnectionState.Connected)
        {
            cts.Cancel();
            startupEvent.ConnectedBypass();
            StartCoroutine(StartupPulse());
        }
        else
        {
            splashScreenManager.FailedConnection();
        }
    }
    private IEnumerator StartupPulse()
    {
        yield return new WaitForSeconds(1f);
        Debug.Log("Startup Pulse");
        float elapsedTime = 0f;
        const float timeInterval = 1f;

        while (elapsedTime < 3f)
        {
            OWO.Send(startup.WithMuscles(Muscle.All));
            yield return new WaitForSeconds(timeInterval);
            elapsedTime += timeInterval;
        }
    }
}
