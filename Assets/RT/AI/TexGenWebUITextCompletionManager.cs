using SimpleJSON;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using System.IO;


public class TexGenWebUITextCompletionManager : MonoBehaviour
{

    public void Start()
    {
        // ExampleOfUse();
    }

    //*  EXAMPLE START (this could be moved to your own code) */

    void ExampleOfUse()
    {
        //build a stack of GTPChatLine so we can add as many as we want

        TexGenWebUITextCompletionManager textCompletionScript = gameObject.GetComponent<TexGenWebUITextCompletionManager>();

        string serverAddress = "put it here";

        string prompt = "crap";

        string json = textCompletionScript.BuildChatCompleteJSON(prompt);
        RTDB db = new RTDB();

        textCompletionScript.SpawnChatCompleteRequest(json, OnCompletedCallback, db, serverAddress);
    }

    void OnCompletedCallback(RTDB db, JSONObject jsonNode)
    {

        if (jsonNode == null)
        {
            //must have been an error
            Debug.Log("Got callback! Data: " + db.ToString());
            RTQuickMessageManager.Get().ShowMessage(db.GetString("msg"));
            return;
        }

        /*
        foreach (KeyValuePair<string, JSONNode> kvp in jsonNode)
        {
            Debug.Log("Key: " + kvp.Key + " Val: " + kvp.Value);
        }
        */

        string reply = jsonNode["choices"][0]["message"]["content"];
        RTQuickMessageManager.Get().ShowMessage(reply);

    }

    //*  EXAMPLE END */
    public bool SpawnChatCompleteRequest(string jsonRequest, Action<RTDB, JSONObject> myCallback, RTDB db, string serverAddress)
    {
        StartCoroutine(GetRequest(jsonRequest, myCallback, db, serverAddress));
        return true;
    }

    

    public string BuildChatCompleteJSON(string msg, int max_tokens = 100, float temperature = 1.0f)
    {
        
        string json =
         $@"{{

""prompt"": ""{SimpleJSON.JSONNode.Escape(msg)}"",
""max_new_tokens"": {max_tokens},
""auto_max_new_tokens"": false,
""preset"": ""None"",
""do_sample"": true,
""temperature"": 0.7,
""top_p"": 0.9,
""typical_p"": 1,
""epsilon_cutoff"": 0,
""eta_cutoff"": 0,
""tfs"": 1,
""top_a"": 0,
""repetition_penalty"": 1.18,
""repetition_penalty_range"": 0,
""top_k"": 40,
""min_length"": 0, 
""no_repeat_ngram_size"": 0, 
""num_beams"": 1, 
""penalty_alpha"": 0, 
""length_penalty"": 1, 
""early_stopping"": false, 
""mirostat_mode"": 0, 
""mirostat_tau"": 5, 
""mirostat_eta"": 0.1,
""guidance_scale"": 1, 
""negative_prompt"": """", 
""seed"": -1,
""add_bos_token"": true,
""truncation_length"": 4096, 
""ban_eos_token"": false, 
""skip_special_tokens"": true, 
""stopping_strings"": []
   }}";

        return json;
    }

    IEnumerator GetRequest(string json, Action<RTDB, JSONObject> myCallback, RTDB db, string serverAddress)
    {

#if UNITY_STANDALONE && !RT_RELEASE
        File.WriteAllText("text_completion_sent.json", json);
#endif
        string url;
        url = serverAddress + "/api/v1/generate";

        using (var postRequest = UnityWebRequest.PostWwwForm(url, "POST"))
        {
            //Start the request with a method instead of the object itself
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(json);
            postRequest.uploadHandler = (UploadHandler)new UploadHandlerRaw(bodyRaw);
               postRequest.SetRequestHeader("Content-Type", "application/json");
            //   postRequest.SetRequestHeader("Authorization", "Bearer "+openAI_APIKey);
            yield return postRequest.SendWebRequest();

            if (postRequest.result != UnityWebRequest.Result.Success)
            {
                string msg = postRequest.error;
                Debug.Log(msg);
                //Debug.Log(postRequest.downloadHandler.text);
#if UNITY_STANDALONE && !RT_RELEASE
                File.WriteAllText("last_error_returned.json", postRequest.downloadHandler.text);
#endif

                db.Set("status", "failed");
                db.Set("msg", msg);
                myCallback.Invoke(db, null);
            }
            else
            {

#if UNITY_STANDALONE && !RT_RELEASE
                //                Debug.Log("Form upload complete! Downloaded " + postRequest.downloadedBytes);

                File.WriteAllText("textgen_json_received.json", postRequest.downloadHandler.text);
#endif
                JSONNode rootNode = JSON.Parse(postRequest.downloadHandler.text);
                yield return null; //wait a frame to lesson the jerkiness

                Debug.Assert(rootNode.Tag == JSONNodeType.Object);

                db.Set("status", "success");
                myCallback.Invoke(db, (JSONObject)rootNode);

            }
        }
    }
}
