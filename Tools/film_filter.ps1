# 胶片 / 老式 TV 滤镜（批处理，保留透明通道）
# 对输入文件夹内所有 png/jpg 应用同一套效果，输出到指定文件夹。
# 关键：按预乘 alpha 处理颜色，输出保留原 alpha，透明背景不会被涂黑。
#
# 当前固化参数：
#   1) 色调匹配参考图            : -Reference（可选；跳过时不做色调匹配）
#   2) 分辨率                    : 源宽度的 ScaleFactor（默认 0.5）
#   3) 高斯模糊                  : radius 3, sigma 1.6
#   4) 受光面光晕(bloom)         : threshold 0.50, intensity 0.45, radius 12, sigma 7
#   5) 扫描线                    : 隔行压暗 0.16
#   6) RGB 色散                  : 2.0
#   7) 行抖动                    : 6% 的行横向 ±2px
#   8) 噪点                      : 8（仅作用于不透明区域）
#   9) 动态模糊                  : 默认关闭（MotionLength=0）
#   无暗角、无边缘弯曲
#
# 用法：
#   powershell -ExecutionPolicy Bypass -File Tools\film_filter.ps1 -Source "Assets\Via\Story\pic\cake" -Destination "Assets\Via\Story\pic\cake_film" -Reference "NONE"

param(
  [string]$Source = "Assets\Via\Story\pic",
  [string]$Destination = "Assets\Via\Story\pic_film",
  [string]$Reference = "",
  [int]$BlurRadius = 3,
  [double]$BlurSigma = 1.6,
  [double]$BloomThreshold = 0.50,
  [double]$BloomIntensity = 0.45,
  [int]$BloomRadius = 12,
  [double]$BloomSigma = 7.0,
  [double]$Scanline = 0.16,
  [double]$Aberration = 2.0,
  [double]$JitterRatio = 0.06,
  [int]$JitterPixels = 2,
  [double]$Grain = 8.0,
  [double]$ScaleFactor = 0.5,
  [double]$MotionLength = 0.0,
  [double]$MotionAngle = 0.0,
  [int]$MotionSamples = 9
)

$ErrorActionPreference = "Stop"

$code = @'
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
public static class ViaFilm {
  static double[] CdfR, CdfG, CdfB;

  public static void LoadReference(string path){
    using(var bmp=new Bitmap(path)){
      int w=bmp.Width,h=bmp.Height;
      var data=bmp.LockBits(new Rectangle(0,0,w,h), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
      int stride=data.Stride; byte[] buf=new byte[stride*h]; Marshal.Copy(data.Scan0,buf,0,buf.Length);
      long[] hr=new long[256],hg=new long[256],hb=new long[256];
      for(int y=0;y<h;y++){int row=y*stride;for(int x=0;x<w;x++){int i=row+x*4;hb[buf[i]]++;hg[buf[i+1]]++;hr[buf[i+2]]++;}}
      bmp.UnlockBits(data);
      CdfR=Cdf(hr,w,h); CdfG=Cdf(hg,w,h); CdfB=Cdf(hb,w,h);
    }
  }

  public static void Apply(string srcPath, string dstPath, int blurRadius, double blurSigma,
    double bloomThreshold, double bloomIntensity, int bloomRadius, double bloomSigma,
    double scanline, double aberration, double jitterRatio, int jitterPixels, double grain, double scaleFactor,
    double motionLength, double motionAngle, int motionSamples){

    using(var src0=new Bitmap(srcPath))
    using(var bmp = src0.PixelFormat==PixelFormat.Format32bppArgb ? src0 : To32(src0)){

      int sw=bmp.Width, sh=bmp.Height;
      int ow=(int)Math.Round(sw*scaleFactor); int oh=(int)Math.Round(sh*scaleFactor);
      double scaleX=(double)sw/ow, scaleY=(double)sh/oh;
      var data=bmp.LockBits(new Rectangle(0,0,sw,sh), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
      int sstride=data.Stride; byte[] sbuf=new byte[sstride*sh]; Marshal.Copy(data.Scan0,sbuf,0,sbuf.Length);

      byte[] lr=null,lg=null,lb=null;
      if(CdfR!=null){
        long[] hr=new long[256],hg=new long[256],hb=new long[256]; long cnt=0;
        for(int y=0;y<sh;y++){int row=y*sstride;for(int x=0;x<sw;x++){int i=row+x*4;if(sbuf[i+3]==0)continue;hr[sbuf[i+2]]++;hg[sbuf[i+1]]++;hb[sbuf[i]]++;cnt++;}}
        if(cnt==0) cnt=1;
        lr=Lut(hr,cnt,CdfR); lg=Lut(hg,cnt,CdfG); lb=Lut(hb,cnt,CdfB);
      }

      int n=sw*sh;
      float[] pr=new float[n], pg=new float[n], pb=new float[n], pa=new float[n];
      for(int y=0;y<sh;y++){int row=y*sstride;for(int x=0;x<sw;x++){int i=row+x*4;int k=y*sw+x;
        float a=sbuf[i+3];
        float bb=lr!=null?lb[sbuf[i]]:sbuf[i];
        float gg=lg!=null?lg[sbuf[i+1]]:sbuf[i+1];
        float rr=lr!=null?lr[sbuf[i+2]]:sbuf[i+2];
        float af=a/255f;
        pa[k]=a; pr[k]=rr*af; pg[k]=gg*af; pb[k]=bb*af;
      }}
      bmp.UnlockBits(data);

      // blur in premultiplied space (and alpha), so transparent edges do not bleed black
      float[] kr=Gauss(pr,sw,sh,blurRadius,blurSigma), kg=Gauss(pg,sw,sh,blurRadius,blurSigma);
      float[] kb=Gauss(pb,sw,sh,blurRadius,blurSigma), ka=Gauss(pa,sw,sh,blurRadius,blurSigma);

      float mlen=(float)(motionLength*scaleX);
      if(mlen>0.5f && motionSamples>=2){
        kr=MotionB(kr,sw,sh,mlen,motionAngle,motionSamples); kg=MotionB(kg,sw,sh,mlen,motionAngle,motionSamples);
        kb=MotionB(kb,sw,sh,mlen,motionAngle,motionSamples); ka=MotionB(ka,sw,sh,mlen,motionAngle,motionSamples);
      }

      float[] fac=new float[n];
      for(int k=0;k<n;k++){
        float a=pa[k]; if(a<=0.5f){ fac[k]=0f; continue; }
        float inv=255f/a;
        float R=pr[k]*inv, G=pg[k]*inv, B=pb[k]*inv;
        float lum=(0.299f*R+0.587f*G+0.114f*B)/255f;
        fac[k]= lum>bloomThreshold ? (float)((lum-bloomThreshold)/(1.0-bloomThreshold)) : 0f;
      }
      float[] br=Gauss(Mul(pr,fac),sw,sh,bloomRadius,bloomSigma);
      float[] bg=Gauss(Mul(pg,fac),sw,sh,bloomRadius,bloomSigma);
      float[] bb2=Gauss(Mul(pb,fac),sw,sh,bloomRadius,bloomSigma);

      using(var outBmp=new Bitmap(ow,oh,PixelFormat.Format32bppArgb)){
        var od=outBmp.LockBits(new Rectangle(0,0,ow,oh), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
        int ostride=od.Stride; byte[] ob=new byte[ostride*oh];
        var rnd=new Random(7);
        int[] rowJ=new int[oh]; for(int y=0;y<oh;y++){ rowJ[y]=(rnd.NextDouble()<jitterRatio)? rnd.Next(-jitterPixels,jitterPixels+1):0; }
        float ccx=ow*0.5f, ccy=oh*0.5f;
        for(int y=0;y<oh;y++){ int orow=y*ostride; float ny=(y-ccy)/ccy; float scan=(y%2==0)?(float)(1.0-scanline):1f;
          for(int x=0;x<ow;x++){
            float nx=(x-ccx)/ccx; float r2=nx*nx+ny*ny;
            float sx=(float)((x+rowJ[y])*scaleX); float sy=(float)(y*scaleY);
            float ca=(float)(aberration*scaleX*(1.0+r2*0.5));
            float A=SampleB(ka,sw,sh,sx,sy);
            float RR=SampleB(kr,sw,sh,sx+ca,sy)*scan; float GG=SampleB(kg,sw,sh,sx,sy)*scan; float BB=SampleB(kb,sw,sh,sx-ca,sy)*scan;
            float bo=(float)bloomIntensity;
            RR+=SampleB(br,sw,sh,sx,sy)*bo; GG+=SampleB(bg,sw,sh,sx,sy)*bo; BB+=SampleB(bb2,sw,sh,sx,sy)*bo;
            float nz=((float)rnd.NextDouble()+(float)rnd.NextDouble()-1f)*(float)grain*(A/255f);
            RR+=nz; GG+=nz; BB+=nz;
            float a=Math.Min(255f,Math.Max(0f,A));
            float r=0f,g2=0f,b2=0f;
            if(a>0.5f){
              RR=Math.Min(a,Math.Max(0f,RR)); GG=Math.Min(a,Math.Max(0f,GG)); BB=Math.Min(a,Math.Max(0f,BB));
              float inv=255f/a; r=RR*inv; g2=GG*inv; b2=BB*inv;
            }
            int i=orow+x*4;
            ob[i]=(byte)Math.Round(b2); ob[i+1]=(byte)Math.Round(g2); ob[i+2]=(byte)Math.Round(r); ob[i+3]=(byte)Math.Round(a);
          }
        }
        Marshal.Copy(ob,0,od.Scan0,ob.Length); outBmp.UnlockBits(od); outBmp.Save(dstPath, ImageFormat.Png);
      }
    }
  }

  static Bitmap To32(Bitmap src){
    var dst=new Bitmap(src.Width, src.Height, PixelFormat.Format32bppArgb);
    using(var g=Graphics.FromImage(dst)){ g.CompositingMode=CompositingMode.SourceCopy; g.DrawImage(src, new Rectangle(0,0,src.Width,src.Height)); }
    return dst;
  }
  static float[] Mul(float[] a, float[] b){ var o=new float[a.Length]; for(int i=0;i<a.Length;i++) o[i]=a[i]*b[i]; return o; }
  static double[] Cdf(long[] h,int w,int hh){ long n=(long)w*hh; double[] c=new double[256]; double a=0; for(int v=0;v<256;v++){a+=h[v];c[v]=a/n;} return c; }
  static byte[] Lut(long[] hs,long n,double[] cr){ double[] cs=new double[256]; double a=0; for(int v=0;v<256;v++){a+=hs[v];cs[v]=a/n;} byte[] lut=new byte[256]; int m=0; for(int v=0;v<256;v++){ while(m<255 && cr[m]<cs[v]) m++; lut[v]=(byte)m; } return lut; }
  static float[] Gauss(float[] src,int w,int h,int r,double sigma){
    if(r<=0){ return (float[])src.Clone(); }
    float[] k=new float[2*r+1]; double s=0; for(int i=-r;i<=r;i++){ double v=Math.Exp(-(i*i)/(2*sigma*sigma)); k[i+r]=(float)v; s+=v; } for(int i=0;i<k.Length;i++) k[i]=(float)(k[i]/s);
    float[] t=new float[src.Length]; for(int y=0;y<h;y++){int row=y*w;for(int x=0;x<w;x++){float a=0;for(int i=-r;i<=r;i++){int xx=x+i;if(xx<0)xx=0;if(xx>=w)xx=w-1;a+=src[row+xx]*k[i+r];}t[row+x]=a;}}
    float[] o=new float[src.Length]; for(int y=0;y<h;y++){for(int x=0;x<w;x++){float a=0;for(int i=-r;i<=r;i++){int yy=y+i;if(yy<0)yy=0;if(yy>=h)yy=h-1;a+=t[yy*w+x]*k[i+r];}o[y*w+x]=a;}}
    return o;
  }
  static float[] MotionB(float[] src,int w,int h,float length,double angleDeg,int samples){
    double a=angleDeg*Math.PI/180.0; float dx=(float)Math.Cos(a); float dy=(float)Math.Sin(a);
    float[] o=new float[src.Length];
    for(int y=0;y<h;y++){ for(int x=0;x<w;x++){ float sum=0f;
      for(int s=0;s<samples;s++){ float t=((s/(float)(samples-1))-0.5f)*length; sum+=SampleB(src,w,h,x+t*dx,y+t*dy); }
      o[y*w+x]=sum/samples; }}
    return o;
  }
  static float SampleB(float[] a,int w,int h,float x,float y){
    if(x<0||y<0||x>w-1||y>h-1) return 0f;
    int x0=(int)x, y0=(int)y; float tx=x-x0, ty=y-y0; int x1=x0+1; if(x1>w-1)x1=w-1; int y1=y0+1; if(y1>h-1)y1=h-1;
    return (a[y0*w+x0]*(1-tx)+a[y0*w+x1]*tx)*(1-ty)+(a[y1*w+x0]*(1-tx)+a[y1*w+x1]*tx)*ty;
  }
}
'@

Add-Type -TypeDefinition $code -ReferencedAssemblies System.Drawing

if($Reference -and (Test-Path -LiteralPath $Reference)){
  $refPath = (Resolve-Path -LiteralPath $Reference).Path
  [ViaFilm]::LoadReference($refPath)
  Write-Output ("参考图: " + $refPath)
} else {
  Write-Output "未使用参考图（跳过色调匹配）"
}

if(-not (Test-Path -LiteralPath $Destination)){ New-Item -ItemType Directory -Path $Destination | Out-Null }

$files = Get-ChildItem -LiteralPath $Source -File | Where-Object { $_.Extension -match '\.(png|jpg|jpeg)$' }
foreach($f in $files){
  $dst = Join-Path $Destination ($f.BaseName + ".png")
  [ViaFilm]::Apply($f.FullName, $dst, $BlurRadius, $BlurSigma, $BloomThreshold, $BloomIntensity, $BloomRadius, $BloomSigma, $Scanline, $Aberration, $JitterRatio, $JitterPixels, $Grain, $ScaleFactor, $MotionLength, $MotionAngle, $MotionSamples)
  Write-Output ("已处理 " + $f.Name + " -> " + (Split-Path $dst -Leaf))
}
