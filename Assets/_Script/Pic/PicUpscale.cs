using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using System;
using SimpleJSON;

public class PicUpscale : MonoBehaviour
{
    float startTime;
    public GameObject m_sprite;
    bool m_bIsGenerating;
    int m_gpu;
    bool m_fixFaces = false;
    float m_upscale = 1.0f; //1 means no change
    public PicMain m_picScript;

    // Start is called before the first frame update
    void Start()
    {
       
    }

    // Update is called once per frame
    void Update()
    {
        if (m_bIsGenerating)
        {
            float elapsed = Time.realtimeSinceStartup - startTime;
            m_picScript.SetStatusMessage(elapsed.ToString("Upscale: 0.0#"));
        }
        else
        {
            
        }
    }

    public void SetForceFinish(bool bNew)
    {
        if (bNew && m_bIsGenerating)
        {
            m_picScript.SetStatusMessage("(killing process)");
            m_gpu = -1; //invalid
        }

    }
    public bool IsBusy()
    {
        return m_bIsGenerating;
    }

    private void OnDestroy()
    {
        if (m_bIsGenerating)
        {
            Config.Get().SetGPUBusy(m_gpu, false);
        }
    }

    public void SetGPU(int gpuID)
    {
        m_gpu = gpuID;
    }

    public void OnForceUpscale(int gpu)
    {
        //Debug.Log("Starting upscale...");
        m_gpu = gpu;
        m_upscale = 2.0f;
        m_fixFaces = true;

        StartWebRequest(true);
    }
    public void StartWebRequest(bool bFromButton)
    {
        var gpuInfo = Config.Get().GetGPUInfo(m_gpu);

        string url = gpuInfo.remoteURL+ "/v1/extras";
       
        if (!bFromButton)
        {
            m_upscale = GameLogic.Get().GetUpscale();
            m_fixFaces = GameLogic.Get().GetFixFaces();

            if (m_upscale > 1.0f)
            {
                m_picScript.GetMaskScript().SetMaskVisible(false); //we don't want to see a rect

            }
        }
        
        if (m_upscale <= 1.0f)
        {
            return; //we are done
        }
        if (Config.Get().IsGPUBusy(m_gpu))
        {
            Debug.LogError("Why is GPU busy?!");
            return;
        }

        Config.Get().SetGPUBusy(m_gpu, true);
        m_bIsGenerating = true;
        startTime = Time.realtimeSinceStartup;

        StartCoroutine(GetRequest( url));
    }

    IEnumerator GetRequest( string url)
    {
        //yield return new WaitForEndOfFrame();

        SpriteRenderer renderer = m_sprite.GetComponent<SpriteRenderer>();
        UnityEngine.Sprite s = renderer.sprite;
        byte[] picPng = s.texture.EncodeToPNG();
        // For testing purposes, also write to a file in the project folder
        //File.WriteAllBytes(Application.dataPath + "/../SavedScreen.png", picPng);
        String finalURL = url;
        Debug.Log("Upscaling with " + finalURL + " local GPU ID " + m_gpu);
        string imgBase64 = Convert.ToBase64String(picPng);
        yield return null; //wait a free to lesson the jerkiness

        var gpuInf = Config.Get().GetGPUInfo(m_gpu);

        //A lot of these parms are hardcoded where I like them, maybe add GUI to the client to control them later
        float gfpgan_visibility = 0;
        float codeformer_visibility = 0;
        bool bRemoveBackground = GameLogic.Get().GetRemoveBackground();

        if (GameLogic.Get().GetFixFaces())
        {
            gfpgan_visibility = 0.3f;
            codeformer_visibility = 0.3f;
        }

        string json =
$@"{{
            ""extrasreq"":
            {{
            ""image"": ""{imgBase64}"",
            ""upscaling_resize"": 2,
            ""upscaler1_name"": ""ESRGAN_4x"",
            ""upscaler2_name"": ""SwinIR_4x"",
            ""extras_upscaler_2_visibility:"": 0.5,
            ""gfpgan_visibility"": {gfpgan_visibility},
            ""codeformer_visibility"": {codeformer_visibility},
            ""alpha_mask_subject"":{bRemoveBackground.ToString().ToLower()},
            ""codeformer_weight"": 0
        }}

        }}";

        //File.WriteAllText("json_to_send.json", json); //for debugging
        using (var postRequest = UnityWebRequest.Post(finalURL, "POST"))
        {
            //Start the request with a method instead of the object itself
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(json);
            postRequest.uploadHandler = (UploadHandler)new UploadHandlerRaw(bodyRaw);
            postRequest.SetRequestHeader("Content-Type", "application/json");
            yield return postRequest.SendWebRequest();

            if (postRequest.result != UnityWebRequest.Result.Success)
            {
                Debug.Log(postRequest.error);
                Config.Get().SetGPUBusy(m_gpu, false);
                m_bIsGenerating = false;
                m_picScript.SetStatusMessage("Processing error");
            }
            else
            {
                //Debug.Log("Form upload complete! Downloaded " + postRequest.downloadedBytes); // + postRequest.downloadHandler.text

                JSONNode rootNode = JSON.Parse(postRequest.downloadHandler.text);
                yield return null; //wait a free to lesson the jerkiness

                Debug.Assert(rootNode.Tag == JSONNodeType.Object);
                var images = rootNode["images"];
                //Debug.Log("images is of type " + images.Tag);
                //Debug.Log("there are " + images.Count + " images");

                Debug.Assert(images.Count == 1); //You better convert the extra images to new pics!

                byte[] imgDataBytes = null;
                if (images != null)
                {
                    for (int i = 0; i < images.Count; i++)
                    {
                        imgDataBytes = Convert.FromBase64String(images[i]);
                        yield return null; //wait a free to lesson the jerkiness
                    }
                }
                else
                {
                    Debug.Log("image data is missing");
                }


                Texture2D texture = new Texture2D(0, 0, TextureFormat.RGBA32, false);

                if (texture.LoadImage(imgDataBytes, false))
                {
                    yield return null; //wait a free to lesson the jerkiness

                    //Debug.Log("Read texture, setting to new image");
                    this.gameObject.GetComponent<PicMain>().AddImageUndo();
                    yield return null; //wait a free to lesson the jerkiness

                    float biggestSize = Math.Max(texture.width, texture.height);
                    UnityEngine.Sprite newSprite = UnityEngine.Sprite.Create(texture, new Rect(0,0, texture.width, texture.height), new Vector2(0.5f, 0.5f), biggestSize / 5.12f);
                    renderer.sprite = newSprite;
                    m_picScript.OnImageReplaced();
                    
                    
                    //m_picScript.GetMaskScript().SetMaskVisible(false); //we don't want to see a rect
                    //or whatever

                }
                else
                {
                    Debug.Log("Error reading texture");
                }

                if (Config.Get().IsValidGPU(m_gpu))
                {
                    if (!Config.Get().IsGPUBusy(m_gpu))
                    {
                        Debug.LogError("Why is GPU not busy?! We were using it!");
                    }
                    else
                    {
                        Config.Get().SetGPUBusy(m_gpu, false);
                    }
                }
                m_bIsGenerating = false;
                m_picScript.SetStatusMessage("");
            }
        }
    }
}
