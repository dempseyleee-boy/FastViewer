using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;

namespace FastViewer
{
static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new MainForm(args.Length > 0 ? args[0] : null));
    }
}

sealed class Params
{
    public int W,H,Stride,Offset,Black,White,Pattern,View,Rotate,Bits;
    public bool AutoLevels,Little,Lsb,Packed;
    public string Format;
}


sealed class ViewerItem
{
    public string Path;
    public Params P;
    public Bitmap Bitmap;
    public int Black,White;
    public string Error;
}
sealed class MainForm : Form
{
    TextBox pathBox=new TextBox(), wBox=new TextBox(), hBox=new TextBox(), strideBox=new TextBox(), offsetBox=new TextBox();
    TextBox blackBox=new TextBox(), whiteBox=new TextBox(), gammaBox=new TextBox();
    ComboBox fmtBox=new ComboBox(), endianBox=new ComboBox(), alignBox=new ComboBox(), patternBox=new ComboBox(), viewBox=new ComboBox(), rotBox=new ComboBox(), exportBox=new ComboBox();
    Panel imagePanel=new Panel(); FlowLayoutPanel gallery=new FlowLayoutPanel(); PictureBox pic=new PictureBox(); Label status=new Label();
    byte[] data; string openedPath; string[] openedPaths; Params p; Bitmap current; double zoom=1.0, galleryZoom=1.0, gammaValue=2.2; bool multiMode=false; int autoBlack=0, autoWhite=16383; byte[] stretchLut; List<Bitmap> galleryBitmaps=new List<Bitmap>(); List<ViewerItem> galleryItems=new List<ViewerItem>(); string exportLockHint="";

    string[] formats={"RAW8_8B","RAW10_16B","RAW10_PACKED","RAW12_16B","RAW12_PACKED","RAW14_16B","RAW14_PACKED","RAW16_16B","RGB24","BGR24","RGBA32","BGRA32","RGB48","BGR48","NV21","NV12","I420","YV12","YUV420P","P010"};
    string[] imageExports={"PNG","BMP","JPEG","TIFF"};
    string[] rgb8Exports={"RGB24","BGR24","RGBA32","BGRA32"};
    string[] rgb16Exports={"RGB48","BGR48"};
    string[] yuv8Exports={"NV21","NV12","I420","YV12","YUV420P"};
    string[] yuv10Exports={"P010"};

    public MainForm(string initial)
    {
        Text="FastViewer"; Width=1380; Height=900; MinimumSize=new Size(1080,660); try{Icon=Icon.ExtractAssociatedIcon(Application.ExecutablePath);}catch{}
        BuildUi();
        if(!String.IsNullOrEmpty(initial)){ pathBox.Text=initial; ApplyFileName(initial); }
    }

    void BuildUi()
    {
        BackColor=Color.FromArgb(15,17,21);
        ForeColor=Color.FromArgb(231,236,244);
        Font=new Font("Segoe UI",9F,FontStyle.Regular,GraphicsUnit.Point);

        var root=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2,RowCount=1,BackColor=Color.FromArgb(15,17,21),Padding=new Padding(12)};
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,350));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
        Controls.Add(root);

        var leftShell=new Panel{Dock=DockStyle.Fill,AutoScroll=true,BackColor=Color.FromArgb(24,27,34),Padding=new Padding(16)};
        root.Controls.Add(leftShell,0,0);

        var left=new TableLayoutPanel{Dock=DockStyle.Top,AutoSize=true,ColumnCount=2,RowCount=30,BackColor=Color.FromArgb(24,27,34)};
        left.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,108));
        left.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
        leftShell.Controls.Add(left);

        AddTitle(left,"FastViewer","Phone Camera RAW / YUV / RGB viewer",0);
        AddSection(left,"FILE",2);
        pathBox.Dock=DockStyle.Fill; StyleTextBox(pathBox); left.Controls.Add(pathBox,0,3); left.SetColumnSpan(pathBox,2);
        AddButton(left,"Browse / Multi",delegate{Browse();},0,4,1); AddButton(left,"Open",delegate{OpenFileNow();},1,4,1);

        AddSection(left,"FRAME",6);
        AddRow(left,"Width",wBox,7,"4096"); AddRow(left,"Height",hBox,8,"3072"); AddRow(left,"Stride",strideBox,9,""); AddRow(left,"Offset",offsetBox,10,"0");

        AddSection(left,"TONE",12);
        AddRow(left,"Black",blackBox,13,"auto"); AddRow(left,"White",whiteBox,14,"auto"); AddRow(left,"Gamma",gammaBox,15,"2.2");

        AddSection(left,"FORMAT",17);
        fmtBox.DropDownStyle=ComboBoxStyle.DropDownList; fmtBox.Items.AddRange(formats); fmtBox.SelectedIndex=5; AddCombo(left,"Format",fmtBox,18);
        endianBox.DropDownStyle=ComboBoxStyle.DropDownList; endianBox.Items.AddRange(new object[]{"Little Endian","Big Endian"}); endianBox.SelectedIndex=0; AddCombo(left,"Endian",endianBox,19);
        alignBox.DropDownStyle=ComboBoxStyle.DropDownList; alignBox.Items.AddRange(new object[]{"LSB aligned","MSB aligned"}); alignBox.SelectedIndex=0; AddCombo(left,"Bits",alignBox,20);
        patternBox.DropDownStyle=ComboBoxStyle.DropDownList; patternBox.Items.AddRange(new object[]{"GRBG","RGGB","BGGR","GBRG"}); patternBox.SelectedIndex=0; AddCombo(left,"Bayer",patternBox,21);
        viewBox.DropDownStyle=ComboBoxStyle.DropDownList; viewBox.Items.AddRange(new object[]{"Color","Bayer gray","Bayer site RGB"}); viewBox.SelectedIndex=0; AddCombo(left,"View",viewBox,22);
        rotBox.DropDownStyle=ComboBoxStyle.DropDownList; rotBox.Items.AddRange(new object[]{"0","90","180","270"}); rotBox.SelectedIndex=0; AddCombo(left,"Rotate",rotBox,23);

        AddSection(left,"VIEW",25);
        AddButton(left,"Fit Window",delegate{FitWindow();},0,26,2);
        exportBox.DropDownStyle=ComboBoxStyle.DropDownList; exportBox.Items.AddRange(imageExports); exportBox.SelectedIndex=0; AddCombo(left,"Export",exportBox,27);
        AddButton(left,"Refresh",delegate{RefreshPreview();},0,28,1); AddButton(left,"Export Image",delegate{ExportImage();},1,28,1);

        var hint=new Label{Text="Filename suffix is case-insensitive\r\n.raw14_grbg_16b  .nv21  .rgb48\r\nMulti-select files in Browse\r\nCtrl + mouse wheel = zoom",Dock=DockStyle.Top,AutoSize=true,Padding=new Padding(0,14,0,0),ForeColor=Color.FromArgb(144,153,166)};
        left.Controls.Add(hint,0,30); left.SetColumnSpan(hint,2);

        var right=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=3,BackColor=Color.FromArgb(15,17,21),Padding=new Padding(14,0,0,0)};
        right.RowStyles.Add(new RowStyle(SizeType.Absolute,42));
        right.RowStyles.Add(new RowStyle(SizeType.Percent,100));
        right.RowStyles.Add(new RowStyle(SizeType.Absolute,36));
        root.Controls.Add(right,1,0);

        var header=new Label{Text="Canvas",Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleLeft,Font=new Font("Segoe UI Semibold",13F,FontStyle.Bold),ForeColor=Color.FromArgb(238,242,248),Padding=new Padding(2,0,0,0)};
        right.Controls.Add(header,0,0);

        var viewerFrame=new Panel{Dock=DockStyle.Fill,BackColor=Color.FromArgb(10,12,16),Padding=new Padding(12)};
        right.Controls.Add(viewerFrame,0,1);
        imagePanel.Dock=DockStyle.Fill; imagePanel.AutoScroll=true; imagePanel.TabStop=true; imagePanel.BackColor=Color.FromArgb(8,10,14); imagePanel.BorderStyle=BorderStyle.FixedSingle; imagePanel.MouseEnter+=delegate{imagePanel.Focus();}; imagePanel.MouseWheel+=ImageWheel; imagePanel.Resize+=delegate{if(multiMode)LayoutGallery();}; viewerFrame.Controls.Add(imagePanel);
        pic.Location=new Point(0,0); pic.SizeMode=PictureBoxSizeMode.StretchImage; pic.BackColor=Color.FromArgb(8,10,14); pic.MouseEnter+=delegate{imagePanel.Focus();}; pic.MouseWheel+=ImageWheel; gallery.MouseEnter+=delegate{imagePanel.Focus();}; gallery.MouseWheel+=ImageWheel; imagePanel.Controls.Add(pic);

        status.Dock=DockStyle.Fill; status.TextAlign=ContentAlignment.MiddleLeft; status.Padding=new Padding(12,0,0,0); status.BackColor=Color.FromArgb(24,27,34); status.ForeColor=Color.FromArgb(196,204,216); status.Text="Open a phone camera RAW/YUV/RGB file."; right.Controls.Add(status,0,2);
        EnableDrop(this); EnableDrop(root); EnableDrop(leftShell); EnableDrop(left); EnableDrop(pathBox); EnableDrop(right); EnableDrop(header); EnableDrop(viewerFrame); EnableDrop(imagePanel); EnableDrop(pic); EnableDrop(gallery); EnableDrop(status);
    }

    void AddTitle(TableLayoutPanel t,string title,string sub,int r){var box=new Panel{Dock=DockStyle.Top,Height=64,BackColor=Color.FromArgb(24,27,34)};var a=new Label{Text=title,Dock=DockStyle.Top,Height=30,Font=new Font("Segoe UI Semibold",15F,FontStyle.Bold),ForeColor=Color.White};var b=new Label{Text=sub,Dock=DockStyle.Top,Height=24,ForeColor=Color.FromArgb(142,153,170)};box.Controls.Add(b);box.Controls.Add(a);t.Controls.Add(box,0,r);t.SetColumnSpan(box,2);}    
    void AddSection(TableLayoutPanel t,string s,int r){var l=new Label{Text=s,Dock=DockStyle.Fill,Height=30,Padding=new Padding(0,12,0,0),Font=new Font("Segoe UI Semibold",8F,FontStyle.Bold),ForeColor=Color.FromArgb(100,181,246)};t.Controls.Add(l,0,r);t.SetColumnSpan(l,2);}    
    void AddWide(TableLayoutPanel t,string s,int r){var l=new Label{Text=s,Dock=DockStyle.Fill,TextAlign=ContentAlignment.BottomLeft,ForeColor=Color.FromArgb(196,204,216)}; t.Controls.Add(l,0,r); t.SetColumnSpan(l,2);}    
    void AddRow(TableLayoutPanel t,string s,TextBox b,int r,string v){var l=new Label{Text=s,Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleLeft,ForeColor=Color.FromArgb(196,204,216),Margin=new Padding(0,4,8,4)}; t.Controls.Add(l,0,r); b.Text=v; b.Dock=DockStyle.Fill; b.Margin=new Padding(0,3,0,3); StyleTextBox(b); t.Controls.Add(b,1,r);}    
    void AddCombo(TableLayoutPanel t,string s,ComboBox b,int r){var l=new Label{Text=s,Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleLeft,ForeColor=Color.FromArgb(196,204,216),Margin=new Padding(0,4,8,4)}; t.Controls.Add(l,0,r); b.Dock=DockStyle.Fill; b.Margin=new Padding(0,3,0,3); StyleCombo(b); t.Controls.Add(b,1,r);}    
    void AddButton(TableLayoutPanel t,string s,EventHandler h,int c,int r,int span){var b=new Button{Text=s,Dock=DockStyle.Fill,Height=34,Margin=new Padding(c==0?0:5,4,c==0?5:0,4),Cursor=Cursors.Hand}; StyleButton(b,s=="Open"); b.Click+=h; t.Controls.Add(b,c,r); if(span>1)t.SetColumnSpan(b,span);}    
    void StyleTextBox(TextBox b){b.BorderStyle=BorderStyle.FixedSingle;b.BackColor=Color.FromArgb(34,38,48);b.ForeColor=Color.FromArgb(235,240,247);}
    void StyleCombo(ComboBox b){b.FlatStyle=FlatStyle.Flat;b.BackColor=Color.FromArgb(34,38,48);b.ForeColor=Color.FromArgb(235,240,247);}
    void StyleButton(Button b,bool primary){b.FlatStyle=FlatStyle.Flat;b.FlatAppearance.BorderSize=primary?0:1;b.FlatAppearance.BorderColor=Color.FromArgb(67,76,94);b.BackColor=primary?Color.FromArgb(0,122,204):Color.FromArgb(35,40,50);b.ForeColor=Color.White;}


    void EnableDrop(Control c)
    {
        c.AllowDrop=true;
        c.DragEnter+=DragFilesEnter;
        c.DragDrop+=DragFilesDrop;
    }
    void DragFilesEnter(object sender,DragEventArgs e)
    {
        e.Effect=(e.Data!=null&&e.Data.GetDataPresent(DataFormats.FileDrop))?DragDropEffects.Copy:DragDropEffects.None;
    }
    void DragFilesDrop(object sender,DragEventArgs e)
    {
        try
        {
            if(e.Data==null||!e.Data.GetDataPresent(DataFormats.FileDrop))return;
            string[] dropped=(string[])e.Data.GetData(DataFormats.FileDrop);
            string[] files=CollectDroppedFiles(dropped);
            if(files.Length==0){MessageBox.Show(this,"No files were dropped.",Text,MessageBoxButtons.OK,MessageBoxIcon.Information);return;}
            if(files.Length==1){pathBox.Text=files[0];ApplyFileName(files[0]);OpenFileNow();}
            else OpenMany(files);
        }
        catch(Exception ex){MessageBox.Show(this,ex.Message,Text,MessageBoxButtons.OK,MessageBoxIcon.Error);}    
    }
    static string[] CollectDroppedFiles(string[] dropped)
    {
        var files=new List<string>();
        if(dropped==null)return files.ToArray();
        foreach(string item in dropped)
        {
            if(File.Exists(item))files.Add(item);
            else if(Directory.Exists(item))
            {
                foreach(string f in Directory.GetFiles(item))files.Add(f);
            }
        }
        return files.ToArray();
    }
    void Browse()
    {
        using(var d=new OpenFileDialog())
        {
            d.Title="Open one or more phone camera files";
            d.Multiselect=true;
            d.Filter="Phone camera files|*.raw*;*.rgb;*.bgr;*.rgba;*.bgra;*.rgb24;*.bgr24;*.rgba32;*.bgra32;*.rgb48;*.bgr48;*.nv21;*.nv12;*.i420;*.yv12;*.yuv420p;*.p010;*.yuv;*.bin;*.dat|All files|*.*";
            if(d.ShowDialog(this)==DialogResult.OK){if(d.FileNames.Length>1)OpenMany(d.FileNames);else{pathBox.Text=d.FileName; ApplyFileName(d.FileName);}}        
        }
    }

    Params DetectParamsFromFile(string path)
    {
        var q=new Params();
        int ww,hh; if(TryDims(path,out ww,out hh)){q.W=ww;q.H=hh;}else{Int32.TryParse(wBox.Text,out q.W);Int32.TryParse(hBox.Text,out q.H);}
        q.Offset=String.IsNullOrWhiteSpace(offsetBox.Text.Trim())?0:Int32.Parse(offsetBox.Text.Trim());
        q.Little=true; q.Lsb=true; q.Pattern=0; q.View=viewBox.SelectedIndex<0?0:viewBox.SelectedIndex; q.Rotate=rotBox.SelectedIndex<0?0:rotBox.SelectedIndex*90;
        q.Format="RAW14_16B"; string ext=Path.GetExtension(path).ToLowerInvariant();
        var m=Regex.Match(ext,@"^\.raw(8|10|12|14|16)_(rggb|grbg|gbrg|bggr)_(packed|16b|8b)$",RegexOptions.IgnoreCase);
        if(m.Success)
        {
            string bits=m.Groups[1].Value, pat=m.Groups[2].Value.ToUpperInvariant(), store=m.Groups[3].Value.ToUpperInvariant();
            q.Format="RAW"+bits+"_"+store;
            q.Pattern=PatternIndex(pat);
            q.Lsb=true;
        }
        else
        {
            string f=ext.Length>1?ext.Substring(1).ToUpperInvariant():"";
            if(f=="RGB")f="RGB24"; if(f=="BGR")f="BGR24"; if(f=="RGBA")f="RGBA32"; if(f=="BGRA")f="BGRA32";
            bool known=false; for(int i=0;i<formats.Length;i++)if(String.Equals(formats[i],f,StringComparison.OrdinalIgnoreCase)){known=true;break;}
            q.Format=known?f:"RAW14_16B";
        }
        q.Stride=q.W>0?DefaultStride(q.W,q.Format):0;
        q.Packed=q.Format.EndsWith("PACKED"); q.Bits=8; var bm=Regex.Match(q.Format,@"RAW(\d+)_"); if(bm.Success)q.Bits=Int32.Parse(bm.Groups[1].Value);
        string b=blackBox.Text.Trim().ToLowerInvariant(), w=whiteBox.Text.Trim().ToLowerInvariant(); q.AutoLevels=b==""||w==""||b=="auto"||w=="auto"; q.Black=q.AutoLevels?0:Int32.Parse(b); q.White=q.AutoLevels?MaxRaw(q.Bits):Int32.Parse(w);
        return q;
    }
    void ApplyDetectedParams(Params q)
    {
        if(q.W>0)wBox.Text=q.W.ToString(); if(q.H>0)hBox.Text=q.H.ToString();
        Select(fmtBox,q.Format); Select(patternBox,PatternName(q.Pattern));
        endianBox.SelectedIndex=q.Little?0:1; alignBox.SelectedIndex=q.Lsb?0:1;
        strideBox.Text=q.Stride>0?q.Stride.ToString():"";
        UpdateExportChoices(new Params[]{q});
    }
    void ApplyFileName(string path)
    {
        Params q=DetectParamsFromFile(path);
        ApplyDetectedParams(q);
        status.Text=WithExportHint("Detected "+Path.GetExtension(path).ToLowerInvariant());
    }
    static int PatternIndex(string pat){string p=pat.ToUpperInvariant(); if(p=="RGGB")return 1;if(p=="BGGR")return 2;if(p=="GBRG")return 3;return 0;}
    static string PatternName(int index){switch(index){case 1:return "RGGB";case 2:return "BGGR";case 3:return "GBRG";default:return "GRBG";}}
    static bool TryDims(string path,out int w,out int h)
    {
        string n=Path.GetFileName(path); string[] ps={@"(?<!\d)(\d{2,5})\s*[xX]\s*(\d{2,5})(?!\d)",@"(?<!\d)w\s*(\d{2,5})\s*h\s*(\d{2,5})(?!\d)",@"(?<!\d)(\d{2,5})\s*[_-]\s*(\d{2,5})(?!\d)"};
        foreach(string p in ps){var m=Regex.Match(n,p,RegexOptions.IgnoreCase); if(m.Success){w=Int32.Parse(m.Groups[1].Value); h=Int32.Parse(m.Groups[2].Value); if(w>=16&&h>=16&&w<=20000&&h<=20000)return true;}}
        w=h=0; return false;
    }

    static bool Select(ComboBox b,string s){for(int i=0;i<b.Items.Count;i++)if(String.Equals(b.Items[i].ToString(),s,StringComparison.OrdinalIgnoreCase)){b.SelectedIndex=i;return true;}return false;}

    void AutoStrideIfEmpty(){ if(String.IsNullOrWhiteSpace(strideBox.Text))SetDefaultStride();}
    void SetDefaultStride(){int w;if(Int32.TryParse(wBox.Text,out w))strideBox.Text=DefaultStride(w,Format()).ToString();}
    string Format(){return fmtBox.SelectedItem==null?"RAW14_16B":fmtBox.SelectedItem.ToString().ToUpperInvariant();}
    static int DefaultStride(int w,string f){if(f=="RAW8_8B")return w;if(f=="RAW10_PACKED")return ((w+3)/4)*5;if(f=="RAW12_PACKED")return ((w+1)/2)*3;if(f=="RAW14_PACKED")return (w*14+7)/8;if(f=="RGB24"||f=="BGR24")return w*3;if(f=="RGBA32"||f=="BGRA32")return w*4;if(f=="RGB48"||f=="BGR48")return w*6;if(f=="NV21"||f=="NV12"||f=="I420"||f=="YV12"||f=="YUV420P")return w;return w*2;}

    Params ReadParams()
    {
        var q=new Params(); q.W=Int32.Parse(wBox.Text.Trim()); q.H=Int32.Parse(hBox.Text.Trim()); q.Format=Format();
        string st=strideBox.Text.Trim(); q.Stride=String.IsNullOrWhiteSpace(st)?DefaultStride(q.W,q.Format):Int32.Parse(st);
        q.Offset=String.IsNullOrWhiteSpace(offsetBox.Text.Trim())?0:Int32.Parse(offsetBox.Text.Trim()); q.Little=endianBox.SelectedIndex==0; q.Lsb=alignBox.SelectedIndex==0;
        q.Pattern=patternBox.SelectedIndex<0?0:patternBox.SelectedIndex; q.View=viewBox.SelectedIndex<0?0:viewBox.SelectedIndex; q.Rotate=rotBox.SelectedIndex<0?0:rotBox.SelectedIndex*90;
        q.Packed=q.Format.EndsWith("PACKED"); q.Bits=8; var m=Regex.Match(q.Format,@"RAW(\d+)_"); if(m.Success)q.Bits=Int32.Parse(m.Groups[1].Value);
        string b=blackBox.Text.Trim().ToLowerInvariant(), w=whiteBox.Text.Trim().ToLowerInvariant(); q.AutoLevels=b==""||w==""||b=="auto"||w=="auto"; q.Black=q.AutoLevels?0:Int32.Parse(b); q.White=q.AutoLevels?MaxRaw(q.Bits):Int32.Parse(w); double gv; if(!Double.TryParse(gammaBox.Text,out gv))gv=2.2; gammaValue=gv;
        if(q.W<=0||q.H<=0)throw new Exception("Width/height must be positive."); if(q.Stride<=0)throw new Exception("Stride must be positive or empty."); if(q.Offset<0)throw new Exception("Offset must be >=0."); return q;
    }
    static int MaxRaw(int bits){return bits>=16?65535:((1<<bits)-1);}    
    bool IsRaw(){return p.Format.StartsWith("RAW");}
    bool IsRgb(){return p.Format=="RGB24"||p.Format=="BGR24"||p.Format=="RGBA32"||p.Format=="BGRA32"||p.Format=="RGB48"||p.Format=="BGR48";}
    bool IsYuv(){return !IsRaw()&&!IsRgb();}
    long ExpectedBytes(Params q){if(q.Format=="P010")return (long)q.Offset+(long)q.Stride*q.H*3/2; if(q.Format=="NV21"||q.Format=="NV12"||q.Format=="I420"||q.Format=="YV12"||q.Format=="YUV420P")return (long)q.Offset+(long)q.Stride*q.H*3/2; return (long)q.Offset+(long)q.Stride*q.H;}

    void OpenFileNow()
    {
        try
        {
            if(String.IsNullOrWhiteSpace(pathBox.Text))Browse(); if(String.IsNullOrWhiteSpace(pathBox.Text))return; openedPath=pathBox.Text.Trim(); ApplyFileName(openedPath); p=ReadParams(); openedPaths=new string[]{openedPath}; ShowSingleMode();
            long expected=ExpectedBytes(p), len=new FileInfo(openedPath).Length; if(len<expected)throw new Exception("File too small: "+len+" bytes, expected at least "+expected+".");
            status.Text="Reading whole file..."; ThreadPool.QueueUserWorkItem(delegate{try{byte[] bytes=File.ReadAllBytes(openedPath); BeginInvoke((Action)delegate{data=bytes; EstimateLevels(); BuildStretchLut(); Render(true);});}catch(Exception ex){ShowErr(ex);}});
        }catch(Exception ex){MessageBox.Show(this,ex.Message,Text,MessageBoxButtons.OK,MessageBoxIcon.Error);}    
    }
    void RefreshPreview(){try{if(multiMode&&openedPaths!=null&&openedPaths.Length>1){OpenMany(openedPaths);return;} p=ReadParams(); if(data==null){OpenFileNow();return;} EstimateLevels(); BuildStretchLut(); Render(true);}catch(Exception ex){MessageBox.Show(this,ex.Message,Text,MessageBoxButtons.OK,MessageBoxIcon.Error);}}
    void FitWindow(){if(multiMode){galleryZoom=1.0;LayoutGallery();UpdateStatus();return;} if(current!=null){zoom=FitZoomFor(current);ApplyZoom();imagePanel.AutoScrollPosition=new Point(0,0);UpdateStatus();}else RefreshPreview();}


    void ShowSingleMode()
    {
        multiMode=false;
        imagePanel.AutoScroll=true;
        if(pic.Parent!=imagePanel){imagePanel.Controls.Clear();imagePanel.Controls.Add(pic);}        
    }
    void ShowMultiStart()
    {
        multiMode=true;
        if(current!=null){current.Dispose();current=null;}
        pic.Image=null;
        foreach(Bitmap b in galleryBitmaps)b.Dispose();
        galleryBitmaps.Clear();
        galleryItems.Clear();
        gallery.Controls.Clear();
        galleryZoom=1.0;
        imagePanel.AutoScroll=false;
        imagePanel.Controls.Clear();
        gallery.Dock=DockStyle.Fill;
        gallery.AutoScroll=true;
        gallery.WrapContents=true;
        gallery.FlowDirection=FlowDirection.LeftToRight;
        gallery.BackColor=Color.FromArgb(8,10,14);
        gallery.Padding=new Padding(10);
        imagePanel.Controls.Add(gallery);
    }
    Params ParamsForFile(string path)
    {
        return DetectParamsFromFile(path);
    }
    void OpenMany(string[] paths)
    {
        try
        {
            if(paths==null||paths.Length==0)return;
            if(paths.Length==1){pathBox.Text=paths[0];ApplyFileName(paths[0]);OpenFileNow();return;}
            var jobs=new List<ViewerItem>();
            foreach(string fp in paths)
            {
                Params q=ParamsForFile(fp);
                long expected=ExpectedBytes(q), len=new FileInfo(fp).Length;
                if(len<expected)throw new Exception(Path.GetFileName(fp)+" too small: "+len+" bytes, expected at least "+expected+".");
                jobs.Add(new ViewerItem{Path=fp,P=q});
            }
            UpdateExportChoicesForItems(jobs); openedPaths=paths; openedPath=paths[paths.Length-1]; p=jobs[jobs.Count-1].P; pathBox.Text=paths.Length+" files selected";
            ShowMultiStart();
            status.Text="Rendering 0 / "+jobs.Count+" images...";
            ThreadPool.QueueUserWorkItem(delegate{
                int done=0;
                foreach(ViewerItem job0 in jobs)
                {
                    ViewerItem job=job0;
                    try
                    {
                        byte[] bytes=File.ReadAllBytes(job.Path);
                        p=job.P; data=bytes; EstimateLevels(); job.Black=autoBlack; job.White=autoWhite; BuildStretchLut(); job.Bitmap=BuildBitmap(true);
                        int n=++done;
                        BeginInvoke((Action)delegate{AddImageCard(job,n,jobs.Count);});
                    }
                    catch(Exception ex)
                    {
                        job.Error=ex.Message; int n=++done;
                        BeginInvoke((Action)delegate{AddErrorCard(job,n,jobs.Count);});
                    }
                }
                BeginInvoke((Action)delegate{UpdateStatus();});
            });
        }
        catch(Exception ex){MessageBox.Show(this,ex.Message,Text,MessageBoxButtons.OK,MessageBoxIcon.Error);}    
    }
    void AddImageCard(ViewerItem job,int done,int total)
    {
        galleryBitmaps.Add(job.Bitmap); galleryItems.Add(job);
        var card=new TableLayoutPanel{ColumnCount=1,RowCount=2,BackColor=Color.FromArgb(24,27,34),Margin=new Padding(8),Padding=new Padding(8)};
        card.RowStyles.Add(new RowStyle(SizeType.Absolute,48));
        card.RowStyles.Add(new RowStyle(SizeType.Percent,100));
        var cap=new Label{Text=Path.GetFileName(job.Path)+"\r\n"+job.P.W+"x"+job.P.H+"  "+job.P.Format+"  levels "+job.Black+"-"+job.White,Dock=DockStyle.Fill,ForeColor=Color.FromArgb(220,226,236),TextAlign=ContentAlignment.MiddleLeft,AutoEllipsis=true};
        var pb=new PictureBox{Dock=DockStyle.Fill,Image=job.Bitmap,SizeMode=PictureBoxSizeMode.Zoom,BackColor=Color.FromArgb(10,12,16)};
        card.Controls.Add(cap,0,0); card.Controls.Add(pb,0,1); gallery.Controls.Add(card); LayoutGallery(); status.Text="Rendering "+done+" / "+total+" images...";
    }
    void AddErrorCard(ViewerItem job,int done,int total)
    {
        var card=new TableLayoutPanel{ColumnCount=1,RowCount=1,BackColor=Color.FromArgb(53,32,37),Margin=new Padding(8),Padding=new Padding(10)};
        var cap=new Label{Text=Path.GetFileName(job.Path)+"\r\nFAILED: "+job.Error,Dock=DockStyle.Fill,ForeColor=Color.FromArgb(255,205,210),TextAlign=ContentAlignment.MiddleLeft};
        card.Controls.Add(cap,0,0); gallery.Controls.Add(card); LayoutGallery(); status.Text="Rendering "+done+" / "+total+" images...";
    }
    void LayoutGallery()
    {
        if(!multiMode||gallery==null)return;
        int vw=Math.Max(360,gallery.ClientSize.Width-28);
        int cols=vw>=1500?3:(vw>=860?2:1);
        int cw=Math.Max(320,(int)(((vw-(cols+1)*18)/(double)cols)*galleryZoom));
        int ch=Math.Max(280,(int)(cw*0.72)+58);
        foreach(Control c in gallery.Controls){c.Width=cw;c.Height=ch;}
    }
    ushort RawAt(int x,int y)
    {
        if(x<0)x=0; else if(x>=p.W)x=p.W-1; if(y<0)y=0; else if(y>=p.H)y=p.H-1; int row=p.Offset+y*p.Stride, val;
        if(p.Packed) val=ReadPacked(row,x,p.Bits); else if(p.Bits==8) val=data[row+x]; else {int off=row+x*2; val=p.Little?(data[off]|(data[off+1]<<8)):((data[off]<<8)|data[off+1]); if(p.Bits<16){int mask=(1<<p.Bits)-1; val=p.Lsb?(val&mask):(val>>(16-p.Bits));}}
        return (ushort)val;
    }
    int ReadPacked(int row,int x,int bits)
    {
        if(bits==10){int g=x/4,i=x&3,o=row+g*5; if(i==0)return data[o]|((data[o+4]&3)<<8); if(i==1)return data[o+1]|((data[o+4]&12)<<6); if(i==2)return data[o+2]|((data[o+4]&48)<<4); return data[o+3]|((data[o+4]&192)<<2);}        
        if(bits==12){int g=x/2,i=x&1,o=row+g*3; if(i==0)return data[o]|((data[o+2]&15)<<8); return data[o+1]|((data[o+2]&240)<<4);}        
        int bit=x*bits, bo=row+bit/8, sh=bit&7, word=data[bo]|(data[bo+1]<<8)|(data[bo+2]<<16); return (word>>sh)&((1<<bits)-1);
    }
    char BayerSite(int x,int y){bool ox=(x&1)!=0,oy=(y&1)!=0; switch(p.Pattern){case 1:return !oy?(ox?'G':'R'):(ox?'B':'G');case 2:return !oy?(ox?'G':'B'):(ox?'R':'G');case 3:return !oy?(ox?'B':'G'):(ox?'G':'R');default:return !oy?(ox?'R':'G'):(ox?'G':'B');}}
    void Demosaic(int x,int y,out int r,out int g,out int b)
    {
        char c=BayerSite(x,y); int v=RawAt(x,y); if(c=='R'){r=v;g=(RawAt(x-1,y)+RawAt(x+1,y)+RawAt(x,y-1)+RawAt(x,y+1))>>2;b=(RawAt(x-1,y-1)+RawAt(x+1,y-1)+RawAt(x-1,y+1)+RawAt(x+1,y+1))>>2;}
        else if(c=='B'){b=v;g=(RawAt(x-1,y)+RawAt(x+1,y)+RawAt(x,y-1)+RawAt(x,y+1))>>2;r=(RawAt(x-1,y-1)+RawAt(x+1,y-1)+RawAt(x-1,y+1)+RawAt(x+1,y+1))>>2;}
        else{g=v; if(BayerSite(x-1,y)=='R'){r=(RawAt(x-1,y)+RawAt(x+1,y))>>1;b=(RawAt(x,y-1)+RawAt(x,y+1))>>1;}else{b=(RawAt(x-1,y)+RawAt(x+1,y))>>1;r=(RawAt(x,y-1)+RawAt(x,y+1))>>1;}}
    }

    void RgbAt(int x,int y,out byte r,out byte g,out byte b)
    {
        if(p.Format=="RGB48"||p.Format=="BGR48")
        {
            int o=p.Offset+y*p.Stride+x*6;
            int hi=p.Little?1:0; byte c0=data[o+hi], c1=data[o+2+hi], c2=data[o+4+hi];
            if(p.Format=="RGB48"){r=c0;g=c1;b=c2;}else{b=c0;g=c1;r=c2;}
            return;
        }
        int ps=(p.Format=="RGB24"||p.Format=="BGR24")?3:4; int off=p.Offset+y*p.Stride+x*ps; byte c0b=data[off],c1b=data[off+1],c2b=data[off+2];
        if(p.Format=="RGB24"||p.Format=="RGBA32"){r=c0b;g=c1b;b=c2b;}else{b=c0b;g=c1b;r=c2b;}
    }
    static byte Clamp(int v){return (byte)(v<0?0:(v>255?255:v));}
    void YuvAt(int x,int y,out byte rr,out byte gg,out byte bb)
    {
        int Y,U=128,V=128; string f=p.Format;
        if(f=="P010")
        {
            int yo=p.Offset+y*p.Stride+x*2; Y=(data[yo]|(data[yo+1]<<8))>>8; int uvb=p.Offset+p.Stride*p.H; int uvo=uvb+(y/2)*p.Stride+(x/2)*4; U=(data[uvo]|(data[uvo+1]<<8))>>8; V=(data[uvo+2]|(data[uvo+3]<<8))>>8;
        }
        else
        {
            Y=data[p.Offset+y*p.Stride+x]; int uvb=p.Offset+p.Stride*p.H;
            if(f=="NV12"||f=="NV21"){int uvo=uvb+(y/2)*p.Stride+(x/2)*2; if(f=="NV12"){U=data[uvo];V=data[uvo+1];}else{V=data[uvo];U=data[uvo+1];}}
            else{int cs=p.Stride/2, ch=(p.H+1)/2, off=(y/2)*cs+(x/2), size=cs*ch; if(f=="YV12"){V=data[uvb+off];U=data[uvb+size+off];}else{U=data[uvb+off];V=data[uvb+size+off];}}
        }
        int c=Y-16,d=U-128,e=V-128; rr=Clamp((298*c+409*e+128)>>8); gg=Clamp((298*c-100*d-208*e+128)>>8); bb=Clamp((298*c+516*d+128)>>8);
    }

    void FastDemosaic2x2(int x,int y,out int r,out int g,out int b)
    {
        int bx=x&~1, by=y&~1, rs=0,gs=0,bs=0,rc=0,gc=0,bc=0;
        for(int yy=0;yy<2;yy++)for(int xx=0;xx<2;xx++)
        {
            int sx=bx+xx, sy=by+yy, v=RawAt(sx,sy); char c=BayerSite(sx,sy);
            if(c=='R'){rs+=v;rc++;} else if(c=='G'){gs+=v;gc++;} else {bs+=v;bc++;}
        }
        r=rc>0?rs/rc:RawAt(x,y); g=gc>0?gs/gc:RawAt(x,y); b=bc>0?bs/bc:RawAt(x,y);
    }
    void EstimateLevels()
    {
        if(!IsRaw()){autoBlack=0;autoWhite=255;return;}
        if(!p.AutoLevels){autoBlack=p.Black;autoWhite=p.White;if(autoWhite<=autoBlack)autoWhite=autoBlack+1;return;}
        int max=MaxRaw(p.Bits); int[] hist=new int[max+1];
        int stepX=Math.Max(1,p.W/512), stepY=Math.Max(1,p.H/512), count=0;
        for(int y=0;y<p.H;y+=stepY)for(int x=0;x<p.W;x+=stepX){hist[RawAt(x,y)]++;count++;}
        long lo=Math.Max(1,count/100), hi=Math.Max(1,count*995/1000), acc=0; autoBlack=0; autoWhite=max;
        for(int i=0;i<hist.Length;i++){acc+=hist[i]; if(acc>=lo){autoBlack=i;break;}}
        acc=0; for(int i=0;i<hist.Length;i++){acc+=hist[i]; if(acc>=hi){autoWhite=i;break;}}
        if(autoWhite<=autoBlack)autoWhite=autoBlack+1;
    }
    void BuildStretchLut()
    {
        int max=IsRaw()?MaxRaw(p.Bits):255;
        stretchLut=new byte[max+1];
        double gamma=gammaValue;
        double invGamma=gamma>0?1.0/gamma:1.0;
        double denom=Math.Max(1,autoWhite-autoBlack);
        for(int i=0;i<stretchLut.Length;i++)
        {
            double n=(i-autoBlack)/denom;
            if(n<0)n=0; else if(n>1)n=1;
            if(gamma>0)n=Math.Pow(n,invGamma);
            stretchLut[i]=Clamp((int)(n*255+0.5));
        }
    }
    byte Stretch(int v){if(stretchLut!=null){if(v<0)v=0;else if(v>=stretchLut.Length)v=stretchLut.Length-1;return stretchLut[v];}return Clamp(v);}    

    Bitmap BuildBitmap(bool full)
    {
        int scale=1; int ow=p.W, oh=p.H;
        Bitmap bmp=new Bitmap(ow,oh,PixelFormat.Format24bppRgb); BitmapData bd=bmp.LockBits(new Rectangle(0,0,ow,oh),ImageLockMode.WriteOnly,PixelFormat.Format24bppRgb); int bs=bd.Stride; byte[] pix=new byte[bs*oh];
        for(int oy=0;oy<oh;oy++){int sy=Math.Min(p.H-1,oy*scale), dst=oy*bs; for(int ox=0;ox<ow;ox++){int sx=Math.Min(p.W-1,ox*scale), pos=dst+ox*3; byte r,g,b;
            if(IsRgb())RgbAt(sx,sy,out r,out g,out b); else if(IsYuv())YuvAt(sx,sy,out r,out g,out b); else if(p.View==1){byte v=Stretch(RawAt(sx,sy));r=g=b=v;} else if(p.View==2){byte v=Stretch(RawAt(sx,sy));char c=BayerSite(sx,sy);r=c=='R'?v:(byte)0;g=c=='G'?v:(byte)0;b=c=='B'?v:(byte)0;} else {int ri,gi,bi;if(!full&&scale>=2)FastDemosaic2x2(sx,sy,out ri,out gi,out bi);else Demosaic(sx,sy,out ri,out gi,out bi);r=Stretch(ri);g=Stretch(gi);b=Stretch(bi);} pix[pos]=b;pix[pos+1]=g;pix[pos+2]=r;}}
        Marshal.Copy(pix,0,bd.Scan0,pix.Length); bmp.UnlockBits(bd); RotateBmp(bmp); return bmp;
    }
    void RotateBmp(Bitmap b){if(p.Rotate==90)b.RotateFlip(RotateFlipType.Rotate90FlipNone);else if(p.Rotate==180)b.RotateFlip(RotateFlipType.Rotate180FlipNone);else if(p.Rotate==270)b.RotateFlip(RotateFlipType.Rotate270FlipNone);}    
    void Render(bool fitToWindow)
    {
        if(data==null)return; status.Text="Rendering full image..."; ThreadPool.QueueUserWorkItem(delegate{try{Bitmap bmp=BuildBitmap(true); BeginInvoke((Action)delegate{ShowSingleMode(); if(current!=null)current.Dispose(); current=bmp; zoom=fitToWindow?FitZoomFor(current):1.0; pic.Image=current; ApplyZoom(); imagePanel.AutoScrollPosition=new Point(0,0); UpdateStatus();});}catch(Exception ex){ShowErr(ex);}});
    }
    void ImageWheel(object sender,MouseEventArgs e)
    {
        if((ModifierKeys&Keys.Control)==0)return; if(multiMode){galleryZoom=Math.Max(0.55,Math.Min(2.4,galleryZoom*(e.Delta>0?1.15:0.87)));LayoutGallery();UpdateStatus();return;} if(current==null)return; double old=zoom; zoom=Math.Max(0.05,Math.Min(16.0,zoom*(e.Delta>0?1.25:0.80))); if(Math.Abs(old-zoom)<0.0001)return;
        Point m=imagePanel.PointToClient(Cursor.Position); int sx=-imagePanel.AutoScrollPosition.X, sy=-imagePanel.AutoScrollPosition.Y; double ix=(sx+m.X)/old, iy=(sy+m.Y)/old; ApplyZoom(); imagePanel.AutoScrollPosition=new Point(Math.Max(0,(int)Math.Round(ix*zoom-m.X)),Math.Max(0,(int)Math.Round(iy*zoom-m.Y))); UpdateStatus();
    }
    double FitZoomFor(Bitmap bmp)
    {
        if(bmp==null)return 1.0;
        int vw=Math.Max(1,imagePanel.ClientSize.Width-4), vh=Math.Max(1,imagePanel.ClientSize.Height-4);
        double zx=vw/(double)bmp.Width, zy=vh/(double)bmp.Height;
        double z=Math.Min(zx,zy);
        if(Double.IsNaN(z)||Double.IsInfinity(z)||z<=0)z=1.0;
        return Math.Max(0.01,Math.Min(16.0,z));
    }
    void ApplyZoom(){if(current==null)return; pic.Size=new Size(Math.Max(1,(int)Math.Round(current.Width*zoom)),Math.Max(1,(int)Math.Round(current.Height*zoom)));}
    static bool Has(string[] arr,string s){foreach(string x in arr)if(String.Equals(x,s,StringComparison.OrdinalIgnoreCase))return true;return false;}
    static bool HasList(List<string> arr,string s){foreach(string x in arr)if(String.Equals(x,s,StringComparison.OrdinalIgnoreCase))return true;return false;}
    static void AddUnique(List<string> arr,string s){if(!HasList(arr,s))arr.Add(s);}
    void AddMany(List<string> arr,string[] values){foreach(string s in values)AddUnique(arr,s);}
    bool IsRawFormatName(string f){return f!=null&&f.StartsWith("RAW");}
    bool IsRgb8FormatName(string f){return f=="RGB24"||f=="BGR24"||f=="RGBA32"||f=="BGRA32";}
    bool IsRgb16FormatName(string f){return f=="RGB48"||f=="BGR48";}
    bool IsYuv8FormatName(string f){return f=="NV21"||f=="NV12"||f=="I420"||f=="YV12"||f=="YUV420P";}
    bool IsP010FormatName(string f){return f=="P010";}
    List<string> AllowedExportsFor(Params q,out string hint)
    {
        var allowed=new List<string>(); hint="";
        AddMany(allowed,imageExports);
        if(q==null){hint="Open a file to enable frame-dump exports.";return allowed;}
        string f=q.Format==null?"":q.Format.ToUpperInvariant(); bool even=(q.W%2==0)&&(q.H%2==0);
        if(IsRawFormatName(f))
        {
            AddMany(allowed,rgb8Exports); AddMany(allowed,rgb16Exports); if(even){AddMany(allowed,yuv8Exports);AddMany(allowed,yuv10Exports);}            
            hint="RAW output is locked: decoded RGB cannot restore original sensor RAW.";
        }
        else if(IsRgb8FormatName(f))
        {
            AddMany(allowed,rgb8Exports); AddMany(allowed,rgb16Exports); if(even)AddMany(allowed,yuv8Exports);
            hint="RAW/P010 outputs are locked: they would be synthetic or fake higher bit-depth data.";
        }
        else if(IsRgb16FormatName(f))
        {
            AddMany(allowed,rgb16Exports); AddMany(allowed,rgb8Exports); if(even){AddMany(allowed,yuv8Exports);AddMany(allowed,yuv10Exports);}            
            hint="RAW output is locked: RGB48/BGR48 cannot restore sensor Bayer RAW.";
        }
        else if(IsYuv8FormatName(f))
        {
            AddMany(allowed,yuv8Exports); AddMany(allowed,rgb8Exports); AddMany(allowed,rgb16Exports);
            hint="RAW/P010 outputs are locked: 8-bit YUV cannot restore RAW or true 10-bit data.";
        }
        else if(IsP010FormatName(f))
        {
            AddMany(allowed,yuv10Exports); AddMany(allowed,yuv8Exports); AddMany(allowed,rgb8Exports); AddMany(allowed,rgb16Exports);
            hint="RAW output is locked: P010 cannot restore sensor Bayer RAW.";
        }
        else
        {
            hint="Unknown source format: only image exports are enabled.";
        }
        if(!even && (IsRawFormatName(f)||IsRgb8FormatName(f)||IsRgb16FormatName(f)))hint+=" YUV420/P010 exports need even width and height.";
        return allowed;
    }
    List<string> IntersectExports(List<string> a,List<string> b)
    {
        var r=new List<string>(); foreach(string s in a)if(HasList(b,s))r.Add(s); return r;
    }
    void UpdateExportChoicesForItems(List<ViewerItem> items)
    {
        var ps=new List<Params>(); foreach(ViewerItem it in items)ps.Add(it.P); UpdateExportChoices(ps.ToArray());
    }
    void UpdateExportChoices(Params[] ps)
    {
        string old=ExportKind(); string hint=""; List<string> allowed=null;
        if(ps==null||ps.Length==0)allowed=AllowedExportsFor(null,out hint);
        else
        {
            for(int i=0;i<ps.Length;i++)
            {
                string h; List<string> one=AllowedExportsFor(ps[i],out h); if(hint.Length==0)hint=h;
                allowed=allowed==null?one:IntersectExports(allowed,one);
            }
            if(ps.Length>1)hint="Multi-image mode: only formats valid for every selected file are shown. "+hint;
        }
        if(allowed==null||allowed.Count==0)allowed=AllowedExportsFor(null,out hint);
        exportBox.BeginUpdate(); exportBox.Items.Clear(); foreach(string s in allowed)exportBox.Items.Add(s); exportBox.EndUpdate();
        int sel=0; for(int i=0;i<exportBox.Items.Count;i++)if(String.Equals(exportBox.Items[i].ToString(),old,StringComparison.OrdinalIgnoreCase)){sel=i;break;}
        if(exportBox.Items.Count>0)exportBox.SelectedIndex=sel;
        exportLockHint=hint;
    }
    bool CurrentExportAllowed(string kind)
    {
        for(int i=0;i<exportBox.Items.Count;i++)if(String.Equals(exportBox.Items[i].ToString(),kind,StringComparison.OrdinalIgnoreCase))return true;
        return false;
    }
    string WithExportHint(string s){return String.IsNullOrEmpty(exportLockHint)?s:(s+"  |  "+exportLockHint);}
    void UpdateStatus(){if(multiMode){status.Text=WithExportHint("Showing "+gallery.Controls.Count+" images  card zoom "+(int)Math.Round(galleryZoom*100)+"%");return;} if(current==null||p==null)return; status.Text=WithExportHint(Path.GetFileName(openedPath)+"  source "+p.W+"x"+p.H+"  format "+p.Format+"  image "+current.Width+"x"+current.Height+"  shown "+pic.Width+"x"+pic.Height+"  zoom "+(int)Math.Round(zoom*100)+"%  rotate "+p.Rotate+"  levels "+autoBlack+"-"+autoWhite);}
    string ExportKind(){return exportBox.SelectedItem==null?"PNG":exportBox.SelectedItem.ToString().ToUpperInvariant();}
    bool IsImageExportKind(string k){return k=="PNG"||k=="BMP"||k=="JPEG"||k=="TIFF";}
    string ExportExt(){return ExportExtFor(ExportKind());}
    string ExportExtFor(string kind)
    {
        string k=(kind??"PNG").ToUpperInvariant();
        if(k=="JPEG")return ".jpg";
        if(k=="TIFF")return ".tif";
        if(k=="PNG"||k=="BMP")return "."+k.ToLowerInvariant();
        if(k.StartsWith("RAW"))
        {
            string pat=patternBox.SelectedItem==null?"GRBG":patternBox.SelectedItem.ToString().ToUpperInvariant();
            var m=Regex.Match(k,@"^(RAW\d+)_(.+)$");
            return m.Success?"."+m.Groups[1].Value+"_"+pat+"_"+m.Groups[2].Value:"."+k;
        }
        return "."+k;
    }
    ImageFormat ExportImageFormat(){return ExportImageFormatFor(ExportKind());}
    ImageFormat ExportImageFormatFor(string kind){string k=(kind??"PNG").ToUpperInvariant();if(k=="BMP")return ImageFormat.Bmp;if(k=="JPEG")return ImageFormat.Jpeg;if(k=="TIFF")return ImageFormat.Tiff;return ImageFormat.Png;}
    string ExportFilterEntryFor(string kind)
    {
        string k=(kind??"PNG").ToUpperInvariant();
        string ext=ExportExtFor(k);
        if(k=="BMP")return "BMP image (*.bmp)|*.bmp";
        if(k=="JPEG")return "JPEG image (*.jpg;*.jpeg)|*.jpg;*.jpeg";
        if(k=="TIFF")return "TIFF image (*.tif;*.tiff)|*.tif;*.tiff";
        if(k=="PNG")return "PNG image (*.png)|*.png";
        return k+" frame dump (*"+ext+")|*"+ext;
    }
    string ExportFilterForAllowed()
    {
        var parts=new List<string>();
        for(int i=0;i<exportBox.Items.Count;i++)
        {
            string k=exportBox.Items[i].ToString().ToUpperInvariant();
            if(CurrentExportAllowed(k))parts.Add(ExportFilterEntryFor(k));
        }
        if(parts.Count==0)parts.Add(ExportFilterEntryFor(ExportKind()));
        parts.Add("All files (*.*)|*.*");
        return String.Join("|",parts.ToArray());
    }
    int ExportAllowedCount(){return exportBox.Items.Count;}
    int ExportFilterIndexFor(string kind)
    {
        for(int i=0;i<exportBox.Items.Count;i++)if(String.Equals(exportBox.Items[i].ToString(),kind,StringComparison.OrdinalIgnoreCase))return i+1;
        return 1;
    }
    string ExportKindFromFilterIndex(int idx)
    {
        int i=idx-1;
        if(i>=0&&i<exportBox.Items.Count)return exportBox.Items[i].ToString().ToUpperInvariant();
        return ExportKind();
    }
    string NormalizeExportPath(string path,string kind,bool forceExt)
    {
        if(!forceExt)return path;
        string ext=ExportExtFor(kind);
        string cur=Path.GetExtension(path);
        if(String.Equals(cur,ext,StringComparison.OrdinalIgnoreCase))return path;
        if(String.IsNullOrEmpty(cur))return path+ext;
        return Path.ChangeExtension(path,ext);
    }
    static string UniquePath(string path)
    {
        if(!File.Exists(path))return path;
        string dir=Path.GetDirectoryName(path), name=Path.GetFileNameWithoutExtension(path), ext=Path.GetExtension(path);
        for(int i=2;i<10000;i++)
        {
            string p2=Path.Combine(dir,name+"_"+i+ext);
            if(!File.Exists(p2))return p2;
        }
        return Path.Combine(dir,name+"_"+DateTime.Now.ToString("yyyyMMdd_HHmmss")+ext);
    }
    byte[] BitmapBytes(Bitmap bmp,out int stride)
    {
        BitmapData bd=bmp.LockBits(new Rectangle(0,0,bmp.Width,bmp.Height),ImageLockMode.ReadOnly,PixelFormat.Format24bppRgb);
        stride=bd.Stride;
        byte[] pix=new byte[stride*bmp.Height];
        Marshal.Copy(bd.Scan0,pix,0,pix.Length);
        bmp.UnlockBits(bd);
        return pix;
    }
    static void BgrAt(byte[] pix,int stride,int x,int y,out int r,out int g,out int b)
    {
        int pos=y*stride+x*3;
        b=pix[pos];g=pix[pos+1];r=pix[pos+2];
    }
    static int Y601(int r,int g,int b){return Clamp(((66*r+129*g+25*b+128)>>8)+16);}
    static int U601(int r,int g,int b){return Clamp(((-38*r-74*g+112*b+128)>>8)+128);}
    static int V601(int r,int g,int b){return Clamp(((112*r-94*g-18*b+128)>>8)+128);}
    static char BayerSiteForPattern(int pattern,int x,int y){bool ox=(x&1)!=0,oy=(y&1)!=0; switch(pattern){case 1:return !oy?(ox?'G':'R'):(ox?'B':'G');case 2:return !oy?(ox?'G':'B'):(ox?'R':'G');case 3:return !oy?(ox?'B':'G'):(ox?'G':'R');default:return !oy?(ox?'R':'G'):(ox?'G':'B');}}
    byte[] EncodeConvertedFrame(Bitmap bmp,string kind)
    {
        int stride; byte[] pix=BitmapBytes(bmp,out stride); int w=bmp.Width,h=bmp.Height;
        if(kind=="RGB24"||kind=="BGR24"||kind=="RGBA32"||kind=="BGRA32"||kind=="RGB48"||kind=="BGR48")return EncodeRgbDump(pix,stride,w,h,kind);
        if(kind=="NV21"||kind=="NV12"||kind=="I420"||kind=="YV12"||kind=="YUV420P"||kind=="P010")return EncodeYuvDump(pix,stride,w,h,kind);
        if(kind.StartsWith("RAW"))return EncodeRawDump(pix,stride,w,h,kind);
        throw new Exception("Unsupported export format: "+kind);
    }
    byte[] EncodeRgbDump(byte[] pix,int stride,int w,int h,string kind)
    {
        int ps=(kind=="RGB24"||kind=="BGR24")?3:(kind=="RGBA32"||kind=="BGRA32"?4:6);
        byte[] outb=new byte[w*h*ps]; int o=0;
        for(int y=0;y<h;y++)for(int x=0;x<w;x++)
        {
            int r,g,b;BgrAt(pix,stride,x,y,out r,out g,out b);
            if(kind=="RGB24"){outb[o++]=(byte)r;outb[o++]=(byte)g;outb[o++]=(byte)b;}
            else if(kind=="BGR24"){outb[o++]=(byte)b;outb[o++]=(byte)g;outb[o++]=(byte)r;}
            else if(kind=="RGBA32"){outb[o++]=(byte)r;outb[o++]=(byte)g;outb[o++]=(byte)b;outb[o++]=255;}
            else if(kind=="BGRA32"){outb[o++]=(byte)b;outb[o++]=(byte)g;outb[o++]=(byte)r;outb[o++]=255;}
            else
            {
                int c0=kind=="RGB48"?r:b,c1=g,c2=kind=="RGB48"?b:r;
                ushort v0=(ushort)(c0*257),v1=(ushort)(c1*257),v2=(ushort)(c2*257); bool little=endianBox.SelectedIndex==0;
                if(little){outb[o++]=(byte)(v0&255);outb[o++]=(byte)(v0>>8);outb[o++]=(byte)(v1&255);outb[o++]=(byte)(v1>>8);outb[o++]=(byte)(v2&255);outb[o++]=(byte)(v2>>8);}
                else{outb[o++]=(byte)(v0>>8);outb[o++]=(byte)(v0&255);outb[o++]=(byte)(v1>>8);outb[o++]=(byte)(v1&255);outb[o++]=(byte)(v2>>8);outb[o++]=(byte)(v2&255);}
            }
        }
        return outb;
    }
    byte[] EncodeYuvDump(byte[] pix,int stride,int w,int h,string kind)
    {
        if((w&1)!=0||(h&1)!=0)throw new Exception(kind+" export requires even width and height.");
        if(kind=="P010")
        {
            byte[] outb=new byte[w*h*3]; int o=0;
            for(int y=0;y<h;y++)for(int x=0;x<w;x++){int r,g,b;BgrAt(pix,stride,x,y,out r,out g,out b);ushort yy=(ushort)(Y601(r,g,b)<<8);outb[o++]=(byte)(yy&255);outb[o++]=(byte)(yy>>8);}            
            for(int y=0;y<h;y+=2)for(int x=0;x<w;x+=2){int u,v;AvgUv(pix,stride,w,h,x,y,out u,out v);ushort uu=(ushort)(u<<8),vv=(ushort)(v<<8);outb[o++]=(byte)(uu&255);outb[o++]=(byte)(uu>>8);outb[o++]=(byte)(vv&255);outb[o++]=(byte)(vv>>8);}            
            return outb;
        }
        byte[] yplane=new byte[w*h];
        int cw=(w+1)/2,ch=(h+1)/2; byte[] uplane=new byte[cw*ch],vplane=new byte[cw*ch];
        for(int y=0;y<h;y++)for(int x=0;x<w;x++){int r,g,b;BgrAt(pix,stride,x,y,out r,out g,out b);yplane[y*w+x]=(byte)Y601(r,g,b);}        
        for(int y=0;y<h;y+=2)for(int x=0;x<w;x+=2){int u,v;AvgUv(pix,stride,w,h,x,y,out u,out v);int ci=(y/2)*cw+(x/2);uplane[ci]=(byte)u;vplane[ci]=(byte)v;}
        if(kind=="NV21"||kind=="NV12")
        {
            byte[] outb=new byte[w*h+w*ch]; Buffer.BlockCopy(yplane,0,outb,0,yplane.Length); int o=yplane.Length;
            for(int cy=0;cy<ch;cy++)for(int cx=0;cx<cw;cx++){int i=cy*cw+cx;if(kind=="NV21"){outb[o++]=vplane[i];outb[o++]=uplane[i];}else{outb[o++]=uplane[i];outb[o++]=vplane[i];}}
            return outb;
        }
        else
        {
            byte[] outb=new byte[yplane.Length+uplane.Length+vplane.Length]; Buffer.BlockCopy(yplane,0,outb,0,yplane.Length);
            if(kind=="YV12"){Buffer.BlockCopy(vplane,0,outb,yplane.Length,vplane.Length);Buffer.BlockCopy(uplane,0,outb,yplane.Length+vplane.Length,uplane.Length);}else{Buffer.BlockCopy(uplane,0,outb,yplane.Length,uplane.Length);Buffer.BlockCopy(vplane,0,outb,yplane.Length+uplane.Length,vplane.Length);}            
            return outb;
        }
    }
    static void AvgUv(byte[] pix,int stride,int w,int h,int x,int y,out int u,out int v)
    {
        int us=0,vs=0,c=0;
        for(int yy=y;yy<y+2&&yy<h;yy++)for(int xx=x;xx<x+2&&xx<w;xx++){int r,g,b;BgrAt(pix,stride,xx,yy,out r,out g,out b);us+=U601(r,g,b);vs+=V601(r,g,b);c++;}
        u=us/Math.Max(1,c);v=vs/Math.Max(1,c);
    }
    byte[] EncodeRawDump(byte[] pix,int stride,int w,int h,string kind)
    {
        var m=Regex.Match(kind,@"^RAW(\d+)_(16B|8B|PACKED)$"); if(!m.Success)throw new Exception("Unsupported RAW export: "+kind);
        int bits=Int32.Parse(m.Groups[1].Value), max=MaxRaw(bits), pattern=patternBox.SelectedIndex<0?0:patternBox.SelectedIndex; string store=m.Groups[2].Value;
        if(store=="8B")
        {
            byte[] outb=new byte[w*h]; int o=0; for(int y=0;y<h;y++)for(int x=0;x<w;x++)outb[o++]=(byte)RawSampleFromRgb(pix,stride,x,y,bits,pattern,max); return outb;
        }
        if(store=="16B")
        {
            byte[] outb=new byte[w*h*2]; int o=0; for(int y=0;y<h;y++)for(int x=0;x<w;x++){ushort v=(ushort)RawSampleFromRgb(pix,stride,x,y,bits,pattern,max);outb[o++]=(byte)(v&255);outb[o++]=(byte)(v>>8);} return outb;
        }
        if(bits==10)return PackRaw10(pix,stride,w,h,bits,pattern,max);
        if(bits==12)return PackRaw12(pix,stride,w,h,bits,pattern,max);
        return PackRawBits(pix,stride,w,h,bits,pattern,max);
    }
    int RawSampleFromRgb(byte[] pix,int stride,int x,int y,int bits,int pattern,int max)
    {
        int r,g,b;BgrAt(pix,stride,x,y,out r,out g,out b); char site=BayerSiteForPattern(pattern,x,y); int c=site=='R'?r:(site=='B'?b:g); return bits==16?c*257:(c*max+127)/255;
    }
    byte[] PackRaw10(byte[] pix,int stride,int w,int h,int bits,int pattern,int max)
    {
        int row=((w+3)/4)*5; byte[] outb=new byte[row*h];
        for(int y=0;y<h;y++)for(int x=0;x<w;x+=4){int o=y*row+(x/4)*5;int v0=RawSampleFromRgb(pix,stride,x,y,bits,pattern,max),v1=RawSampleFromRgb(pix,stride,Math.Min(x+1,w-1),y,bits,pattern,max),v2=RawSampleFromRgb(pix,stride,Math.Min(x+2,w-1),y,bits,pattern,max),v3=RawSampleFromRgb(pix,stride,Math.Min(x+3,w-1),y,bits,pattern,max);outb[o]=(byte)v0;outb[o+1]=(byte)v1;outb[o+2]=(byte)v2;outb[o+3]=(byte)v3;outb[o+4]=(byte)((v0>>8)|((v1>>8)<<2)|((v2>>8)<<4)|((v3>>8)<<6));}
        return outb;
    }
    byte[] PackRaw12(byte[] pix,int stride,int w,int h,int bits,int pattern,int max)
    {
        int row=((w+1)/2)*3; byte[] outb=new byte[row*h];
        for(int y=0;y<h;y++)for(int x=0;x<w;x+=2){int o=y*row+(x/2)*3;int v0=RawSampleFromRgb(pix,stride,x,y,bits,pattern,max),v1=RawSampleFromRgb(pix,stride,Math.Min(x+1,w-1),y,bits,pattern,max);outb[o]=(byte)v0;outb[o+1]=(byte)v1;outb[o+2]=(byte)((v0>>8)|((v1>>8)<<4));}
        return outb;
    }
    byte[] PackRawBits(byte[] pix,int stride,int w,int h,int bits,int pattern,int max)
    {
        int row=(w*bits+7)/8; byte[] outb=new byte[row*h];
        for(int y=0;y<h;y++)for(int x=0;x<w;x++){int val=RawSampleFromRgb(pix,stride,x,y,bits,pattern,max), bit=x*bits, bo=y*row+bit/8, sh=bit&7; int word=val<<sh; outb[bo]|=(byte)word; if(bo+1<outb.Length)outb[bo+1]|=(byte)(word>>8); if(bo+2<outb.Length)outb[bo+2]|=(byte)(word>>16);}
        return outb;
    }
    void ExportImage()
    {
        if(multiMode){ExportManyImages();return;}
        if(data==null){MessageBox.Show(this,"Open a file first.",Text,MessageBoxButtons.OK,MessageBoxIcon.Information);return;}
        using(var d=new SaveFileDialog())
        {
            string initialKind=ExportKind();
            if(!CurrentExportAllowed(initialKind)){MessageBox.Show(this,"This export route is locked. "+exportLockHint,Text,MessageBoxButtons.OK,MessageBoxIcon.Information);return;}
            d.Title="Export image/frame";
            d.Filter=ExportFilterForAllowed();
            d.FilterIndex=ExportFilterIndexFor(initialKind);
            d.FileName=Path.GetFileNameWithoutExtension(openedPath)+ExportExtFor(initialKind);
            d.DefaultExt=ExportExtFor(initialKind).TrimStart('.');
            d.AddExtension=true;
            if(d.ShowDialog(this)!=DialogResult.OK)return;
            string kind=ExportKindFromFilterIndex(d.FilterIndex);
            if(!CurrentExportAllowed(kind)){MessageBox.Show(this,"This export route is locked. "+exportLockHint,Text,MessageBoxButtons.OK,MessageBoxIcon.Information);return;}
            bool forceExt=d.FilterIndex<=ExportAllowedCount();
            string outPath=NormalizeExportPath(d.FileName,kind,forceExt);
            status.Text="Exporting "+kind+"...";
            ThreadPool.QueueUserWorkItem(delegate{try{Bitmap b=BuildBitmap(true); if(IsImageExportKind(kind))b.Save(outPath,ExportImageFormatFor(kind)); else File.WriteAllBytes(outPath,EncodeConvertedFrame(b,kind)); b.Dispose(); BeginInvoke((Action)delegate{status.Text="Exported: "+outPath;});}catch(Exception ex){ShowErr(ex);}});
        }
    }
    void ExportManyImages()
    {
        if(galleryItems.Count==0){MessageBox.Show(this,"No rendered images to export.",Text,MessageBoxButtons.OK,MessageBoxIcon.Information);return;}
        using(var d=new FolderBrowserDialog())
        {
            d.Description="Choose export folder for "+galleryItems.Count+" "+ExportKind()+" files";
            if(d.ShowDialog(this)!=DialogResult.OK)return;
            string folder=d.SelectedPath; string kind=ExportKind(); string ext=ExportExtFor(kind); ImageFormat fmt=ExportImageFormatFor(kind); if(!CurrentExportAllowed(kind)){MessageBox.Show(this,"This export route is locked. "+exportLockHint,Text,MessageBoxButtons.OK,MessageBoxIcon.Information);return;} bool imageOut=IsImageExportKind(kind);
            status.Text="Exporting 0 / "+galleryItems.Count+" "+kind+" files...";
            ThreadPool.QueueUserWorkItem(delegate{try{
                int n=0;
                foreach(ViewerItem item in galleryItems)
                {
                    string name=Path.GetFileNameWithoutExtension(item.Path);
                    string outPath=UniquePath(Path.Combine(folder,name+ext));
                    if(imageOut)item.Bitmap.Save(outPath,fmt); else File.WriteAllBytes(outPath,EncodeConvertedFrame(item.Bitmap,kind));
                    n++;
                    int shown=n;
                    BeginInvoke((Action)delegate{status.Text="Exporting "+shown+" / "+galleryItems.Count+" "+kind+" files...";});
                }
                BeginInvoke((Action)delegate{status.Text="Exported "+galleryItems.Count+" "+kind+" files to "+folder;});
            }catch(Exception ex){ShowErr(ex);}});
        }
    }
    void ShowErr(Exception ex){BeginInvoke((Action)delegate{status.Text="Failed.";MessageBox.Show(this,ex.Message,Text,MessageBoxButtons.OK,MessageBoxIcon.Error);});}
    protected override void Dispose(bool disposing){if(disposing){if(current!=null)current.Dispose();foreach(Bitmap b in galleryBitmaps)b.Dispose();galleryBitmaps.Clear();galleryItems.Clear();}base.Dispose(disposing);}    
}
}

