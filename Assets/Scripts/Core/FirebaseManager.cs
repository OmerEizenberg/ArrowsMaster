using Firebase;
using Firebase.Crashlytics;
using Firebase.Extensions;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FirebaseManager : MonoBehaviour
{
    void Start()
    {
        FirebaseApp.CheckAndFixDependenciesAsync().ContinueWithOnMainThread(task => {
            DependencyStatus dependencyStatus = task.Result;
            
            if (dependencyStatus == DependencyStatus.Available)
            {
                // Initialize Firebase
                FirebaseApp app = FirebaseApp.DefaultInstance;

                // Enable Crashlytics collection
                Crashlytics.ReportUncaughtExceptionsAsFatal = true;
                
                Debug.Log("Firebase Crashlytics is ready.");
            }
            else
            {
                Debug.LogError($"Could not resolve Firebase dependencies: {dependencyStatus}");
            }
        });
        //StartCoroutine(fakeReport());
    }

    public void ThrowTestException() {
        throw new System.Exception("Firebase Test Crash!");
    }

    public IEnumerator fakeReport()
    {
        yield return new WaitForSeconds(3.0f);
        ThrowTestException();
    }
}