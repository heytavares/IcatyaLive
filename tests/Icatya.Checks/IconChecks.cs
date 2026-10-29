using System;
using System.Drawing;
using System.IO;

internal static class IconChecks
{
    public static int Run()
    {
        try
        {
            using var file=File.OpenRead(Path.Combine(Directory.GetCurrentDirectory(),"src","Icatya","Assets","Icatya.ico"));
            using var reader=new BinaryReader(file);
            if(reader.ReadUInt16()!=0||reader.ReadUInt16()!=1||reader.ReadUInt16()!=7)throw new Exception("Invalid multi-size icon");
            int[] sizes=[16,24,32,48,64,128,256];
            for(int index=0;index<sizes.Length;index++)
            {
                file.Position=6+16*index;
                int width=reader.ReadByte();if(width==0)width=256;
                int height=reader.ReadByte();if(height==0)height=256;
                reader.ReadBytes(6);int length=reader.ReadInt32(),offset=reader.ReadInt32();
                if(width!=sizes[index]||height!=width)throw new Exception("Missing Windows icon size");
                file.Position=offset;using var bytes=new MemoryStream(reader.ReadBytes(length));using var bitmap=new Bitmap(bytes);
                int left=width,top=height,right=-1,bottom=-1;
                for(int y=0;y<height;y++)for(int x=0;x<width;x++)if(bitmap.GetPixel(x,y).A>16)
                {left=Math.Min(left,x);right=Math.Max(right,x);top=Math.Min(top,y);bottom=Math.Max(bottom,y);}
                double fill=(double)Math.Max(right-left+1,bottom-top+1)/width;
                if(fill<.82||fill>=1||bitmap.GetPixel(0,0).A!=0)throw new Exception("Icon has excessive padding, clipped edge or opaque background");
                Console.WriteLine($"PASS icon {width}px: transparent background; visible artwork occupies {fill:P0} of canvas");
            }
            return 0;
        }
        catch(Exception ex){Console.Error.WriteLine(ex);return 1;}
    }
}
