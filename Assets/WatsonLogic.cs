﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using IBM.Watson.DeveloperCloud.Services.Assistant.v1;
using IBM.Watson.DeveloperCloud.Services.SpeechToText.v1;
using IBM.Watson.DeveloperCloud.Services.TextToSpeech.v1;
using IBM.Watson.DeveloperCloud.Utilities;
using IBM.Watson.DeveloperCloud.Logging;
using System;
using IBM.Watson.DeveloperCloud.Connection;
using FullSerializer;
using UnityEngine.UI;
using IBM.Watson.DeveloperCloud.DataTypes;

public class WatsonLogic : MonoBehaviour
{

    [SerializeField]
    private string assistantUsername;
    [SerializeField]
    private string assistantPassword;
    [SerializeField]
    private string assistantURL;
    [SerializeField]
    private string assistantWorkspace;
    [SerializeField]
    private string SpeechToTextUsername;
    [SerializeField]
    private string SpeechToTextPassword;
    [SerializeField]
    private string SpeechToTextURL;
    [SerializeField]
    private string TextToSpeechUsername;
    [SerializeField]
    private string TextToSpeechPassword;
    [SerializeField]
    private string TextToSpeechURL;

    private int _recordingRoutine = 0;
    private string _microphoneID = null;
    private AudioClip _recording = null;
    private int _recordingBufferSize = 2;
    private int _recordingHZ = 22050;
    private byte[] _acousticResourceData;
    private string _acousticResourceMimeType;

    private string outputText = "Hello";
    private Assistant _assistant;
    private SpeechToText _speechToText;
    private TextToSpeech _textToSpeech;
    private bool firstMessage;
    private bool _stopListeningFlag = false;

    private fsSerializer _serializer = new fsSerializer();

    public Dictionary<string, object> inputObj = new Dictionary<string, object>();

    Animator animator;

    void Awake()
    {
        InitializeServices();
    }

    // Use this for initialization
    private void Start()
    {
        animator = gameObject.GetComponent<Animator>();
    }

    //***********************
    //Watson Assistant Below
    //***********************

    private void InitializeServices()
    {
        Credentials credentials = new Credentials(assistantUsername, assistantPassword, assistantURL);
        _assistant = new Assistant(credentials);
        //be sure to give it a Version Date
        _assistant.VersionDate = "2018-02-16";

        Credentials credentials2 = new Credentials(TextToSpeechUsername, TextToSpeechPassword, TextToSpeechURL);
        _textToSpeech = new TextToSpeech(credentials2);
        //give Watson a voice type
        _textToSpeech.Voice = VoiceType.en_US_Allison;

        Credentials credentials3 = new Credentials(SpeechToTextUsername, SpeechToTextPassword, SpeechToTextURL);
        _speechToText = new SpeechToText(credentials3);

        // Send first message, create inputObj w/ no context
        Message0();

        Active = true;

        StartRecording();   // Setup recording

    }

    //  Send a message perserving conversation context
    private Dictionary<string, object> _context; // context to persist

    //  Initiate a conversation
    private void Message0()
    {
        firstMessage = true;
        inputObj.Add("text", outputText);
        MessageRequest messageRequest = new MessageRequest()
        {
            Input = inputObj
        };

        if (!_assistant.Message(OnMessage, OnFail, assistantWorkspace, messageRequest))
            Log.Debug("ExampleAssistant.Message()", "Failed to message!");
    }


    private void OnMessage(object response, Dictionary<string, object> customData)
    {
        if (!firstMessage)
        {    
            Log.Debug("ExampleAssistant.OnMessage()", "Response: {0}", customData["json"].ToString());

            //  Convert resp to fsdata
            fsData fsdata = null;
            fsResult r = _serializer.TrySerialize(response.GetType(), response, out fsdata);
            if (!r.Succeeded)
                throw new WatsonException(r.FormattedMessages);

            //  Convert fsdata to MessageResponse
            MessageResponse messageResponse = new MessageResponse();
            object obj = messageResponse;
            r = _serializer.TryDeserialize(fsdata, obj.GetType(), ref obj);
            if (!r.Succeeded)
                throw new WatsonException(r.FormattedMessages);

            //  Set context for next round of messaging
            object _tempContext = null;
            (response as Dictionary<string, object>).TryGetValue("context", out _tempContext);
            if (_tempContext != null)
                _context = _tempContext as Dictionary<string, object>;
            else
                Log.Debug("ExampleAssistant.OnMessage()", "Failed to get context");

            //  Get intent
            object tempIntentsObj = null;
            (response as Dictionary<string, object>).TryGetValue("intents", out tempIntentsObj);
            object tempIntentObj = (tempIntentsObj as List<object>)[0];

            object tempIntent = null;
            (tempIntentObj as Dictionary<string, object>).TryGetValue("intent", out tempIntent);
            string intent = tempIntent.ToString();


            MakeAMove(intent);


            //get Watson Output
            object tempOutputObj = null;
            (response as Dictionary<string, object>).TryGetValue("output", out tempOutputObj);
            object tempText = null;
            (tempOutputObj as Dictionary<string, object>).TryGetValue("text", out tempText);
            string outputText2 = (tempText as List<object>)[0].ToString();

            
                CallTextToSpeech(outputText2);
            }

        firstMessage = false;

    }

    // Generic Failure for Watson Assistant Service
    private void OnFail(RESTConnector.Error error, Dictionary<string, object> customData)
    {
        Log.Debug("ExampleAssistant.OnFail()", "Response: {0}", customData["json"].ToString());
        Log.Error("TestAssistant.OnFail()", "Error received: {0}", error.ToString());
    }

    private void MakeAMove(string intent)
    {
        if (intent.ToLower() == "forward")
        {
            animator.SetBool("isIdle", false);
            animator.SetBool("isWalkingBackward", false);
            animator.SetBool("isWalkingForward", true);
        }
        else if (intent.ToLower() == "backward")
        {
            animator.SetBool("isIdle", false);
            animator.SetBool("isWalkingForward", false);
            animator.SetBool("isWalkingBackward", true);
        }
        else if (intent.ToLower() == "idle")
        {
            animator.SetBool("isIdle", true);
            animator.SetBool("isWalkingBackward", false);
            animator.SetBool("isWalkingForward", false);
        }
        else
        {
            animator.SetBool("isIdle", true);
            animator.SetBool("isWalkingBackward", false);
            animator.SetBool("isWalkingForward", false);
        }

    }

    private void BuildSpokenRequest(string spokenText)
    {
        MessageRequest messageRequest = new MessageRequest()
        {
            Input = new Dictionary<string, object>()
            {
                { "text", spokenText }
            },
            Context = _context
        };

        if (_assistant.Message(OnMessage, OnFail, assistantWorkspace, messageRequest))
            Log.Debug("Assistant, Spoken Request", "Failed to message!");
    }

    private void CallTextToSpeech(string outputText)
    {
        Debug.Log("Sent to Watson Text To Speech: " + outputText);
        if (!_textToSpeech.ToSpeech(OnSynthesize, OnFail, outputText, false))
            Log.Debug("ExampleTextToSpeech.ToSpeech()", "Failed to synthesize!");
    }

    private void OnSynthesize(AudioClip clip, Dictionary<string, object> customData)
    {
        Debug.Log("Received audio file from Watson Text To Speech");

        if (Application.isPlaying && clip != null)
        {
            GameObject audioObject = new GameObject("AudioObject");
            AudioSource source = audioObject.AddComponent<AudioSource>();
            source.spatialBlend = 0.0f;
            source.volume = 1.0f;
            source.loop = false;
            source.clip = clip;
            source.Play();

            Invoke("RecordAgain", source.clip.length);
            Destroy(audioObject, clip.length);
        }
    }

    private void RecordAgain()
    {
        Debug.Log("Played Audio received from Watson Text To Speech");
        if (!_stopListeningFlag)
        {
            OnListen();
        }
    }

    private void OnListen()
    {
        Log.Debug("ExampleStreaming", "Start();");

        Active = true;

        StartRecording();
    }

    public bool Active
    {
        get { return _speechToText.IsListening; }
        set
        {
            if (value && !_speechToText.IsListening)
            {
                _speechToText.DetectSilence = true;
                _speechToText.EnableWordConfidence = false;
                _speechToText.EnableTimestamps = false;
                _speechToText.SilenceThreshold = 0.03f;
                _speechToText.MaxAlternatives = 1;
                _speechToText.EnableInterimResults = true;
                _speechToText.OnError = OnError;
                _speechToText.StartListening(OnRecognize);
            }
            else if (!value && _speechToText.IsListening)
            {
                _speechToText.StopListening();
            }
        }
    }

    private void OnRecognize(SpeechRecognitionEvent result, Dictionary<string, object> customData)
    {
        if (result != null && result.results.Length > 0)
        {
            foreach (var res in result.results)
            {
                foreach (var alt in res.alternatives)
                {
                    if (res.final && alt.confidence > 0)
                    {
                        StopRecording();
                        string text = alt.transcript;
                        Debug.Log("Watson hears : " + text + " Confidence: " + alt.confidence);
                        BuildSpokenRequest(text);
                    }
                }
            }
        }

    }


    private void StartRecording()
    {
        if (_recordingRoutine == 0)
        {
            Debug.Log("Started Recording");
            UnityObjectUtil.StartDestroyQueue();
            _recordingRoutine = Runnable.Run(RecordingHandler());
        }
    }

    private void StopRecording()
    {
        if (_recordingRoutine != 0)
        {
            Debug.Log("Stopped Recording");
            Microphone.End(_microphoneID);
            Runnable.Stop(_recordingRoutine);
            _recordingRoutine = 0;
        }
    }

    private void OnError(string error)
    {
        Active = false;

        Log.Debug("ExampleStreaming", "Error! {0}", error);
    }

    private IEnumerator RecordingHandler()
    {
        _recording = Microphone.Start(_microphoneID, true, _recordingBufferSize, _recordingHZ);
        yield return null;      // let m_RecordingRoutine get set..

        if (_recording == null)
        {
            StopRecording();
            yield break;
        }

        bool bFirstBlock = true;
        int midPoint = _recording.samples / 2;
        float[] samples = null;

        while (_recordingRoutine != 0 && _recording != null)
        {
            int writePos = Microphone.GetPosition(_microphoneID);
            if (writePos > _recording.samples || !Microphone.IsRecording(_microphoneID))
            {
                Log.Error("MicrophoneWidget", "Microphone disconnected.");

                StopRecording();
                yield break;
            }

            if ((bFirstBlock && writePos >= midPoint)
                || (!bFirstBlock && writePos < midPoint))
            {
                // front block is recorded, make a RecordClip and pass it onto our callback.
                samples = new float[midPoint];
                _recording.GetData(samples, bFirstBlock ? 0 : midPoint);

                AudioData record = new AudioData();
                record.MaxLevel = Mathf.Max(samples);
                record.Clip = AudioClip.Create("Recording", midPoint, _recording.channels, _recordingHZ, false);
                record.Clip.SetData(samples, 0);

                _speechToText.OnListen(record);

                bFirstBlock = !bFirstBlock;
            }
            else
            {
                // calculate the number of samples remaining until we ready for a block of audio,
                // and wait that amount of time it will take to record.
                int remaining = bFirstBlock ? (midPoint - writePos) : (_recording.samples - writePos);
                float timeRemaining = (float)remaining / (float)_recordingHZ;

                yield return new WaitForSeconds(timeRemaining);
            }

        }

        yield break;
    }
}