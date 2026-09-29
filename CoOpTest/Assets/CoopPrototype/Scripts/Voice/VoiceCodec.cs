using System;

namespace CoopPrototype.Voice
{
    /// <summary>Independent 20ms mono IMA ADPCM blocks. Lost packets cannot corrupt subsequent blocks.
    /// 16kHz / 4-bit speech: 164 bytes per frame (~8.2KB/s while speaking, before transport headers).</summary>
    public static class VoiceCodec
    {
        public const int SampleRate = 16000, Samples = 320, Bytes = 164;
        static readonly int[] Index = { -1,-1,-1,-1,2,4,6,8 };
        static readonly int[] Step = {7,8,9,10,11,12,13,14,16,17,19,21,23,25,28,31,34,37,41,45,50,55,60,66,73,80,88,97,107,118,130,143,157,173,190,209,230,253,279,307,337,371,408,449,494,544,598,658,724,796,876,963,1060,1166,1282,1411,1552,1707,1878,2066,2272,2499,2749,3024,3327,3660,4026,4428,4871,5358,5894,6484,7132,7845,8630,9493,10442,11487,12635,13899,15289,16818,18500,20350,22385,24623,27086,29794,32767};
        static int Clamp(int x, int a, int b) => Math.Max(a, Math.Min(b, x));
        public static void Encode(float[] pcm, byte[] block)
        {
            Array.Clear(block, 0, Bytes);
            int predictor = Clamp((int)(pcm[0] * 32767), -32768, 32767), index = 40;
            block[0]=(byte)predictor; block[1]=(byte)(predictor>>8); block[2]=(byte)index;
            for (int i=1; i<Samples; i++)
            {
                int delta=Clamp((int)(pcm[i]*32767),-32768,32767)-predictor, step=Step[index];
                int code=delta<0 ? 8 : 0; delta=Math.Abs(delta);
                if(delta>=step) { code|=4; delta-=step; }
                if(delta>=step/2) { code|=2; delta-=step/2; }
                if(delta>=step/4) code|=1;
                Advance(code,ref predictor,ref index);
                block[4+(i-1)/2] |= (byte)(code << (((i-1)&1)*4));
            }
        }
        public static bool Decode(byte[] block, float[] pcm)
        {
            if(block.Length!=Bytes || block[2]>88 || block[3]!=0 || pcm.Length<Samples) return false;
            int predictor=(short)(block[0]|block[1]<<8), index=block[2]; pcm[0]=predictor/32768f;
            for(int i=1;i<Samples;i++)
            {
                int code=(block[4+(i-1)/2] >> (((i-1)&1)*4)) & 15;
                Advance(code,ref predictor,ref index); pcm[i]=predictor/32768f;
            }
            return true;
        }
        static void Advance(int code, ref int predictor, ref int index)
        {
            int step=Step[index], delta=step>>3;
            if((code&4)!=0) delta+=step;
            if((code&2)!=0) delta+=step>>1;
            if((code&1)!=0) delta+=step>>2;
            predictor=Clamp(predictor+((code&8)!=0 ? -delta : delta),-32768,32767);
            index=Clamp(index+Index[code&7],0,88);
        }
    }
}
