using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using Xceed.Wpf.Toolkit;

namespace BrushesEx
{
    class BrushPickerDialog : Window
    {

        public event EventHandler<string> BrushChanged = null;

        public int tChoiceIndex { get; private set; }

        BrushManager parent;

        List<Border> textureBorders = new List<Border>();  // Keep track of rectangles created on the GUI

        public BrushPickerDialog(BrushManager parent, string initialBrushName)
        {
            this.parent = parent;
            Width = 380;
            Height = 500;

            Canvas result = new Canvas() { HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Stretch, Background = Brushes.LightGray };
            Content = result;
            Label lbl = new Label { Content = "Solid Brushes ... " };
            result.Children.Add(lbl);

            ColorPicker picker = new ColorPicker() { Width = 130 };


            // Try to find a standard brush of this name so that "magenta" also works.
            try
            {
                Color theColour = (Color)ColorConverter.ConvertFromString(initialBrushName);
                initialBrushName = theColour.ToString();  // Normalize back to HTML hex representation
            }
            catch { } 
 
            if (initialBrushName.StartsWith('#'))
            {
                picker.SelectedColor = (Color)new ColorConverter().ConvertFrom(initialBrushName);
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
                Brush bw = parent.GetBrushByName(tName);
                Rectangle rect = new Rectangle() { Height = 48, Width = 48, Fill = bw, Tag = tName };
                rect.MouseDown += Rect_MouseDown;
                bd.Child = rect;
                textureWrapPanel.Children.Add(bd);
                textureBorders.Add(bd);
            }

            AddButton(result, "Apply Change", 250, 10, (s, e) => { DialogResult = true; Close(); }).IsDefault = true;
            AddButton(result, "Cancel", 200, 10, (s, e) => { DialogResult = false; Close(); }).IsCancel = true;

        }

        private void Picker_SelectedColorChanged(object sender, RoutedPropertyChangedEventArgs<Color?> e)
        {
            Color SolidColor = (Color)e.NewValue;
            string name = SolidColor.ToString();
            BrushChanged?.Invoke(this, name);
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
            string name = (string)rect.Tag;
            highlightNexTextureChoice(name);
        }

        void highlightNexTextureChoice(string name)
        {
            textureBorders[tChoiceIndex].BorderBrush = Brushes.Gray;
            tChoiceIndex = parent.getIndexOf(name);
            textureBorders[tChoiceIndex].BorderBrush = Brushes.Red;
            BrushChanged?.Invoke(this, name);
        }
    }
}
