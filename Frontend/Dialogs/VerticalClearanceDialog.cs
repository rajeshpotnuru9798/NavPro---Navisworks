using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;
using Autodesk.Navisworks.Api;
using NavPro.Backend.Core;
using NavPro.Backend.Models;
using NavPro.Backend.Services;
using NavPro.Frontend.Ui;
using Color=System.Drawing.Color;

namespace NavPro.Frontend.Dialogs
{
    public sealed class VerticalClearanceDialog : Form
    {
        private readonly ClashReviewSession session;
        private readonly VerticalClearanceService service;
        private readonly ComboBox pipeSet=new ComboBox{DropDownStyle=ComboBoxStyle.DropDownList,Width=240}, surfaceSet=new ComboBox{DropDownStyle=ComboBoxStyle.DropDownList,Width=240};
        private readonly ComboBox pipeMode=new ComboBox{DropDownStyle=ComboBoxStyle.DropDownList},surfaceMode=new ComboBox{DropDownStyle=ComboBoxStyle.DropDownList};
        private readonly ToolTip sourceTip=new ToolTip{AutoPopDelay=20000};
        private readonly Label pipeLabel=new Label{AutoSize=true,Text="No pipes loaded"},surfaceLabel=new Label{AutoSize=true,Text="No surface loaded"};
        private readonly TextBox minimum=new TextBox{Width=75,AccessibleName="Required cover"},filter=new TextBox{Width=200,Name="ResultFilter",AccessibleName="Filter results"};
        private readonly NumericUpDown tolerance=new NumericUpDown{DecimalPlaces=4,Increment=.001M,Value=.001M,Maximum=1,Width=80};
        private readonly ComboBox units=new ComboBox{DropDownStyle=ComboBoxStyle.DropDownList,Width=110};
        private readonly Label status=new Label{Dock=DockStyle.Fill,AutoEllipsis=true,TextAlign=ContentAlignment.MiddleLeft};
        private readonly DataGridView grid=new DataGridView{Dock=DockStyle.Fill,ReadOnly=true,AllowUserToAddRows=false,AllowUserToDeleteRows=false,AutoGenerateColumns=false,RowHeadersVisible=false,SelectionMode=DataGridViewSelectionMode.FullRowSelect,MultiSelect=false};
        private readonly Panel profile=new Panel{Dock=DockStyle.Fill,BackColor=Color.White,AutoScroll=true,AutoScrollMinSize=new Size(0,440)};
        private readonly Timer watch=new Timer{Interval=700};
        private List<ModelItem> pipes=new List<ModelItem>(),surface=new List<ModelItem>();
        private List<VerticalClearanceFinding> findings=new List<VerticalClearanceFinding>();
        private long revision, resultRevision=-1;
        private string pipeSource="",surfaceSource="";
        private bool busy;
        private readonly Button run=new Button{Text="Check vertical cover",AutoSize=true}, export=new Button{Text="Export results CSV",AutoSize=true};

        public VerticalClearanceDialog(Document document) : this(document,false) { }
        internal VerticalClearanceDialog() : this(null,true) { }
        private VerticalClearanceDialog(Document document,bool renderFixture)
        {
            if(!renderFixture) { session=new ClashReviewSession(document); service=new VerticalClearanceService(session); revision=session.Revision; }
            Text="NavPro | Vertical Clearance Checker"; Size=new Size(1360,780); MinimumSize=new Size(1100,650); StartPosition=FormStartPosition.CenterParent; AutoScaleMode=AutoScaleMode.Dpi;
            var layout=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=5,Padding=new Padding(12)};
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
            foreach(var style in new[]{new RowStyle(SizeType.Absolute,94),new RowStyle(SizeType.AutoSize),new RowStyle(SizeType.AutoSize),new RowStyle(SizeType.Percent,100),new RowStyle(SizeType.Absolute,64)}) layout.RowStyles.Add(style);
            var header=new Panel{Dock=DockStyle.Fill,BackColor=DialogTheme.Brand};
            header.Controls.Add(new Label{Text="Vertical Clearance Checker",AutoSize=true,Font=new Font("Segoe UI",20,FontStyle.Bold),ForeColor=Color.White,Location=new Point(12,10)});
            header.Controls.Add(new Label{Text="Surface level − outer pipe top at the same X/Y. World +Z is vertical. Horizontal clearance is never tested.",AutoSize=true,ForeColor=Color.White,Location=new Point(14,55)}); layout.Controls.Add(header,0,0);
            var sources=ResizableLayout.Split(SourcePanel("1. Complete pipe elements",pipeMode,pipeSet,pipeLabel,true),SourcePanel("2. Ground/top surface (single-valued TIN)",surfaceMode,surfaceSet,surfaceLabel,false),Orientation.Vertical,.5,"PipeSurfaceSourcesDivider");
            sources.Size=new Size(1280,142);sources.MinimumSize=new Size(0,142);sources.Panel1MinSize=sources.Panel2MinSize=350;layout.Controls.Add(sources,0,1);
            units.Items.AddRange(new object[]{Units.Meters,Units.Millimeters,Units.Feet,Units.Inches}); units.SelectedItem=Units.Meters;
            var settings=new FlowLayoutPanel{Dock=DockStyle.Fill,AutoSize=true,WrapContents=true,Padding=new Padding(0,4,0,8)};
            settings.Controls.AddRange(new Control[]{new Label{Text="Project minimum cover",AutoSize=true,Padding=new Padding(0,6,0,0)},minimum,units,new Label{Text="Decision tolerance (metres)",AutoSize=true,Padding=new Padding(6,6,0,0)},tolerance,run,export});
            settings.SetFlowBreak(export,true);
            settings.Controls.Add(new Label{Text="Review the model's units/orientation first. Mesh results reflect Navisworks tessellation; borderline, incomplete and ambiguous geometry require review.",AutoSize=true,MaximumSize=new Size(1200,0)}); layout.Controls.Add(settings,0,2);
            var body=new SplitContainer{Dock=DockStyle.Fill,Size=new Size(1280,430),SplitterDistance=980,Panel1MinSize=450,Panel2MinSize=220,Name="ResultsProfileDivider"};
            var resultPanel=new TableLayoutPanel{Dock=DockStyle.Fill,RowCount=2,ColumnCount=1}; resultPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize)); resultPanel.RowStyles.Add(new RowStyle(SizeType.Percent,100));
            resultPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
            var tools=new FlowLayoutPanel{AutoSize=true,Dock=DockStyle.Fill}; tools.Controls.Add(new Label{Text="Filter results",AutoSize=true,Padding=new Padding(0,5,0,0)}); tools.Controls.Add(filter);
            var refresh=new Button{Text="Refresh sources",AutoSize=true};refresh.Click+=(s,e)=>Safe(()=>{RefreshSets();pipes.Clear();surface.Clear();pipeLabel.Text="Reload pipes";surfaceLabel.Text="Reload surface";InvalidateResults("Sets/model choices refreshed. Reload review sources.");});tools.Controls.Add(refresh);
            var close=new Button{Text="Close",AutoSize=true};close.Click+=(s,e)=>Close();tools.Controls.Add(close);CancelButton=close;
            var show=new Button{Text="Select / zoom to pipe",AutoSize=true}; show.Click+=(s,e)=>Safe(()=>{CheckFresh();service.Show(Selected());});tools.Controls.Add(show);
            resultPanel.Controls.Add(tools,0,0); resultPanel.Controls.Add(grid,0,1);body.Panel1.Controls.Add(resultPanel);body.Panel2.Controls.Add(profile);layout.Controls.Add(body,0,3);
            layout.Controls.Add(status,0,4);Controls.Add(layout);
            foreach(var spec in new[]{new[]{"Pipe","Pipe"},new[]{"State","Result"},new[]{"Cover","Min cover"},new[]{"Required","Required"},new[]{"Shortfall","Shortfall"},new[]{"Surface","Surface Z"},new[]{"PipeTop","Pipe top Z"},new[]{"X","X"},new[]{"Y","Y"},new[]{"Coverage","Coverage %"},new[]{"Note","Review note"}})
                grid.Columns.Add(new DataGridViewTextBoxColumn{Name=spec[0],HeaderText=spec[1],Width=spec[0]=="Note"?350:spec[0]=="Pipe"?180:135,MinimumWidth=45,Resizable=DataGridViewTriState.True,AutoSizeMode=DataGridViewAutoSizeColumnMode.None,SortMode=DataGridViewColumnSortMode.NotSortable});
            grid.AllowUserToResizeColumns=true;grid.AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.None;grid.ShowCellToolTips=true;
            run.Click+=(s,e)=>Safe(Run); export.Click+=(s,e)=>Safe(Export); filter.TextChanged+=(s,e)=>Render();grid.SelectionChanged+=(s,e)=>profile.Invalidate();profile.Paint+=PaintProfile;
            minimum.TextChanged+=(s,e)=>InvalidateResults("Cover requirement changed. Run again."); units.SelectedIndexChanged+=(s,e)=>InvalidateResults("Units changed. Run again."); tolerance.ValueChanged+=(s,e)=>InvalidateResults("Decision tolerance changed. Run again.");
            FormClosing+=(s,e)=>{if(busy)e.Cancel=true;}; FormClosed+=(s,e)=>{watch.Stop();watch.Dispose();sourceTip.Dispose();session?.Dispose();};
            watch.Tick+=(s,e)=>{try{session.CheckHost();if(revision!=session.Revision){revision=session.Revision;pipes.Clear();surface.Clear();pipeLabel.Text="Reload pipes — model changed";surfaceLabel.Text="Reload surface — model changed";InvalidateResults("Model changed. Reload sources and rerun.");}}catch(Exception ex){run.Enabled=false;export.Enabled=false;status.Text=ex.Message;watch.Stop();}};
            Shown+=(s,e)=>{if(!renderFixture)Safe(RefreshSets);}; DialogTheme.Apply(this,run); status.Text="Select complete pipes in the model or a saved set, load each source, enter your project minimum cover, then check."; export.Enabled=false;if(!renderFixture)watch.Start();
            if(!VerticalClearanceService.GeometrySupported) {run.Enabled=false;status.Text="Vertical geometry is unavailable in this build: matching COM API bridge references are required. Use the Manage 2024 build.";}
        }
        private Control SourcePanel(string title,ComboBox mode,ComboBox set,Label label,bool isPipe)
        {
            string side=isPipe?"Pipe":"Surface";
            var panel=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=4,Padding=new Padding(5),Margin=Padding.Empty};panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
            for(int i=0;i<3;i++)panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));panel.RowStyles.Add(new RowStyle(SizeType.Percent,100));
            panel.Controls.Add(new Label{Text=title,AutoSize=true,Font=new Font("Segoe UI",10,FontStyle.Bold)},0,0);
            var modeRow=new TableLayoutPanel{Dock=DockStyle.Top,AutoSize=true,ColumnCount=3,Margin=Padding.Empty};modeRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));modeRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));modeRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            mode.Items.AddRange(new object[]{"Sets","Standard","Compact"});mode.SelectedIndex=0;mode.Dock=DockStyle.Fill;mode.Name=side+"SourceMode";mode.AccessibleName=side+" selection view";
            var selection=new Button{Text="Load current selection",AutoSize=true}; selection.Click+=(s,e)=>Safe(()=>LoadSource(service.CurrentSelection(),"Model selection",isPipe));
            modeRow.Controls.Add(new Label{Text="Selection view",AutoSize=true,Padding=new Padding(0,5,0,0)},0,0);modeRow.Controls.Add(mode,1,0);modeRow.Controls.Add(selection,2,0);panel.Controls.Add(modeRow,0,1);
            var row=new TableLayoutPanel{Dock=DockStyle.Top,AutoSize=true,ColumnCount=2,Margin=Padding.Empty};row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));row.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            set.Dock=DockStyle.Fill;set.Name=side+"SourceChoice";set.AccessibleName=side+" set or model";set.IntegralHeight=false;set.DropDownHeight=300;
            set.DropDown+=(s,e)=>{int width=set.Width;foreach(var item in set.Items)width=Math.Max(width,TextRenderer.MeasureText(item.ToString(),set.Font).Width+40);set.DropDownWidth=Math.Min(Screen.FromControl(set).WorkingArea.Width-40,width);};
            set.SelectedIndexChanged+=(s,e)=>sourceTip.SetToolTip(set,set.SelectedItem?.ToString()??"");
            sourceTip.SetToolTip(mode,"Sets: saved Selection/Search Sets. Standard: loaded model files. Compact: immediate model-tree child nodes (as in Clash Matrix).");
            mode.SelectedIndexChanged+=(s,e)=>Safe(()=>{if(service!=null)RefreshChoices(mode,set);if(isPipe){pipes.Clear();pipeLabel.Text="Reload pipes";}else{surface.Clear();surfaceLabel.Text="Reload surface";}InvalidateResults("Selection view changed. Load the chosen source and rerun.");});
            var saved=new Button{Text="Load source",AutoSize=true};saved.Click+=(s,e)=>Safe(()=>{var choice=set.SelectedItem as ClashSetChoice;LoadSource(service.FromChoice(choice),choice.Path,isPipe);});
            row.Controls.Add(set,0,0);row.Controls.Add(saved,1,0);panel.Controls.Add(row,0,2);
            label.AutoSize=false;label.AutoEllipsis=true;label.Dock=DockStyle.Fill;panel.Controls.Add(label,0,3);label.TextChanged+=(s,e)=>sourceTip.SetToolTip(label,label.Text);return panel;
        }
        private void RefreshSets()
        { RefreshChoices(pipeMode,pipeSet);RefreshChoices(surfaceMode,surfaceSet); }
        private void RefreshChoices(ComboBox mode,ComboBox choices)
        { var items=service.ReadChoices(Convert.ToString(mode.SelectedItem));choices.Items.Clear();choices.Items.AddRange(items.Cast<object>().ToArray()); }
        private void LoadSource(List<ModelItem> items,string sourceName,bool isPipe)
        {
            session.CheckHost();if(items.Count==0)throw new InvalidOperationException("This source contains no selected items.");
            if(revision!=session.Revision){pipes.Clear();surface.Clear();pipeLabel.Text="Reload pipes — model changed";surfaceLabel.Text="Reload surface — model changed";}
            if(isPipe){pipes=items;pipeSource=sourceName;pipeLabel.Text=items.Count+" pipe root(s) loaded · "+sourceName;}else{surface=items;surfaceSource=sourceName;surfaceLabel.Text=items.Count+" surface root(s) loaded · "+sourceName;}
            revision=session.Revision;InvalidateResults("Sources changed. Run a fresh cover review.");
        }
        private void InvalidateResults(string message)
        { findings.Clear();resultRevision=-1;grid.Rows.Clear();export.Enabled=false;status.Text=message;profile.Invalidate(); }
        private void Run()
        {
            session.CheckHost();
            if(revision!=session.Revision)throw new InvalidOperationException("The model changed. Reload pipe and surface sources before checking.");
            if(!double.TryParse(minimum.Text,NumberStyles.Float,CultureInfo.CurrentCulture,out double required)||double.IsNaN(required)||double.IsInfinity(required)||required<0)
                throw new InvalidOperationException("Enter your project's non-negative minimum cover in the selected units.");
            InvalidateResults("Reading native geometry. The window may pause; a running native read cannot be safely interrupted.");
            busy=true;run.Enabled=false;watch.Stop();UseWaitCursor=true;
            try
            {
                double scale=UnitConversion.ScaleFactor((Units)units.SelectedItem,Units.Meters);
                findings=service.Run(pipes,surface,required*scale,(double)tolerance.Value,message=>{status.Text=message;status.Refresh();});
                resultRevision=session.Revision;Render();export.Enabled=findings.Count>0;
                status.Text=findings.Count+" pipe result(s): "+string.Join(" · ",findings.GroupBy(f=>f.Result.State).Select(g=>g.Count()+" "+g.Key))+". Results are a separate vertical review; native clash tests and source models are unchanged.";
            }
            finally{busy=false;run.Enabled=true;watch.Start();UseWaitCursor=false;}
        }
        private void Render()
        {
            grid.Rows.Clear();double scale=UnitConversion.ScaleFactor(Units.Meters,(Units)(units.SelectedItem??Units.Meters));
            foreach(var f in findings.Where(f=>(f.Pipe+" "+f.Result.State+" "+f.Result.Note).IndexOf(filter.Text,StringComparison.CurrentCultureIgnoreCase)>=0))
            {
                var r=f.Result;Func<double?,string> number=v=>v.HasValue?(v.Value*scale).ToString("0.###",CultureInfo.CurrentCulture):"";
                int index=grid.Rows.Add(f.Pipe,r.State,number(r.Cover),number(r.Required),number(r.Shortfall),number(r.SurfaceLevel),number(r.PipeTop),r.Cover.HasValue?number(r.X):"",r.Cover.HasValue?number(r.Y):"",r.CoveragePercent.ToString("0.##"),r.Note);grid.Rows[index].Tag=f;
                grid.Rows[index].Cells[1].Style.BackColor=r.State.StartsWith("Meets",StringComparison.Ordinal)?Color.Honeydew:r.State.Contains("Insufficient")||r.State.StartsWith("Exposed",StringComparison.Ordinal)?Color.MistyRose:Color.LemonChiffon;
            }
            foreach(var name in new[]{"Cover","Required","Shortfall","Surface","PipeTop","X","Y"}) grid.Columns[name].HeaderText=grid.Columns[name].HeaderText.Split('[')[0].Trim()+" ["+units.SelectedItem+"]";
            profile.Invalidate();
        }
        private VerticalClearanceFinding Selected()=>grid.SelectedRows.Count==0?null:grid.SelectedRows[0].Tag as VerticalClearanceFinding;
        private void CheckFresh(){session.CheckHost();if(resultRevision<0||resultRevision!=session.Revision)throw new InvalidOperationException("Run a fresh vertical cover check first.");}
        private void Export()
        {
            CheckFresh();using(var save=new SaveFileDialog{Filter="CSV (*.csv)|*.csv",FileName="NavPro-vertical-clearance.csv"})if(save.ShowDialog(this)==DialogResult.OK)
            {
                var rows=new List<IEnumerable<string>>{new[]{"Pipe","State","Minimum vertical cover (m)","Required (m)","Decision tolerance (m)","Shortfall (m)","Surface Z (m)","Pipe top Z (m)","X (m)","Y (m)","Coverage %","Pipe source","Surface source","Method","Note"}};
                Func<double?,string> num=v=>v?.ToString("R",CultureInfo.InvariantCulture)??"";
                rows.AddRange(findings.Select(f=>new[]{f.Pipe,f.Result.State,num(f.Result.Cover),num(f.Result.Required),num((double)tolerance.Value),num(f.Result.Shortfall),num(f.Result.SurfaceLevel),num(f.Result.PipeTop),f.Result.Cover.HasValue?num(f.Result.X):"",f.Result.Cover.HasValue?num(f.Result.Y):"",num(f.Result.CoveragePercent),pipeSource,surfaceSource,"World +Z; complete mesh footprint; no horizontal clearance",f.Result.Note}));
                ReviewCsv.Write(save.FileName,rows);status.Text="Exported all "+findings.Count+" reviewed results in metres, with source names and coverage states.";
            }
        }
        private void PaintProfile(object sender,PaintEventArgs e)
        {
            var r=Selected()?.Result;var g=e.Graphics;g.TranslateTransform(profile.AutoScrollPosition.X,profile.AutoScrollPosition.Y);using(var ink=new SolidBrush(DialogTheme.Ink))
            {
                g.DrawString("Selected vertical section\nSchematic — not to scale",Font,ink,12,12);
                if(r?.Cover==null){g.DrawString("Select a measurable result.",Font,ink,new RectangleF(12,75,profile.Width-24,100));return;}
                using(var groundPen=new Pen(Color.SaddleBrown,3))using(var pipePen=new Pen(DialogTheme.Brand,3))using(var arrow=new Pen(Color.Teal,2))
                {
                    int groundY=r.Cover.Value<0?220:120,pipeY=r.Cover.Value<0?120:220;
                    g.DrawLine(groundPen,20,groundY,profile.Width-20,groundY);g.DrawEllipse(pipePen,55,pipeY,100,50);g.DrawLine(arrow,40,120,40,220);g.DrawLine(arrow,35,128,40,120);g.DrawLine(arrow,45,128,40,120);g.DrawLine(arrow,35,212,40,220);g.DrawLine(arrow,45,212,40,220);
                }
                double scale=UnitConversion.ScaleFactor(Units.Meters,(Units)units.SelectedItem);
                g.DrawString("Surface Z: "+(r.SurfaceLevel.Value*scale).ToString("0.###"),Font,ink,20,r.Cover.Value<0?196:96);
                g.DrawString("Pipe top Z: "+(r.PipeTop.Value*scale).ToString("0.###"),Font,ink,20,r.Cover.Value<0?96:196);
                g.DrawString("Vertical cover: "+(r.Cover.Value*scale).ToString("0.###")+" "+units.SelectedItem+"\n"+r.State,Font,ink,new RectangleF(12,325,profile.Width-24,90));
            }
        }
        private void Safe(Action action){try{action();}catch(Exception ex){status.Text=ex.Message;MessageBox.Show(this,ex.Message,Text,MessageBoxButtons.OK,MessageBoxIcon.Warning);}}
    }
}
