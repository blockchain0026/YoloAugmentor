using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace YoloAugmentor.Datasets
{
    public class YoloAugmentation
    {
        public int ClassId { get; set; }
        public float XCenter { get; set; }
        public float YCenter { get; set; }
        public float WidthByImage { get; set; }
        public float HeightByImage { get; set; }
    }
}
