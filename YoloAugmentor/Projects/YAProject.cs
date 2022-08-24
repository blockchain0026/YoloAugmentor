using System;
using System.Collections.Generic;
using System.Text;
using YoloAugmentor.Datasets;

namespace YoloAugmentor.Projects
{
    public class YAProject
    {
        public string Name { get; set; }
        public string LastSeenImageName { get; set; }
        public string ProjectPath { get; set; }
        public string DatasetPath { get; set; }
        public List<YoloClass> Classes { get; set; } = new List<YoloClass>();
    }
}
