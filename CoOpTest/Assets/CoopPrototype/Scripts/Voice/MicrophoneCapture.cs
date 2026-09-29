using System;
using UnityEngine;

namespace CoopPrototype.Voice
{
    /// <summary>Unity microphone ring-buffer reader. All Unity API access stays on the main thread.</summary>
    public sealed class MicrophoneCapture : IDisposable
    {
        AudioClip clip;
        string device;
        int readPosition, frameSamples;
        float[] input;
        float started;
        public bool Running => clip != null;
        public string Error { get; private set; } = "";
        public bool Start(string selection)
        {
            if(Running) return true;
            Error="";
            if(Microphone.devices.Length==0) { Error="No microphone found. Check Windows microphone permissions."; return false; }
            if(!string.IsNullOrEmpty(selection) && Array.IndexOf(Microphone.devices,selection)<0)
            { Error="Selected microphone is unavailable. Refresh devices or choose System default."; return false; }
            device=string.IsNullOrEmpty(selection) ? null : selection;
            try
            {
                clip=Microphone.Start(device,true,1,VoiceCodec.SampleRate);
                if(clip==null) { Error="Microphone could not start."; return false; }
                frameSamples=Mathf.Max(1,clip.frequency/50);
                input=new float[frameSamples*clip.channels]; readPosition=0; started=Time.unscaledTime;
                return true;
            }
            catch(Exception e) { Error="Microphone unavailable: "+e.Message; Dispose(); return false; }
        }
        public bool TryRead(float[] output, float gain, out float rms)
        {
            rms=0;
            if(clip==null) return false;
            int cursor=Microphone.GetPosition(device);
            if(cursor<=0 && Time.unscaledTime-started>2) { Error="No microphone samples. Check permissions / device access."; Dispose(); return false; }
            int available=(cursor-readPosition+clip.samples)%clip.samples;
            if(available>frameSamples*8) { readPosition=(cursor-frameSamples+clip.samples)%clip.samples; available=frameSamples; }
            if(available<frameSamples) return false;
            if(!clip.GetData(input,readPosition)) { Error="Cannot read microphone samples."; Dispose(); return false; }
            readPosition=(readPosition+frameSamples)%clip.samples;
            // Device sample rate/channels can differ from the requested format. Downmix and resample.
            for(int i=0;i<output.Length;i++)
            {
                float position=i*(frameSamples-1f)/(output.Length-1); int a=(int)position, b=Mathf.Min(a+1,frameSamples-1);
                float sample=0;
                for(int c=0;c<clip.channels;c++) sample+=Mathf.Lerp(input[a*clip.channels+c],input[b*clip.channels+c],position-a);
                output[i]=Mathf.Clamp(sample/clip.channels*gain,-1,1); rms+=output[i]*output[i];
            }
            rms=Mathf.Sqrt(rms/output.Length); return true;
        }
        public void Dispose()
        {
            if(clip==null) return;
            Microphone.End(device); UnityEngine.Object.Destroy(clip); clip=null; input=null;
        }
    }
}
