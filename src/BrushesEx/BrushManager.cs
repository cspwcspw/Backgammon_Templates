using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Resources;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

// Pete, April 2022.

namespace BrushesEx
{
    public class BrushManager
    {

        public event EventHandler<string> BrushChanged = null;

        private Dictionary<string, string> brushNameCache = new Dictionary<string, string>();

        private List<string> brushNames = new List<string>();

        public int getIndexOf(string brushName)
        {
            return brushNames.IndexOf(brushName);
        }

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

        public bool showPickerDialog(string initialBrush)
        {
            BrushPickerDialog picker = new BrushPickerDialog(this, initialBrush);
            picker.BrushChanged += Picker_BrushChanged;
            bool? response = picker.ShowDialog();
            if (response == true)
            {
                return true;
            }
            else
            {
                BrushChanged?.Invoke(this, initialBrush);
                return false;
            }
        }


        private void Picker_BrushChanged(object? sender, string e)
        {
            // Bubble event up to our listeners 
            BrushChanged?.Invoke(sender, e);
        }

        // Return a new instance of a texture brush, or a standard brush).  Because brushes can be animated, or the caller might want to 
        // fiddle with the viewport, or freeze the brush, etc. we don't cache and reuse the same object.
        public Brush GetBrushByName(string shortName)
        {
            if (shortName.StartsWith('#')) // this is a HTML hex description of a solid brush.
            {
                SolidColorBrush br = new SolidColorBrush((Color)new ColorConverter().ConvertFrom(shortName));
                return br;
            }


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
                SolidColorBrush b1 = conv.ConvertFromString(lcName) as SolidColorBrush;
                return b1;
            }
            catch
            {
                return Brushes.Gray;
            }
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
}