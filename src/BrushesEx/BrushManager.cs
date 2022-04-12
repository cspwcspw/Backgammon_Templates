using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Resources;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Xceed.Wpf.Toolkit;


// Pete, April 2022.

namespace BrushesEx
{
    public class BrushManager
    {
        public event EventHandler<Brush> BrushChanged = null;

       // public Brush LastPickedBrush { get; private set; }


        private Dictionary<string, string> brushNameCache = new Dictionary<string, string>();

        private List<string> brushNames = new List<string>();



        public List<string> TBrushes
        {
            get
            {
                if (brushNameCache.Count == 0) // If this is first use, do some lazy setup
                {
                    BuildCache();
                }
                return brushNames;
            }
        }

        public BrushManager()
        {

        }


        //Color lastPickedColour = Colors.LightGreen;
        //int lastPickedBrushIndex = 3;
        public bool showPickerDialog(Brush currBrush)
        {
       
            BrushPicker picker = new BrushPicker(this, currBrush);
            picker.BrushChanged += Picker_BrushChanged;
            bool? response = picker.ShowDialog();
            if (response == true)
            {
              //  lastPickedColour = picker.SolidColor;
              //  lastPickedBrushIndex = picker.TextureIndex;
                return true;
            }
            else
            {
                BrushChanged?.Invoke(this, currBrush);
                return false;
            }
        }

        private void Picker_BrushChanged(object? sender, Brush e)
        {
            // Bubble event up to our listeners 
            BrushChanged?.Invoke(sender, e);
        }

        // Return a new instance of a texture brush (or a standard brush).  Because brushes can be animated, or the caller might want to 
        // fiddle with the viewport, freeze the brush, etc. we don't cache and reuse the same object.
        public Brush GetBrushByName(string shortName)
        {
            if (brushNameCache.Count == 0)  // If this is first use, do some lazy setup
            {
                BuildCache();
            }

            string lcName = shortName.ToLower();
            if (brushNameCache.ContainsKey(lcName))
            {
                string fullName = brushNameCache[lcName];
                BitmapImage bmi = loadResourceImage(fullName);
                Brush result = makeTextureBrush(bmi);
                return result;
            }
            // If we don't know the name of the brush, try to find a standard brush of this name,
            // so that "magenta" also works.
            BrushConverter conv = new BrushConverter();
            try
            {
                Brush b1 = conv.ConvertFromString(lcName) as SolidColorBrush;
                return b1;
            }
            catch
            {
                return Brushes.Gray;
            }
        }

        public Brush GetBrushByIndex(int bNum)
        {
            return GetBrushByName(brushNames[bNum]);
        }

        public List<Brush> GetBrushesByName(IEnumerable<string> names)
        {
            List<Brush> result = new List<Brush>();
            foreach (string s in names)
            {
                result.Add(GetBrushByName(s));
            }
            return result;
        }

        public List<Brush> GetBrushesByIndex(IEnumerable<int> bNums)
        {
            List<Brush> result = new List<Brush>();
            foreach (int bNum in bNums)
            {
                result.Add(GetBrushByName(brushNames[bNum]));
            }
            return result;
        }

        public List<Brush> GetBrushes(params string[] names)
        {
            return GetBrushesByName(names);
        }

        public List<Brush> GetBrushes(params int[] bNums)
        {
            return GetBrushesByIndex(bNums);
        }

        private void BuildCache()
        {
            if (brushNameCache.Count != 0) return;
            List<string> fullNames = GetResourceNames();
            int magicN = "textures/".Length;
            foreach (string fullName in fullNames)
            {
                int pos = fullName.IndexOf('.'); // Also strip off any extension / type of the file
                string shortName = fullName.Substring(magicN, pos - magicN); // 
                brushNameCache.Add(shortName, fullName);
                brushNames.Add(shortName);
            }
        }

        private static List<string> GetResourceNames()
        {
            var asm = Assembly.GetExecutingAssembly();
            string resName = asm.GetName().Name + ".g.resources";
            using (Stream stream = asm.GetManifestResourceStream(resName))
            {
                using (ResourceReader reader = new System.Resources.ResourceReader(stream))
                {
                    List<string> names = new List<string>();
                    foreach (DictionaryEntry de in reader)
                    {
                        // For reasons I don't understand, these entries are all in lowercase
                        string name = (string)de.Key;
                        if (name.StartsWith("textures"))
                        {
                            names.Add(name);
                        }
                    }
                    return names;
                }
            }
        }

        static private Brush makeTextureBrush(BitmapImage bmi)
        {
            ImageBrush result = new ImageBrush()
            {
                ImageSource = bmi,
                Stretch = Stretch.None,
                TileMode = TileMode.Tile,
                ViewportUnits = BrushMappingMode.Absolute,
                Viewport = new Rect(0, 0, bmi.PixelWidth, bmi.PixelHeight)
            };
            return result;
        }

        static private BitmapImage loadResourceImage(string src)
        {
            // BEEG DEAL.
            // WinForms needs Build Action = "Embedded resource" which must be loaded via a Resource Manager.
            // But WPF needs Build Action = "Resource" and is loaded differently.
            // if you get this subtle thing wrong, the wheels will fall off.
            //   ImageService;Component/
            Uri uri = new Uri($"pack://application:,,,/BrushesEx;Component/{src}");
            BitmapImage bmi = new BitmapImage(uri);
            return bmi;
        }

    }




    class BrushPicker : Window
    {

        public event EventHandler<Brush> BrushChanged = null;

        public Brush SolidBrush { get; private set; }
     //   public Color SolidColor { get; private set; }

    //    public Brush TextureBrush { get; private set; }
         public int tChoiceIndex { get; private set; }

        BrushManager parent;


        List<Border> textureBorders = new List<Border>();  // Keep track

        public BrushPicker(BrushManager parent, Brush curr)
        {
            this.parent = parent;
            Width = 380;
            Height = 500;

            SolidColorBrush scb = curr as SolidColorBrush;
            bool isSolidBrush = scb != null;
      //      tChoiceIndex = initialTextureIndex;

            Canvas result = new Canvas() { HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Stretch, Background = Brushes.LightGray };
            Content = result;
            Label lbl = new Label { Content = "Solid Brushes ... " };
            result.Children.Add(lbl);

            ColorPicker picker = new ColorPicker() { Width = 130 };
            // SolidColor = initialColor;
            //  SolidBrush = new SolidColorBrush(SolidColor);
            if (isSolidBrush)
            {
                picker.SelectedColor = scb.Color;
            }
            picker.SelectedColorChanged += Picker_SelectedColorChanged;

            Canvas.SetLeft(picker, 20);
            Canvas.SetTop(picker, 30);
            result.Children.Add(picker);


            Label lbl2 = new Label { Content = "Texture Brushes ... " };
            Canvas.SetTop(lbl2, 80);
            result.Children.Add(lbl2);

            Border brd = new Border() { BorderBrush = Brushes.Gray, BorderThickness = new Thickness(2), Margin = new Thickness(0), HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Stretch };

            Canvas.SetLeft(brd, 20);
            Canvas.SetTop(brd, 110);
            result.Children.Add(brd);

            this.MinHeight = Canvas.GetTop(brd) + 60;
            this.MinWidth = Canvas.GetLeft(brd) + 30;

            ScrollViewer scroller = new ScrollViewer()
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Visible,
                Background = Brushes.Magenta
            };


            WrapPanel textureWrapPanel = new WrapPanel() { MaxWidth = this.Width - 2 * Canvas.GetLeft(brd) - SystemParameters.VerticalScrollBarWidth }; //  Width = 300,
            scroller.Content = textureWrapPanel;
            brd.Child = scroller;

            this.SizeChanged += (s, e) =>
            {
                textureWrapPanel.MaxWidth = this.Width - 2 * Canvas.GetLeft(brd) - SystemParameters.VerticalScrollBarWidth;
                textureWrapPanel.MaxHeight = this.Height - Canvas.GetTop(brd);
            };


            // Populate the available brushes into the canvas hosted by TabItem
            for (int b = 0; b < parent.TBrushes.Count; b++)
            {
                string tName = parent.TBrushes[b];

                Border bd = new Border() { BorderBrush = Brushes.Gray, BorderThickness = new Thickness(3), Margin = new Thickness(4) };

                Rectangle rect = new Rectangle() { Height = 48, Width = 48, Fill = parent.GetBrushByName(tName), Tag = b };
                rect.MouseDown += Rect_MouseDown;
                bd.Child = rect;
                textureWrapPanel.Children.Add(bd);
                textureBorders.Add(bd);
            }

           // highlightNexTextureChoice(tChoiceIndex);

           AddButton(result, "Apply Change", 250, 10, (s, e) => { DialogResult = true; Close(); }).IsDefault=true;  
           AddButton(result, "Cancel", 200, 10, (s, e) => { DialogResult = false; Close(); }).IsCancel = true;
        
        }

        private void Picker_SelectedColorChanged(object sender, RoutedPropertyChangedEventArgs<Color?> e)
        {
            Color SolidColor = (Color)e.NewValue;
            SolidBrush = new SolidColorBrush(SolidColor);
            BrushChanged?.Invoke(this, SolidBrush);
        }

        private Button AddButton(Panel pnl, string header, double x, double y, RoutedEventHandler clickHandler) // Action<object, RoutedEventArgs> clickHandler)
        {
            Button btn = new Button() { Content = header, Margin = new Thickness(x, y, 0, 0), Padding = new Thickness(4, 4, 4, 4) };
            btn.Click += clickHandler;
            pnl.Children.Add(btn);
            return btn;
        }


        private Button MakeButton(Canvas cnvs, string header, double x, double y)
        {
            Button btn = new Button() { Content = header, Padding = new Thickness(4, 4, 4, 4) };
            Canvas.SetLeft(btn, x);
            Canvas.SetTop(btn, y);
            cnvs.Children.Add(btn);
            return btn;
        }

        private void Rect_MouseDown(object sender, MouseButtonEventArgs e)
        {
            Rectangle rect = sender as Rectangle;
            int b = (int)rect.Tag;
            highlightNexTextureChoice(b);
        }

        void highlightNexTextureChoice(int newIndex)
        {
            textureBorders[tChoiceIndex].BorderBrush = Brushes.Gray;
            tChoiceIndex = newIndex;
            textureBorders[tChoiceIndex].BorderBrush = Brushes.Red;
            Brush TextureBrush = parent.GetBrushByIndex(tChoiceIndex);
            BrushChanged?.Invoke(this, TextureBrush);
        }
    }
}
