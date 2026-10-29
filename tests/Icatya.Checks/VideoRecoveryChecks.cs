using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Icatya.Models;
using Icatya.Services;
using Icatya.Services.Video;
using SIPSorcery.Net;

internal static class VideoRecoveryChecks
{
    private sealed record Packet(ushort Sequence,uint Timestamp,bool Marker,byte[] Payload,int Nal);
    private static void Require(bool value,string message){if(!value)throw new Exception(message);}
    private static List<byte[]> Split(byte[] data)
    {
        var starts=new List<(int Index,int Prefix)>();
        for(int i=0;i<data.Length-2;i++) {
            if(data[i]!=0||data[i+1]!=0)continue;
            if(data[i+2]==1){starts.Add((i,3));i+=2;}
            else if(i+3<data.Length&&data[i+2]==0&&data[i+3]==1){starts.Add((i,4));i+=3;}
        }
        var nals=new List<byte[]>();
        for(int i=0;i<starts.Count;i++){int start=starts[i].Index+starts[i].Prefix,end=i+1<starts.Count?starts[i+1].Index:data.Length;if(end>start)nals.Add(data.AsSpan(start,end-start).ToArray());}
        return nals;
    }
    private static List<Packet> Packetize(byte[] encoded,ref ushort sequence,uint timestamp)
    {
        var result=new List<Packet>();int nalIndex=0;
        foreach(var nal in Split(encoded)) {
            if(nal.Length<=220)result.Add(new(sequence++,timestamp,false,nal,nalIndex));
            else for(int offset=1;offset<nal.Length;offset+=220) {
                int count=Math.Min(220,nal.Length-offset);var payload=new byte[count+2];
                payload[0]=(byte)((nal[0]&224)|28);payload[1]=(byte)((nal[0]&31)|(offset==1?128:0)|(offset+count==nal.Length?64:0));
                nal.AsSpan(offset,count).CopyTo(payload.AsSpan(2));result.Add(new(sequence++,timestamp,false,payload,nalIndex));
            }
            nalIndex++;
        }
        Require(result.Count>0,"Encoder não gerou Annex B.");result[^1]=result[^1] with{Marker=true};return result;
    }
    private static void Feed(H264VideoReceiver receiver,IEnumerable<Packet> packets,uint ssrc=123)
    {
        foreach(var p in packets)receiver.Push(ssrc,p.Sequence,p.Timestamp,p.Marker,p.Payload);
    }
    private static void Draw(VideoFrame frame,int phase)
    {
        Array.Fill(frame.Pixels,(byte)245,0,frame.Stride*frame.Height);
        for(int y=16;y<frame.Height-16;y++)for(int x=16;x<frame.Width/2;x++) {
            bool ink=((y+phase*7)%19<3)&&((x/7+phase)%5!=0) || (x+phase*13)%101<2;
            int pixel=y*frame.Stride+x*3;byte color=ink?(byte)15:(byte)245;
            frame.Pixels[pixel]=color;frame.Pixels[pixel+1]=color;frame.Pixels[pixel+2]=color;
        }
        for(int y=32;y<96;y++)for(int x=frame.Width-150;x<frame.Width-32;x++) {
            int pixel=y*frame.Stride+x*3;frame.Pixels[pixel]=(byte)(phase%2==0?180:20);frame.Pixels[pixel+1]=80;frame.Pixels[pixel+2]=(byte)(phase%2==0?20:180);
        }
    }
    private static double Error(VideoFrame actual,VideoFrame expected)
    {
        long error=0;int count=0;
        for(int y=20;y<expected.Height-20;y+=3)for(int x=20;x<expected.Width-20;x+=3)for(int c=0;c<3;c++){int i=y*expected.Stride+x*3+c;error+=Math.Abs(actual.Pixels[i]-expected.Pixels[i]);count++;}
        return (double)error/count;
    }
    public static async Task<int> RunAsync()
    {
        try {
            foreach(var profile in new[]{CaptureProfile.Available[0],CaptureProfile.Available[2]}) {
                using var encoder=new H264VideoCodec();encoder.Configure(profile,softwareOnly:true);
                using var decoder=new H264VideoCodec();using var image=VideoFrame.Rent(profile.Width,profile.Height);
                VideoFrame? latest=null;int presented=0,requests=0;
                var receiver=new H264VideoReceiver(decoder,frame=>{latest?.Dispose();latest=frame.Retain();presented++;},()=>requests++);
                try {
                    ushort sequence=65520;uint timestamp=uint.MaxValue-6000;
                    Draw(image,0);var first=encoder.Encode(image)!;var packets=Packetize(first,ref sequence,timestamp);
                    Feed(receiver,packets);Require(presented==1&&latest!=null,"Não decodificou primeiro IDR completo.");
                    Require(Error(latest!,image)<12,"Primeira página não corresponde à origem.");
                    Draw(image,1);timestamp=unchecked(timestamp+3000);var second=Packetize(encoder.Encode(image)!,ref sequence,timestamp);
                    Feed(receiver,second.AsEnumerable().Reverse());
                    Require(presented==2,"Não remontou fragmentos fora de ordem com marcador antecipado.");
                    Require(Error(latest!,image)<15,"Pixels corrompidos após reordenação.");
                    Console.WriteLine($"PASS {profile.Height}p: páginas/texto, fragmentação, reordenação e wrap de sequência");
                    Draw(image,2);timestamp=unchecked(timestamp+3000);var damaged=Packetize(encoder.Encode(image)!,ref sequence,timestamp);
                    int missing=damaged.FindIndex(p=>p.Payload.Length>2&&(p.Payload[0]&31)==28&&(p.Payload[1]&192)==0);
                    Require(missing>=0,"Caso de teste não gerou fragmentos intermediários.");
                    int before=presented;Feed(receiver,damaged.Where((_,i)=>i!=missing));await Task.Delay(85);receiver.Expire();
                    Require(presented==before&&requests>0&&receiver.RejectedFrames>0,"Quadro incompleto foi exibido ou não pediu recuperação.");
                    for(int i=3;i<6;i++) {
                        Draw(image,i);timestamp=unchecked(timestamp+3000);Feed(receiver,Packetize(encoder.Encode(image)!,ref sequence,timestamp));
                    }
                    // Force the equivalent of the sender honoring the reliable keyframe request.
                    encoder.ForceKeyFrame();Draw(image,6);timestamp=unchecked(timestamp+3000);
                    Feed(receiver,Packetize(encoder.Encode(image)!,ref sequence,timestamp));
                    Require(presented>before&&Error(latest!,image)<15,"Não recuperou uma imagem íntegra depois da perda.");
                    for(int i=7;i<24;i++) {
                        Draw(image,i);timestamp=unchecked(timestamp+3000);int previous=presented;
                        Feed(receiver,Packetize(encoder.Encode(image)!,ref sequence,timestamp));
                        Require(presented==previous+1&&Error(latest!,image)<18,"Mudança rápida de página produziu corrupção.");
                    }
                    Require(receiver.DecodeErrors==0,"Houve erro de decodificação em dados completos.");
                    Console.WriteLine($"PASS {profile.Height}p: perda de fragmento não aparece na live, recuperação por IDR e 17 trocas rápidas verificadas por pixels");
                    foreach(bool loseFirst in new[]{true,false}) {
                        encoder.ForceKeyFrame();Draw(image,30);timestamp=unchecked(timestamp+3000);
                        var broken=Packetize(encoder.Encode(image)!,ref sequence,timestamp);int previous=presented;
                        Feed(receiver,loseFirst?broken.Skip(1):broken.Take(broken.Count-1));
                        await Task.Delay(85);receiver.Expire();
                        Require(presented==previous,"Quadro com início/final perdido foi exibido.");
                        encoder.ForceKeyFrame();timestamp=unchecked(timestamp+3000);
                        var restored=Packetize(encoder.Encode(image)!,ref sequence,timestamp);
                        Feed(receiver,restored);Feed(receiver,restored);
                        Require(presented==previous+1,"Falhou recuperação ou duplicou imagem após retransmissão duplicada.");
                    }
                    Console.WriteLine($"PASS {profile.Height}p: perda de início, perda de marcador final e duplicatas sem corromper/duplicar exibição");
                }finally{latest?.Dispose();}
            }
            var buffer=new H264ReceiveBuffer();int output=0;buffer.FrameReady+=_=>output++;
            for(ushort i=0;i<30;i++)buffer.Push(999,i,(uint)i*3000,false,new byte[]{0x7c,0x85,0x80});
            Require(buffer.BufferedFrames<=3&&buffer.DroppedFrames>0&&output==0,"Fila incompleta cresceu sem limite.");
            Console.WriteLine("PASS buffer de rede limitado diante de quadros sem final");
            buffer.Reset();
            buffer.Push(999,10,9000,false,new byte[]{0x61,0x80});
            buffer.Push(999,12,9000,true,new byte[]{0x61,0x80});
            buffer.Push(999,13,9000,false,new byte[]{0x61,0x80});
            await Task.Delay(85);buffer.Expire();
            Require(output==0,"Pacote após marcador ocultou um fragmento perdido.");
            Console.WriteLine("PASS marcador inconsistente não permite entregar um quadro com lacunas");
            await ControlFeedbackAsync();
            return 0;
        }catch(Exception ex){Console.Error.WriteLine(ex);return 1;}
    }
    private static async Task ControlFeedbackAsync()
    {
        using var sender=new PeerSession(true,false);using var receiver=new PeerSession(false,false);
        var requested=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        sender.KeyFrameRequested+=()=>requested.TrySetResult();
        sender.ApplyAnswer(await receiver.AcceptOfferAsync(await sender.CreateOfferAsync()));
        await Task.WhenAll(sender.Ready,receiver.Ready).WaitAsync(TimeSpan.FromSeconds(15));
        sender.SetSending(true);await requested.Task.WaitAsync(TimeSpan.FromSeconds(5));sender.SetSending(false);
        Console.WriteLine("PASS pedido de imagem completa pelo canal WebRTC protegido chega ao transmissor");
    }
}
