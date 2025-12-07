using UnityEngine;
using System;
using System.Net;
using System.Threading;
using System.IO;
using System.Text;
using System.Collections.Generic;
using UnityEngine.SceneManagement;

using UnityEngine.Networking;
using System.Net.Http;

public class HttpServer : MonoBehaviour
{
    private HttpListener listener;
    private Thread listenerThread;
    private static readonly HttpClient client = new HttpClient();

    [SerializeField] private TiffSlicer tiffSlicer;

    // Serializable classes for creating JSON responses
    [Serializable]
    private class SceneObject
    {
        public string name;
        public int id;
    }

    [Serializable]
    private class ObjectListWrapper
    {
        public List<SceneObject> objects;
    }

    [Serializable]
    private class LoadTiffFromUrlRequest
    {
        public string fileUrl;
    }

    void Awake()
    {
        // Start the HTTP server on a background thread
        listenerThread = new Thread(StartServer);
        listenerThread.IsBackground = true;
        listenerThread.Start();
        Debug.Log("HTTP Server started.");
    }

    void StartServer()
    {
        try
        {
            // Listen on localhost port 8080
            listener = new HttpListener();
            listener.Prefixes.Add("http://localhost:8080/");
            listener.Start();

            while (listener.IsListening)
            {
                // Wait for an incoming request
                HttpListenerContext context = listener.GetContext();
                ProcessRequest(context);
            }
        }
        catch (Exception e)
        {
            // Log any exceptions
            Debug.LogError($"HttpListenerException: {e.Message}");
        }
    }

    private void ProcessRequest(HttpListenerContext context)
    {
        HttpListenerRequest request = context.Request;
        HttpListenerResponse response = context.Response;

        // Log the request URL
        Debug.Log($"Received request for: {request.Url}");

        // Default response
        string responseString = "{\"status\": \"error\", \"message\": \"Not Found\"}";
        int statusCode = 404;
        
        // Simple routing based on the URL path
        if (request.Url.AbsolutePath == "/")
        {
            responseString = "{\"status\": \"success\", \"message\": \"Welcome to the Unity 3D Visualizer API\"}";
            statusCode = 200;
        }
        else if (request.Url.AbsolutePath == "/scene/info")
        {
            responseString = "{\"sceneName\": \"SampleScene\", \"objectCount\": 10}"; // Example data
            statusCode = 200;
        }
        else if (request.Url.AbsolutePath == "/api/scene/objects")
        {
            // This needs to run on the main thread, so we dispatch it and wait.
            string jsonResponse = null;
            var mre = new ManualResetEvent(false);

            MainThreadDispatcher.Instance()?.Enqueue(() => {
                try
                {
                    var rootObjects = SceneManager.GetActiveScene().GetRootGameObjects();
                    var objectList = new List<SceneObject>();
                    foreach (var obj in rootObjects)
                    {
                        objectList.Add(new SceneObject { name = obj.name, id = obj.GetInstanceID() });
                    }

                    // JsonUtility cannot serialize a root list, so we use a wrapper object.
                    ObjectListWrapper wrapper = new ObjectListWrapper { objects = objectList };
                    jsonResponse = JsonUtility.ToJson(wrapper);
                }
                catch (Exception e)
                {
                    Debug.LogError($"Error getting scene objects: {e.Message}");
                    jsonResponse = "{\"status\": \"error\", \"message\": \"Failed to retrieve scene objects.\"}";
                }
                finally
                {
                    mre.Set();
                }
            });

            // Wait for the main thread to finish its work.
            mre.WaitOne();

            responseString = jsonResponse;
            statusCode = 200; // Assuming success, error is handled inside the JSON
        }
        else if (request.Url.AbsolutePath == "/api/tiff/load" && request.HttpMethod == "POST")
        {
            if (tiffSlicer == null)
            {
                responseString = "{\"status\": \"error\", \"message\": \"TiffSlicer not assigned.\"}";
                statusCode = 500;
            }
            else
            {
                string requestBody = new StreamReader(request.InputStream, request.ContentEncoding).ReadToEnd();
                LoadTiffFromUrlRequest loadRequest = JsonUtility.FromJson<LoadTiffFromUrlRequest>(requestBody);

                if (loadRequest == null || string.IsNullOrEmpty(loadRequest.fileUrl))
                {
                    responseString = "{\"status\": \"error\", \"message\": \"Invalid request body. 'fileUrl' is required.\"}";
                    statusCode = 400;
                }
                else
                {
                    try
                    {
                        byte[] tiffData = client.GetByteArrayAsync(loadRequest.fileUrl).Result;
                        
                        var mre = new ManualResetEvent(false);
                        bool loadSuccess = false;

                        MainThreadDispatcher.Instance()?.Enqueue(() => {
                            try
                            {
                                tiffSlicer.LoadTiffFromData(tiffData);
                                loadSuccess = true;
                            }
                            catch (Exception e)
                            {
                                Debug.LogError($"Error loading TIFF file: {e.Message}");
                            }
                            finally
                            {
                                mre.Set();
                            }
                        });
                        mre.WaitOne();

                        if (loadSuccess)
                        {
                            responseString = "{\"status\": \"success\", \"message\": \"TIFF file loaded successfully.\"}";
                            statusCode = 200;
                        }
                        else
                        {
                            responseString = "{\"status\": \"error\", \"message\": \"Failed to load TIFF file. Check Unity console for details.\"}";
                            statusCode = 500;
                        }
                    }
                    catch (Exception e)
                    {
                        Debug.LogError($"Failed to download TIFF file: {e.Message}");
                        responseString = "{\"status\": \"error\", \"message\": \"Failed to download TIFF file.\"}";
                        statusCode = 500;
                    }
                }
            }
        }
        else if (request.Url.AbsolutePath == "/api/tiff/slice" && request.HttpMethod == "GET")
        {
            if (tiffSlicer == null)
            {
                responseString = "{\"status\": \"error\", \"message\": \"TiffSlicer not assigned.\"}";
                statusCode = 500;
            }
            else
            {
                string plane = request.QueryString["plane"];
                string sliceIndexString = request.QueryString["sliceIndex"];

                if (string.IsNullOrEmpty(plane) || string.IsNullOrEmpty(sliceIndexString))
                {
                    responseString = "{\"status\": \"error\", \"message\": \"Missing 'plane' or 'sliceIndex' query parameter.\"}";
                    statusCode = 400;
                }
                else if (!int.TryParse(sliceIndexString, out int sliceIndex))
                {
                    responseString = "{\"status\": \"error\", \"message\": \"Invalid 'sliceIndex' parameter. Must be an integer.\"}";
                    statusCode = 400;
                }
                else
                {
                    var mre = new ManualResetEvent(false);
                    byte[] pngData = null;

                    MainThreadDispatcher.Instance()?.Enqueue(() => {
                        try
                        {
                            pngData = tiffSlicer.GetSlice(plane, sliceIndex);
                        }
                        catch (Exception e)
                        {
                            Debug.LogError($"Error getting TIFF slice: {e.Message}");
                        }
                        finally
                        {
                            mre.Set();
                        }
                    });
                    mre.WaitOne();

                    if (pngData != null && pngData.Length > 0)
                    {
                        response.StatusCode = 200;
                        response.ContentType = "image/png";
                        response.ContentLength64 = pngData.Length;
                        
                        try
                        {
                            Stream output = response.OutputStream;
                            output.Write(pngData, 0, pngData.Length);
                            output.Close();
                        }
                        catch(Exception e)
                        {
                            Debug.LogError($"Error writing image response: {e.Message}");
                        }
                        return; // Important: Exit here as we've already sent the response
                    }
                    else
                    {
                        responseString = "{\"status\": \"error\", \"message\": \"Failed to get TIFF slice. Check Unity console for details.\"}";
                        statusCode = 500;
                    }
                }
            }
        }

        // Sending the response
        response.StatusCode = statusCode;
        response.ContentType = "application/json";
        byte[] buffer = Encoding.UTF8.GetBytes(responseString);
        response.ContentLength64 = buffer.Length;
        
        try
        {
            Stream output = response.OutputStream;
            output.Write(buffer, 0, buffer.Length);
            output.Close();
        }
        catch(Exception e)
        {
            Debug.LogError($"Error writing response: {e.Message}");
        }
    }

    void OnDestroy()
    {
        // Cleanly stop the server when the application closes
        if (listener != null && listener.IsListening)
        {
            Debug.Log("Stopping HTTP Server.");
            listener.Stop();
            listener.Close();
        }

        if (listenerThread != null && listenerThread.IsAlive)
        {
            listenerThread.Abort();
        }
    }
}
