using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace BoardGenerator
{
    public class Palette : SortedDictionary<Zone, string>
    {
        public String Name { get; private set; }
        public String SouthName { get; private set; }
        public String NorthName { get; private set; }

        public Palette(string name, string southName, string northName, string[] brushNames)
        {
            Name = name;
            SouthName = southName;
            NorthName = northName;

            int i = 0;
            foreach (Zone e in Enum.GetValues(typeof(Zone)))
            {
                Add(e, brushNames[i]);
                i++;
            }
        }

        public void CopyCodeToClipboard()
        {
            StringBuilder sb = new StringBuilder();
            // I want to generate a string like this, to paste into the code
            //   knownPalettes.Add(new Palette("dark", "Red", "Brown", new string[] { "black", "LightCoral", "BeigeFelt", "DarkWood1", "Red", "RosyBrown" }));
            sb.Append($"knownPalettes.Add(new Palette(\"{Name}\", \"{SouthName}\", \"{NorthName}\",  new string[]   ");
            char sep = '{';
            foreach (Zone z in Keys)
            {
                sb.Append($"{sep}\"{this[z]}\" ");
                sep = ',';
            }
            sb.AppendLine("}));");
            Clipboard.SetData(DataFormats.Text, (Object)sb.ToString());
        }

    }
}
