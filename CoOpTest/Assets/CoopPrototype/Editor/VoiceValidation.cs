using System;
using CoopPrototype.Voice;
using UnityEditor;
using UnityEngine;

namespace CoopPrototype.Editor
{
    public static class VoiceValidation
    {
        [MenuItem("Coop Prototype/Voice/Validate Speech Codec")]
        public static void Run()
        {
            var input=new float[VoiceCodec.Samples]; var output=new float[input.Length]; var packet=new byte[VoiceCodec.Bytes];
            for(int i=0;i<input.Length;i++) input[i]=.35f*Mathf.Sin(i*2*Mathf.PI*400/VoiceCodec.SampleRate)+.08f*Mathf.Sin(i*2*Mathf.PI*1100/VoiceCodec.SampleRate);
            VoiceCodec.Encode(input,packet);
            if(!VoiceCodec.Decode(packet,output)) throw new Exception("Valid speech frame rejected");
            double energy=0,error=0;
            for(int i=32;i<input.Length;i++) { energy+=input[i]*input[i]; error+=(input[i]-output[i])*(input[i]-output[i]); }
            double snr=10*Math.Log10(energy/Math.Max(error,1e-10));
            if(snr<18) throw new Exception("Speech distortion too high: "+snr);
            packet[2]=255;
            if(VoiceCodec.Decode(packet,output)) throw new Exception("Invalid ADPCM index accepted");
            Array.Clear(input,0,input.Length); VoiceCodec.Encode(input,packet); VoiceCodec.Decode(packet,output);
            foreach(float value in output) if(Math.Abs(value)>.02f || float.IsNaN(value)) throw new Exception("Silence / independent-frame decode failed");
            Debug.Log($"[Voice test] PASS: speech SNR {snr:0.0}dB, invalid-frame rejection, independent silence frame; {VoiceCodec.Bytes} bytes / 20ms.");
        }
    }
}
