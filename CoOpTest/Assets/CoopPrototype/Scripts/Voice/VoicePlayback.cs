using System;
using UnityEngine;

namespace CoopPrototype.Voice
{
    /// <summary>Bounded jitter queue, read on Unity's audio thread. Underruns are silence, never old speech.</summary>
    public sealed class VoicePlayback : MonoBehaviour
    {
        readonly object gate=new();
        readonly float[] queue=new float[VoiceCodec.SampleRate/2];
        readonly float[] decoded=new float[VoiceCodec.Samples];
        int read, count;
        bool primed;
        AudioSource source;
        AudioClip clip;
        public float LastPacket { get; private set; } = -100;
        public int PlayedSamples { get; private set; }
        public float SpatialBlend => source.spatialBlend;
        public float PlaybackVolume => source.volume;
        void Awake()
        {
            source=gameObject.AddComponent<AudioSource>(); source.playOnAwake=false; source.loop=true;
            source.dopplerLevel=0; source.rolloffMode=AudioRolloffMode.Linear; source.priority=0;
            clip=AudioClip.Create("Live voice",VoiceCodec.SampleRate,1,VoiceCodec.SampleRate,true,ReadAudio);
            source.clip=clip; source.Play();
        }
        public void Configure(Transform speaker, bool positional, float range, float near, float volume, bool silent)
        {
            if(speaker!=null) transform.position=speaker.position+Vector3.up*.65f;
            source.spatialBlend=positional ? 1 : 0;
            source.minDistance=Mathf.Min(near,range*.5f); source.maxDistance=range;
            source.volume=silent ? 0 : volume;
            if(silent) Flush();
        }
        public void Enqueue(byte[] block)
        {
            if(!VoiceCodec.Decode(block,decoded)) return;
            lock(gate)
            {
                if(Time.unscaledTime-LastPacket>.25f || count+decoded.Length>queue.Length) { read=0; count=0; primed=false; }
                for(int i=0;i<decoded.Length;i++) queue[(read+count+i)%queue.Length]=decoded[i];
                count+=decoded.Length;
            }
            LastPacket=Time.unscaledTime;
        }
        void ReadAudio(float[] data)
        {
            lock(gate)
            {
                // ~60ms pre-roll absorbs ordinary arrival jitter, without waiting on the audio thread.
                if(!primed && count>=Math.Max(VoiceCodec.Samples*3,data.Length)) primed=true;
                for(int i=0;i<data.Length;i++)
                {
                    if(primed && count>0) { data[i]=queue[read]; read=(read+1)%queue.Length; count--; PlayedSamples++; }
                    else data[i]=0;
                }
                if(count==0) primed=false;
            }
        }
        public void Flush() { lock(gate) { read=0; count=0; primed=false; } }
        void Update()
        {
            // Drain the final short tail after a talk burst instead of retaining it until the next speaker packet.
            if(Time.unscaledTime-LastPacket>.12f) lock(gate) { if(count>0) primed=true; }
        }
        void OnDestroy() { if(source!=null) source.Stop(); if(clip!=null) Destroy(clip); }
    }
}
