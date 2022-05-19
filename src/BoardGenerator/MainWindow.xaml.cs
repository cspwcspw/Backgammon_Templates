using BrushesEx;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
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
    //    Dictionary<Zone, WrappedBrush> oldPalette;

        Palette activePalette;

        Matrix playgroundTransform;  // Controls zoom and dragging / panning of rendered playground relative to the outerPanel
        Point mouseLastSeenAt;       // Used when dragging the playground around. (i.e. it looks like you are dragging the graph)

        IntegerUpDown pointMargin;

        Image backgroundImageViewer;

        ContextMenu theContextMenuFore;
        ContextMenu theContextMenuBack;

        public MainWindow()
        {
            InitializeComponent();
            // First BlitsDruk Boards. I think the frame is too similar to the other wood
            theBrushman = new BrushManager();
            theBrushman.BrushChanged += brushChanged;

            // Set up some pre-designed options
            List<Palette> knownPalettes = new List<Palette>();
            knownPalettes.Add(new Palette("default", "Red", "Brown", new string[] { "LightSalmon", "LightGreen", "LightYellow", "beigefelt", "Red", "#FFA0522D" }));

            knownPalettes.Add(new Palette("BlitsDruk", "Brown", "Red", new string[] { "darkWood1", "lightWood1", "lightmono1", "mediumwood1", "RosyBrown", "Red" }));
            knownPalettes.Add(new Palette("alt 1", "Red", "Blue", new string[] { "darkWood1", "lightWood1", "lightMono1", "DarkMono1", "Red", "Blue" }));
            knownPalettes.Add(new Palette("alt2", "Red", "Blue", new string[] { "LightSalmon", "LightGreen", "LightYellow", "beigefelt", "Red", "#FFA0522D" }));

            knownPalettes.Add(new Palette("dark", "Red", "Brown", new string[] { "black", "LightCoral", "BeigeFelt", "DarkWood1", "Red", "RosyBrown" }));


            activePalette = knownPalettes[0];

            makeGui(knownPalettes);
            theContextMenuFore = setupContextMenu(false);
            theContextMenuBack = setupContextMenu(true);
            newBoard();
        }

        private void brushChanged(object? sender, string e)
        {
            activePalette[ZoneBrushBeingManipulated] = e;
            newBoard();
        }

        private void changePaletteAt(Zone zone)
        {
            ZoneBrushBeingManipulated = zone;
            bool accepted = theBrushman.showPickerDialog(activePalette[zone]);
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
            if (e.MiddleButton == MouseButtonState.Pressed)
            {
                mouseLastSeenAt = e.GetPosition(outerPanel);
            }
        }

        private void MainGrid_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.MiddleButton == MouseButtonState.Pressed)
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

        #region Make the board 


        double baseWidth = (A3_Landscape_96dpi.Width - 2 * borderWidth - barWidth) / 12;
        double bdHeight = A3_Landscape_96dpi.Height;
        double bdWidth = A3_Landscape_96dpi.Width;

        const double borderWidth = 30;
        const double barWidth = 90;
       
        double manSz = 60;

        Canvas makeBoard()
        {
            double margin = showOverlays.IsChecked ? 160 : 0;

            Canvas offBoard = new Canvas() { Name="offBoard", Width = bdWidth + 2 * margin, Height = bdHeight + 2 * margin, Background = Brushes.WhiteSmoke };

          //  Canvas cnvs = new Canvas() { Name = "Board", Width = bdWidth, Height = bdHeight, Background = oldPalette[Zone.Interior].TheBrush, ContextMenu=theContextMenuBack, Tag=Zone.Interior};
            Canvas cnvs = new Canvas() { Name = "Board", Width = bdWidth, Height = bdHeight, Background = theBrushman.GetBrushByName(activePalette[Zone.Interior]), ContextMenu = theContextMenuBack, Tag = Zone.Interior };

            //// Add a background image with some transparency. get context menu clicks propagating
            //BitmapImage bitmap = new BitmapImage();
            //bitmap.BeginInit();
            //bitmap.UriSource = new Uri("C:\\temp\\background.jpg");
            //bitmap.EndInit();
            //backgroundImageViewer = new Image() { Width = 1200, Height = 800, Source = bitmap };
            //Canvas.SetLeft(backgroundImageViewer, 100);
            //Canvas.SetTop(backgroundImageViewer, 200);
            //cnvs.Children.Add(backgroundImageViewer);
            //backgroundImageViewer.Opacity = 0.3;

            createBareBoard(cnvs);

            if (showOverlays.IsChecked)    
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
            Polygon p = new Polygon() { Points = PointCollection.Parse(a + b + c + d + a + e + f + g + h + ix + j + k + l + e), Fill =
                Background = theBrushman.GetBrushByName(activePalette[Zone.Frame]), ContextMenu = theContextMenuFore, Tag = Zone.Frame };
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

           addPointNumberingSouth(cnvs, theBrushman.GetBrushByName(activePalette[Zone.PlayerSouth]), false);
           addPointNumberingNorth(cnvs, theBrushman.GetBrushByName(activePalette[Zone.PlayerNorth]), true);

            FontFamily fam = new FontFamily("Comic Sans MS");

            Label innerBrown = new Label() { Content = $"{activePalette.SouthName} Inner Board", FontSize = 64, FontWeight=FontWeights.DemiBold, FontFamily=fam };
            Canvas.SetBottom(innerBrown, -150);
            Canvas.SetLeft(innerBrown, getXForPoint(6));
            cnvs.Children.Add(innerBrown);


            Label innerRed = new Label() { Content = $"{activePalette.NorthName} Inner Board", FontSize = 64, FontWeight = FontWeights.DemiBold, FontFamily=fam };
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

            Brush fillBrush = theBrushman.GetBrushByName(activePalette[paletteIndex]);


            if (pointNum >= 13)
            {
                arrowLeft = !arrowLeft;
            }
       
            for (int man=0; man<numMen; man++)
            {
                Ellipse theMan = new Ellipse() { Width = manSz, Height = manSz, 
                    Stroke = Brushes.Black, StrokeThickness=3, Fill = fillBrush,
                    ContextMenu = theContextMenuFore,
                    Tag = paletteIndex
                };
                Canvas.SetLeft(theMan, x-3);   // don't know why this is a little off
                Canvas.SetTop(theMan, y);
                cnvs.Children.Add(theMan);

                bool mustAddArrow =  arrowOption == ArrowOptions.All || (arrowOption == ArrowOptions.Topmost && man == numMen - 1);

                if (mustAddArrow)
                {
                    Matrix transform = new Matrix();
                    if (pointNum == 12)
                    {
                        transform.Rotate(-45);
                    }
                    else if (pointNum == 13)
                    {
                        transform.Scale(1, -1);
                        transform.Rotate(45);
                    }
                    else if (pointNum < 12)
                    {
                        transform.Scale(1, -1);
                    }

                    UIElement theArrow = mkArrow(fillBrush);  //  

                    Canvas.SetTop(theArrow, y + manSz / 2);
                    if (arrowLeft)
                    {
                        transform.Scale(-1, 1);
                        Canvas.SetLeft(theArrow, x - 4);
                    }
                    else
                    {
                        Canvas.SetLeft(theArrow, x + manSz);
                    }

                    theArrow.RenderTransform = new MatrixTransform(transform);
                    cnvs.Children.Add(theArrow);
                }
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
              //  Fill = oldPalette[paletteZone].TheBrush,
                Fill = theBrushman.GetBrushByName(activePalette[paletteZone]),
                Points = pts,
                StrokeLineJoin = PenLineJoin.Round,
                ContextMenu = theContextMenuFore,
                Tag = paletteZone
            };

            cnvs.Children.Add(pg);
            Canvas.SetLeft(pg, left);
            Canvas.SetTop(pg, top);

            Polygon pg2 = new Polygon()
            {
                Stroke = Brushes.LightGoldenrodYellow,
                StrokeThickness = 2.5,
                //   Fill = oldPalette[paletteZone].TheBrush,
                Fill = theBrushman.GetBrushByName(activePalette[paletteZone]),
                Points = pts,
                StrokeLineJoin = PenLineJoin.Round,
                ContextMenu = theContextMenuFore,
                Tag = paletteZone
            };
            cnvs.Children.Add(pg2);
            Canvas.SetLeft(pg2, left);
            Canvas.SetTop(pg2, top);
        }

         private UIElement mkArrow(Brush fillBrush)
        {  // https://docs.microsoft.com/en-us/dotnet/desktop/wpf/graphics-multimedia/path-markup-syntax?view=netframeworkdesktop-4.8
            string arrow = "M 2,15 L 4,15 C 20,37 42,35 63,25 L58,18 90,9 79,41 72,34 C 30,50 3,37 0,20 L 2,15";     
 
            Geometry pg = PathGeometry.Parse(arrow);
            Path result = new Path() { Data = pg, Stroke = Brushes.Black, StrokeThickness = 3, Fill = fillBrush };
            return result;
        }

        #endregion

    }
}
