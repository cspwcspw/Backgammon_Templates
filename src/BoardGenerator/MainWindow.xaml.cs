using BrushesEx;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using System.Windows.Xps;
using System.Windows.Xps.Packaging;
using Xceed.Wpf.Toolkit;

// Written by Pete, April/May 2022.
// Use WPF to lay out some bits of a backgammon board on a WPF Canvas, then generate image files 
// (vector or pixel) files that can be sent to the print-shop for printing on vinyl, paper, Correx, etc.
// Also provide for overlays to generate point numbering lesson notes, piece configurations, etc.

namespace BoardGenerator
{
    public enum Zone { DarkPoint, LightPoint, Interior, Frame, PlayerSouth, PlayerNorth}
    public partial class MainWindow : Window
    {
        public static Size A5inInches = new Size(5.83, 8.27); // http://www.all-size-paper.com/A5/a5-paper-size.php
        public static Size A4inInches = new Size(8.27, 11.7);
        public static Size A3inInches = new Size(11.69, 16.54);

        public static Size A5_96dpi = new Size(A5inInches.Width * 96.0, A5inInches.Height * 96.0);
        public static Size A4_96dpi = new Size(A4inInches.Width * 96.0, A4inInches.Height * 96.0);
        public static Size A3_Landscape_96dpi = new Size(A3inInches.Height * 96.0, A3inInches.Width * 96.0);

        public static Size A3_300_dpi = new Size(4961, 3508);

        public static Size PaperSize = A4_96dpi;

        DockPanel outerPanel;
        Canvas playground;

        bool wantPiecesShown = true;
        bool wantPointLabels = true;

        BrushManager theBrushman;

        Zone ZoneBrushBeingManipulated = Zone.Interior;
        Dictionary<Zone, Brush> palette;

        Matrix playgroundTransform;  // Controls zoom and dragging / panning of rendered playground relative to the outerPanel
        Point mouseLastSeenAt;       // Used when dragging the playground around. (i.e. it looks like you are dragging the graph)

        IntegerUpDown pointMargin;

        bool wantOverlays = true;


        ContextMenu theContextMenu;

        public MainWindow()
        {
            InitializeComponent();
            // First BlitsDruk Boards. I think the frame is too similar to the other wood
            theBrushman = new BrushManager();
            theBrushman.BrushChanged += TheBrushman_BrushChanged;
            palette = new Dictionary<Zone, Brush>()
            {
                  {Zone.DarkPoint,      theBrushman.GetBrushByName("darkWood1") },
                  {Zone.LightPoint,     theBrushman.GetBrushByName("lightWood1") },
                  {Zone.Frame,          theBrushman.GetBrushByName("LightBrownFelt") },
                  {Zone.Interior,     theBrushman.GetBrushByName("lightMono1") },
                  {Zone.PlayerSouth, Brushes.RosyBrown},
                  {Zone.PlayerNorth, Brushes.Red
                }
            }; 

            makeGui();
            setupContextMenu();
            newBoard();
        }

        private void TheBrushman_BrushChanged(object? sender, Brush e)
        {
            palette[ZoneBrushBeingManipulated] = e;
            newBoard();
        }

        private void setupContextMenu()
        {
            theContextMenu = new ContextMenu();
            MenuItem mi = new MenuItem() { Header = "Change brush" };
            mi.Click += (s, e) => 
            {
                FrameworkElement q = theContextMenu.PlacementTarget as FrameworkElement;
                if (q.Tag != null)
                {
                    Zone whichZone = (Zone)q.Tag;
                    changePaletteAt(whichZone);
                }
            };
            theContextMenu.Items.Add(mi);
        }

        private void makeGui()
        {
            //  I prefer to build my GUI in code.
            Menu main = new Menu() { };
            mainGrid.Children.Add(main);

            MenuItem save = new MenuItem() { Header = "Save" };
            main.Items.Add(save);

            MenuItem designs = new MenuItem() { Header = "Designs" };
            main.Items.Add(designs);

            MenuItem changeBrush = new MenuItem() { Header = "Change Brush" };
            designs.Items.Add(changeBrush);


            MenuItem cbDark = new MenuItem() { Header = "Dark Point" };
            cbDark.Click += (s, e) => { changePaletteAt(Zone.DarkPoint); };
            changeBrush.Items.Add(cbDark);


            MenuItem cbLight = new MenuItem() { Header = "Light Point" };
            cbLight.Click += (s, e) => { changePaletteAt(Zone.LightPoint); };
            changeBrush.Items.Add(cbLight);

            MenuItem cbFrame = new MenuItem() { Header = "Frame" };
            cbFrame.Click += (s, e) => { changePaletteAt(Zone.Frame); };
            changeBrush.Items.Add(cbFrame);

            MenuItem cbInterior = new MenuItem() { Header = "Interior" };
            cbInterior.Click += (s, e) => { changePaletteAt(Zone.Interior); };
            changeBrush.Items.Add(cbInterior);

            designs.Items.Add(new Separator());

            MenuItem design1 = new MenuItem() { Header = "Alt 1" };
            designs.Items.Add(design1);
            design1.Click += (s, e) =>
            {
                palette[Zone.DarkPoint] = theBrushman.GetBrushByName("darkWood1");
                palette[Zone.LightPoint] = theBrushman.GetBrushByName("lightWood1");
                palette[Zone.Frame] = theBrushman.GetBrushByName("DarkMono1");
                palette[Zone.Interior] = theBrushman.GetBrushByName("lightMono1");
                newBoard();
            };

            MenuItem design2 = new MenuItem() { Header = "Alt 2" };
            designs.Items.Add(design2);
            design2.Click += (s, e) =>
            {
                palette[Zone.DarkPoint] = Brushes.LightSalmon;
                palette[Zone.LightPoint] = Brushes.LightGreen;
                palette[Zone.Frame] = theBrushman.GetBrushByName("DarkWood1");
                palette[Zone.Interior] = Brushes.LightYellow;
                newBoard();
            };

            MenuItem design3 = new MenuItem() { Header = "Alt 3" };
            designs.Items.Add(design3);
            design3.Click += (s, e) =>
            {
                palette[Zone.DarkPoint] = Brushes.Black;
                palette[Zone.LightPoint] = Brushes.LightCoral;
                palette[Zone.Frame] = theBrushman.GetBrushByName("DarkWood1");
                palette[Zone.Interior] = theBrushman.GetBrushByName("BeigeFelt");
                newBoard();
            };

            MenuItem help = new MenuItem() { Header = "Help" };
            help.Click += Help_Click;
            main.Items.Add(help);

            MenuItem savePng = new MenuItem() { Header = "Save As png (96 dpi)" };
            savePng.Click += (s, e) => { saveImageOfDesign(new PngBitmapEncoder(), "png", 96); };
            save.Items.Add(savePng);

            MenuItem savePngHiRes = new MenuItem() { Header = "Save As png (300 dpi)" };
            savePngHiRes.Click += (s, e) => { saveImageOfDesign(new PngBitmapEncoder(), "png", 300); };
            save.Items.Add(savePngHiRes);

            MenuItem saveXPS = new MenuItem() { Header = "Save As XPS" };
            saveXPS.Click += btnSaveXPS_Click;
            save.Items.Add(saveXPS);


            MenuItem tools = new MenuItem() { Header = "Tools" };
            main.Items.Add(tools);

            //MenuItem showPieces = new MenuItem() { Header = "Show Pieces" };
            //showPieces.Click += (s, e) => { wantPiecesShown = !wantPiecesShown; newBoard(); };
            //tools.Items.Add(showPieces);

            //MenuItem labelPoints = new MenuItem() { Header = "Label points" };
            //labelPoints.Click += (s, e) => { wantPointLabels = !wantPointLabels; newBoard(); };
            //tools.Items.Add(labelPoints);


            MenuItem offboarding = new MenuItem() { Header = "Toggle overlays annotations" };
            offboarding.Click += (s, e) => { wantOverlays = !wantOverlays; newBoard(); };
            tools.Items.Add(offboarding);
            // tools.Items.Add(newGame);

            //MenuItem showHide = new MenuItem() { Header = "Show / Hide debug panel" };
            //showHide.Click += (object sender, RoutedEventArgs e) =>
            //{ debug.Visibility = (debug.Visibility == Visibility.Visible) ? Visibility.Collapsed : Visibility.Visible; };
            //tools.Items.Add(showHide);


            StackPanel sp = new StackPanel()
            { Orientation = Orientation.Horizontal };
            sp.Children.Add(new Label() { Content = "Inter-point gap" });
            pointMargin = new IntegerUpDown() { Width = 40, Value = 12, Minimum = 0, Maximum = 30, AllowTextInput = true };
            sp.Children.Add(pointMargin);
            Button newGameb = new Button() { Content = "New game", Margin = new Thickness(4, 2, 4, 2) };
            newGameb.Click += (object sender, RoutedEventArgs e) => { newBoard(); };
            sp.Children.Add(newGameb);

            main.Items.Add(sp);


            outerPanel = new DockPanel() { Background = Brushes.LightPink, Margin = new Thickness(0, 28, 0, 0) };
            mainGrid.Children.Add(outerPanel);
            mainGrid.MouseDown += MainGrid_MouseDown;
            mainGrid.MouseMove += MainGrid_MouseMove;
            MouseWheel += Window_MouseWheel;
            playground = new Canvas() { Name="playground", Background = Brushes.LightBlue };
            outerPanel.Children.Add(playground);
            double scale = 0.4;
            this.Width = 890;
            this.Height = 720;

            playgroundTransform = new Matrix(scale, 0, 0, scale, 10, 10);
            playground.RenderTransform = new MatrixTransform(playgroundTransform);
        }

        private void changePaletteAt(Zone zone)
        {
            ZoneBrushBeingManipulated = zone;
            bool accepted = theBrushman.showPickerDialog(palette[zone]);
        }


        void newBoard()
        {
            playground.Children.Clear();
            Canvas theBoard = makeBoard();
            playground.Width = theBoard.Width;
            playground.Height = theBoard.Height;
            playground.Children.Add(theBoard);
        }


        #region Transforms for Pan and Zoom of playground

        private void MainGrid_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                mouseLastSeenAt = e.GetPosition(outerPanel);
            }
        }

        private void MainGrid_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                Point mouseNowAt = e.GetPosition(outerPanel);
                double dx = (mouseNowAt.X - mouseLastSeenAt.X);
                double dy = (mouseNowAt.Y - mouseLastSeenAt.Y);
                mouseLastSeenAt = mouseNowAt;
                playgroundTransform.Translate(dx, dy);
                playground.RenderTransform = new MatrixTransform(playgroundTransform);
                e.Handled = true;
            }
        }

        private void Window_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            Point mPos = e.GetPosition(outerPanel);
            double scale = e.Delta < 0 ? 0.9 : 1.1;   // Zoom in or out
            playgroundTransform.ScaleAt(scale, scale, mPos.X, mPos.Y);
            playground.RenderTransform = new MatrixTransform(playgroundTransform);
        }
        #endregion

        #region Make a full board 

 
        double baseWidth = (A3_Landscape_96dpi.Width - 2 * borderWidth - barWidth) / 12;
        double bdHeight = A3_Landscape_96dpi.Height;
        double bdWidth = A3_Landscape_96dpi.Width;

        const double borderWidth = 30;
        const double barWidth = 90;
       
        double manSz = 60;

        Canvas makeBoard()
        {
            double margin = wantOverlays ? 160 : 0;

            Canvas offBoard = new Canvas() { Name="offBoard", Width = bdWidth + 2 * margin, Height = bdHeight + 2 * margin, Background = Brushes.WhiteSmoke };

            Canvas cnvs = new Canvas() { Name = "Board", Width = bdWidth, Height = bdHeight, Background = palette[Zone.Interior], ContextMenu=theContextMenu, Tag=Zone.Interior};

            createBareBoard(cnvs);

            if (wantOverlays)
            {
                addFullBoardOverlays(cnvs);
            }

            Canvas.SetLeft(cnvs, margin);
            Canvas.SetTop(cnvs, margin);
            offBoard.Children.Add(cnvs);
            return offBoard;
        }

        private void createBareBoard(Canvas cnvs)
        {
            double height = cnvs.Height;
            double width = cnvs.Width;
            for (int i = 0; i < 12; i++)
            {
                double nextX = getXForColumn(i);
                addBdPoint(cnvs, nextX, borderWidth, i % 2 == 0 ? Zone.LightPoint : Zone.DarkPoint, baseWidth - 4, 1);
            }

            for (int i = 0; i < 12; i++)
            {
                double nextX = getXForColumn(i);
                addBdPoint(cnvs, nextX, height-borderWidth, i % 2 == 0 ? Zone.DarkPoint : Zone.LightPoint, baseWidth - 4, -1);
            }

            // Now the border.  A single (but complicated) polygon rather than four border rectangles
            // provides a frame such that the interior brush fill is seamless.  The frame is overlayed on top
            // of the points and background (with little +2 width increases) to hide some base-of-the-point ugliness.
            // The border is described by 12 points, going countclockwise ABCD outside, EFGH on the inner corners of the border,
            // and i,j,k,l is an clockwise cutout that provides the bar. 
            // The G-H points are give a wider frame on the Bar. 

            string a = "0,0 ";
            string b = $"0,{height} ";
            string c = $"{width},{height} ";
            string d = $"{width},0 ";
            string e = $"{borderWidth},{borderWidth + 2} ";
            string f = $"{borderWidth},{height - borderWidth - 2} ";
            string g = $"{width - borderWidth},{height - borderWidth - 2} ";
            string h = $"{width - borderWidth},{borderWidth + 2} ";
            string ix = $"{width/2+barWidth/2},{borderWidth+2} ";
            string j = $"{width / 2 + barWidth / 2},{height-borderWidth - 2} ";
            string k = $"{width / 2 - barWidth / 2},{height - borderWidth - 2} ";
            string l = $"{width / 2 - barWidth / 2},{borderWidth + 2} ";
            Polygon p = new Polygon() { Points = PointCollection.Parse(a + b + c + d + a + e + f + g + h + ix + j + k + l + e), Fill = palette[Zone.Frame], ContextMenu = theContextMenu, Tag = Zone.Frame };
            cnvs.Children.Add(p);
        }

        private double getXForColumn(int i)   // Columns are numbered 0 to 11 from left to right.  The result shows the left margin of the point.
        {
            double magicAdjustment = 3;  // Some arb prettiness for a margin
            double x = borderWidth + i * baseWidth + magicAdjustment; 
            if (i >= 6) x += barWidth;
            return x;
        }

        double getXForPoint(int pointNum)  // Points are numbered 1 to 24 fron South's point of vuew. The result is the left margin of the man on the point
        {
            int col = pointNum >= 13 ? (pointNum - 13) : (12 - pointNum);
            return getXForColumn(col) + baseWidth/2 - manSz/2;
        }

        void addFullBoardOverlays(Canvas cnvs)
        {
            // Add pieces, numbering labels, and so on.
            addMen(cnvs,  24, 2, Zone.PlayerSouth);
            addMen(cnvs, 13, 5, Zone.PlayerSouth);
            addMen(cnvs,  8, 3, Zone.PlayerSouth);
            addMen(cnvs,  6, 5, Zone.PlayerSouth);
            addMen(cnvs,  1, 2, Zone.PlayerNorth);
            addMen(cnvs, 12, 5, Zone.PlayerNorth);
            addMen(cnvs, 17, 3, Zone.PlayerNorth);
            addMen(cnvs, 19, 5, Zone.PlayerNorth);

            addPointNumberingSouth(cnvs, palette[Zone.PlayerSouth], false);
            addPointNumberingNorth(cnvs, palette[Zone.PlayerNorth], true);

            FontFamily fam = new FontFamily("Comic Sans MS");

            Label innerBrown = new Label() { Content = "Brown Inner Board", FontSize = 64, FontWeight=FontWeights.DemiBold, FontFamily=fam };
            Canvas.SetBottom(innerBrown, -150);
            Canvas.SetLeft(innerBrown, getXForPoint(6));
            cnvs.Children.Add(innerBrown);


            Label innerRed = new Label() { Content = "Red Inner Board", FontSize = 64, FontWeight = FontWeights.DemiBold, FontFamily=fam };
            Canvas.SetTop(innerRed, -150);
            Canvas.SetLeft(innerRed, getXForPoint(1));
            innerRed.RenderTransform = new RotateTransform(180, 0, 48);
            cnvs.Children.Add(innerRed);

            Label bar = new Label() { Content = "BAR", FontSize = 64, FontWeight = FontWeights.DemiBold, FontFamily=fam};
            Canvas.SetTop(bar, bdHeight/2+10);
            Canvas.SetLeft(bar, bdWidth/ 2-4);
            bar.RenderTransform = new RotateTransform(270, 0, 48);
            cnvs.Children.Add(bar);
        }

        private void addArrow(Canvas cnvs, double x, double y, bool isLeft)
        {
            double w = 80;
            double w3 = w / 2.7;
            double h = 24;
            double h2 = h / 2;
            double neck = 6;
            // Point[] thePoints = { new Point(0, 0), new Point(w3, -h), new Point(w3, -h2), new Point(w, -h2), new Point(w, h2), new Point(w3, h2), new Point(w3, h) };
            // A bigger arrow that overlaps the piece and has a narrow neck
            Point[] thePoints = { new Point(0, 0), new Point(w3, -h), new Point(w3-5,-neck),  
          
                new Point(w, -h2), new Point(w, h2),  new Point(w3-5,neck),  new Point(w3,h),
        
                new Point(w3, h) };
            PointCollection pts = new PointCollection(thePoints);
            Polygon pg = new Polygon()
            {
                Stroke = Brushes.Black,
                StrokeThickness = 2,
                Fill = Brushes.Cyan,
                Points = pts,
            };

            Canvas.SetTop(pg, y);
            if (isLeft)
            {
                Canvas.SetLeft(pg, x-w/2-10);
            }
            else
            {
                RotateTransform rt = new RotateTransform(180, 0, 0);
                Canvas.SetLeft(pg, x+w);
                pg.RenderTransform = rt;
            }
            cnvs.Children.Add(pg);
        }
 
        private void addPointNumberingSouth(Canvas cnvs, Brush playerBrush, bool offBoard)
        {
            double h = cnvs.Height;
             for (int pt=1; pt <=24; pt++)
            {
                double y = (pt <= 12) ? h/2 + 90 : h / 2 - 90 - 32;
                if (offBoard)
                {
                    y = (pt <= 12) ? h + 5 : -115;
                }
                double x = getXForPoint(pt);
                TextBlock lbl = new TextBlock()
                {
                    Text = pt.ToString(),
               //     Background = playerBrush,
                    Foreground = Brushes.Black,
                    FontSize = 32,
                    FontWeight = FontWeights.Bold,
                    Padding = new Thickness(2, 0, 2, 0),
                    TextAlignment = TextAlignment.Center,
                    Width=50
                };
          
                Canvas.SetLeft(lbl, x);
                Canvas.SetTop(lbl, y);
                cnvs.Children.Add(lbl);
            }
        }

        private void addPointNumberingNorth(Canvas cnvs, Brush playerBrush, bool offBoard)
        {
            double h = cnvs.Height;
            for (int pt = 1; pt <= 24; pt++)
            {
                double y = (pt <= 12) ? h / 2 + 90 : h / 2 - 90;
                if (offBoard)
                {
                    y = (pt <= 12) ? h + 65 : -10;
                }
                double x = getXForPoint(pt);
                TextBlock lbl = new TextBlock()
                {
                    Text = (25 - pt).ToString(),
                    //    Background = playerBrush,
                    Foreground = Brushes.Black,
                    FontSize = 32,
                    FontWeight = FontWeights.Bold,
                    Padding = new Thickness(2, 0, 2, 0),
                    TextAlignment = TextAlignment.Center,

                    Width = 50,
                    //Height = 60
                };

                lbl.RenderTransform = new RotateTransform(180, 25, 30);
                Canvas.SetLeft(lbl, x);
                Canvas.SetTop(lbl, y - 60);
                cnvs.Children.Add(lbl);
            }
        }


        private void addMen(Canvas cnvs, int pointNum, int numMen, Zone paletteIndex)
        {
            double x = getXForPoint(pointNum);  
            double y = pointNum >= 13 ? borderWidth : bdHeight - borderWidth - manSz;
            double ydelta = pointNum >= 13 ? manSz : -manSz;
            bool arrowLeft = paletteIndex == Zone.PlayerNorth;
            
            if (pointNum >= 13)
            {
                arrowLeft = !arrowLeft;
            }
       
            for (int man=0; man<numMen; man++)
            {
                Ellipse theMan = new Ellipse() { Width = manSz, Height = manSz, 
                    Stroke = Brushes.Black, StrokeThickness=3, Fill = palette[paletteIndex],
                    ContextMenu = theContextMenu,
                    Tag = paletteIndex
                };
                Canvas.SetLeft(theMan, x-3);   // don't know why this is a little off
                Canvas.SetTop(theMan, y);
                cnvs.Children.Add(theMan);
                addArrow(cnvs, x + manSz/5, y+manSz/2, arrowLeft);
                y += ydelta;
            }
        }
        private void addBdPoint(Canvas cnvs, double left, double top, Zone paletteZone, double baseWidth, double flipY)
        {
            double pointHeight = baseWidth * 3.3 * flipY;
            double eGap = (double)pointMargin.Value / 2.0;
            // Give this an extra point at the top for a slightly rounded look
            Point[] thePoints = { new Point(eGap, 0), new Point(baseWidth - 2 * eGap, 0), new Point(baseWidth / 2 + 0.5, pointHeight), new Point(baseWidth / 2 - 0.5, pointHeight) };
            PointCollection pts = new PointCollection(thePoints);
            Polygon pg = new Polygon()
            {
                Stroke = Brushes.Black,
                StrokeThickness = 6,
                Fill = palette[paletteZone],
                Points = pts,
                StrokeLineJoin = PenLineJoin.Round,
                ContextMenu = theContextMenu,
                Tag = paletteZone
            };

            cnvs.Children.Add(pg);
            Canvas.SetLeft(pg, left);
            Canvas.SetTop(pg, top);

            Polygon pg2 = new Polygon()
            {
                Stroke = Brushes.LightGoldenrodYellow,
                StrokeThickness = 2.5,
                Fill = palette[paletteZone],
                Points = pts,
                StrokeLineJoin = PenLineJoin.Round,
                ContextMenu = theContextMenu,
                Tag = paletteZone
            };
            cnvs.Children.Add(pg2);
            Canvas.SetLeft(pg2, left);
            Canvas.SetTop(pg2, top);
        }

        #endregion;

        private void saveImageOfDesign(BitmapEncoder encoder, string extension, double targetSaveDpi)
        {
            // WPF units are 96dpi based.  Larget targetSaveDpi gives better quality for professional printing
            Cursor savedCursor = Cursor;
            Cursor = Cursors.Wait;
            RenderTargetBitmap rtb =
               new RenderTargetBitmap((int)Math.Round(playground.Width * targetSaveDpi / 96.0),
                                      (int)Math.Round(playground.Height * targetSaveDpi / 96.0),
                                         targetSaveDpi,
                                         targetSaveDpi,
                                         System.Windows.Media.PixelFormats.Default);

            // Ensure we remove effects of current rendering transform
            playground.RenderTransform = new MatrixTransform();
            Application.Current.Dispatcher.Invoke(DispatcherPriority.Background, () => { });

            rtb.Render(playground);
            encoder.Frames.Add(BitmapFrame.Create(rtb));
            string timestamp = DateTime.Now.ToString("yyyyMMddHHmmss"); https://docs.microsoft.com/en-us/dotnet/standard/base-types/custom-date-and-time-format-strings
            string filename = $"C:\\temp\\Backgammon_{timestamp}.{extension}";
            System.IO.Stream stm = System.IO.File.Create(filename);
            encoder.Save(stm);
            stm.Close();
            Cursor = savedCursor;
            System.Windows.MessageBox.Show("File saved to " + filename, "Done");
            // Fix the playground transform back to what it should be
            playground.RenderTransform = new MatrixTransform(playgroundTransform);
        }



        private void btnSaveXPS_Click(object sender, RoutedEventArgs e)
        {
            // http://ericsink.com/wpf3d/B_Printing.html
            string timestamp = DateTime.Now.ToString("yyyyMMddHHmmss");
            string filename = $"C:\\temp\\Backgammon_{timestamp}.xps";

            Canvas board = makeBoard();
            Size sz = new Size(board.Width, board.Height);
            FixedDocument doc = new FixedDocument();
     
            doc.DocumentPaginator.PageSize = sz;

            PageContent page = new PageContent();
            FixedPage fixedPage = CreateOneFixedPage(sz, board);
            ((IAddChild)page).AddChild(fixedPage);
            doc.Pages.Add(page);

            XpsDocument xpsd = new XpsDocument(filename, System.IO.FileAccess.Write);
            XpsDocumentWriter xw = XpsDocument.CreateXpsDocumentWriter(xpsd);
            xw.Write(doc);
            xpsd.Close();

            System.Windows.MessageBox.Show("File saved to " + filename, "Done");
        }

        private FixedPage CreateOneFixedPage(Size sz, Canvas board)
        {    
            FixedPage page = new FixedPage();
            page.Background = Brushes.White;
            page.Width = sz.Width;
            page.Height = sz.Height;
            FixedPage.SetLeft(board, 0);
            FixedPage.SetTop(board, 0);
            page.Children.Add((UIElement)board);
            page.Measure(sz);
            page.Arrange(new Rect(new Point(), sz));
            page.UpdateLayout();
            return page;
        }


        private void Help_Click(object sender, RoutedEventArgs e)
        {
            string helpText =
              //  "Right-click on point, background, or border allows you to pick another texture.\n" +
                "\nUse the Mouse Wheel to zoom in or out." +
                "\nDragging the mouse while holding down the Right Mouse Button moves everything.";


            System.Windows.MessageBox.Show(helpText, "Planarity help:");
        }

    }
}
