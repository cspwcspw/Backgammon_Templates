
using BrushesEx;
using Microsoft.Win32;
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

// This is part of the main window class, it just reduces the size of the file and helpts a bit with management.
// Without the need for real design or abstraction :-(

namespace BoardGenerator
{
    public partial class MainWindow : Window
    {
        enum ArrowOptions { None, Topmost, All };

        ArrowOptions arrowOption = ArrowOptions.All;
        MenuItem showOverlays;

        #region Main GUI construction
        private ContextMenu setupContextMenu(bool allowBGPic)
        {
            ContextMenu mnu = new ContextMenu();
            MenuItem mi = new MenuItem() { Header = "Change brush" };
            mi.Click += (s, e) =>
            {
                MenuItem theItem = s as MenuItem;
                ContextMenu theMenu = theItem.Parent as ContextMenu;
                FrameworkElement q = theMenu.PlacementTarget as FrameworkElement;
                if (q.Tag != null)
                {
                    Zone whichZone = (Zone)q.Tag;
                    changePaletteAt(whichZone);
                }
            };
            mnu.Items.Add(mi);

            if (allowBGPic)  // add more stuff
            {
                MenuItem miBackgroundPic = new MenuItem() { Header = "Change background pic" };
                miBackgroundPic.Click += (s, e) => {
                    //  MenuItem theItem = s as MenuItem;
                    //  ContextMenu theMenu = theItem.Parent as ContextMenu;
                    loadImageToBackground();
                };
                mnu.Items.Add(miBackgroundPic);
            }
            return mnu;
        }

        private void loadImageToBackground()
        {
            OpenFileDialog dlg = new OpenFileDialog();
            dlg.InitialDirectory = "c:\\";
            dlg.Filter = "Image files (*.jpg)|*.jpg|All Files (*.*)|*.*";
            dlg.RestoreDirectory = true;


            if (dlg.ShowDialog() == true)
            {
                string selectedFileName = dlg.FileName;
              //  FileNameLabel.Content = selectedFileName;
                BitmapImage bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri(selectedFileName);
                bitmap.EndInit();

                backgroundImageViewer.Source = bitmap;
            }
        }

        private void makeGui(List<Palette> knownPalettes)
        {
            //  I prefer to build my GUI in code.
            Menu main = new Menu() {  };
            mainGrid.Children.Add(main);

            MenuItem save = new MenuItem() { Header = "Save" };
            main.Items.Add(save);

            MenuItem designs = new MenuItem() { Header = "Designs" };
            main.Items.Add(designs);

            MenuItem toClipboard = new MenuItem() { Header = "Copy this colour scheme to clipboard" };
            designs.Items.Add(toClipboard);
            toClipboard.Click += (s, e) =>
            {
                activePalette.CopyCodeToClipboard();
            };


            designs.Items.Add(new Separator());

            for (int i=0; i < knownPalettes.Count; i++)
            {
                MenuItem design = new MenuItem() { Header = knownPalettes[i].Name, Tag=knownPalettes[i] };
                designs.Items.Add(design);
                design.Click += (s, e) =>
                {
                    MenuItem itm = s as MenuItem;
                    activePalette = itm.Tag as Palette;
                    
                    newBoard();
                };
            }

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


            showOverlays = new MenuItem() { Header = "Show overlays", IsCheckable = true, IsChecked = true };
            showOverlays.Click += (s, e) => { newBoard(); };
            tools.Items.Add(showOverlays);
            tools.Items.Add(new Separator());

            // Test
            ComboBox cb = new ComboBox() { Width = 120, SelectedIndex = 2 };
            cb.Items.Add("No Arrows");
            cb.Items.Add("Topmost Arrows");
            cb.Items.Add("All Arrows");
            cb.SelectionChanged += (s, e) =>
            {
             //   ComboBox cbs = ;
                arrowOption = (ArrowOptions)(s as ComboBox).SelectedIndex;
                tools.IsSubmenuOpen = false; // Close the menu
                newBoard();
            };

           // MenuItem wrapper = new MenuItem() { Header = cb };
            tools.Items.Add(cb);

            MenuItem test = new MenuItem() { Header = "Test" };
            test.Click += (s, e) => {
                string dump = XamlWriter.Save(main);
                Clipboard.SetData(DataFormats.Text, dump);
            };
            tools.Items.Add(test);

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
            playground = new Canvas() { Name = "playground", Background = Brushes.LightBlue };
            outerPanel.Children.Add(playground);
            double scale = 0.4;
            this.Width = 890;
            this.Height = 720;

            playgroundTransform = new Matrix(scale, 0, 0, scale, 10, 10);
            playground.RenderTransform = new MatrixTransform(playgroundTransform);
        }

        #endregion

        #region Save to PNG or XPS

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
                "\nDragging the mouse while holding down the Middle Mouse Button (i.e. the scroll wheel) moves everything.";


            System.Windows.MessageBox.Show(helpText, "Planarity help:");
        }

        #endregion

    }
}
