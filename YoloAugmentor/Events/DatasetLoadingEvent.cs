using System;
using System.Collections.Generic;
using System.Text;

namespace YoloAugmentor.Events
{
    public class DatasetLoadingEvent
    {
        public int TotalCount { get; set; }
        public int LoadedCount { get; set; }
        public bool IsValidationSet { get; set; }
    }
}
