using System;
using System.Collections.Generic;
using System.Text;

namespace YoloAugmentor.Datasets
{
    public class YoloClass
    {
        public YoloClass(int classId, string name, System.Windows.Media.Color color)
        {
            ClassId = classId;
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Color = color;
        }

        public int ClassId { get; private set; }
        public string Name { get; private set; }
        public System.Windows.Media.Color Color { get; private set; }



        public static YoloClass From(int tagId, string name)
        {
            return new YoloClass(tagId, name, GetRandomColor());
        }

        public void ChangeName(string name)
        {
            this.Name = name;
        }

        public void ChangeColor()
        {
            this.Color = GetRandomColor();
        }

        private static System.Windows.Media.Color GetRandomColor()
        {
            var colors = typeof(System.Windows.Media.Colors).GetProperties();
            var colorIndex = new Random().Next(colors.Length - 1);
            var systemColor = (System.Windows.Media.Color)colors[colorIndex].GetValue(typeof(System.Windows.Media.Colors), null);

            var color = System.Windows.Media.Color.FromRgb(
                (byte)new Random().Next(30, 180),
                (byte)new Random().Next(30, 180),
                (byte)new Random().Next(30, 180)
                );
            return color;
        }
    }
}
